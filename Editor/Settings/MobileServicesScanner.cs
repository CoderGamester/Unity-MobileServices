using System;
using System.Collections.Generic;
using System.Reflection;
using GameLovers.MobileServices.Device;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Settings
{
	/// <summary>Outcome of <see cref="MobileServicesScanner.Scan"/>.</summary>
	public sealed class ProjectScanResult
	{
		/// <summary>Permissions the scanned assemblies reference, so the build can request exactly those.</summary>
		public HashSet<AppPermission> ReferencedPermissions { get; } = new HashSet<AppPermission>();
		public bool UsesAtt;
		public bool UsesNotifications;
		public bool UsesDeepLinks;
		public bool UsesNativeUiShare;

		/// <summary>Returns advisory mismatches without changing the persisted config.</summary>
		public IReadOnlyList<string> GetConfigurationWarnings(MobileServicesConfig config)
		{
			var warnings = new List<string>();
			if (config == null) return warnings;
			if (UsesNotifications && !config.Capabilities.PushNotifications && !config.AndroidManifest.PostNotifications)
			{
				warnings.Add("Notifications are referenced but Push Notifications and Android POST_NOTIFICATIONS are both disabled.");
			}
			if (UsesAtt && !config.Capabilities.AppTracking)
			{
				warnings.Add("App Tracking Transparency is referenced but Capabilities.AppTracking is disabled.");
			}
			if (UsesDeepLinks && config.DeepLinks.IosUrlSchemes.Count == 0 && config.DeepLinks.AndroidIntentFilters.Count == 0)
			{
				warnings.Add("Deep-link services are referenced but no native deep-link scheme or Android intent-filter is configured.");
			}
			if (UsesNativeUiShare && !config.AndroidManifest.IncludeShareQueriesBlock)
			{
				warnings.Add("Native UI sharing is referenced but Android share-package queries are disabled.");
			}
			foreach (var permission in ReferencedPermissions)
			{
				if (MobileServicesConfig.GetIosUsageKey(permission) != null && string.IsNullOrWhiteSpace(config.GetUsageDescriptionEn(permission)))
				{
					warnings.Add($"{permission} is referenced but has no English iOS usage description.");
				}
			}
			return warnings;
		}
	}

	/// <summary>
	/// Reflection-based scan over user assemblies that pre-fills capability toggles for the
	/// Settings Provider and the build postprocessor. Pessimistic by design — false positives are
	/// preferred over false negatives (a build shipping without a required entitlement).
	/// </summary>
	internal static class MobileServicesScanner
	{
		/// <summary>
		/// Reflects over the project's user assemblies to find which mobile services are actually
		/// referenced, so the build only requests the entitlements and permissions in use.
		/// </summary>
		public static ProjectScanResult Scan()
		{
			var result = new ProjectScanResult();

			var notificationsType = typeof(MobileServices.Notifications.MobileNotificationService);
			var deepLinkType = typeof(DeepLinkService);
			var permissionsType = typeof(PermissionsService);
			var permissionsInterface = typeof(IPermissionsService);
			var attType = typeof(AttService);
			var attInterface = typeof(IAttService);
			var nativeUiType = typeof(MobileServices.NativeUi.NativeUiService);

			// Walk every user assembly. Built-in / package assemblies are skipped to keep the scan fast.
			var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
			foreach (var assembly in assemblies)
			{
				if (!IsScannableAssembly(assembly)) continue;

				var refs = assembly.GetReferencedAssemblies();
				var refsRuntime = false;
				foreach (var name in refs)
				{
					if (name.Name == "GameLovers.MobileServices")
					{
						refsRuntime = true;
						break;
					}
				}
				if (!refsRuntime) continue;

				Type[] types;
				try { types = assembly.GetTypes(); }
				catch (ReflectionTypeLoadException e) { types = e.Types ?? Array.Empty<Type>(); }
				catch { continue; }

				foreach (var type in types)
				{
					if (type == null) continue;
					if (TypeReferences(type, notificationsType))     result.UsesNotifications = true;
					if (TypeReferences(type, deepLinkType))          result.UsesDeepLinks = true;
					if (TypeReferences(type, attType) ||
					    TypeReferences(type, attInterface))          result.UsesAtt = true;
					if (TypeReferences(type, permissionsType) ||
					    TypeReferences(type, permissionsInterface))
					{
						// Pessimistic: when permissions are referenced but we can't infer which ones,
						// flag every permission as potentially-required so the user is prompted to
						// fill in usage descriptions explicitly. They can untoggle the ones they
						// don't actually call.
						foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
						{
							result.ReferencedPermissions.Add(p);
						}
					}
					if (TypeReferences(type, nativeUiType))           result.UsesNativeUiShare = true;
				}
			}

			return result;
		}

		private static bool IsScannableAssembly(Assembly assembly)
		{
			// Skip Unity-installed / mscorlib / NuGet-style assemblies — their names are well-known.
			var name = assembly.GetName().Name;
			if (string.IsNullOrEmpty(name)) return false;
			if (name.StartsWith("Unity")) return false;
			if (name.StartsWith("System")) return false;
			if (name.StartsWith("Microsoft")) return false;
			if (name.StartsWith("mscorlib")) return false;
			if (name.StartsWith("netstandard")) return false;
			if (name.StartsWith("nunit")) return false;
			if (name.StartsWith("NUnit")) return false;
			if (name.StartsWith("NSubstitute")) return false;
			if (name.StartsWith("Mono.")) return false;
			return true;
		}

		private static bool TypeReferences(Type type, Type target)
		{
			try
			{
				foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
				{
					if (Match(field.FieldType, target)) return true;
				}
				foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
				{
					if (Match(prop.PropertyType, target)) return true;
				}
				foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
				{
					if (Match(method.ReturnType, target)) return true;
					foreach (var p in method.GetParameters())
					{
						if (Match(p.ParameterType, target)) return true;
					}
				}
			}
			catch
			{
				// Reflection on a few framework types throws TypeLoadException intermittently; ignore.
			}
			return false;
		}

		private static bool Match(Type candidate, Type target)
		{
			if (candidate == target) return true;
			if (candidate.IsArray) return Match(candidate.GetElementType(), target);
			if (candidate.IsGenericType)
			{
				foreach (var a in candidate.GetGenericArguments())
				{
					if (Match(a, target)) return true;
				}
			}
			return false;
		}
	}
}

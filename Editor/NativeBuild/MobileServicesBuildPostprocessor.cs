using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GameLovers.MobileServices.Editor.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

#if UNITY_ANDROID
using System.Xml.Linq;
#endif

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.NativeBuild
{
	/// <summary>Owns the package-wide, configuration-driven Android and iOS native build mutations.</summary>
	public sealed class MobileServicesBuildPostprocessor : IPostprocessBuildWithReport
#if UNITY_ANDROID
		, UnityEditor.Android.IPostGenerateGradleAndroidProject
#endif
	{
#if UNITY_ANDROID
		private const string PlayReviewArtifactKey = "com.google.android.play:review";
		private static readonly XNamespace AndroidNamespace = "http://schemas.android.com/apk/res/android";
#endif

		/// <inheritdoc />
		public int callbackOrder => 1000;

		/// <inheritdoc />
		public void OnPostprocessBuild(BuildReport report)
		{
			if (report == null) return;
			var settings = ResolveBuildConfig();
			if (settings == null || settings.ManageNativeBuildManually) return;
			if (report.summary.platform != BuildTarget.iOS) return;

			var scan = MobileServicesScanner.Scan();
			var warnings = scan.GetConfigurationWarnings(settings);
			LogScannerWarnings(warnings);
			PostprocessIos(report, settings);
			Debug.Log($"[GameLovers.MobileServices] Native build summary: platform={report.summary.platform} source={DescribeSource(settings)} permissions={settings.PermissionDescriptions.Count} iosSchemes={settings.DeepLinks.IosUrlSchemes.Count} androidDeepLinks={settings.DeepLinks.AndroidIntentFilters.Count} scannerWarnings={warnings.Count}.");
		}

#if UNITY_ANDROID
		/// <summary>Mutates configured Android declarations and injects Play Review when needed.</summary>
		public void OnPostGenerateGradleAndroidProject(string path)
		{
			var settings = ResolveBuildConfig();
			if (settings == null || settings.ManageNativeBuildManually || string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
			var scan = MobileServicesScanner.Scan();
			var warnings = scan.GetConfigurationWarnings(settings);
			LogScannerWarnings(warnings);
			if (NeedsAndroidManifestMutation(settings)) ProcessGeneratedAndroidManifest(path, settings);
			if (!settings.IncludePlayReviewDependency)
			{
				Debug.Log($"[GameLovers.MobileServices] Native build summary: platform=Android source={DescribeSource(settings)} permissions={settings.PermissionDescriptions.Count} androidDeepLinks={settings.DeepLinks.AndroidIntentFilters.Count} playReview=disabled scannerWarnings={warnings.Count}.");
				return;
			}

			string[] gradleFiles;
			try { gradleFiles = Directory.GetFiles(path, "*.gradle", SearchOption.AllDirectories); }
			catch (Exception exception) { throw new BuildFailedException($"[GameLovers.MobileServices] Could not enumerate Gradle files under '{path}': {exception.Message}"); }
			foreach (var file in gradleFiles)
			{
				var contents = ReadTextOrThrow(file);
				if (contents.Contains(PlayReviewArtifactKey, StringComparison.Ordinal))
				{
					Debug.Log($"[GameLovers.MobileServices] '{PlayReviewArtifactKey}' already exists in '{file}' — preserving the existing dependency owner.");
					Debug.Log($"[GameLovers.MobileServices] Native build summary: platform=Android source={DescribeSource(settings)} permissions={settings.PermissionDescriptions.Count} androidDeepLinks={settings.DeepLinks.AndroidIntentFilters.Count} playReview=existing scannerWarnings={warnings.Count}.");
					return;
				}
			}

			var targetGradle = FindModuleBuildGradle(path, gradleFiles);
			if (targetGradle == null)
			{
				throw new BuildFailedException($"[GameLovers.MobileServices] Could not find a Unity Android module with a dependencies block under '{path}'. Add implementation '{settings.PlayReviewDependencyCoordinate}' manually or disable Include Play Review Dependency.");
			}

			var injected = InsertIntoDependenciesBlock(ReadTextOrThrow(targetGradle), $"implementation '{settings.PlayReviewDependencyCoordinate}'");
			if (injected == null)
			{
				throw new BuildFailedException($"[GameLovers.MobileServices] No dependencies block was found in '{targetGradle}'. Add implementation '{settings.PlayReviewDependencyCoordinate}' manually.");
			}

			File.WriteAllText(targetGradle, injected);
			Debug.Log($"[GameLovers.MobileServices] Injected '{settings.PlayReviewDependencyCoordinate}' into '{targetGradle}'.");
			Debug.Log($"[GameLovers.MobileServices] Native build summary: platform=Android source={DescribeSource(settings)} permissions={settings.PermissionDescriptions.Count} androidDeepLinks={settings.DeepLinks.AndroidIntentFilters.Count} playReview=changed scannerWarnings={warnings.Count}.");
		}
#endif

		private static MobileServicesConfig ResolveBuildConfig()
		{
			try
			{
				if (!MobileServicesBuildContext.TryGetEffectiveConfig(out var settings)) return null;
				var errors = new List<string>();
				settings.CollectValidationErrors(errors);
				if (errors.Count > 0)
				{
					throw new BuildFailedException("[GameLovers.MobileServices] Invalid configuration:\n- " + string.Join("\n- ", errors));
				}
				return settings;
			}
			catch (BuildFailedException)
			{
				throw;
			}
			catch (Exception exception)
			{
				throw new BuildFailedException($"[GameLovers.MobileServices] Could not resolve the build configuration: {exception.Message}");
			}
		}

		private static void LogScannerWarnings(IReadOnlyList<string> warnings)
		{
			if (warnings.Count == 0) return;
			Debug.LogWarning("[GameLovers.MobileServices] Scanner warnings (explicit config remains authoritative):\n- " + string.Join("\n- ", warnings));
		}

		private static string DescribeSource(MobileServicesConfig settings)
		{
			var path = AssetDatabase.GetAssetPath(settings);
			return string.IsNullOrEmpty(path)
				? $"temporary:{MobileServicesBuildContext.ActiveIdentity}"
				: $"asset:{path}";
		}

		private static bool NeedsIosMutation(MobileServicesConfig settings)
		{
			if (settings.Capabilities.PushNotifications || settings.Capabilities.AppTracking || settings.Capabilities.AssociatedDomains) return true;
			if (settings.DeepLinks.IosUrlSchemes.Count > 0) return true;
			foreach (var row in settings.PermissionDescriptions)
			{
				if (row?.Entries == null) continue;
				if (row.Entries.Any(entry => entry != null && !string.IsNullOrWhiteSpace(entry.UsageDescription))) return true;
			}
			return settings.AttUsageDescription?.Entries?.Any(entry => entry != null && !string.IsNullOrWhiteSpace(entry.UsageDescription)) == true;
		}

		private static void PostprocessIos(BuildReport report, MobileServicesConfig settings)
		{
			if (!NeedsIosMutation(settings)) return;
#if UNITY_IOS
			InjectIosBuild(report, settings);
#else
			Debug.Log("[GameLovers.MobileServices] iOS native mutation is unavailable on this active editor target; configuration validation completed.");
#endif
		}

#if UNITY_IOS
		private static void InjectIosBuild(BuildReport report, MobileServicesConfig settings)
		{
			var buildPath = report.summary.outputPath;
			if (string.IsNullOrEmpty(buildPath) || !Directory.Exists(buildPath))
				throw new BuildFailedException("[GameLovers.MobileServices] iOS build output path is missing.");

			var plistPath = Path.Combine(buildPath, "Info.plist");
			if (!File.Exists(plistPath)) throw new BuildFailedException($"[GameLovers.MobileServices] Expected generated iOS file '{plistPath}' was not found.");
			var plist = new PlistDocument();
			try { plist.ReadFromFile(plistPath); }
			catch (Exception exception) { throw new BuildFailedException($"[GameLovers.MobileServices] Could not parse '{plistPath}': {exception.Message}"); }

			var changed = false;
			foreach (var row in settings.PermissionDescriptions)
			{
				var key = MobileServicesConfig.GetIosUsageKey(row.Permission);
				var value = settings.GetUsageDescriptionEn(row.Permission);
				if (key != null && !string.IsNullOrWhiteSpace(value)) changed |= MergePlistString(plist.root, key, value, plistPath);
			}
			if (settings.Capabilities.AppTracking)
			{
				changed |= MergePlistString(plist.root, "NSUserTrackingUsageDescription", settings.GetAttUsageDescriptionEn(), plistPath);
			}
			changed |= MergePlistLocalizations(plist.root, settings.GetNonDefaultLocaleCodes(), plistPath);
			changed |= MergeUrlSchemes(plist.root, settings.DeepLinks.IosUrlSchemes, plistPath);
			if (changed) plist.WriteToFile(plistPath);

			var pbxPath = PBXProject.GetPBXProjectPath(buildPath);
			if (!File.Exists(pbxPath)) throw new BuildFailedException($"[GameLovers.MobileServices] Expected generated Xcode project '{pbxPath}' was not found.");
			var pbx = new PBXProject();
			try { pbx.ReadFromFile(pbxPath); }
			catch (Exception exception) { throw new BuildFailedException($"[GameLovers.MobileServices] Could not parse '{pbxPath}': {exception.Message}"); }

			var mainTargetGuid = pbx.GetUnityMainTargetGuid();
			var pbxChanged = EmitLocalizedInfoPlistStrings(buildPath, settings, pbx, mainTargetGuid);
			var capabilityNeeded = settings.Capabilities.PushNotifications ||
				(settings.Capabilities.AssociatedDomains && settings.Capabilities.AssociatedDomainList.Count > 0);
			if (capabilityNeeded)
			{
				var entitlementsName = pbx.GetBuildPropertyForAnyConfig(mainTargetGuid, "CODE_SIGN_ENTITLEMENTS");
				if (string.IsNullOrEmpty(entitlementsName))
				{
					entitlementsName = "GameLoversMobileServices.entitlements";
					pbx.AddBuildProperty(mainTargetGuid, "CODE_SIGN_ENTITLEMENTS", entitlementsName);
					pbxChanged = true;
				}
				if (pbxChanged) pbx.WriteToFile(pbxPath);
				var capability = new ProjectCapabilityManager(pbxPath, entitlementsName, null, mainTargetGuid);
				if (settings.Capabilities.PushNotifications) capability.AddPushNotifications(true);
				if (settings.Capabilities.AssociatedDomains && settings.Capabilities.AssociatedDomainList.Count > 0)
					capability.AddAssociatedDomains(settings.Capabilities.AssociatedDomainList.ToArray());
				capability.WriteToFile();
				MergeEntitlementDomains(Path.Combine(buildPath, entitlementsName), settings.Capabilities.AssociatedDomainList);
			}
			else if (pbxChanged)
			{
				pbx.WriteToFile(pbxPath);
			}
			Debug.Log($"[GameLovers.MobileServices] iOS native processing completed for '{buildPath}'.");
		}

		private static bool MergePlistString(PlistElementDict root, string key, string requested, string file)
		{
			if (string.IsNullOrWhiteSpace(requested)) return false;
			if (root.values.TryGetValue(key, out var existing))
			{
				var value = existing.AsString();
				if (value == null)
					throw new BuildFailedException($"[GameLovers.MobileServices] Conflict in '{file}' for key '{key}': existing value is not a string.");
				if (!string.IsNullOrEmpty(value) && !string.Equals(value, requested, StringComparison.Ordinal))
					throw new BuildFailedException($"[GameLovers.MobileServices] Conflict in '{file}' for key '{key}': existing='{value}', requested='{requested}'.");
				if (string.Equals(value, requested, StringComparison.Ordinal)) return false;
			}
			root.SetString(key, requested);
			return true;
		}

		private static bool MergePlistLocalizations(PlistElementDict root, IReadOnlyList<string> locales, string file)
		{
			if (locales.Count == 0) return false;
			PlistElementArray array;
			if (root.values.TryGetValue("CFBundleLocalizations", out var existing))
			{
				array = existing.AsArray();
				if (array == null) throw new BuildFailedException($"[GameLovers.MobileServices] Key 'CFBundleLocalizations' in '{file}' is not an array.");
			}
			else
			{
				array = root.CreateArray("CFBundleLocalizations");
			}

			var changed = false;
			foreach (var locale in locales.Concat(new[] { MobileServicesConfig.DefaultLocaleCode }))
			{
				if (array.values.Any(value => string.Equals(value.AsString(), locale, StringComparison.OrdinalIgnoreCase))) continue;
				array.AddString(locale);
				changed = true;
			}
			return changed;
		}

		private static bool MergeUrlSchemes(PlistElementDict root, IReadOnlyList<string> requestedSchemes, string file)
		{
			if (requestedSchemes.Count == 0) return false;
			PlistElementArray urlTypes;
			if (root.values.TryGetValue("CFBundleURLTypes", out var existing))
			{
				urlTypes = existing.AsArray();
				if (urlTypes == null) throw new BuildFailedException($"[GameLovers.MobileServices] Key 'CFBundleURLTypes' in '{file}' is not an array.");
			}
			else
			{
				urlTypes = root.CreateArray("CFBundleURLTypes");
			}

			var missing = requestedSchemes.Where(scheme => !urlTypes.values.Any(value => ContainsUrlScheme(value.AsDict(), scheme))).ToArray();
			if (missing.Length == 0) return false;
			PlistElementDict owner = null;
			foreach (var value in urlTypes.values)
			{
				var dictionary = value.AsDict();
				if (dictionary == null) continue;
				if (dictionary.values.TryGetValue("CFBundleURLName", out var name) && string.Equals(name.AsString(), "GameLovers.MobileServices", StringComparison.Ordinal))
				{
					owner = dictionary;
					break;
				}
			}
			owner ??= urlTypes.AddDict();
			owner.SetString("CFBundleURLName", "GameLovers.MobileServices");
			var schemes = owner.values.TryGetValue("CFBundleURLSchemes", out var schemesValue) ? schemesValue.AsArray() : owner.CreateArray("CFBundleURLSchemes");
			if (schemes == null) throw new BuildFailedException($"[GameLovers.MobileServices] Package URL owner in '{file}' has a non-array CFBundleURLSchemes value.");
			foreach (var scheme in missing) schemes.AddString(scheme);
			return true;
		}

		private static bool ContainsUrlScheme(PlistElementDict dictionary, string scheme)
		{
			if (dictionary == null || !dictionary.values.TryGetValue("CFBundleURLSchemes", out var value)) return false;
			var schemes = value.AsArray();
			return schemes != null && schemes.values.Any(item => string.Equals(item.AsString(), scheme, StringComparison.OrdinalIgnoreCase));
		}

		private static bool EmitLocalizedInfoPlistStrings(string buildPath, MobileServicesConfig settings, PBXProject pbx, string mainTargetGuid)
		{
			var projectChanged = false;
			foreach (var locale in settings.GetNonDefaultLocaleCodes())
			{
				var values = new Dictionary<string, string>();
				foreach (var row in settings.PermissionDescriptions)
				{
					var key = MobileServicesConfig.GetIosUsageKey(row.Permission);
					var value = settings.GetUsageDescription(row.Permission, locale);
					if (key != null && !string.IsNullOrWhiteSpace(value)) values[key] = value;
				}
				if (settings.Capabilities.AppTracking)
				{
					var att = settings.GetAttUsageDescription(locale);
					if (!string.IsNullOrWhiteSpace(att)) values["NSUserTrackingUsageDescription"] = att;
				}
				if (values.Count == 0) continue;

				var relativeFile = $"{locale}.lproj/InfoPlist.strings";
				var absoluteFile = Path.Combine(buildPath, relativeFile);
				var contents = File.Exists(absoluteFile) ? File.ReadAllText(absoluteFile) : string.Empty;
				var changed = false;
				foreach (var pair in values)
				{
					var result = MergeStringsEntry(contents, pair.Key, pair.Value, absoluteFile);
					contents = result.Contents;
					changed |= result.Changed;
				}
				if (changed || !File.Exists(absoluteFile))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(absoluteFile));
					File.WriteAllText(absoluteFile, contents);
					projectChanged = true;
				}
				if (string.IsNullOrEmpty(pbx.FindFileGuidByProjectPath(relativeFile)))
				{
					var fileGuid = pbx.AddFile(relativeFile, relativeFile, PBXSourceTree.Source);
					pbx.AddFileToBuild(mainTargetGuid, fileGuid);
					projectChanged = true;
				}
			}
			return projectChanged;
		}

		private static (string Contents, bool Changed) MergeStringsEntry(string contents, string key, string requested, string file)
		{
			var lines = contents.Replace("\r\n", "\n").Split('\n').ToList();
			for (var i = 0; i < lines.Count; i++)
			{
				if (!TryParseStringsEntry(lines[i], out var existingKey, out var existingValue) || !string.Equals(existingKey, key, StringComparison.Ordinal)) continue;
				if (!string.Equals(existingValue, requested, StringComparison.Ordinal))
					throw new BuildFailedException($"[GameLovers.MobileServices] Conflict in '{file}' for localized key '{key}': existing='{existingValue}', requested='{requested}'.");
				return (contents, false);
			}

			if (lines.Count == 1 && string.IsNullOrEmpty(lines[0])) lines.Clear();
			lines.Add($"\"{key}\" = \"{EscapeStringsValue(requested)}\";");
			return (string.Join("\n", lines), true);
		}

		private static bool TryParseStringsEntry(string line, out string key, out string value)
		{
			var match = Regex.Match(line, "^\\s*\\\"(?<key>(?:\\\\.|[^\\\"])*)\\\"\\s*=\\s*\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\"\\s*;\\s*$");
			key = null;
			value = null;
			if (!match.Success) return false;
			key = match.Groups["key"].Value;
			value = UnescapeStringsValue(match.Groups["value"].Value);
			return true;
		}

		private static string UnescapeStringsValue(string value) => value.Replace("\\\\", "\\").Replace("\\\"", "\"").Replace("\\n", "\n");
		private static string EscapeStringsValue(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

		private static void MergeEntitlementDomains(string path, IReadOnlyList<string> requested)
		{
			if (requested.Count == 0) return;
			var plist = new PlistDocument();
			if (File.Exists(path)) plist.ReadFromFile(path);
			var root = plist.root;
			PlistElementArray domains;
			if (root.values.TryGetValue("com.apple.developer.associated-domains", out var existing))
			{
				domains = existing.AsArray();
				if (domains == null) throw new BuildFailedException($"[GameLovers.MobileServices] Entitlement '{path}' has a non-array associated-domains value.");
			}
			else domains = root.CreateArray("com.apple.developer.associated-domains");
			var changed = false;
			foreach (var domain in requested)
			{
				if (domains.values.Any(value => string.Equals(value.AsString(), domain, StringComparison.Ordinal))) continue;
				domains.AddString(domain);
				changed = true;
			}
			if (changed) plist.WriteToFile(path);
		}
#endif

#if UNITY_ANDROID
		private static bool NeedsAndroidManifestMutation(MobileServicesConfig settings) =>
			GetConfiguredAndroidPermissions(settings.AndroidManifest).Any() ||
			settings.AndroidManifest.IncludeShareQueriesBlock ||
			settings.DeepLinks.AndroidIntentFilters.Count > 0;

		private static void ProcessGeneratedAndroidManifest(string gradleProjectPath, MobileServicesConfig settings)
		{
			var manifestFiles = Directory.GetFiles(gradleProjectPath, "AndroidManifest.xml", SearchOption.AllDirectories)
				.Where(path => !IsGeneratedIntermediatePath(path)).ToArray();
			var candidates = new List<(string path, XDocument document, XElement activity)>();
			foreach (var manifestPath in manifestFiles)
			{
				XDocument document;
				try { document = XDocument.Load(manifestPath, LoadOptions.PreserveWhitespace); }
				catch (Exception exception) { throw new BuildFailedException($"[GameLovers.MobileServices] Could not parse generated Android manifest '{manifestPath}': {exception.Message}"); }
				var ns = document.Root?.Name.Namespace ?? XNamespace.None;
				var activities = document.Descendants(ns + "activity").Where(activity => IsUnityPlayerActivity(GetAndroidAttribute(activity, "name"))).ToList();
				if (activities.Count == 1) candidates.Add((manifestPath, document, activities[0]));
				else if (activities.Count > 1) throw new BuildFailedException($"[GameLovers.MobileServices] Android manifest '{manifestPath}' contains multiple Unity player activities.");
			}
			if (candidates.Count != 1)
			{
				var paths = candidates.Count == 0 ? "none" : string.Join(", ", candidates.Select(candidate => candidate.path));
				throw new BuildFailedException($"[GameLovers.MobileServices] Expected exactly one generated Android manifest containing UnityPlayerActivity or UnityPlayerGameActivity, found {candidates.Count} ({paths}).");
			}

			var target = candidates[0];
			var root = target.document.Root;
			if (root == null) throw new BuildFailedException($"[GameLovers.MobileServices] Generated Android manifest '{target.path}' has no root element.");
			var changed = false;
			foreach (var permission in GetConfiguredAndroidPermissions(settings.AndroidManifest))
			{
				if (root.Elements(root.Name.Namespace + "uses-permission").Any(element => string.Equals(GetAndroidAttribute(element, "name"), permission, StringComparison.Ordinal))) continue;
				root.AddFirst(new XElement(root.Name.Namespace + "uses-permission", new XAttribute(AndroidNamespace + "name", permission)));
				changed = true;
			}
			if (settings.AndroidManifest.IncludeShareQueriesBlock) changed |= AddShareQueriesIfMissing(root);
			foreach (var registration in settings.DeepLinks.AndroidIntentFilters)
				changed |= AddAndroidIntentFilterIfMissing(target.activity, registration);
			if (!changed) return;
			target.document.Save(target.path, SaveOptions.DisableFormatting);
			Debug.Log($"[GameLovers.MobileServices] Patched generated Android manifest '{target.path}'.");
		}

		private static IEnumerable<string> GetConfiguredAndroidPermissions(AndroidManifestToggles toggles)
		{
			if (toggles.Camera) yield return "android.permission.CAMERA";
			if (toggles.RecordAudio) yield return "android.permission.RECORD_AUDIO";
			if (toggles.AccessFineLocation) yield return "android.permission.ACCESS_FINE_LOCATION";
			if (toggles.ReadMediaImages) yield return "android.permission.READ_MEDIA_IMAGES";
			if (toggles.PostNotifications) yield return "android.permission.POST_NOTIFICATIONS";
			if (toggles.Vibrate) yield return "android.permission.VIBRATE";
		}

		private static bool AddShareQueriesIfMissing(XElement root)
		{
			var ns = root.Name.Namespace;
			var queries = root.Element(ns + "queries");
			if (queries == null)
			{
				queries = new XElement(ns + "queries");
				var application = root.Element(ns + "application");
				if (application != null) application.AddBeforeSelf(queries); else root.Add(queries);
			}
			if (queries.Elements(ns + "intent").Any(intent =>
				intent.Elements(ns + "action").Any(action => string.Equals(GetAndroidAttribute(action, "name"), "android.intent.action.SEND", StringComparison.Ordinal)) &&
				intent.Elements(ns + "data").Any(data => string.Equals(GetAndroidAttribute(data, "mimeType"), "*/*", StringComparison.Ordinal)))) return false;
			queries.Add(new XElement(ns + "intent", new XElement(ns + "action", new XAttribute(AndroidNamespace + "name", "android.intent.action.SEND")), new XElement(ns + "data", new XAttribute(AndroidNamespace + "mimeType", "*/*"))));
			return true;
		}

		private static bool AddAndroidIntentFilterIfMissing(XElement activity, AndroidDeepLinkRegistration registration)
		{
			var ns = activity.Name.Namespace;
			var name = AndroidNamespace + "name";
			var matches = activity.Elements(ns + "intent-filter").Any(filter =>
				filter.Elements(ns + "action").Any(action => string.Equals((string)action.Attribute(name), "android.intent.action.VIEW", StringComparison.Ordinal)) &&
				filter.Elements(ns + "category").Any(category => string.Equals((string)category.Attribute(name), "android.intent.category.DEFAULT", StringComparison.Ordinal)) &&
				filter.Elements(ns + "category").Any(category => string.Equals((string)category.Attribute(name), "android.intent.category.BROWSABLE", StringComparison.Ordinal)) &&
				filter.Elements(ns + "data").Any(data => string.Equals((string)data.Attribute(AndroidNamespace + "scheme"), registration.Scheme, StringComparison.OrdinalIgnoreCase) &&
					string.Equals((string)data.Attribute(AndroidNamespace + "host") ?? string.Empty, registration.Host ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
					string.Equals((string)data.Attribute(AndroidNamespace + "pathPrefix") ?? string.Empty, registration.PathPrefix ?? string.Empty, StringComparison.Ordinal)));
			if (matches) return false;
			var data = new XElement(ns + "data", new XAttribute(AndroidNamespace + "scheme", registration.Scheme));
			if (!string.IsNullOrEmpty(registration.Host)) data.Add(new XAttribute(AndroidNamespace + "host", registration.Host));
			if (!string.IsNullOrEmpty(registration.PathPrefix)) data.Add(new XAttribute(AndroidNamespace + "pathPrefix", registration.PathPrefix));
			activity.Add(new XElement(ns + "intent-filter", new XElement(ns + "action", new XAttribute(name, "android.intent.action.VIEW")), new XElement(ns + "category", new XAttribute(name, "android.intent.category.DEFAULT")), new XElement(ns + "category", new XAttribute(name, "android.intent.category.BROWSABLE")), data));
			return true;
		}

		private static bool IsGeneratedIntermediatePath(string path)
		{
			var normalized = path.Replace('\\', '/');
			return normalized.Contains("/build/") || normalized.Contains("/intermediates/") || normalized.Contains("/outputs/") || normalized.Contains("/.gradle/");
		}

		private static bool IsUnityPlayerActivity(string activityName) => !string.IsNullOrEmpty(activityName) &&
			(activityName.EndsWith(".UnityPlayerActivity", StringComparison.Ordinal) || activityName.EndsWith(".UnityPlayerGameActivity", StringComparison.Ordinal) || string.Equals(activityName, "UnityPlayerActivity", StringComparison.Ordinal) || string.Equals(activityName, "UnityPlayerGameActivity", StringComparison.Ordinal));

		private static string GetAndroidAttribute(XElement element, string localName) => element?.Attribute(AndroidNamespace + localName)?.Value;

		private static string FindModuleBuildGradle(string path, string[] gradleFiles)
		{
			var unityLibraryGradle = Path.Combine(path, "unityLibrary", "build.gradle");
			if (File.Exists(unityLibraryGradle) && ReadTextOrThrow(unityLibraryGradle).Contains("dependencies", StringComparison.Ordinal)) return unityLibraryGradle;
			foreach (var file in gradleFiles)
			{
				var text = ReadTextOrThrow(file);
				if (!text.Contains("dependencies", StringComparison.Ordinal) || Path.GetFileName(Path.GetDirectoryName(file)) != "unityLibrary") continue;
				return file;
			}
			return null;
		}

		private static string InsertIntoDependenciesBlock(string gradle, string implementationLine)
		{
			const string marker = "dependencies {";
			var index = gradle.IndexOf(marker, StringComparison.Ordinal);
			return index < 0 ? null : gradle.Insert(index + marker.Length, "\n    " + implementationLine);
		}

		private static string ReadTextOrThrow(string path)
		{
			try { return File.ReadAllText(path); }
			catch (Exception exception) { throw new BuildFailedException($"[GameLovers.MobileServices] Could not read generated file '{path}': {exception.Message}"); }
		}
#endif
	}
}

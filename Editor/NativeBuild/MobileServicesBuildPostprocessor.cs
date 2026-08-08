using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

#if UNITY_ANDROID
using System.Linq;
using System.Xml.Linq;
#endif

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.NativeBuild
{
	/// <summary>
	/// Validates iOS usage descriptions and mutates the post-build Xcode project / generated
	/// Android manifest with the keys + capabilities configured in
	/// <see cref="MobileServicesConfig"/>. See <c>docs/build-pipeline.md</c> for details.
	/// </summary>
	public sealed class MobileServicesBuildPostprocessor : IPostprocessBuildWithReport
#if UNITY_ANDROID
		, UnityEditor.Android.IPostGenerateGradleAndroidProject
#endif
	{
#if UNITY_ANDROID
		private const string PlayReviewArtifactKey = "com.google.android.play:review";
		private static readonly XNamespace _androidNamespace = "http://schemas.android.com/apk/res/android";
#endif

		/// <inheritdoc />
		public int callbackOrder => 0;

		/// <inheritdoc />
		public void OnPostprocessBuild(BuildReport report)
		{
			if (report == null)
			{
				return;
			}

			if (MobileServicesBuildContext.EffectiveConfig.ManageNativeBuildManually)
			{
				Debug.Log("[GameLovers.MobileServices] 'Manage Native Build Manually' is enabled — skipping all plist / entitlements / manifest mutation.");
				return;
			}

			switch (report.summary.platform)
			{
				case BuildTarget.iOS:
					PostprocessIos(report);
					break;
			}
		}

#if UNITY_ANDROID
		/// <summary>Mutates the generated Android manifest and injects the configured Play Review dependency.</summary>
		public void OnPostGenerateGradleAndroidProject(string path)
		{
			var settings = MobileServicesBuildContext.EffectiveConfig;
			if (settings.ManageNativeBuildManually || string.IsNullOrEmpty(path) || !Directory.Exists(path))
			{
				return;
			}

			ProcessGeneratedAndroidManifest(path, settings);

			if (!settings.IncludePlayReviewDependency)
			{
				return;
			}

			string[] gradleFiles;
			try
			{
				gradleFiles = Directory.GetFiles(path, "*.gradle", SearchOption.AllDirectories);
			}
			catch (System.Exception exception)
			{
				Debug.LogWarning($"[GameLovers.MobileServices] Could not enumerate gradle files under '{path}': {exception.Message}. Add '{settings.PlayReviewDependencyCoordinate}' manually if RequestReview() is needed.");
				return;
			}

			foreach (var file in gradleFiles)
			{
				if (ReadAllTextSafe(file).Contains(PlayReviewArtifactKey))
				{
					Debug.Log($"[GameLovers.MobileServices] '{PlayReviewArtifactKey}' is already declared in the Gradle project — skipping auto-injection.");
					return;
				}
			}

			var targetGradle = FindModuleBuildGradle(path, gradleFiles);
			if (targetGradle == null)
			{
				Debug.LogWarning("[GameLovers.MobileServices] Could not find a module build.gradle with a dependencies block to inject the Play Review dependency. " +
					$"Add 'implementation \"{settings.PlayReviewDependencyCoordinate}\"' manually if RequestReview() is needed, or disable 'Include Play Review dependency' in Project Settings.");
				return;
			}

			var contents = ReadAllTextSafe(targetGradle);
			var injected = InsertIntoDependenciesBlock(contents, $"implementation '{settings.PlayReviewDependencyCoordinate}'");
			if (injected == null)
			{
				Debug.LogWarning($"[GameLovers.MobileServices] No 'dependencies {{ }}' block found in '{targetGradle}' — could not inject the Play Review dependency.");
				return;
			}

			File.WriteAllText(targetGradle, injected);
			Debug.Log($"[GameLovers.MobileServices] Injected '{settings.PlayReviewDependencyCoordinate}' into '{Path.GetFileName(targetGradle)}' for Play In-App Review.");
		}
#endif

		// ---- iOS ----

		private void PostprocessIos(BuildReport report)
		{
			var settings = MobileServicesBuildContext.EffectiveConfig;
			var scan = MobileServicesScanner.Scan();

			var missing = settings.GetMissingUsageDescriptions(scan.ReferencedPermissions);
			var attMissing = scan.UsesAtt && string.IsNullOrWhiteSpace(settings.GetAttUsageDescriptionEn());

			if (missing.Count > 0 || attMissing)
			{
				var sb = new StringBuilder();
				sb.AppendLine("[GameLovers.MobileServices] iOS build failed because the following Info.plist keys are required by referenced services but have empty usage descriptions:");
				foreach (var p in missing)
				{
					sb.AppendLine($"  - {MobileServicesConfig.GetIosUsageKey(p)} (for AppPermission.{p})");
				}
				if (attMissing)
				{
					sb.AppendLine("  - NSUserTrackingUsageDescription (App Tracking Transparency capability is enabled)");
				}
				sb.AppendLine();
				sb.AppendLine("Fix: open Tools > GameLovers > Mobile Services > Select Mobile Services Config and fill in the missing usage descriptions");
				sb.AppendLine("(use the 'Fill missing English descriptions with suggested copy' button for a quick start), or enable 'Manage Native Build Manually' if you manage Info.plist yourself.");
				throw new BuildFailedException(sb.ToString());
			}

#if UNITY_IOS
			InjectIosBuild(report, settings, scan);
#else
			Debug.Log("[GameLovers.MobileServices] iOS Xcode project mutation skipped — UNITY_IOS not defined on this build host (validator only ran).");
#endif
		}

#if UNITY_IOS
		private static void InjectIosBuild(BuildReport report, MobileServicesConfig settings, ProjectScanResult scan)
		{
			var buildPath = report.summary.outputPath;
			if (string.IsNullOrEmpty(buildPath) || !Directory.Exists(buildPath))
			{
				Debug.LogWarning("[GameLovers.MobileServices] Build output path missing — skipping Xcode project mutation.");
				return;
			}

			// Info.plist
			var plistPath = Path.Combine(buildPath, "Info.plist");
			if (File.Exists(plistPath))
			{
				var plist = new PlistDocument();
				plist.ReadFromFile(plistPath);
				var rootDict = plist.root;

				foreach (var row in settings.PermissionDescriptions)
				{
					var key = MobileServicesConfig.GetIosUsageKey(row.Permission);
					if (key == null) continue;
					var en = settings.GetUsageDescriptionEn(row.Permission);
					if (string.IsNullOrWhiteSpace(en)) continue;
					rootDict.SetString(key, en);
				}

				if (settings.Capabilities.AppTracking)
				{
					var attCopy = settings.GetAttUsageDescriptionEn();
					if (!string.IsNullOrWhiteSpace(attCopy))
					{
						rootDict.SetString("NSUserTrackingUsageDescription", attCopy);
					}
				}

				// Declare the supported localizations so iOS knows to consult the <locale>.lproj/
				// InfoPlist.strings files emitted below (the base/en value above is the fallback).
				var localesForPlist = settings.GetNonDefaultLocaleCodes();
				if (localesForPlist.Count > 0)
				{
					if (!rootDict.values.ContainsKey("CFBundleDevelopmentRegion"))
					{
						rootDict.SetString("CFBundleDevelopmentRegion", MobileServicesConfig.DefaultLocaleCode);
					}
					var localizations = rootDict.CreateArray("CFBundleLocalizations");
					localizations.AddString(MobileServicesConfig.DefaultLocaleCode);
					foreach (var locale in localesForPlist)
					{
						localizations.AddString(locale);
					}
				}

				plist.WriteToFile(plistPath);
			}

			// PBXProject + capabilities
			var pbxPath = PBXProject.GetPBXProjectPath(buildPath);
			if (!File.Exists(pbxPath)) return;

			var pbx = new PBXProject();
			pbx.ReadFromFile(pbxPath);

			var mainTargetGuid = pbx.GetUnityMainTargetGuid();
			var frameworkTargetGuid = pbx.GetUnityFrameworkTargetGuid();
			if (frameworkTargetGuid == null) frameworkTargetGuid = mainTargetGuid;

			// Emit + register the localized usage descriptions BEFORE ProjectCapabilityManager re-reads
			// the .pbxproj from disk (otherwise capability.WriteToFile() would overwrite these changes).
			EmitLocalizedInfoPlistStrings(buildPath, settings, pbx, mainTargetGuid);
			pbx.WriteToFile(pbxPath);

			var entitlementsRelativeName = "GameLoversMobileServices.entitlements";
			var entitlementsAbs = Path.Combine(buildPath, entitlementsRelativeName);
			var capability = new ProjectCapabilityManager(pbxPath, entitlementsRelativeName, null, mainTargetGuid);

			if (settings.Capabilities.PushNotifications)
			{
				capability.AddPushNotifications(true);
			}
			if (settings.Capabilities.AssociatedDomains && settings.Capabilities.AssociatedDomainList.Count > 0)
			{
				var domains = new string[settings.Capabilities.AssociatedDomainList.Count];
				for (var i = 0; i < domains.Length; i++)
				{
					domains[i] = settings.Capabilities.AssociatedDomainList[i];
				}
				capability.AddAssociatedDomains(domains);
			}

			capability.WriteToFile();
		}

		// Writes + registers one <locale>.lproj/InfoPlist.strings per non-default locale (base/en stays in Info.plist root).
		private static void EmitLocalizedInfoPlistStrings(string buildPath, MobileServicesConfig settings, PBXProject pbx, string mainTargetGuid)
		{
			var locales = settings.GetNonDefaultLocaleCodes();
			if (locales.Count == 0)
			{
				return;
			}

			foreach (var locale in locales)
			{
				var sb = new StringBuilder();
				sb.AppendLine("/* Auto-generated by GameLovers.MobileServices — localized usage descriptions. Do not edit. */");

				var wroteAny = false;
				foreach (var row in settings.PermissionDescriptions)
				{
					var key = MobileServicesConfig.GetIosUsageKey(row.Permission);
					if (key == null) continue;
					var value = settings.GetUsageDescription(row.Permission, locale);
					if (string.IsNullOrWhiteSpace(value)) continue;
					sb.AppendLine($"\"{key}\" = \"{EscapeStringsValue(value)}\";");
					wroteAny = true;
				}

				if (settings.Capabilities.AppTracking)
				{
					var att = settings.GetAttUsageDescription(locale);
					if (!string.IsNullOrWhiteSpace(att))
					{
						sb.AppendLine($"\"NSUserTrackingUsageDescription\" = \"{EscapeStringsValue(att)}\";");
						wroteAny = true;
					}
				}

				if (!wroteAny)
				{
					continue;
				}

				var relDir = $"{locale}.lproj";
				Directory.CreateDirectory(Path.Combine(buildPath, relDir));
				var relFile = $"{relDir}/InfoPlist.strings";
				File.WriteAllText(Path.Combine(buildPath, relFile), sb.ToString());

				var fileGuid = pbx.AddFile(relFile, relFile, PBXSourceTree.Source);
				pbx.AddFileToBuild(mainTargetGuid, fileGuid);
			}

			Debug.Log($"[GameLovers.MobileServices] Emitted localized InfoPlist.strings for {locales.Count} locale(s): {string.Join(", ", locales)}.");
		}

		// Escapes a value for the .strings format ("key" = "value";).
		private static string EscapeStringsValue(string value) =>
			value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
#endif

#if UNITY_ANDROID
		// ---- Android generated-project mutation ----

		private static void ProcessGeneratedAndroidManifest(string gradleProjectPath, MobileServicesConfig settings)
		{
			var manifestFiles = Directory.GetFiles(gradleProjectPath, "AndroidManifest.xml", SearchOption.AllDirectories)
				.Where(path => !IsGeneratedIntermediatePath(path))
				.ToArray();

			var candidates = new List<(string path, XDocument document, XElement activity)>();
			foreach (var manifestPath in manifestFiles)
			{
				XDocument document;
				try
				{
					document = XDocument.Load(manifestPath, LoadOptions.PreserveWhitespace);
				}
				catch (System.Exception exception)
				{
					throw new BuildFailedException($"[GameLovers.MobileServices] Could not parse generated Android manifest '{manifestPath}': {exception.Message}");
				}

				var manifestNamespace = document.Root?.Name.Namespace ?? XNamespace.None;
				var activities = document.Descendants(manifestNamespace + "activity")
					.Where(activity => IsUnityPlayerActivity(GetAndroidAttribute(activity, "name")))
					.ToList();
				if (activities.Count == 1)
				{
					candidates.Add((manifestPath, document, activities[0]));
				}
				else if (activities.Count > 1)
				{
					throw new BuildFailedException($"[GameLovers.MobileServices] Android manifest '{manifestPath}' contains multiple Unity player activities. The build target is ambiguous.");
				}
			}

			if (candidates.Count != 1)
			{
				var paths = candidates.Count == 0 ? "none" : string.Join(", ", candidates.Select(candidate => candidate.path));
				throw new BuildFailedException($"[GameLovers.MobileServices] Expected exactly one generated Android manifest containing UnityPlayerActivity or UnityPlayerGameActivity, found {candidates.Count} ({paths}).");
			}

			var target = candidates[0];
			var root = target.document.Root;
			if (root == null)
			{
				throw new BuildFailedException($"[GameLovers.MobileServices] Generated Android manifest '{target.path}' has no document root.");
			}

			var changed = false;
			foreach (var permission in GetConfiguredAndroidPermissions(settings.AndroidManifest))
			{
				if (root.Elements(root.Name.Namespace + "uses-permission")
					.Any(element => string.Equals(GetAndroidAttribute(element, "name"), permission, StringComparison.Ordinal)))
				{
					continue;
				}

				root.AddFirst(new XElement(root.Name.Namespace + "uses-permission",
					new XAttribute(_androidNamespace + "name", permission)));
				changed = true;
			}

			if (settings.AndroidManifest.IncludeShareQueriesBlock && AddShareQueriesIfMissing(root))
			{
				changed = true;
			}

			if (!changed)
			{
				return;
			}

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
				if (application != null)
				{
					application.AddBeforeSelf(queries);
				}
				else
				{
					root.Add(queries);
				}
			}

			var exists = queries.Elements(ns + "intent").Any(intent =>
				intent.Elements(ns + "action").Any(action => string.Equals(GetAndroidAttribute(action, "name"), "android.intent.action.SEND", StringComparison.Ordinal)) &&
				intent.Elements(ns + "data").Any(data => string.Equals(GetAndroidAttribute(data, "mimeType"), "*/*", StringComparison.Ordinal)));
			if (exists)
			{
				return false;
			}

			queries.Add(new XElement(ns + "intent",
				new XElement(ns + "action", new XAttribute(_androidNamespace + "name", "android.intent.action.SEND")),
				new XElement(ns + "data", new XAttribute(_androidNamespace + "mimeType", "*/*"))));
			return true;
		}

		private static bool IsGeneratedIntermediatePath(string path)
		{
			var normalized = path.Replace('\\', '/');
			return normalized.Contains("/build/") || normalized.Contains("/intermediates/") ||
				normalized.Contains("/outputs/") || normalized.Contains("/.gradle/");
		}

		private static bool IsUnityPlayerActivity(string activityName)
		{
			return !string.IsNullOrEmpty(activityName) &&
				(activityName.EndsWith(".UnityPlayerActivity", StringComparison.Ordinal) ||
				 activityName.EndsWith(".UnityPlayerGameActivity", StringComparison.Ordinal) ||
				 string.Equals(activityName, "UnityPlayerActivity", StringComparison.Ordinal) ||
				 string.Equals(activityName, "UnityPlayerGameActivity", StringComparison.Ordinal));
		}

		private static string GetAndroidAttribute(XElement element, string localName)
		{
			return element?.Attribute(_androidNamespace + localName)?.Value;
		}

		// Prefer the unityLibrary module (where Unity puts app dependencies); otherwise the first
		// build.gradle that applies an Android plugin and has a dependencies block.
		private static string FindModuleBuildGradle(string path, string[] gradleFiles)
		{
			var unityLibraryGradle = Path.Combine(path, "build.gradle");
			if (File.Exists(unityLibraryGradle) && ReadAllTextSafe(unityLibraryGradle).Contains("dependencies"))
			{
				return unityLibraryGradle;
			}

			string firstWithDependencies = null;
			foreach (var file in gradleFiles)
			{
				var dir = Path.GetFileName(Path.GetDirectoryName(file) ?? string.Empty);
				var text = ReadAllTextSafe(file);
				if (!text.Contains("dependencies"))
				{
					continue;
				}
				if (dir == "unityLibrary" && (text.Contains("com.android.library") || text.Contains("com.android.application")))
				{
					return file;
				}
				if (firstWithDependencies == null && (text.Contains("com.android.library") || text.Contains("com.android.application")))
				{
					firstWithDependencies = file;
				}
			}
			return firstWithDependencies;
		}

		// Inserts a line at the top of the first top-level `dependencies { ... }` block. Returns null
		// when no such block exists.
		private static string InsertIntoDependenciesBlock(string gradle, string implementationLine)
		{
			const string marker = "dependencies {";
			var index = gradle.IndexOf(marker, System.StringComparison.Ordinal);
			if (index < 0)
			{
				return null;
			}
			var insertAt = index + marker.Length;
			return gradle.Insert(insertAt, "\n    " + implementationLine);
		}

		private static string ReadAllTextSafe(string file)
		{
			try
			{
				return File.ReadAllText(file);
			}
			catch
			{
				return string.Empty;
			}
		}
#endif
	}
}

using System.Collections.Generic;
using System.IO;
using System.Text;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Build
{
	/// <summary>
	/// Validates iOS usage descriptions and mutates the post-build Xcode project / Android
	/// <c>mainTemplate.xml</c> with the keys + capabilities configured in
	/// <see cref="MobileServicesConfig"/>. See <c>docs/build-pipeline.md</c> for details.
	/// </summary>
	public sealed class MobileServicesBuildPostprocessor : IPostprocessBuildWithReport
#if UNITY_ANDROID
		, UnityEditor.Android.IPostGenerateGradleAndroidProject
#endif
	{
		public int callbackOrder => 0;

		public void OnPostprocessBuild(BuildReport report)
		{
			if (report == null)
			{
				return;
			}

			if (MobileServicesConfig.Instance.ManageNativeBuildManually)
			{
				Debug.Log("[GameLovers.MobileServices] 'Manage Native Build Manually' is enabled — skipping all plist / entitlements / manifest mutation.");
				return;
			}

			switch (report.summary.platform)
			{
				case BuildTarget.iOS:
					PostprocessIos(report);
					break;
				case BuildTarget.Android:
					PostprocessAndroid();
					break;
			}
		}

		// ---- iOS ----

		private void PostprocessIos(BuildReport report)
		{
			var settings = MobileServicesConfig.Instance;
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

		// ---- Android ----

		private void PostprocessAndroid()
		{
			var settings = MobileServicesConfig.Instance;
			var a = settings.AndroidManifest;

			var templatePath = Path.Combine(Application.dataPath, "Plugins", "Android", "mainTemplate.xml");
			if (!File.Exists(templatePath))
			{
				Debug.LogWarning($"[GameLovers.MobileServices] Android mainTemplate.xml not found at {templatePath}. Permission entries will not be auto-injected — copy Unity's default template from Player Settings > Publishing Settings > Custom Main Manifest before next build.");
				return;
			}

			var contents = File.ReadAllText(templatePath);

			var permissions = new List<string>();
			if (a.Camera)                permissions.Add("android.permission.CAMERA");
			if (a.RecordAudio)           permissions.Add("android.permission.RECORD_AUDIO");
			if (a.AccessFineLocation)    permissions.Add("android.permission.ACCESS_FINE_LOCATION");
			if (a.ReadMediaImages)       permissions.Add("android.permission.READ_MEDIA_IMAGES");
			if (a.PostNotifications)     permissions.Add("android.permission.POST_NOTIFICATIONS");

			var changed = false;
			foreach (var perm in permissions)
			{
				var line = $"    <uses-permission android:name=\"{perm}\" />";
				if (!contents.Contains($"android:name=\"{perm}\""))
				{
					contents = InsertBeforeApplication(contents, line);
					changed = true;
				}
			}

			if (a.IncludeShareQueriesBlock && !contents.Contains("ACTION_SEND"))
			{
				var queriesBlock = "    <queries>\n" +
				                   "        <intent>\n" +
				                   "            <action android:name=\"android.intent.action.SEND\" />\n" +
				                   "            <data android:mimeType=\"*/*\" />\n" +
				                   "        </intent>\n" +
				                   "    </queries>";
				contents = InsertBeforeApplication(contents, queriesBlock);
				changed = true;
			}

			if (changed)
			{
				File.WriteAllText(templatePath, contents);
				AssetDatabase.Refresh();
				Debug.Log("[GameLovers.MobileServices] Patched Android mainTemplate.xml with configured permissions and queries.");
			}
		}

		private static string InsertBeforeApplication(string xml, string snippet)
		{
			var applicationIndex = xml.IndexOf("<application");
			if (applicationIndex < 0)
			{
				return xml;
			}
			var insertAt = xml.LastIndexOf('\n', applicationIndex);
			if (insertAt < 0) insertAt = applicationIndex;
			return xml.Insert(insertAt, "\n" + snippet);
		}

#if UNITY_ANDROID
		// ---- Android Gradle dependency injection ----

		private const string PlayReviewArtifactKey = "com.google.android.play:review";

		/// <summary>
		/// Auto-injects the Play In-App Review dependency into the generated Gradle project so
		/// <c>NativeUiService.RequestReview()</c> works with zero manual setup. Idempotent and
		/// conflict-safe: skips entirely if the dependency is already declared by ANY gradle file in
		/// the generated project (hand-written gradle, EDM4U, another SDK), so it never double-declares
		/// or fights a consumer's version pin.
		/// </summary>
		public void OnPostGenerateGradleAndroidProject(string path)
		{
			var settings = MobileServicesConfig.Instance;
			if (settings.ManageNativeBuildManually || !settings.IncludePlayReviewDependency)
			{
				return;
			}

			if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
			{
				return;
			}

			string[] gradleFiles;
			try
			{
				gradleFiles = Directory.GetFiles(path, "*.gradle", SearchOption.AllDirectories);
			}
			catch (System.Exception e)
			{
				Debug.LogWarning($"[GameLovers.MobileServices] Could not enumerate gradle files under '{path}': {e.Message}. Add '{settings.PlayReviewDependencyCoordinate}' manually if RequestReview() is needed.");
				return;
			}

			// Conflict-safe skip: if any gradle file already declares the artifact (any version /
			// source), leave the consumer's declaration untouched.
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

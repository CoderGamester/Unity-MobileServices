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
	/// <see cref="MobileServicesSettings"/>. See <c>docs/build-pipeline.md</c> for details.
	/// </summary>
	public sealed class MobileServicesBuildPostprocessor : IPostprocessBuildWithReport
	{
		public int callbackOrder => 0;

		public void OnPostprocessBuild(BuildReport report)
		{
			if (report == null)
			{
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
			var settings = MobileServicesSettings.instance;
			var scan = MobileServicesScanner.Scan();

			var missing = settings.GetMissingUsageDescriptions(scan.ReferencedPermissions);
			var attMissing = scan.UsesAtt && string.IsNullOrWhiteSpace(settings.GetAttUsageDescriptionEn());

			if (missing.Count > 0 || attMissing)
			{
				if (!settings.AllowPlaceholderUsageDescriptions)
				{
					var sb = new StringBuilder();
					sb.AppendLine("[GameLovers.MobileServices] iOS build failed because the following Info.plist keys are required by referenced services but have empty usage descriptions:");
					foreach (var p in missing)
					{
						sb.AppendLine($"  - {MobileServicesSettings.GetIosUsageKey(p)} (for AppPermission.{p})");
					}
					if (attMissing)
					{
						sb.AppendLine("  - NSUserTrackingUsageDescription (App Tracking Transparency capability is enabled)");
					}
					sb.AppendLine();
					sb.AppendLine("Fix: open Edit > Project Settings > GameLovers > Mobile Services and fill in the missing usage descriptions.");
					sb.AppendLine("Or enable 'Allow build with placeholder usage descriptions' for CI / preview builds (Apple will reject those placeholders).");
					throw new BuildFailedException(sb.ToString());
				}

				Debug.LogWarning("[GameLovers.MobileServices] Injecting placeholder usage descriptions because 'Allow build with placeholder usage descriptions' is enabled. Apple WILL reject these on App Store submission.");
				foreach (var p in missing)
				{
					settings.SetUsageDescriptionEn(p, "[GameLovers placeholder — replace before App Store submission]");
				}
				if (attMissing)
				{
					settings.SetAttUsageDescriptionEn("[GameLovers placeholder — replace before App Store submission]");
				}
			}

#if UNITY_IOS
			InjectIosBuild(report, settings, scan);
#else
			Debug.Log("[GameLovers.MobileServices] iOS Xcode project mutation skipped — UNITY_IOS not defined on this build host (validator only ran).");
#endif
		}

#if UNITY_IOS
		private static void InjectIosBuild(BuildReport report, MobileServicesSettings settings, ProjectScanResult scan)
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
					var key = MobileServicesSettings.GetIosUsageKey(row.Permission);
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

				if (settings.Capabilities.BackgroundAudio)
				{
					var bg = rootDict.values.ContainsKey("UIBackgroundModes")
						? rootDict["UIBackgroundModes"].AsArray()
						: rootDict.CreateArray("UIBackgroundModes");
					if (!ArrayContains(bg, "audio"))
					{
						bg.AddString("audio");
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

			var entitlementsRelativeName = "GameLoversMobileServices.entitlements";
			var entitlementsAbs = Path.Combine(buildPath, entitlementsRelativeName);
			var capability = new ProjectCapabilityManager(pbxPath, entitlementsRelativeName, null, mainTargetGuid);

			if (settings.Capabilities.PushNotifications)
			{
				capability.AddPushNotifications(true);
			}
			if (settings.Capabilities.BackgroundAudio)
			{
				capability.AddBackgroundModes(BackgroundModesOptions.Audio);
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

		private static bool ArrayContains(PlistElementArray array, string value)
		{
			foreach (var element in array.values)
			{
				if (element != null && element.AsString() == value) return true;
			}
			return false;
		}
#endif

		// ---- Android ----

		private void PostprocessAndroid()
		{
			var settings = MobileServicesSettings.instance;
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

			Debug.Log("[GameLovers.MobileServices] Android build: ensure 'com.google.android.play:review:2.0.1' is on the gradle classpath if you call NativeUiService.RequestReview().");
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
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameLovers.MobileServices.Samples;
using GameLovers.MobileServices.Samples.Editor.Build;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

#if UNITY_ANDROID
using System.Xml.Linq;
#endif

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.Editor.NativeBuild
{
	/// <summary>Adds native URL registration only when the bundled Deep Link Router scene is built.</summary>
	internal sealed class DeepLinkRouterSampleBuildPostprocessor : IPostprocessBuildWithReport
#if UNITY_ANDROID
		, UnityEditor.Android.IPostGenerateGradleAndroidProject
#endif
	{
		private const string AndroidNamespaceUri = "http://schemas.android.com/apk/res/android";

		public int callbackOrder => 1000;

		public void OnPostprocessBuild(BuildReport report)
		{
			if (report == null || report.summary.platform != BuildTarget.iOS || !IsDeepLinkSampleSelected()) return;
#if UNITY_IOS
			var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
			if (!File.Exists(plistPath)) return;
			var plist = new PlistDocument();
			plist.ReadFromFile(plistPath);
			AddUrlScheme(plist.root, DeepLinkSampleScheme.FromIdentifier(PlayerSettings.applicationIdentifier));
			plist.WriteToFile(plistPath);
#endif
		}

#if UNITY_ANDROID
		public void OnPostGenerateGradleAndroidProject(string path)
		{
			if (!IsDeepLinkSampleSelected() || string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
			var target = FindUnityActivity(path);
			if (!AddAndroidIntentFilter(target.activity, DeepLinkSampleScheme.FromIdentifier(PlayerSettings.applicationIdentifier))) return;
			target.document.Save(target.path, SaveOptions.DisableFormatting);
		}
#endif

		private static bool IsDeepLinkSampleSelected()
		{
			if (!MobileServicesSampleBuildCatalog.TryGetScenePath(MobileServicesSamplePage.Links, out var scenePath)) return false;
			var scenes = MobileServicesSampleBuildCommands.GetEffectiveScenes();
			return scenes.Any(scene => scene != null && scene.enabled && string.Equals(scene.path, scenePath, StringComparison.Ordinal));
		}

#if UNITY_ANDROID
		private static (string path, XDocument document, XElement activity) FindUnityActivity(string gradleProjectPath)
		{
			var candidates = new List<(string path, XDocument document, XElement activity)>();
			foreach (var manifestPath in Directory.GetFiles(gradleProjectPath, "AndroidManifest.xml", SearchOption.AllDirectories))
			{
				if (IsGeneratedIntermediatePath(manifestPath)) continue;
				XDocument document;
				try
				{
					document = XDocument.Load(manifestPath, LoadOptions.PreserveWhitespace);
				}
				catch (Exception exception)
				{
					throw new BuildFailedException($"[GameLovers.MobileServices.Samples] Could not parse generated Android manifest '{manifestPath}': {exception.Message}");
				}

				var ns = document.Root?.Name.Namespace ?? XNamespace.None;
				var activities = document.Descendants(ns + "activity")
					.Where(activity => IsUnityPlayerActivity(GetAndroidAttribute(activity, "name")))
					.ToArray();
				if (activities.Length > 1)
				{
					throw new BuildFailedException($"[GameLovers.MobileServices.Samples] Android manifest '{manifestPath}' contains multiple Unity player activities.");
				}
				if (activities.Length == 1) candidates.Add((manifestPath, document, activities[0]));
			}

			if (candidates.Count != 1)
			{
				var paths = candidates.Count == 0 ? "none" : string.Join(", ", candidates.Select(candidate => candidate.path));
				throw new BuildFailedException($"[GameLovers.MobileServices.Samples] Expected one generated Android manifest containing a Unity player activity, found {candidates.Count} ({paths}).");
			}
			return candidates[0];
		}

		private static bool AddAndroidIntentFilter(XElement activity, string scheme)
		{
			var ns = activity.Name.Namespace;
			var name = XName.Get("name", AndroidNamespaceUri);
			var schemeName = XName.Get("scheme", AndroidNamespaceUri);
			var exists = activity.Elements(ns + "intent-filter").Any(filter =>
				filter.Elements(ns + "action").Any(action => string.Equals(action.Attribute(name)?.Value, "android.intent.action.VIEW", StringComparison.Ordinal)) &&
				filter.Elements(ns + "category").Any(category => string.Equals(category.Attribute(name)?.Value, "android.intent.category.DEFAULT", StringComparison.Ordinal)) &&
				filter.Elements(ns + "category").Any(category => string.Equals(category.Attribute(name)?.Value, "android.intent.category.BROWSABLE", StringComparison.Ordinal)) &&
				filter.Elements(ns + "data").Any(data => string.Equals(data.Attribute(schemeName)?.Value, scheme, StringComparison.OrdinalIgnoreCase)));
			if (exists) return false;

			activity.Add(new XElement(ns + "intent-filter",
				new XElement(ns + "action", new XAttribute(name, "android.intent.action.VIEW")),
				new XElement(ns + "category", new XAttribute(name, "android.intent.category.DEFAULT")),
				new XElement(ns + "category", new XAttribute(name, "android.intent.category.BROWSABLE")),
				new XElement(ns + "data", new XAttribute(schemeName, scheme))));
			return true;
		}

		private static string GetAndroidAttribute(XElement element, string localName) =>
			element?.Attribute(XName.Get(localName, AndroidNamespaceUri))?.Value;

		private static bool IsGeneratedIntermediatePath(string path)
		{
			var normalized = path.Replace('\\', '/');
			return normalized.Contains("/build/") || normalized.Contains("/intermediates/") ||
				normalized.Contains("/outputs/") || normalized.Contains("/.gradle/");
		}

		private static bool IsUnityPlayerActivity(string activityName) =>
			!string.IsNullOrEmpty(activityName) &&
			(activityName.EndsWith(".UnityPlayerActivity", StringComparison.Ordinal) ||
			 activityName.EndsWith(".UnityPlayerGameActivity", StringComparison.Ordinal) ||
			 string.Equals(activityName, "UnityPlayerActivity", StringComparison.Ordinal) ||
			 string.Equals(activityName, "UnityPlayerGameActivity", StringComparison.Ordinal));
#endif

#if UNITY_IOS
		private static void AddUrlScheme(PlistElementDict root, string scheme)
		{
			PlistElementArray urlTypes;
			if (root.values.TryGetValue("CFBundleURLTypes", out var existing) && existing.AsArray() != null)
			{
				urlTypes = existing.AsArray();
			}
			else
			{
				urlTypes = root.CreateArray("CFBundleURLTypes");
			}

			foreach (var value in urlTypes.values)
			{
				var dictionary = value.AsDict();
				if (dictionary == null || !dictionary.values.TryGetValue("CFBundleURLSchemes", out var schemesValue)) continue;
				var schemes = schemesValue.AsArray();
				if (schemes != null && schemes.values.Any(item => string.Equals(item.AsString(), scheme, StringComparison.OrdinalIgnoreCase))) return;
			}

			var entry = urlTypes.AddDict();
			entry.SetString("CFBundleURLName", "GameLovers Mobile Services Deep Link Sample");
			entry.CreateArray("CFBundleURLSchemes").AddString(scheme);
		}
#endif
	}
}

using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Settings;
using GameLovers.MobileServices.Samples;
using UnityEditor;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.Editor.Build
{
	/// <summary>Resolves the four stable scene assets and their combined native requirements.</summary>
	internal static class MobileServicesSampleBuildCatalog
	{
		private static readonly IReadOnlyDictionary<MobileServicesSamplePage, string> _sceneGuids =
			new Dictionary<MobileServicesSamplePage, string>
			{
				{ MobileServicesSamplePage.Overview, "55555555555555555555555555555551" },
				{ MobileServicesSamplePage.Haptics, "55555555555555555555555555555521" },
				{ MobileServicesSamplePage.Notifications, "55555555555555555555555555555531" },
				{ MobileServicesSamplePage.Links, "55555555555555555555555555555541" }
			};

		internal static bool TryGetScenePath(MobileServicesSamplePage page, out string scenePath)
		{
			scenePath = null;
			if (!_sceneGuids.TryGetValue(page, out var sceneGuid)) return false;
			scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
			return !string.IsNullOrEmpty(scenePath);
		}

		internal static bool TryGetAllScenePaths(out string[] scenePaths, out string missingPage)
		{
			var paths = new List<string>();
			foreach (var page in MobileServicesSamplePages.All)
			{
				if (!TryGetScenePath(page, out var scenePath))
				{
					scenePaths = null;
					missingPage = MobileServicesSamplePages.GetDisplayName(page);
					return false;
				}
				paths.Add(scenePath);
			}

			scenePaths = paths.ToArray();
			missingPage = null;
			return true;
		}

		internal static void ConfigureAll(MobileServicesConfig config)
		{
			var permissions = new[]
			{
				AppPermission.Camera,
				AppPermission.Microphone,
				AppPermission.LocationWhenInUse,
				AppPermission.LocationAlways,
				AppPermission.PhotoLibrary,
				AppPermission.PhotoLibraryAddOnly,
				AppPermission.Notifications
			};
			foreach (var permission in permissions)
			{
				if (string.IsNullOrWhiteSpace(config.GetUsageDescriptionEn(permission)))
				{
					config.SetUsageDescriptionEn(permission, MobileServicesConfig.GetSuggestedCopy(permission));
				}
			}

			if (string.IsNullOrWhiteSpace(config.GetAttUsageDescriptionEn()))
			{
				config.SetAttUsageDescriptionEn(MobileServicesConfig.GetSuggestedAttCopy());
			}
			config.Capabilities.AppTracking = true;
			config.AndroidManifest.Camera = true;
			config.AndroidManifest.RecordAudio = true;
			config.AndroidManifest.AccessFineLocation = true;
			config.AndroidManifest.ReadMediaImages = true;
			config.AndroidManifest.PostNotifications = true;
			config.AndroidManifest.Vibrate = true;
			config.AndroidManifest.IncludeShareQueriesBlock = true;
			config.IncludePlayReviewDependency = true;
		}

		internal static bool ContainsAllSampleScenes(IEnumerable<EditorBuildSettingsScene> scenes)
		{
			if (!TryGetAllScenePaths(out var paths, out _)) return false;
			var selected = new HashSet<string>(StringComparer.Ordinal);
			foreach (var scene in scenes)
			{
				if (scene != null && scene.enabled) selected.Add(scene.path);
			}
			foreach (var path in paths)
			{
				if (!selected.Contains(path)) return false;
			}
			return true;
		}
	}
}

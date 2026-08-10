using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Settings;
using GameLovers.MobileServices.Samples;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.Editor.Build
{
	/// <summary>Resolves the four catalogued scene assets and their combined native requirements.</summary>
	internal static class MobileServicesSampleBuildCatalog
	{
		private static bool TryLoadCatalog(out MobileServicesSampleBuildCatalogAsset catalog, out string error)
		{
			catalog = null;
			error = null;
			var catalogGuids = AssetDatabase.FindAssets($"t:{nameof(MobileServicesSampleBuildCatalogAsset)}");
			if (catalogGuids.Length == 0)
			{
				error = $"No {nameof(MobileServicesSampleBuildCatalogAsset)} asset was found. Reimport Mobile Services Samples from Package Manager.";
				return false;
			}

			if (catalogGuids.Length > 1)
			{
				var paths = new List<string>();
				foreach (var guid in catalogGuids)
				{
					paths.Add(AssetDatabase.GUIDToAssetPath(guid));
				}
				error = $"Multiple {nameof(MobileServicesSampleBuildCatalogAsset)} assets were found: {string.Join(", ", paths)}. Keep exactly one imported sample bundle.";
				return false;
			}

			var catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[0]);
			catalog = AssetDatabase.LoadAssetAtPath<MobileServicesSampleBuildCatalogAsset>(catalogPath);
			if (catalog == null)
			{
				error = $"The sample catalog at '{catalogPath}' could not be loaded as {nameof(MobileServicesSampleBuildCatalogAsset)}.";
				return false;
			}

			return ValidateCatalog(catalog, catalogPath, out error);
		}

		internal static bool TryGetScenePath(MobileServicesSamplePage page, out string scenePath, out string error)
		{
			scenePath = null;
			if (!TryLoadCatalog(out var catalog, out error)) return false;
			var catalogPath = AssetDatabase.GetAssetPath(catalog);

			foreach (var entry in catalog.Entries)
			{
				if (entry.Page != page) continue;
				scenePath = AssetDatabase.GetAssetPath(entry.Scene);
				if (string.IsNullOrEmpty(scenePath))
				{
					error = $"The {MobileServicesSamplePages.GetDisplayName(page)} scene in catalog '{catalogPath}' has no resolvable asset path.";
					return false;
				}
				return true;
			}

			error = $"The sample catalog has no entry for {MobileServicesSamplePages.GetDisplayName(page)}.";
			return false;
		}

		internal static bool TryGetScenePath(MobileServicesSamplePage page, out string scenePath)
		{
			return TryGetScenePath(page, out scenePath, out _);
		}

		internal static bool TryGetOrderedScenePaths(out string[] scenePaths, out string error)
		{
			scenePaths = null;
			if (!TryLoadCatalog(out var catalog, out error)) return false;
			var catalogPath = AssetDatabase.GetAssetPath(catalog);

			var paths = new List<string>(MobileServicesSamplePages.All.Count);
			foreach (var page in MobileServicesSamplePages.All)
			{
				foreach (var entry in catalog.Entries)
				{
					if (entry.Page != page) continue;
					var path = AssetDatabase.GetAssetPath(entry.Scene);
					if (string.IsNullOrEmpty(path))
					{
						error = $"The {MobileServicesSamplePages.GetDisplayName(page)} scene in catalog '{catalogPath}' has no resolvable asset path.";
						return false;
					}
					paths.Add(path);
					break;
				}
			}

			scenePaths = paths.ToArray();
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
			var sampleScheme = DeepLinkSampleScheme.FromIdentifier(PlayerSettings.applicationIdentifier);
			config.DeepLinks.AddIosUrlScheme(sampleScheme);
			config.DeepLinks.AddAndroidIntentFilter(sampleScheme);
		}

		internal static bool MatchesCanonicalEnabledScenes(IEnumerable<EditorBuildSettingsScene> scenes)
		{
			if (!TryGetOrderedScenePaths(out var paths, out _)) return false;
			var selected = new List<string>();
			foreach (var scene in scenes)
			{
				if (scene != null && scene.enabled) selected.Add(scene.path);
			}
			if (selected.Count != paths.Length)
			{
				return false;
			}
			for (var i = 0; i < paths.Length; i++)
			{
				if (!string.Equals(selected[i], paths[i], StringComparison.Ordinal)) return false;
			}
			return true;
		}

		internal static bool ContainsAllSampleScenes(IEnumerable<EditorBuildSettingsScene> scenes)
		{
			return MatchesCanonicalEnabledScenes(scenes);
		}

		private static bool ValidateCatalog(MobileServicesSampleBuildCatalogAsset catalog, string catalogPath, out string error)
		{
			error = null;
			if (catalog.Entries == null || catalog.Entries.Count != MobileServicesSamplePages.All.Count)
			{
				error = $"The sample catalog '{catalogPath}' must contain exactly {MobileServicesSamplePages.All.Count} entries.";
				return false;
			}

			var pages = new HashSet<MobileServicesSamplePage>();
			for (var i = 0; i < catalog.Entries.Count; i++)
			{
				var entry = catalog.Entries[i];
				if (entry == null)
				{
					error = $"The sample catalog '{catalogPath}' contains a null entry at index {i}.";
					return false;
				}
				if (entry.Scene == null)
				{
					error = $"The {MobileServicesSamplePages.GetDisplayName(entry.Page)} entry in '{catalogPath}' has no SceneAsset reference.";
					return false;
				}
				if (!pages.Add(entry.Page))
				{
					error = $"The sample catalog '{catalogPath}' contains duplicate page {MobileServicesSamplePages.GetDisplayName(entry.Page)}.";
					return false;
				}
				if (entry.Page != MobileServicesSamplePages.All[i])
				{
					error = $"The sample catalog '{catalogPath}' is not in Overview-first order at index {i}.";
					return false;
				}
				var scenePath = AssetDatabase.GetAssetPath(entry.Scene);
				if (string.IsNullOrEmpty(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
				{
					error = $"The {MobileServicesSamplePages.GetDisplayName(entry.Page)} entry in '{catalogPath}' does not resolve to a scene asset.";
					return false;
				}
			}

			return true;
		}
	}
}

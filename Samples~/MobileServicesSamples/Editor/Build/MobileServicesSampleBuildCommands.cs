using System;
using GameLovers.MobileServices.Editor.NativeBuild;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

#if UNITY_6000_1_OR_NEWER
using UnityEditor.Build.Profile;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.Editor.Build
{
	/// <summary>Prepares and restores the one supported, four-scene Mobile Services sample player.</summary>
	internal static class MobileServicesSampleBuildCommands
	{
		private const string SnapshotKey = "GameLovers.MobileServices.Samples.BuildSnapshot";

		internal static EditorBuildSettingsScene[] GetEffectiveScenes()
		{
			return GetCurrentTarget().Scenes;
		}

		[MenuItem("Tools/Mobile Samples Examples/Build All", priority = 100)]
		private static void BuildAll()
		{
			if (!CanChangeBuildScenes()) return;
			if (!MobileServicesSampleBuildCatalog.TryGetAllScenePaths(out var paths, out var missingPage))
			{
				EditorUtility.DisplayDialog("Mobile Services samples", $"The {missingPage} scene could not be resolved. Reimport Mobile Services Samples from Package Manager.", "OK");
				return;
			}

			var target = GetCurrentTarget();
			if (TryReadSnapshot(out var existing) && !existing.Matches(target))
			{
				EditorUtility.DisplayDialog("Mobile Services samples", "Restore the existing Mobile Services sample build setup before preparing a different Build Profile.", "OK");
				return;
			}

			if (!TryReadSnapshot(out _))
			{
				WriteSnapshot(new BuildSceneSnapshot(target));
			}

			target.SetScenes(paths);
			OpenNativeBuildWindow();
		}

		[MenuItem("Tools/Mobile Samples Examples/Build All", true)]
		private static bool ValidateBuildAll() => CanChangeBuildScenes(false);

		[MenuItem("Tools/Mobile Samples Examples/Restore All", priority = 101)]
		private static void RestoreAll()
		{
			if (!TryReadSnapshot(out var snapshot)) return;
			if (!snapshot.TryResolveTarget(out var target))
			{
				EditorUtility.DisplayDialog("Mobile Services samples", "The Build Profile captured by Build All no longer exists. The restore snapshot was kept so it can be recovered if the profile is restored.", "OK");
				return;
			}

			target.SetScenes(snapshot.ToScenes());
			SessionState.EraseString(SnapshotKey);
		}

		[MenuItem("Tools/Mobile Samples Examples/Restore All", true)]
		private static bool ValidateRestoreAll() => CanChangeBuildScenes(false) && TryReadSnapshot(out _);

		private static bool CanChangeBuildScenes(bool showDialog = true)
		{
			if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode && !BuildPipeline.isBuildingPlayer) return true;
			if (showDialog)
			{
				EditorUtility.DisplayDialog("Mobile Services samples", "Exit Play Mode and wait for compilation or any current player build to finish before changing the sample build setup.", "OK");
			}
			return false;
		}

		private static void OpenNativeBuildWindow()
		{
			if (TryOpenNativeWindow("UnityEditor.Build.Profile.BuildProfileWindow", "ShowBuildProfileWindow")) return;
			if (TryOpenNativeWindow("UnityEditor.BuildPlayerWindow", "ShowBuildPlayerWindow")) return;
		}

		private static bool TryOpenNativeWindow(string typeName, string methodName)
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				var type = assembly.GetType(typeName);
				var method = type?.GetMethod(methodName);
				if (method == null) continue;
				method.Invoke(null, null);
				return true;
			}

			return false;
		}

		private static bool TryReadSnapshot(out BuildSceneSnapshot snapshot)
		{
			snapshot = null;
			var json = SessionState.GetString(SnapshotKey, string.Empty);
			if (string.IsNullOrEmpty(json)) return false;
			try
			{
				snapshot = JsonUtility.FromJson<BuildSceneSnapshot>(json);
				return snapshot != null && snapshot.SceneEntries != null;
			}
			catch (ArgumentException)
			{
				return false;
			}
		}

		private static void WriteSnapshot(BuildSceneSnapshot snapshot)
		{
			SessionState.SetString(SnapshotKey, JsonUtility.ToJson(snapshot));
		}

		private static BuildSceneTarget GetCurrentTarget()
		{
#if UNITY_6000_1_OR_NEWER
			var profile = BuildProfile.GetActiveBuildProfile();
			if (profile != null && profile.overrideGlobalScenes) return new BuildSceneTarget(profile);
#endif
			return new BuildSceneTarget();
		}

		[Serializable]
		private sealed class BuildSceneSnapshot
		{
			public bool UsesBuildProfile;
			public string BuildProfileGuid;
			public SceneEntry[] SceneEntries;

			public BuildSceneSnapshot(BuildSceneTarget target)
			{
				UsesBuildProfile = target.UsesBuildProfile;
				BuildProfileGuid = target.BuildProfileGuid;
				SceneEntries = SceneEntry.From(target.Scenes);
			}

			public bool Matches(BuildSceneTarget target) =>
				UsesBuildProfile == target.UsesBuildProfile &&
				string.Equals(BuildProfileGuid, target.BuildProfileGuid, StringComparison.Ordinal);

			public bool TryResolveTarget(out BuildSceneTarget target)
			{
				if (!UsesBuildProfile)
				{
					target = new BuildSceneTarget();
					return true;
				}

#if UNITY_6000_1_OR_NEWER
				var profilePath = AssetDatabase.GUIDToAssetPath(BuildProfileGuid);
				var profile = string.IsNullOrEmpty(profilePath) ? null : AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
				if (profile != null)
				{
					target = new BuildSceneTarget(profile);
					return true;
				}
#endif
				target = null;
				return false;
			}

			public EditorBuildSettingsScene[] ToScenes() => SceneEntry.ToScenes(SceneEntries);
		}

		[Serializable]
		private sealed class SceneEntry
		{
			public string Path;
			public bool Enabled;

			public static SceneEntry[] From(EditorBuildSettingsScene[] scenes)
			{
				var entries = new SceneEntry[scenes?.Length ?? 0];
				for (var i = 0; i < entries.Length; i++)
				{
					entries[i] = new SceneEntry { Path = scenes[i].path, Enabled = scenes[i].enabled };
				}
				return entries;
			}

			public static EditorBuildSettingsScene[] ToScenes(SceneEntry[] entries)
			{
				var scenes = new EditorBuildSettingsScene[entries?.Length ?? 0];
				for (var i = 0; i < scenes.Length; i++)
				{
					scenes[i] = new EditorBuildSettingsScene(entries[i].Path, entries[i].Enabled);
				}
				return scenes;
			}
		}

		private sealed class BuildSceneTarget
		{
#if UNITY_6000_1_OR_NEWER
			private readonly BuildProfile _profile;
#endif

			public bool UsesBuildProfile
			{
				get
				{
#if UNITY_6000_1_OR_NEWER
					return _profile != null;
#else
					return false;
#endif
				}
			}

			public string BuildProfileGuid
			{
				get
				{
#if UNITY_6000_1_OR_NEWER
					return _profile == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_profile));
#else
					return string.Empty;
#endif
				}
			}

			public EditorBuildSettingsScene[] Scenes
			{
				get
				{
#if UNITY_6000_1_OR_NEWER
					if (_profile != null) return _profile.scenes;
#endif
					return EditorBuildSettings.scenes;
				}
			}

			public BuildSceneTarget()
			{
			}

#if UNITY_6000_1_OR_NEWER
			public BuildSceneTarget(BuildProfile profile)
			{
				_profile = profile;
			}
#endif

			public void SetScenes(string[] paths)
			{
				var scenes = new EditorBuildSettingsScene[paths.Length];
				for (var i = 0; i < paths.Length; i++) scenes[i] = new EditorBuildSettingsScene(paths[i], true);
				SetScenes(scenes);
			}

			public void SetScenes(EditorBuildSettingsScene[] scenes)
			{
#if UNITY_6000_1_OR_NEWER
				if (_profile != null)
				{
					_profile.scenes = scenes;
					EditorUtility.SetDirty(_profile);
					AssetDatabase.SaveAssetIfDirty(_profile);
					return;
				}
#endif
				EditorBuildSettings.scenes = scenes;
			}
		}
	}

	/// <summary>Applies the sample bundle's temporary native requirements for one player build.</summary>
	internal sealed class MobileServicesSampleBuildContext : IPreprocessBuildWithReport, IPostprocessBuildWithReport
	{
		private static IDisposable _scope;
		private static double _releaseAfter;

		public int callbackOrder => 2000;

		public void OnPreprocessBuild(BuildReport report)
		{
			Release();
			if (!MobileServicesSampleBuildCatalog.ContainsAllSampleScenes(MobileServicesSampleBuildCommands.GetEffectiveScenes())) return;

			_scope = MobileServicesBuildContext.Push("MobileServicesSamples.All", MobileServicesSampleBuildCatalog.ConfigureAll);
			_releaseAfter = EditorApplication.timeSinceStartup + 1d;
		}

		public void OnPostprocessBuild(BuildReport report)
		{
			Release();
		}

		[InitializeOnLoadMethod]
		private static void InstallCleanup()
		{
			EditorApplication.update += ReleaseAfterCancelledBuild;
		}

		private static void ReleaseAfterCancelledBuild()
		{
			if (_scope == null || BuildPipeline.isBuildingPlayer || EditorApplication.timeSinceStartup < _releaseAfter) return;
			Release();
		}

		private static void Release()
		{
			_scope?.Dispose();
			_scope = null;
			_releaseAfter = 0d;
		}
	}
}

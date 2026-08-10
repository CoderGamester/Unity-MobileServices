using GameLovers.MobileServices.Samples;
using GameLovers.MobileServices.Samples.Editor.Build;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.Editor.Navigation
{
	/// <summary>Loads imported sample scenes in Play Mode without requiring Build Settings preparation.</summary>
	[InitializeOnLoad]
	internal static class MobileServicesSampleNavigationEditorBridge
	{
		static MobileServicesSampleNavigationEditorBridge()
		{
			Install();
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange _)
		{
			Install();
		}

		private static void Install()
		{
			MobileServicesSampleNavigation.EditorSceneLoader = LoadSceneInPlayMode;
		}

		private static bool LoadSceneInPlayMode(MobileServicesSamplePage page)
		{
			if (!EditorApplication.isPlaying) return false;
			if (!MobileServicesSampleBuildCatalog.TryGetScenePath(page, out var scenePath)) return false;

			EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
			return true;
		}
	}
}

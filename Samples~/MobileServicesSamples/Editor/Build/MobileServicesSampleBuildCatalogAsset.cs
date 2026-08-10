using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.Editor.Build
{
	/// <summary>One page and its Unity-authored scene reference in the imported sample bundle.</summary>
	[Serializable]
	internal sealed class MobileServicesSampleBuildCatalogEntry
	{
		[SerializeField] private MobileServicesSamplePage _page;
		[SerializeField] private SceneAsset _scene;

		internal MobileServicesSamplePage Page => _page;
		internal SceneAsset Scene => _scene;
	}

	/// <summary>Editor-only catalog of the four sample scenes, backed by serialized SceneAsset references.</summary>
	internal sealed class MobileServicesSampleBuildCatalogAsset : ScriptableObject
	{
		[SerializeField] private List<MobileServicesSampleBuildCatalogEntry> _entries =
			new List<MobileServicesSampleBuildCatalogEntry>();

		internal IReadOnlyList<MobileServicesSampleBuildCatalogEntry> Entries => _entries;
	}
}

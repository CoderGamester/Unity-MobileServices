using UnityEditor;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Settings
{
	/// <summary>
	/// Locates (or creates) the single <see cref="MobileServicesConfig"/> asset and selects it, so the
	/// developer edits it in the Inspector. Mirrors the GameLovers family convention
	/// (<c>UiConfigsMenuItems</c>, <c>GoogleSheetImporter</c>): a <c>Select …</c> entry under
	/// <c>Tools &gt; GameLovers &gt; …</c>.
	/// </summary>
	public static class MobileServicesConfigMenuItems
	{
		[MenuItem("Tools/GameLovers/Mobile Services/Select Mobile Services Config", priority = 100)]
		private static void SelectMobileServicesConfig()
		{
			var config = MobileServicesConfig.GetOrCreateAsset();
			Selection.activeObject = config;
			EditorGUIUtility.PingObject(config);
		}
	}
}

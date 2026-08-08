using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples
{
	/// <summary>Identifies one page in the combined Mobile Services sample player.</summary>
	internal enum MobileServicesSamplePage
	{
		Overview,
		Haptics,
		Notifications,
		Links
	}

	/// <summary>Maps the stable sample scene assets to player-navigation labels.</summary>
	internal static class MobileServicesSamplePages
	{
		private static readonly IReadOnlyList<MobileServicesSamplePage> _all = new[]
		{
			MobileServicesSamplePage.Overview,
			MobileServicesSamplePage.Haptics,
			MobileServicesSamplePage.Notifications,
			MobileServicesSamplePage.Links
		};

		internal static IReadOnlyList<MobileServicesSamplePage> All => _all;

		internal static string GetDisplayName(MobileServicesSamplePage page)
		{
			switch (page)
			{
				case MobileServicesSamplePage.Overview: return "Overview";
				case MobileServicesSamplePage.Haptics: return "Haptics";
				case MobileServicesSamplePage.Notifications: return "Notifications";
				case MobileServicesSamplePage.Links: return "Links";
				default: throw new ArgumentOutOfRangeException(nameof(page), page, null);
			}
		}

		internal static string GetSceneName(MobileServicesSamplePage page)
		{
			switch (page)
			{
				case MobileServicesSamplePage.Overview: return "MobileServicesPlayground";
				case MobileServicesSamplePage.Haptics: return "HapticsPalette";
				case MobileServicesSamplePage.Notifications: return "NotificationsScheduler";
				case MobileServicesSamplePage.Links: return "DeepLinkRouter";
				default: throw new ArgumentOutOfRangeException(nameof(page), page, null);
			}
		}

		internal static bool TryGetPage(string sceneName, out MobileServicesSamplePage page)
		{
			foreach (var candidate in _all)
			{
				if (!string.Equals(GetSceneName(candidate), sceneName, StringComparison.Ordinal)) continue;
				page = candidate;
				return true;
			}

			page = default;
			return false;
		}
	}
}

using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device.Internal
{
	/// <summary>Builds the web link that opens an app's store page in the platform's store app.</summary>
	internal static class StoreLinks
	{
		/// <summary>
		/// Returns the store page URL for <paramref name="platform"/>, or null when the platform has no store or the
		/// identifier it needs is missing.
		/// </summary>
		/// <param name="platform">The runtime platform.</param>
		/// <param name="iosAppId">The numeric App Store id, with or without an <c>id</c> prefix.</param>
		/// <param name="androidPackage">The Android application id.</param>
		/// <param name="writeReview">Opens the iOS write-review sheet; Android has no review deep link.</param>
		public static string Build(RuntimePlatform platform, string iosAppId, string androidPackage, bool writeReview)
		{
			switch (platform)
			{
				case RuntimePlatform.IPhonePlayer:
				{
					var id = (iosAppId ?? string.Empty).Trim();
					if (id.StartsWith("id", StringComparison.OrdinalIgnoreCase)) id = id.Substring(2);
					if (id.Length == 0 || !IsDigits(id)) return null;
					return "https://apps.apple.com/app/id" + id + (writeReview ? "?action=write-review" : string.Empty);
				}
				case RuntimePlatform.Android:
				{
					var package = (androidPackage ?? string.Empty).Trim();
					return package.Length == 0 ? null : "https://play.google.com/store/apps/details?id=" + Uri.EscapeDataString(package);
				}
				default:
					return null;
			}
		}

		private static bool IsDigits(string value)
		{
			foreach (var character in value)
			{
				if (character < '0' || character > '9') return false;
			}
			return true;
		}
	}
}

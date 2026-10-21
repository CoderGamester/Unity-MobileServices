using System;
using System.Collections.Generic;
using System.Linq;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.NativeBuild;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Settings
{
	/// <summary>
	/// Describes, per feature, the native build entries the current <see cref="MobileServicesConfig"/> adds, so the
	/// inspector can show exactly what a toggle changes without running a build.
	/// </summary>
	internal static class MobileServicesBuildSummary
	{
		private const string AndroidPrefix = "android.permission.";

		public static IReadOnlyList<string> Permissions(MobileServicesConfig config)
		{
			var lines = new List<string>();
			var plistKeys = new List<string>();
			foreach (var row in config.PermissionDescriptions)
			{
				var key = row == null ? null : MobileServicesConfig.GetIosUsageKey(row.Permission);
				if (key != null && !string.IsNullOrWhiteSpace(config.GetUsageDescriptionEn(row.Permission))) plistKeys.Add(key);
			}
			if (config.Capabilities.AppTracking && !string.IsNullOrWhiteSpace(config.GetAttUsageDescriptionEn()))
			{
				plistKeys.Add("NSUserTrackingUsageDescription");
			}
			if (plistKeys.Count > 0) lines.Add("iOS Info.plist: " + string.Join(", ", plistKeys.Distinct()));

			var groups = IosPermissionGroups.All
				.Where(group => group.PlistKeys.Any(plistKeys.Contains))
				.Select(group => group.Name)
				.ToList();
			lines.Add(groups.Count == 0
				? "iOS permission code compiled in: none from this config (keys already in Player Settings or Info.plist also count)"
				: "iOS permission code compiled in: " + string.Join(", ", groups) + " (undeclared groups stay out of the binary)");

			AddAndroidPermissions(lines, config.AndroidManifest, permission => permission != "POST_NOTIFICATIONS" && permission != "VIBRATE");
			return lines;
		}

		public static IReadOnlyList<string> Notifications(MobileServicesConfig config)
		{
			var lines = new List<string>();
			if (config.Capabilities.PushNotifications) lines.Add("iOS entitlement: Push Notifications (aps-environment)");
			AddAndroidPermissions(lines, config.AndroidManifest, permission => permission == "POST_NOTIFICATIONS" || permission == "VIBRATE");
			return lines;
		}

		public static IReadOnlyList<string> DeepLinks(MobileServicesConfig config)
		{
			var lines = new List<string>();
			var iosSchemes = config.DeepLinks.IosUrlSchemes.Where(scheme => !string.IsNullOrWhiteSpace(scheme)).ToList();
			if (iosSchemes.Count > 0) lines.Add("iOS Info.plist URL schemes: " + string.Join(", ", iosSchemes));
			if (config.Capabilities.AssociatedDomains && config.Capabilities.AssociatedDomainList.Count > 0)
			{
				lines.Add("iOS entitlement: Associated Domains (" + string.Join(", ", config.Capabilities.AssociatedDomainList) + ")");
			}
			foreach (var filter in config.DeepLinks.AndroidIntentFilters.Where(filter => filter != null))
			{
				lines.Add($"Android intent filter: {filter.Scheme}://{filter.Host}{filter.PathPrefix}{(filter.AutoVerify ? " (autoVerify)" : string.Empty)}");
			}
			return lines;
		}

		public static IReadOnlyList<string> Sharing(MobileServicesConfig config)
		{
			var lines = new List<string>();
			if (config.AndroidManifest.IncludeShareQueriesBlock) lines.Add("Android manifest: <queries> entry for ACTION_SEND share targets");
			if (config.AndroidManifest.IncludeShareFileProvider) lines.Add("Android manifest: GameLoversFileProvider (images can be shared on Android 7+) and res/xml/gamelovers_file_paths.xml");
			return lines;
		}

		public static IReadOnlyList<string> StoreReview(MobileServicesConfig config)
		{
			var lines = new List<string>();
			if (config.IncludePlayReviewDependency) lines.Add($"Android Gradle: implementation '{config.PlayReviewDependencyCoordinate}'");
			return lines;
		}

		private static void AddAndroidPermissions(List<string> lines, AndroidManifestToggles toggles, Func<string, bool> filter)
		{
			var permissions = toggles.GetPermissions()
				.Select(entry => (Name: entry.Permission.StartsWith(AndroidPrefix, StringComparison.Ordinal) ? entry.Permission.Substring(AndroidPrefix.Length) : entry.Permission, entry.MaxSdkVersion))
				.Where(entry => filter(entry.Name))
				.Select(entry => entry.MaxSdkVersion.HasValue ? $"{entry.Name} (up to API {entry.MaxSdkVersion.Value})" : entry.Name)
				.ToList();
			if (permissions.Count > 0) lines.Add("Android manifest: " + string.Join(", ", permissions));
		}
	}
}

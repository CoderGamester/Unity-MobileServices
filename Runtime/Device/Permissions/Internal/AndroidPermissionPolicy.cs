using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device.Internal
{
	/// <summary>
	/// Platform-neutral Android runtime-permission rules: which manifest permissions an <see cref="AppPermission"/>
	/// needs at a given API level and how their grant states fold into one <see cref="PermissionStatus"/>.
	/// </summary>
	internal static class AndroidPermissionPolicy
	{
		/// <summary>Android 10, which added scoped storage and the separate background location permission.</summary>
		public const int ApiLevelQ = 29;
		/// <summary>Android 13, which introduced the notification and granular media permissions.</summary>
		public const int ApiLevelTiramisu = 33;
		/// <summary>Android 14, which introduced partial photo access.</summary>
		public const int ApiLevelUpsideDownCake = 34;

		/// <summary>Manifest permission <c>CAMERA</c>.</summary>
		public const string Camera = "android.permission.CAMERA";
		/// <summary>Manifest permission <c>RECORD_AUDIO</c>.</summary>
		public const string RecordAudio = "android.permission.RECORD_AUDIO";
		/// <summary>Manifest permission <c>ACCESS_FINE_LOCATION</c>.</summary>
		public const string FineLocation = "android.permission.ACCESS_FINE_LOCATION";
		/// <summary>Manifest permission <c>ACCESS_COARSE_LOCATION</c>.</summary>
		public const string CoarseLocation = "android.permission.ACCESS_COARSE_LOCATION";
		/// <summary>Manifest permission <c>ACCESS_BACKGROUND_LOCATION</c>.</summary>
		public const string BackgroundLocation = "android.permission.ACCESS_BACKGROUND_LOCATION";
		/// <summary>Manifest permission <c>READ_MEDIA_IMAGES</c>.</summary>
		public const string ReadMediaImages = "android.permission.READ_MEDIA_IMAGES";
		/// <summary>Manifest permission <c>READ_MEDIA_VISUAL_USER_SELECTED</c>.</summary>
		public const string ReadMediaVisualUserSelected = "android.permission.READ_MEDIA_VISUAL_USER_SELECTED";
		/// <summary>Manifest permission <c>READ_EXTERNAL_STORAGE</c>.</summary>
		public const string ReadExternalStorage = "android.permission.READ_EXTERNAL_STORAGE";
		/// <summary>Manifest permission <c>WRITE_EXTERNAL_STORAGE</c>.</summary>
		public const string WriteExternalStorage = "android.permission.WRITE_EXTERNAL_STORAGE";
		/// <summary>Manifest permission <c>POST_NOTIFICATIONS</c>.</summary>
		public const string PostNotifications = "android.permission.POST_NOTIFICATIONS";

		/// <summary>
		/// The manifest permissions to request in one system dialog. Empty when the OS has nothing to ask
		/// (for example saving to the gallery on API 29+, or notifications below API 33).
		/// </summary>
		public static IReadOnlyList<string> GetForegroundRequest(AppPermission permission, int apiLevel)
		{
			switch (permission)
			{
				case AppPermission.Camera:
					return new[] { Camera };
				case AppPermission.Microphone:
					return new[] { RecordAudio };
				case AppPermission.LocationWhenInUse:
				case AppPermission.LocationAlways:
					// Android 12+ ignores a precise-only request, so both are always asked together.
					return new[] { FineLocation, CoarseLocation };
				case AppPermission.PhotoLibrary:
					if (apiLevel >= ApiLevelUpsideDownCake) return new[] { ReadMediaImages, ReadMediaVisualUserSelected };
					return new[] { apiLevel >= ApiLevelTiramisu ? ReadMediaImages : ReadExternalStorage };
				case AppPermission.PhotoLibraryAddOnly:
					return apiLevel >= ApiLevelQ ? Array.Empty<string>() : new[] { WriteExternalStorage };
				case AppPermission.Notifications:
					return apiLevel >= ApiLevelTiramisu ? new[] { PostNotifications } : Array.Empty<string>();
				default:
					return Array.Empty<string>();
			}
		}

		/// <summary>
		/// Whether <paramref name="permission"/> needs a second, separate request after the foreground one.
		/// Android requires background location to be requested on its own, after foreground access is granted.
		/// </summary>
		public static bool NeedsBackgroundRequest(AppPermission permission, int apiLevel) =>
			permission == AppPermission.LocationAlways && apiLevel >= ApiLevelQ;

		/// <summary>
		/// Folds the individual grant states into one status; a refusal is reported once the dialog was shown or the OS
		/// rationale flag is set.
		/// </summary>
		public static PermissionStatus Resolve(
			AppPermission permission,
			int apiLevel,
			Func<string, bool> isGranted,
			bool wasRequested,
			bool showRationale)
		{
			var denied = wasRequested || showRationale ? PermissionStatus.Denied : PermissionStatus.NotDetermined;
			switch (permission)
			{
				case AppPermission.LocationWhenInUse:
				case AppPermission.LocationAlways:
				{
					var fine = isGranted(FineLocation);
					if (!fine && !isGranted(CoarseLocation)) return denied;
					if (!fine) return PermissionStatus.Limited;
					var needsBackground = permission == AppPermission.LocationAlways && apiLevel >= ApiLevelQ;
					return needsBackground && !isGranted(BackgroundLocation) ? PermissionStatus.Limited : PermissionStatus.Granted;
				}
				case AppPermission.PhotoLibrary:
				{
					var full = isGranted(apiLevel >= ApiLevelTiramisu ? ReadMediaImages : ReadExternalStorage);
					if (full) return PermissionStatus.Granted;
					if (apiLevel >= ApiLevelUpsideDownCake && isGranted(ReadMediaVisualUserSelected)) return PermissionStatus.Limited;
					return denied;
				}
				case AppPermission.PhotoLibraryAddOnly:
					if (apiLevel >= ApiLevelQ) return PermissionStatus.Granted;
					return isGranted(WriteExternalStorage) ? PermissionStatus.Granted : denied;
				case AppPermission.Notifications:
					if (apiLevel < ApiLevelTiramisu) return PermissionStatus.NotDetermined;
					return isGranted(PostNotifications) ? PermissionStatus.Granted : denied;
				default:
				{
					var request = GetForegroundRequest(permission, apiLevel);
					if (request.Count == 0) return PermissionStatus.NotDetermined;
					foreach (var manifestPermission in request)
					{
						if (!isGranted(manifestPermission)) return denied;
					}
					return PermissionStatus.Granted;
				}
			}
		}

		/// <summary>
		/// Status of notifications below API 33, where no runtime permission exists and the only signal is the
		/// user-controlled "notifications enabled" switch.
		/// </summary>
		public static PermissionStatus ResolveLegacyNotifications(bool areNotificationsEnabled) =>
			areNotificationsEnabled ? PermissionStatus.Granted : PermissionStatus.Denied;
	}
}

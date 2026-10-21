using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GameLovers.MobileServices.Device.Internal;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class PermissionsService : IPermissionsService
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		private const string RequestedKeyPrefix = nameof(PermissionsService) + ".Requested.";
		private static readonly SemaphoreSlim _androidRequestGate = new SemaphoreSlim(1, 1);
#endif

#if UNITY_IOS && !UNITY_EDITOR
		[DllImport("__Internal")] private static extern int  _GameLoversPermissionsCheck(int permissionId);
		[DllImport("__Internal")] private static extern void _GameLoversPermissionsRequest(int permissionId, int requestId, string callbackGameObject, string callbackMethod);
#endif

#if UNITY_EDITOR
		// Editor-only override hooks consumed by EditorPlatformSimulator so the Device Simulator can
		// model the real OS flow in the editor. When set, the editor short-circuit paths consult these
		// instead of returning the default Granted; when unset, the default is unchanged and runtime
		// non-Editor builds carry none of this surface.
		//   - EditorCheckOverride / EditorRequestOverride: synchronous reads (the simulated persisted decision).
		//   - EditorRequestAsyncOverride: the first request for a NotDetermined permission shows a prompt and the
		//     returned Task completes when the user answers; takes precedence over the synchronous EditorRequestOverride.
		internal static Func<AppPermission, PermissionStatus> EditorCheckOverride;
		internal static Func<AppPermission, PermissionStatus> EditorRequestOverride;
		internal static Func<AppPermission, Task<PermissionStatus>> EditorRequestAsyncOverride;
#endif

		/// <inheritdoc />
		public PermissionStatus Check(AppPermission permission)
		{
#if UNITY_EDITOR
			var over = EditorCheckOverride;
			return over != null ? over(permission) : PermissionStatus.Granted;
#elif UNITY_IOS
			return (PermissionStatus)_GameLoversPermissionsCheck((int)permission);
#elif UNITY_ANDROID
			return CheckAndroid(permission);
#else
			return PermissionStatus.NotDetermined;
#endif
		}

		/// <summary>
		/// Snapshot of <see cref="Check"/> across every <see cref="AppPermission"/> value. Used by
		/// the Device Simulator panel's Permissions foldout to render the per-permission status grid
		/// without forcing callers to iterate the enum themselves.
		/// </summary>
		/// <remarks>Editor introspection accessor — not part of the public surface.</remarks>
		internal IReadOnlyDictionary<AppPermission, PermissionStatus> CheckSnapshot()
		{
			var dict = new Dictionary<AppPermission, PermissionStatus>();
			foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
			{
				dict[p] = Check(p);
			}
			return dict;
		}

		/// <inheritdoc />
		public Task<PermissionStatus> RequestAsync(AppPermission permission)
		{
#if UNITY_EDITOR
			var asyncOver = EditorRequestAsyncOverride;
			if (asyncOver != null)
			{
				return asyncOver(permission);
			}
			var over = EditorRequestOverride;
			return Task.FromResult(over != null ? over(permission) : PermissionStatus.Granted);
#elif UNITY_IOS
			var tcs = new TaskCompletionSource<PermissionStatus>();
			var id = PermissionsCallbackReceiver.Instance.Register(tcs);
			_GameLoversPermissionsRequest((int)permission, id, "PermissionsCallbackReceiver", "OnPermissionResult");
			return tcs.Task;
#elif UNITY_ANDROID
			return RequestAndroidAsync(permission);
#else
			return Task.FromResult(PermissionStatus.NotDetermined);
#endif
		}

#if UNITY_ANDROID && !UNITY_EDITOR
		private static PermissionStatus CheckAndroid(AppPermission permission)
		{
			var apiLevel = AndroidApiLevel.Current;
			if (permission == AppPermission.Notifications && apiLevel < AndroidPermissionPolicy.ApiLevelTiramisu)
			{
				return AndroidPermissionPolicy.ResolveLegacyNotifications(AreNotificationsEnabled(apiLevel));
			}

			return AndroidPermissionPolicy.Resolve(
				permission,
				apiLevel,
				Permission.HasUserAuthorizedPermission,
				WasRequested(permission),
				ShouldShowRationale(permission, apiLevel));
		}

		private static async Task<PermissionStatus> RequestAndroidAsync(AppPermission permission)
		{
			var apiLevel = AndroidApiLevel.Current;
			var current = CheckAndroid(permission);
			var legacyNotifications = permission == AppPermission.Notifications && apiLevel < AndroidPermissionPolicy.ApiLevelTiramisu;
			if (current == PermissionStatus.Granted || legacyNotifications)
			{
				return current;
			}

			// Android shows one system dialog at a time; a concurrent request would be dropped without a callback.
			await _androidRequestGate.WaitAsync();
			try
			{
				var missing = new List<string>();
				foreach (var manifestPermission in AndroidPermissionPolicy.GetForegroundRequest(permission, apiLevel))
				{
					if (!Permission.HasUserAuthorizedPermission(manifestPermission)) missing.Add(manifestPermission);
				}
				if (missing.Count > 0)
				{
					await RequestManifestPermissionsAsync(missing.ToArray());
				}

				var hasForegroundLocation = Permission.HasUserAuthorizedPermission(AndroidPermissionPolicy.FineLocation) ||
					Permission.HasUserAuthorizedPermission(AndroidPermissionPolicy.CoarseLocation);
				if (AndroidPermissionPolicy.NeedsBackgroundRequest(permission, apiLevel) && hasForegroundLocation &&
					!Permission.HasUserAuthorizedPermission(AndroidPermissionPolicy.BackgroundLocation))
				{
					await RequestManifestPermissionsAsync(new[] { AndroidPermissionPolicy.BackgroundLocation });
				}

				PlayerPrefs.SetInt(RequestedKeyPrefix + permission, 1);
				PlayerPrefs.Save();
				return CheckAndroid(permission);
			}
			finally
			{
				_androidRequestGate.Release();
			}
		}

		private static Task RequestManifestPermissionsAsync(string[] manifestPermissions)
		{
			var completion = new TaskCompletionSource<bool>();
			var remaining = manifestPermissions.Length;
			void OnAnswered(string _)
			{
				if (--remaining == 0) completion.TrySetResult(true);
			}

			var callbacks = new PermissionCallbacks();
			callbacks.PermissionGranted += OnAnswered;
			callbacks.PermissionDenied += OnAnswered;
			Permission.RequestUserPermissions(manifestPermissions, callbacks);
			return completion.Task;
		}

		private static bool WasRequested(AppPermission permission) => PlayerPrefs.GetInt(RequestedKeyPrefix + permission, 0) == 1;

		private static bool ShouldShowRationale(AppPermission permission, int apiLevel)
		{
			foreach (var manifestPermission in AndroidPermissionPolicy.GetForegroundRequest(permission, apiLevel))
			{
				if (Permission.ShouldShowRequestPermissionRationale(manifestPermission)) return true;
			}
			return false;
		}

		private static bool AreNotificationsEnabled(int apiLevel)
		{
			// NotificationManager.areNotificationsEnabled needs API 24; older devices cannot disable them per app.
			if (apiLevel < 24) return true;
			try
			{
				using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
				using var manager = activity.Call<AndroidJavaObject>("getSystemService", "notification");
				return manager.Call<bool>("areNotificationsEnabled");
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"[GameLovers.MobileServices] Could not read the notifications-enabled state: {exception.Message}");
				return true;
			}
		}
#endif
	}
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
#if UNITY_IOS && !UNITY_EDITOR
		[DllImport("__Internal")] private static extern int  _GameLoversPermissionsCheck(int permissionId);
		[DllImport("__Internal")] private static extern void _GameLoversPermissionsRequest(int permissionId, int requestId, string callbackGameObject, string callbackMethod);
#endif

#if UNITY_EDITOR
		// Editor-only override hooks consumed by EditorPlatformSimulator. When set, the editor
		// short-circuit paths consult these instead of returning the default Granted. Keeps
		// runtime non-Editor builds untouched.
		internal static Func<AppPermission, PermissionStatus> EditorCheckOverride;
		internal static Func<AppPermission, PermissionStatus> EditorRequestOverride;
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
		/// the Mobile Services Explorer Permissions tab to render the per-permission status grid
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
		private static string AndroidManifestPermission(AppPermission permission)
		{
			return permission switch
			{
				AppPermission.Camera               => Permission.Camera,
				AppPermission.Microphone           => Permission.Microphone,
				AppPermission.LocationWhenInUse    => Permission.FineLocation,
				AppPermission.LocationAlways       => Permission.FineLocation,
				AppPermission.PhotoLibrary         => "android.permission.READ_MEDIA_IMAGES",
				AppPermission.PhotoLibraryAddOnly  => "android.permission.READ_MEDIA_IMAGES",
				AppPermission.Notifications        => "android.permission.POST_NOTIFICATIONS",
				_                                  => null
			};
		}

		private static PermissionStatus CheckAndroid(AppPermission permission)
		{
			var manifestId = AndroidManifestPermission(permission);
			if (string.IsNullOrEmpty(manifestId))
			{
				return PermissionStatus.NotDetermined;
			}
			return Permission.HasUserAuthorizedPermission(manifestId)
				? PermissionStatus.Granted
				: PermissionStatus.NotDetermined;
		}

		private static Task<PermissionStatus> RequestAndroidAsync(AppPermission permission)
		{
			var manifestId = AndroidManifestPermission(permission);
			if (string.IsNullOrEmpty(manifestId))
			{
				return Task.FromResult(PermissionStatus.NotDetermined);
			}

			if (Permission.HasUserAuthorizedPermission(manifestId))
			{
				return Task.FromResult(PermissionStatus.Granted);
			}

			var tcs = new TaskCompletionSource<PermissionStatus>();
			var callbacks = new PermissionCallbacks();
			callbacks.PermissionGranted             += _ => tcs.TrySetResult(PermissionStatus.Granted);
			callbacks.PermissionDenied              += _ => tcs.TrySetResult(PermissionStatus.Denied);
			callbacks.PermissionDeniedAndDontAskAgain += _ => tcs.TrySetResult(PermissionStatus.Denied);

			Permission.RequestUserPermission(manifestId, callbacks);
			return tcs.Task;
		}
#endif
	}
}

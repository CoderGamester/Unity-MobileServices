using System;
using System.Collections.Generic;
using System.Linq;
using GameLovers.MobileServices.Device.Internal;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class DeviceService : IDeviceService, IDisposable
	{
		/// <summary>The most dynamic shortcuts both platforms reliably show.</summary>
		internal const int MaxShortcuts = 4;

#if UNITY_EDITOR
		/// <summary>Editor simulator hook that stands in for the OS Settings page.</summary>
		internal static Action EditorOpenAppSettingsOverride;
#endif

		/// <summary>
		/// Controls whether the device screen stays awake. When <c>true</c>, sets
		/// <c>Screen.sleepTimeout</c> to <c>SleepTimeout.NeverSleep</c>; when <c>false</c>,
		/// restores <c>SleepTimeout.SystemSetting</c>.
		/// </summary>
		public static bool KeepAwake
		{
			get => Screen.sleepTimeout == SleepTimeout.NeverSleep;
			set => Screen.sleepTimeout = value ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
		}

		/// <inheritdoc />
		public IBatteryService Battery { get; }
		/// <inheritdoc />
		public IIosAudioSessionService AudioSession { get; }
		/// <inheritdoc />
		public IPermissionsService Permissions { get; }
		/// <inheritdoc />
		public IAttService Att { get; }
		/// <inheritdoc />
		public IDeepLinkService DeepLink { get; }
		/// <inheritdoc />
		public IBackgroundTaskService BackgroundTasks { get; }

		public DeviceService() : this(BuildDefaults()) { }

		public DeviceService(
			IBatteryService battery,
			IIosAudioSessionService audioSession,
			IPermissionsService permissions,
			IAttService att,
			IDeepLinkService deepLink,
			IBackgroundTaskService backgroundTasks)
		{
			Battery = battery;
			AudioSession = audioSession;
			Permissions = permissions;
			Att = att;
			DeepLink = deepLink;
			BackgroundTasks = backgroundTasks;
		}

		// Tuple-routed delegating ctor so the host-dependent children share one explicit host
		// instance constructed up-front, not separate accesses to the singleton during a
		// constructor chain (cleaner ownership signal in the umbrella's call stack).
		private DeviceService((IBatteryService,
			IIosAudioSessionService, IPermissionsService, IAttService, IDeepLinkService, IBackgroundTaskService) defaults)
			: this(defaults.Item1, defaults.Item2, defaults.Item3,
				   defaults.Item4, defaults.Item5, defaults.Item6)
		{
		}

		/// <summary>
		/// Replaces the home-screen quick actions on the app icon. Tapping one opens the app and delivers the shortcut's
		/// URI through <see cref="IDeepLinkService"/>, including as the cold-start link when the tap launched the app.
		/// </summary>
		/// <remarks>
		/// At most four shortcuts are kept; entries without an id, title or absolute URI, and repeated ids, are skipped
		/// with a log message. Android needs API 25 or newer.
		/// </remarks>
		public static void SetShortcuts(IReadOnlyList<AppShortcut> shortcuts)
		{
			AppShortcutsBridge.Set(NormalizeShortcuts(shortcuts));
		}

		/// <summary>
		/// Removes every shortcut set with <see cref="SetShortcuts"/>.
		/// </summary>
		public static void ClearShortcuts()
		{
			AppShortcutsBridge.Clear();
		}

		/// <summary>
		/// Opens this application's page in the platform's store app: the App Store on iOS or Google Play on Android.
		/// </summary>
		/// <param name="iosAppId">The numeric App Store id, with or without an <c>id</c> prefix. Required on iOS.</param>
		/// <param name="androidPackage">The Android application id. Defaults to <c>Application.identifier</c>.</param>
		/// <param name="writeReview">
		/// Opens the iOS write-review sheet. Android has no review deep link, so the store page opens instead.
		/// </param>
		public static void OpenStorePage(string iosAppId, string androidPackage = null, bool writeReview = false)
		{
			var url = BuildStoreUrl(Application.platform, iosAppId, androidPackage ?? Application.identifier, writeReview);
			if (url == null)
			{
				Debug.LogWarning($"[GameLovers.MobileServices] OpenStorePage has no store page to open on {Application.platform} " +
					"(iOS needs a numeric App Store id; Android needs a package name).");
				return;
			}

#if UNITY_EDITOR
			Debug.Log($"[GameLovers.MobileServices] OpenStorePage would open {url}");
#else
			Application.OpenURL(url);
#endif
		}

		/// <summary>
		/// Opens this application's page in the operating system Settings, where the player can change a denied
		/// permission. Opening Settings can move the app to the background.
		/// </summary>
		public static void OpenAppSettings()
		{
#if UNITY_EDITOR
			if (EditorOpenAppSettingsOverride != null)
			{
				EditorOpenAppSettingsOverride.Invoke();
				return;
			}
			Debug.Log("[GameLovers.MobileServices] OpenAppSettings() is a no-op in the Editor unless the Mobile Services simulator is engaged.");
#elif UNITY_IOS
			Application.OpenURL("app-settings:");
#elif UNITY_ANDROID
			try
			{
				using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
				using var uriClass = new AndroidJavaClass("android.net.Uri");
				using var uri = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", activity.Call<string>("getPackageName"), (string)null);
				using var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri);
				activity.Call("startActivity", intent);
			}
			catch (Exception exception)
			{
				Debug.LogError($"[GameLovers.MobileServices] OpenAppSettings failed: {exception.Message}");
			}
#endif
		}

		/// <inheritdoc />
		public void Dispose()
		{
			(Battery as IDisposable)?.Dispose();
			(DeepLink as IDisposable)?.Dispose();
		}

		/// <summary>Builds the store page link for a platform, or null when there is no store or identifier.</summary>
		internal static string BuildStoreUrl(RuntimePlatform platform, string iosAppId, string androidPackage, bool writeReview)
		{
			switch (platform)
			{
				case RuntimePlatform.IPhonePlayer:
				{
					var id = (iosAppId ?? string.Empty).Trim();
					if (id.StartsWith("id", StringComparison.OrdinalIgnoreCase)) id = id.Substring(2);
					if (id.Length == 0 || !id.All(character => character >= '0' && character <= '9')) return null;
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

		/// <summary>Keeps the usable shortcuts in order, dropping incomplete, repeated and surplus ones.</summary>
		internal static IReadOnlyList<AppShortcut> NormalizeShortcuts(IReadOnlyList<AppShortcut> shortcuts)
		{
			var result = new List<AppShortcut>();
			if (shortcuts == null)
			{
				return result;
			}

			var ids = new HashSet<string>();
			foreach (var shortcut in shortcuts)
			{
				if (shortcut == null || string.IsNullOrWhiteSpace(shortcut.Id) || string.IsNullOrWhiteSpace(shortcut.Title) ||
					!Uri.TryCreate(shortcut.Uri, UriKind.Absolute, out _))
				{
					Debug.LogWarning($"[GameLovers.MobileServices] Skipped shortcut '{shortcut?.Id}': it needs an id, a title and an absolute URI.");
					continue;
				}

				if (!ids.Add(shortcut.Id))
				{
					Debug.LogWarning($"[GameLovers.MobileServices] Skipped shortcut '{shortcut.Id}': the id is already used.");
					continue;
				}

				if (result.Count == MaxShortcuts)
				{
					Debug.LogWarning($"[GameLovers.MobileServices] Skipped shortcut '{shortcut.Id}': at most {MaxShortcuts} shortcuts are supported.");
					continue;
				}

				result.Add(shortcut);
			}
			return result;
		}

		private static (IBatteryService,
			IIosAudioSessionService, IPermissionsService, IAttService, IDeepLinkService, IBackgroundTaskService) BuildDefaults()
		{
			var host = DeviceServicesHost.Instance;
			return (
				new BatteryService(host),
				new IosAudioSessionService(),
				new PermissionsService(),
				new AttService(),
				new DeepLinkService(),
				new BackgroundTaskService());
		}
	}
}

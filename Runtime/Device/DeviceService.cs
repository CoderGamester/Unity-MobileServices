using System;
using GameLovers.MobileServices.Device.Internal;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class DeviceService : IDeviceService, IDisposable
	{
#if UNITY_EDITOR
		// Editor-only simulator hook, installed and removed symmetrically by EditorPlatformSimulator.
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

		public DeviceService() : this(BuildDefaults()) { }

		public DeviceService(
			IBatteryService battery,
			IIosAudioSessionService audioSession,
			IPermissionsService permissions,
			IAttService att,
			IDeepLinkService deepLink)
		{
			Battery = battery;
			AudioSession = audioSession;
			Permissions = permissions;
			Att = att;
			DeepLink = deepLink;
		}

		// Tuple-routed delegating ctor so the host-dependent children share one explicit host
		// instance constructed up-front, not separate accesses to the singleton during a
		// constructor chain (cleaner ownership signal in the umbrella's call stack).
		private DeviceService((IBatteryService,
			IIosAudioSessionService, IPermissionsService, IAttService, IDeepLinkService) defaults)
			: this(defaults.Item1, defaults.Item2, defaults.Item3,
				   defaults.Item4, defaults.Item5)
		{
		}

		private static (IBatteryService,
			IIosAudioSessionService, IPermissionsService, IAttService, IDeepLinkService) BuildDefaults()
		{
			var host = DeviceServicesHost.Instance;
			return (
				new BatteryService(host),
				new IosAudioSessionService(),
				new PermissionsService(),
				new AttService(),
				new DeepLinkService());
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
	}
}

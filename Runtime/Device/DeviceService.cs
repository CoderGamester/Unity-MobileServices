using System;
using GameLovers.MobileServices.Device.Internal;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class DeviceService : IDeviceService, IDisposable
	{
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
		public ISafeAreaService SafeArea { get; }
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
			ISafeAreaService safeArea,
			IBatteryService battery,
			IIosAudioSessionService audioSession,
			IPermissionsService permissions,
			IAttService att,
			IDeepLinkService deepLink)
		{
			SafeArea = safeArea;
			Battery = battery;
			AudioSession = audioSession;
			Permissions = permissions;
			Att = att;
			DeepLink = deepLink;
		}

		// Tuple-routed delegating ctor so the host-dependent children share one explicit host
		// instance constructed up-front, not separate accesses to the singleton during a
		// constructor chain (cleaner ownership signal in the umbrella's call stack).
		private DeviceService((ISafeAreaService, IBatteryService,
			IIosAudioSessionService, IPermissionsService, IAttService, IDeepLinkService) defaults)
			: this(defaults.Item1, defaults.Item2, defaults.Item3,
				   defaults.Item4, defaults.Item5, defaults.Item6)
		{
		}

		private static (ISafeAreaService, IBatteryService,
			IIosAudioSessionService, IPermissionsService, IAttService, IDeepLinkService) BuildDefaults()
		{
			var host = DeviceServicesHost.Instance;
			return (
				new SafeAreaService(host),
				new BatteryService(host),
				new IosAudioSessionService(),
				new PermissionsService(),
				new AttService(),
				new DeepLinkService());
		}

		/// <inheritdoc />
		public void Dispose()
		{
			(SafeArea as IDisposable)?.Dispose();
			(Battery as IDisposable)?.Dispose();
			(DeepLink as IDisposable)?.Dispose();
		}
	}
}

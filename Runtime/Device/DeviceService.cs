using System;
using GameLovers.MobileServices.Device.Internal;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class DeviceService : IDeviceService, IDisposable
	{
		/// <inheritdoc />
		public ISafeAreaService SafeArea { get; }
		/// <inheritdoc />
		public IScreenWakeService ScreenWake { get; }
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

		/// <summary>
		/// Constructs the umbrella with the default child implementations for the current platform.
		/// The host-dependent children (SafeArea, Battery) share a single
		/// <see cref="DeviceServicesHost"/> spawned by this constructor — no extra GameObjects.
		/// </summary>
		public DeviceService() : this(BuildDefaults()) { }

		/// <summary>
		/// Constructs the umbrella with injected children. Used by tests to supply mocks.
		/// Children that implement <see cref="IDisposable"/> will be disposed by <see cref="Dispose"/>.
		/// </summary>
		public DeviceService(
			ISafeAreaService safeArea,
			IScreenWakeService screenWake,
			IBatteryService battery,
			IIosAudioSessionService audioSession,
			IPermissionsService permissions,
			IAttService att,
			IDeepLinkService deepLink)
		{
			SafeArea = safeArea;
			ScreenWake = screenWake;
			Battery = battery;
			AudioSession = audioSession;
			Permissions = permissions;
			Att = att;
			DeepLink = deepLink;
		}

		// Tuple-routed delegating ctor so the host-dependent children share one explicit host
		// instance constructed up-front, not separate accesses to the singleton during a
		// constructor chain (cleaner ownership signal in the umbrella's call stack).
		private DeviceService((ISafeAreaService, IScreenWakeService, IBatteryService,
			IIosAudioSessionService, IPermissionsService, IAttService, IDeepLinkService) defaults)
			: this(defaults.Item1, defaults.Item2, defaults.Item3,
				   defaults.Item4, defaults.Item5, defaults.Item6, defaults.Item7)
		{
		}

		private static (ISafeAreaService, IScreenWakeService, IBatteryService,
			IIosAudioSessionService, IPermissionsService, IAttService, IDeepLinkService) BuildDefaults()
		{
			var host = DeviceServicesHost.Instance;
			return (
				new SafeAreaService(host),
				new ScreenWakeService(),
				new BatteryService(host),
				new IosAudioSessionService(),
				new PermissionsService(),
				new AttService(),
				new DeepLinkService());
		}

		public void Dispose()
		{
			(SafeArea as IDisposable)?.Dispose();
			(Battery as IDisposable)?.Dispose();
			(DeepLink as IDisposable)?.Dispose();
		}
	}
}

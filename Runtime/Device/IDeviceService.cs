// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Umbrella facade aggregating every device-touching service in the package. Use as a single
	/// DI registration to expose the full Device subsystem; each child interface is also
	/// independently registerable for testing/mocking.
	/// </summary>
	public interface IDeviceService
	{
		/// <summary>Display safe-area events (notch, dynamic island, orientation).</summary>
		ISafeAreaService SafeArea { get; }

		/// <summary>Toggle <c>Screen.sleepTimeout</c> (keep the screen awake).</summary>
		IScreenWakeService ScreenWake { get; }

		/// <summary>Battery level / status / low-power-mode awareness.</summary>
		IBatteryService Battery { get; }

		/// <summary>iOS audio session category override (silent-switch). No-op elsewhere.</summary>
		IIosAudioSessionService AudioSession { get; }

		/// <summary>Unified iOS+Android runtime permissions (Camera, Mic, Location, Photos, Notifications).</summary>
		IPermissionsService Permissions { get; }

		/// <summary>iOS App Tracking Transparency (no-op on Android / Editor / unsupported).</summary>
		IAttService Att { get; }

		/// <summary>OS deep link delivery with cold-start link queueing.</summary>
		IDeepLinkService DeepLink { get; }
	}
}

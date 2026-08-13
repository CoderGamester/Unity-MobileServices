// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Umbrella facade aggregating the stateful, injectable device services in the package. Use as
	/// a single DI registration to expose the Device subsystem; each child interface is also
	/// independently registerable for testing/mocking. Stateless global conveniences are exposed
	/// directly by <see cref="DeviceService"/>.
	/// </summary>
	public interface IDeviceService
	{
		/// <summary>Display safe-area events (notch, dynamic island, orientation).</summary>
		ISafeAreaService SafeArea { get; }

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

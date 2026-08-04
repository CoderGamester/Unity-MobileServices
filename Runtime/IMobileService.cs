using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.NativeUi;
using GameLovers.MobileServices.Notifications;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices
{
	/// <summary>
	/// Umbrella facade aggregating every Mobile Services subsystem behind a single DI registration.
	/// Mirrors the design of <see cref="IDeviceService"/> at the package-wide level — each child is
	/// also independently registerable / mockable.
	/// </summary>
	/// <remarks>
	/// Gestures are NOT exposed here. <c>GestureController</c> is a <c>MonoBehaviour</c> consumers
	/// attach to a scene GameObject; surfacing it through a service-locator-style facade would create
	/// the wrong mental model (the gesture surface is per-scene, not per-app).
	/// </remarks>
	public interface IMobileService
	{
		/// <summary>Native UI (alerts, toasts, share, review).</summary>
		INativeUiService NativeUi { get; }

		/// <summary>Local notification scheduling.</summary>
		INotificationService Notifications { get; }

		/// <summary>Cross-platform haptic feedback.</summary>
		IHapticsService Haptics { get; }

		/// <summary>Device sub-services (safe area, screen wake, battery, audio session, permissions, ATT, deep link).</summary>
		IDeviceService Device { get; }
	}

	/// <summary>
	/// Default <see cref="IMobileService"/> implementation. Constructs every child internally using
	/// each subsystem's default constructor — adequate for the common case. Tests should construct
	/// the children themselves and pass them through the injection constructor.
	/// </summary>
	public sealed class MobileService : IMobileService, System.IDisposable
	{
		/// <inheritdoc />
		public INativeUiService NativeUi { get; }
		/// <inheritdoc />
		public INotificationService Notifications { get; }
		/// <inheritdoc />
		public IHapticsService Haptics { get; }
		/// <inheritdoc />
		public IDeviceService Device { get; }

		public MobileService() : this(
			new NativeUiServiceInstance(),
			new MobileNotificationService(new GameNotificationChannel("default", "Default", "Default notifications")),
			new HapticsService(),
			new DeviceService())
		{
		}

		public MobileService(
			INativeUiService nativeUi,
			INotificationService notifications,
			IHapticsService haptics,
			IDeviceService device)
		{
			NativeUi = nativeUi;
			Notifications = notifications;
			Haptics = haptics;
			Device = device;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			(Device as System.IDisposable)?.Dispose();
		}
	}
}

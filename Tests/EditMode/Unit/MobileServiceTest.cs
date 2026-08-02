using GameLovers.MobileServices;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.NativeUi;
using GameLovers.MobileServices.Notifications;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class MobileServiceTest
	{
		[Test]
		// ADMIT: MobileService's injection ctor could store something other than the supplied child instance.
		// RCR: IMobileService.cs MobileService(4-arg) — `Haptics = haptics` → `= new HapticsService()` → RED (AreSame fails on Haptics).
		public void InjectionCtor_StoresEachChild()
		{
			var nativeUi = Substitute.For<INativeUiService>();
			var notifications = Substitute.For<INotificationService>();
			var haptics = Substitute.For<IHapticsService>();
			var device = Substitute.For<IDeviceService>();

			var mobile = new MobileService(nativeUi, notifications, haptics, device);

			Assert.AreSame(nativeUi, mobile.NativeUi);
			Assert.AreSame(notifications, mobile.Notifications);
			Assert.AreSame(haptics, mobile.Haptics);
			Assert.AreSame(device, mobile.Device);
		}

		[Test]
		// ADMIT: MobileService.Dispose could hard-cast Device to IDisposable and throw for a non-disposable implementation.
		// RCR: IMobileService.cs MobileService.Dispose — `(Device as System.IDisposable)?.Dispose()` → `((System.IDisposable) Device).Dispose()` → RED (InvalidCastException where none expected).
		public void Dispose_DoesNotThrow_WhenDeviceIsNotDisposable()
		{
			var mobile = new MobileService(
				Substitute.For<INativeUiService>(),
				Substitute.For<INotificationService>(),
				Substitute.For<IHapticsService>(),
				Substitute.For<IDeviceService>());
			Assert.DoesNotThrow(mobile.Dispose);
		}
	}
}

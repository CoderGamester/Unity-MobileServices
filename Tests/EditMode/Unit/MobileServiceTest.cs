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

		[Test]
		// ADMIT: MobileService.Dispose could omit a disposable child or dispose a child again on a repeated call.
		// RCR: IMobileService.cs MobileService.Dispose — remove DisposeChild(NativeUi, disposed) → RED
		// (native UI Dispose count expected 1, was 0).
		public void Dispose_DisposesEachDistinctDisposableInstance_Once()
		{
			var nativeUi = Substitute.For<INativeUiService, System.IDisposable>();
			var notifications = Substitute.For<INotificationService, System.IDisposable>();
			var haptics = Substitute.For<IHapticsService, System.IDisposable>();
			var device = Substitute.For<IDeviceService, System.IDisposable>();
			var mobile = new MobileService(
				nativeUi,
				notifications,
				haptics,
				device);

			mobile.Dispose();
			mobile.Dispose();

			((System.IDisposable)nativeUi).Received(1).Dispose();
			((System.IDisposable)notifications).Received(1).Dispose();
			((System.IDisposable)haptics).Received(1).Dispose();
			((System.IDisposable)device).Received(1).Dispose();
		}

		[Test]
		// ADMIT: MobileService.Dispose could invoke Dispose twice when two facade children share one instance.
		// RCR: IMobileService.cs DisposeChild — replace the ReferenceEquals return with continue → RED
		// (shared Dispose count expected 1, was 2).
		public void Dispose_SharedDisposableChild_DisposesOnce()
		{
			var shared = Substitute.For<INotificationService, IDeviceService, System.IDisposable>();
			var mobile = new MobileService(
				Substitute.For<INativeUiService>(),
				shared,
				Substitute.For<IHapticsService>(),
				(IDeviceService)shared);

			mobile.Dispose();

			((System.IDisposable)shared).Received(1).Dispose();
		}
	}
}

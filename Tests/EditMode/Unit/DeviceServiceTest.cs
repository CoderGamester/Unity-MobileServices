using System;
using GameLovers.MobileServices.Device;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class DeviceServiceTest
	{
		// Marker interfaces co-implemented with IDisposable so NSubstitute can produce a proxy
		// that satisfies both the umbrella's interface contract AND the IDisposable check inside
		// DeviceService.Dispose. The DeviceService ctor accepts the parent interface; the more-
		// derived disposable variant is implicitly convertible to it.
		public interface ISafeAreaServiceDisposable : ISafeAreaService, IDisposable { }
		public interface IBatteryServiceDisposable  : IBatteryService,  IDisposable { }
		public interface IDeepLinkServiceDisposable : IDeepLinkService, IDisposable { }

		private ISafeAreaServiceDisposable _safeArea;
		private IBatteryServiceDisposable _battery;
		private IIosAudioSessionService _audioSession;
		private IPermissionsService _permissions;
		private IAttService _att;
		private IDeepLinkServiceDisposable _deepLink;
		private int _originalSleepTimeout;

		[SetUp]
		public void Init()
		{
			_originalSleepTimeout = Screen.sleepTimeout;
			_safeArea = Substitute.For<ISafeAreaServiceDisposable>();
			_battery = Substitute.For<IBatteryServiceDisposable>();
			_audioSession = Substitute.For<IIosAudioSessionService>();
			_permissions = Substitute.For<IPermissionsService>();
			_att = Substitute.For<IAttService>();
			_deepLink = Substitute.For<IDeepLinkServiceDisposable>();
		}

		[TearDown]
		public void Cleanup()
		{
			Screen.sleepTimeout = _originalSleepTimeout;
		}

		[Test]
		// ADMIT: DeviceService's injection ctor could store something other than the supplied instance on a child property.
		// RCR: DeviceService.cs DeviceService(6-arg) — `Att = att` → `Att = new AttService()` → RED (AreSame fails on Att).
		public void InjectionCtor_StoresEachChildOnMatchingProperty()
		{
			var service = new DeviceService(
				_safeArea,
				_battery,
				_audioSession,
				_permissions,
				_att,
				_deepLink);

			Assert.AreSame(_safeArea, service.SafeArea);
			Assert.AreSame(_battery, service.Battery);
			Assert.AreSame(_audioSession, service.AudioSession);
			Assert.AreSame(_permissions, service.Permissions);
			Assert.AreSame(_att, service.Att);
			Assert.AreSame(_deepLink, service.DeepLink);
		}

		[Test]
		// ADMIT: DeviceService.Dispose could skip a disposable child, leaking that child's host registrations.
		// RCR: DeviceService.cs Dispose — drop `(Battery as IDisposable)?.Dispose()` → RED (Battery expected 1 call to Dispose, got 0).
		public void Dispose_DisposesDisposableChildren_OnlyOnce()
		{
			var service = new DeviceService(
				_safeArea,
				_battery,
				_audioSession,
				_permissions,
				_att,
				_deepLink);

			service.Dispose();

			_safeArea.Received(1).Dispose();
			_battery.Received(1).Dispose();
			_deepLink.Received(1).Dispose();
		}

		[Test]
		// ADMIT: DeviceService.Dispose could hard-cast children to IDisposable and throw for a non-disposable implementation.
		// RCR: DeviceService.cs Dispose — `(SafeArea as IDisposable)?.Dispose()` → `((IDisposable) SafeArea).Dispose()` → RED (InvalidCastException where none expected).
		public void Dispose_NonDisposableChildren_NoThrow()
		{
			var nonDisposableSafeArea = Substitute.For<ISafeAreaService>();
			var nonDisposableBattery = Substitute.For<IBatteryService>();
			var nonDisposableDeepLink = Substitute.For<IDeepLinkService>();

			var service = new DeviceService(
				nonDisposableSafeArea,
				nonDisposableBattery,
				_audioSession,
				_permissions,
				_att,
				nonDisposableDeepLink);

			Assert.DoesNotThrow(service.Dispose);
		}

		[Test]
		// ADMIT: DeviceService.KeepAwake = true could fail to prevent the screen from dimming.
		// RCR: DeviceService.cs KeepAwake.set — true branch `NeverSleep` → `SystemSetting` → RED (expected NeverSleep). 2026-08-13
		public void KeepAwake_True_SetsScreenSleepTimeoutNeverSleep()
		{
			DeviceService.KeepAwake = true;

			Assert.AreEqual(SleepTimeout.NeverSleep, Screen.sleepTimeout);
		}

		[Test]
		// ADMIT: DeviceService.KeepAwake = false could leave the screen awake after the application releases the override.
		// RCR: DeviceService.cs KeepAwake.set — false branch `SystemSetting` → `NeverSleep` → RED (expected SystemSetting). 2026-08-13
		public void KeepAwake_False_RestoresSystemSetting()
		{
			DeviceService.KeepAwake = true;
			DeviceService.KeepAwake = false;

			Assert.AreEqual(SleepTimeout.SystemSetting, Screen.sleepTimeout);
		}

		[Test]
		// ADMIT: DeviceService.KeepAwake could report the inverse of the active screen timeout.
		// RCR: DeviceService.cs KeepAwake.get — `==` → `!=` → RED (expected true for NeverSleep). 2026-08-13
		public void KeepAwake_Get_ReflectsScreenSleepTimeout()
		{
			Screen.sleepTimeout = SleepTimeout.NeverSleep;
			Assert.IsTrue(DeviceService.KeepAwake);

			Screen.sleepTimeout = SleepTimeout.SystemSetting;
			Assert.IsFalse(DeviceService.KeepAwake);
		}
	}
}

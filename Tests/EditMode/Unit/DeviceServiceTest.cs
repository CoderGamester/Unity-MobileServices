using System;
using GameLovers.MobileServices.Device;
using NSubstitute;
using NUnit.Framework;

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
		private IScreenWakeService _screenWake;
		private IBatteryServiceDisposable _battery;
		private IIosAudioSessionService _audioSession;
		private IPermissionsService _permissions;
		private IAttService _att;
		private IDeepLinkServiceDisposable _deepLink;

		[SetUp]
		public void Init()
		{
			_safeArea = Substitute.For<ISafeAreaServiceDisposable>();
			_screenWake = Substitute.For<IScreenWakeService>();
			_battery = Substitute.For<IBatteryServiceDisposable>();
			_audioSession = Substitute.For<IIosAudioSessionService>();
			_permissions = Substitute.For<IPermissionsService>();
			_att = Substitute.For<IAttService>();
			_deepLink = Substitute.For<IDeepLinkServiceDisposable>();
		}

		[Test]
		public void InjectionCtor_StoresEachChildOnMatchingProperty()
		{
			var service = new DeviceService(
				_safeArea,
				_screenWake,
				_battery,
				_audioSession,
				_permissions,
				_att,
				_deepLink);

			Assert.AreSame(_safeArea, service.SafeArea);
			Assert.AreSame(_screenWake, service.ScreenWake);
			Assert.AreSame(_battery, service.Battery);
			Assert.AreSame(_audioSession, service.AudioSession);
			Assert.AreSame(_permissions, service.Permissions);
			Assert.AreSame(_att, service.Att);
			Assert.AreSame(_deepLink, service.DeepLink);
		}

		[Test]
		public void Dispose_DisposesDisposableChildren_OnlyOnce()
		{
			var service = new DeviceService(
				_safeArea,
				_screenWake,
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
		public void Dispose_NonDisposableChildren_NoThrow()
		{
			var nonDisposableSafeArea = Substitute.For<ISafeAreaService>();
			var nonDisposableBattery = Substitute.For<IBatteryService>();
			var nonDisposableDeepLink = Substitute.For<IDeepLinkService>();

			var service = new DeviceService(
				nonDisposableSafeArea,
				_screenWake,
				nonDisposableBattery,
				_audioSession,
				_permissions,
				_att,
				nonDisposableDeepLink);

			Assert.DoesNotThrow(service.Dispose);
		}
	}
}

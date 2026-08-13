using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Device.Internal;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	public class DeviceServicePlayModeTest
	{
		private DeviceService _service;

		[SetUp]
		public void Init()
		{
			_service = new DeviceService();
		}

		[TearDown]
		public void Cleanup()
		{
			_service.Dispose();
			DeviceServicesHost.ResetForTests();
		}

		[Test]
		// ADMIT: DeviceService.BuildDefaults could leave a child service unconstructed, NREing the first consumer that touches it.
		// RCR: DeviceService.cs BuildDefaults — `new IosAudioSessionService()` → `null` → RED (AudioSession expected not null).
		public void DefaultCtor_WiresAllSubServices_NonNull()
		{
			Assert.IsNotNull(_service.SafeArea);
			Assert.IsNotNull(_service.Battery);
			Assert.IsNotNull(_service.AudioSession);
			Assert.IsNotNull(_service.Permissions);
			Assert.IsNotNull(_service.Att);
			Assert.IsNotNull(_service.DeepLink);
		}
	}
}

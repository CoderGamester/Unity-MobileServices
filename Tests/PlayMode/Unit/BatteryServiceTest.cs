using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Device.Internal;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	public class BatteryServiceTest
	{
		private BatteryService _service;

		[SetUp]
		public void Init()
		{
			_service = new BatteryService(DeviceServicesHost.Instance);
		}

		[TearDown]
		public void Cleanup()
		{
			_service.Dispose();
			DeviceServicesHost.ResetForTests();
		}

		[Test]
		public void Ctor_CapturesInitialLevelStatusAndLowPowerMode()
		{
			Assert.AreEqual(SystemInfo.batteryLevel, _service.Level);
			Assert.AreEqual(SystemInfo.batteryStatus, _service.Status);
			// On Editor / unsupported platforms, low-power-mode is reported as false (see BatteryService.QueryLowPowerMode).
			Assert.IsFalse(_service.IsLowPowerMode);
		}

		[Test]
		public void Dispose_UnregistersAllHostHandlers()
		{
			Assert.DoesNotThrow(_service.Dispose);
			Assert.DoesNotThrow(_service.Dispose, "Dispose should be idempotent (subtracting an already-removed handler is a no-op).");
		}
	}
}

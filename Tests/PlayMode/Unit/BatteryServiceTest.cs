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
		// ADMIT: BatteryService.QueryLowPowerMode could report low-power mode on a bare Editor where no override is engaged.
		// RCR: BatteryService.cs QueryLowPowerMode — editor branch `return EditorLowPowerModeOverride` → `return !EditorLowPowerModeOverride` → RED (IsLowPowerMode expected False). Also reddens DefaultCtor_UsesSharedHost_CapturesInitialState.
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

		[Test]
		public void DefaultCtor_UsesSharedHost_CapturesInitialState()
		{
			var service = new BatteryService();

			Assert.AreEqual(SystemInfo.batteryLevel, service.Level);
			Assert.AreEqual(SystemInfo.batteryStatus, service.Status);
			Assert.IsFalse(service.IsLowPowerMode);

			service.Dispose();
		}
	}
}

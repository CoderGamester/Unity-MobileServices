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
		// RCR: BatteryService.cs QueryLowPowerMode — editor branch `return EditorLowPowerModeOverride` → `return !EditorLowPowerModeOverride` → RED (IsLowPowerMode expected False). Also reddens DefaultCtor_RegistersOnSharedHost.
		public void Ctor_CapturesInitialLevelStatusAndLowPowerMode()
		{
			Assert.AreEqual(SystemInfo.batteryLevel, _service.Level);
			Assert.AreEqual(SystemInfo.batteryStatus, _service.Status);
			// On Editor / unsupported platforms, low-power-mode is reported as false (see BatteryService.QueryLowPowerMode).
			Assert.IsFalse(_service.IsLowPowerMode);
		}

		[Test]
		// ADMIT: BatteryService.Dispose must unregister from DeviceServicesHost, or a disposed service keeps
		// reacting to the host's iOS low-power-mode fan-out.
		// RCR: BatteryService.cs Dispose - comment out
		// `_host.UnregisterIosLowPowerModeChanged(OnIosLowPowerModeChanged);` -> RED (fired expected 0, was
		// 1). 2026-08-04
		public void Dispose_UnregistersAllHostHandlers()
		{
#if UNITY_EDITOR
			var host = DeviceServicesHost.Instance;
			var fired = 0;
			_service.OnLowPowerModeChanged += () => fired++;

			_service.Dispose();

			var lowPowerBeforeDrive = _service.IsLowPowerMode;
			BatteryService.EditorLowPowerModeOverride = !lowPowerBeforeDrive;
			try
			{
				host.OnIosLowPowerModeChanged(string.Empty);

				Assert.AreEqual(0, fired, "A disposed BatteryService must not still be on the host's LPM fan-out.");
				Assert.AreEqual(lowPowerBeforeDrive, _service.IsLowPowerMode,
					"A disposed BatteryService must not refresh its cached low-power-mode state.");
			}
			finally
			{
				BatteryService.EditorLowPowerModeOverride = false;
			}
#endif
			Assert.DoesNotThrow(_service.Dispose, "Dispose should be idempotent (subtracting an already-removed handler is a no-op).");
		}

		[Test]
		// ADMIT: BatteryService's parameterless ctor must chain to DeviceServicesHost.Instance, or a service
		// built that way never hears the shared host's low-power-mode signal.
		// RCR: BatteryService.cs BatteryService() - `: this(DeviceServicesHost.Instance)` -> a privately
		// constructed host -> RED (fired expected 1, was 0), while
		// Ctor_CapturesInitialLevelStatusAndLowPowerMode stays green. 2026-08-04
		public void DefaultCtor_RegistersOnSharedHost()
		{
#if !UNITY_EDITOR
			Assert.Ignore("Drives BatteryService.EditorLowPowerModeOverride, which only exists in the editor.");
#else
			// The override is a process-wide static, so pin it low before the ctor snapshots it.
			BatteryService.EditorLowPowerModeOverride = false;

			var service = new BatteryService();
			var fired = 0;
			service.OnLowPowerModeChanged += () => fired++;

			BatteryService.EditorLowPowerModeOverride = true;
			try
			{
				DeviceServicesHost.Instance.OnIosLowPowerModeChanged(string.Empty);

				Assert.AreEqual(1, fired,
					"The parameterless ctor must register on DeviceServicesHost.Instance, not on a private host.");
				Assert.IsTrue(service.IsLowPowerMode);
			}
			finally
			{
				BatteryService.EditorLowPowerModeOverride = false;
				service.Dispose();
			}
#endif
		}
	}
}

using GameLovers.MobileServices.Device;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class ScreenWakeServiceTest
	{
		private ScreenWakeService _service;
		private int _originalTimeout;

		[SetUp]
		public void Init()
		{
			_originalTimeout = Screen.sleepTimeout;
			_service = new ScreenWakeService();
		}

		[TearDown]
		public void Cleanup()
		{
			Screen.sleepTimeout = _originalTimeout;
		}

		[Test]
		// ADMIT: ScreenWakeService.KeepAwake = true could fail to set SleepTimeout.NeverSleep, letting the screen dim mid-session.
		// RCR: ScreenWakeService.cs KeepAwake.set — true branch `SleepTimeout.NeverSleep` → `SleepTimeout.SystemSetting` → RED (expected NeverSleep was SystemSetting).
		public void KeepAwake_True_SetsScreenSleepTimeoutNeverSleep()
		{
			_service.KeepAwake = true;

			Assert.AreEqual(SleepTimeout.NeverSleep, Screen.sleepTimeout);
		}

		[Test]
		// ADMIT: ScreenWakeService.KeepAwake = false could fail to restore SleepTimeout.SystemSetting, keeping the screen awake forever.
		// RCR: ScreenWakeService.cs KeepAwake.set — false branch `SleepTimeout.SystemSetting` → `SleepTimeout.NeverSleep` → RED (expected SystemSetting was NeverSleep).
		public void KeepAwake_False_RestoresSystemSetting()
		{
			_service.KeepAwake = true;
			_service.KeepAwake = false;

			Assert.AreEqual(SleepTimeout.SystemSetting, Screen.sleepTimeout);
		}

		[Test]
		// ADMIT: ScreenWakeService.KeepAwake's getter could invert its comparison against Screen.sleepTimeout.
		// RCR: ScreenWakeService.cs KeepAwake.get — `==` → `!=` → RED (expected True was False for NeverSleep).
		public void KeepAwake_Get_ReflectsScreenSleepTimeout()
		{
			Screen.sleepTimeout = SleepTimeout.NeverSleep;
			Assert.IsTrue(_service.KeepAwake);

			Screen.sleepTimeout = SleepTimeout.SystemSetting;
			Assert.IsFalse(_service.KeepAwake);
		}
	}
}

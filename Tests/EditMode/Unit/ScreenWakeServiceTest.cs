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
		public void KeepAwake_True_SetsScreenSleepTimeoutNeverSleep()
		{
			_service.KeepAwake = true;

			Assert.AreEqual(SleepTimeout.NeverSleep, Screen.sleepTimeout);
		}

		[Test]
		public void KeepAwake_False_RestoresSystemSetting()
		{
			_service.KeepAwake = true;
			_service.KeepAwake = false;

			Assert.AreEqual(SleepTimeout.SystemSetting, Screen.sleepTimeout);
		}

		[Test]
		public void KeepAwake_Get_ReflectsScreenSleepTimeout()
		{
			Screen.sleepTimeout = SleepTimeout.NeverSleep;
			Assert.IsTrue(_service.KeepAwake);

			Screen.sleepTimeout = SleepTimeout.SystemSetting;
			Assert.IsFalse(_service.KeepAwake);
		}
	}
}

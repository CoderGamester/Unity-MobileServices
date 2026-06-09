using System.Collections;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Device.Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	public class SafeAreaServiceTest
	{
		private SafeAreaService _service;

		[SetUp]
		public void Init()
		{
			_service = new SafeAreaService(DeviceServicesHost.Instance);
		}

		[TearDown]
		public void Cleanup()
		{
			_service.Dispose();
			DeviceServicesHost.ResetForTests();
		}

		[Test]
		public void Ctor_CapturesInitialSafeArea()
		{
			Assert.AreEqual(Screen.safeArea, _service.SafeArea);
		}

		[Test]
		public void DefaultCtor_UsesSharedHost_CapturesInitialSafeArea()
		{
			var service = new SafeAreaService();

			Assert.AreEqual(Screen.safeArea, service.SafeArea);

			service.Dispose();
		}

		[UnityTest]
		public IEnumerator Tick_ScreenSafeAreaUnchanged_DoesNotFireEvent()
		{
			var fireCount = 0;
			_service.OnSafeAreaChanged += _ => fireCount++;

			yield return null;
			yield return null;
			yield return null;

			Assert.AreEqual(0, fireCount);
		}

		[UnityTest]
		public IEnumerator Dispose_UnregistersFromHost()
		{
			var fireCountAfterDispose = 0;
			_service.OnSafeAreaChanged += _ => fireCountAfterDispose++;
			_service.Dispose();

			yield return null;
			yield return null;

			Assert.AreEqual(0, fireCountAfterDispose,
				"After Dispose, the safe-area service should be detached from the host's LateUpdate fan-out.");
		}
	}
}

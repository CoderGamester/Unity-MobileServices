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
		// ADMIT: SafeAreaService could report a safe area other than the one it captured at construction.
		// RCR: SafeAreaService.cs SafeArea — offset the returned rect's x by 1 → RED (SafeArea != Screen.safeArea). Also reddens DefaultCtor_UsesSharedHost_CapturesInitialSafeArea.
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
		// ADMIT: SafeAreaService.Tick could fire OnSafeAreaChanged every LateUpdate instead of only on a real diff.
		// RCR: SafeAreaService.cs Tick — diff guard `current == _lastSafeArea` → `current != _lastSafeArea` → RED (fireCount expected 0, fired every frame).
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
		// ADMIT: SafeAreaService.Dispose must detach Tick from DeviceServicesHost's LateUpdate fan-out.
		// RCR: none exists — with the safe area unchanged, no event fires whether or not the handler is still registered:
		// Dispose's UnregisterLateUpdate and Tick's `current == _lastSafeArea` diff guard each suppress it alone (verified).
		// Double-covered, not single-line falsifiable.
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

using System.Collections;
using GameLovers.MobileServices.Device.Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	public class DeviceServicesHostTest
	{
		[TearDown]
		public void Cleanup()
		{
			DeviceServicesHost.ResetForTests();
		}

		[UnityTest]
		public IEnumerator Instance_LazilySpawnsGameObject_DontDestroyOnLoad()
		{
			Assert.IsNull(GameObject.Find("DeviceServicesHost"), "Pre-condition: no host before access");

			var host = DeviceServicesHost.Instance;
			yield return null;

			var go = GameObject.Find("DeviceServicesHost");
			Assert.IsNotNull(go);
			Assert.AreEqual(go, host.gameObject);
			Assert.AreEqual("DontDestroyOnLoad", go.scene.name);
		}

		[UnityTest]
		public IEnumerator RegisterLateUpdate_FiresEachLateUpdateFrame()
		{
			var host = DeviceServicesHost.Instance;
			var callCount = 0;
			host.RegisterLateUpdate(() => callCount++);

			yield return null;
			yield return null;

			Assert.GreaterOrEqual(callCount, 2);
		}

		[UnityTest]
		public IEnumerator RegisterSecondTick_FiresApproximatelyOncePerSecond()
		{
			var host = DeviceServicesHost.Instance;
			var callCount = 0;
			host.RegisterSecondTick(() => callCount++);

			yield return new WaitForSecondsRealtime(2.2f);

			Assert.GreaterOrEqual(callCount, 1);
			Assert.LessOrEqual(callCount, 4, "Second-tick should not fire more than ~once per second");
		}

		[UnityTest]
		public IEnumerator RegisterFocusChanged_FiresOnApplicationFocus()
		{
			var host = DeviceServicesHost.Instance;
			yield return null;

			var lastFocus = false;
			var callCount = 0;
			host.RegisterFocusChanged(focused =>
			{
				lastFocus = focused;
				callCount++;
			});

			// Drive Unity's MonoBehaviour message dispatcher — same path the engine uses to
			// notify focus changes. This is the documented black-box entry point for testing
			// Unity callbacks; no private-field reflection is used.
			host.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);

			Assert.AreEqual(1, callCount);
			Assert.IsTrue(lastFocus);
		}

		[Test]
		public void OnIosLowPowerModeChanged_PublicMethod_FanOutsToSubscribers()
		{
			var host = DeviceServicesHost.Instance;
			var callCount = 0;
			host.RegisterIosLowPowerModeChanged(() => callCount++);

			// Public entry point — same one the iOS native bridge invokes via UnitySendMessage.
			host.OnIosLowPowerModeChanged(string.Empty);
			host.OnIosLowPowerModeChanged(string.Empty);

			Assert.AreEqual(2, callCount);
		}

		[UnityTest]
		public IEnumerator ResetForTests_DestroysSingleton()
		{
			_ = DeviceServicesHost.Instance;
			Assert.IsNotNull(GameObject.Find("DeviceServicesHost"));

			DeviceServicesHost.ResetForTests();

			// In PlayMode ResetForTests routes through Object.Destroy(go) which is deferred
			// to end-of-frame; yield once so Find can no longer locate the destroyed GO.
			yield return null;

			Assert.IsNull(GameObject.Find("DeviceServicesHost"));
		}
	}
}

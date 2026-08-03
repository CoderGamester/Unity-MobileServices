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
		// ADMIT: DeviceServicesHost.Instance could spawn its host outside DontDestroyOnLoad, killing every device poll on the first scene load.
		// RCR: DeviceServicesHost.cs Instance — drop `DontDestroyOnLoad(go)` → RED (go.scene.name expected 'DontDestroyOnLoad').
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
		// ADMIT: DeviceServicesHost.LateUpdate could stop fanning out to its per-frame subscribers, freezing SafeAreaService.
		// RCR: DeviceServicesHost.cs LateUpdate — drop `_onLateUpdate?.Invoke()` → RED (callCount 0, expected >= 2).
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
		// ADMIT: DeviceServicesHost could stop crossing its one-second accumulator threshold, so BatteryService never polls.
		// RCR: DeviceServicesHost.cs LateUpdate — `_secondAccumulator >= 1f` → `>= 100f` → RED (callCount 0, expected >= 1).
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
		// ADMIT: DeviceServicesHost.OnApplicationFocus could forward the wrong focus value to its subscribers.
		// RCR: DeviceServicesHost.cs OnApplicationFocus — `Invoke(focused)` → `Invoke(!focused)` → RED (lastFocus expected True was False).
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
		// ADMIT: DeviceServicesHost.OnIosLowPowerModeChanged (the UnitySendMessage entry point) could stop fanning out to subscribers.
		// RCR: DeviceServicesHost.cs OnIosLowPowerModeChanged — drop `_onIosLowPowerModeChanged?.Invoke()` → RED (callCount expected 2 was 0).
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
		// ADMIT: DeviceServicesHost.ResetForTests could clear the static without destroying the GameObject, leaking a host per fixture.
		// RCR: DeviceServicesHost.cs ResetForTests — replace `Destroy(go)` with a no-op → RED (Find('DeviceServicesHost') expected null). Cascades: also reddens Instance_LazilySpawnsGameObject's no-host precondition.
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

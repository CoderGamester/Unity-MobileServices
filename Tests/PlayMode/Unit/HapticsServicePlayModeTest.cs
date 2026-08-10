using System.Collections;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Haptics.Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	public class HapticsServicePlayModeTest
	{
		private FakeHapticsBackend _backend;
		private HapticsService _haptics;

		[SetUp]
		public void Init()
		{
			_backend = new FakeHapticsBackend { IsSupportedValue = true };
			_haptics = new HapticsService(_backend);
		}

		[TearDown]
		public void Cleanup()
		{
			_haptics.StopCurrentHaptic();

			// HapticsHost is DontDestroyOnLoad and lazy-spawned; tear it down between tests so
			// each [SetUp] starts from a clean slate without cross-test pollution.
			var host = GameObject.Find("HapticsHost");
			if (host != null)
			{
				Object.Destroy(host);
			}
		}

		[UnityTest]
		// ADMIT: HapticsService.PlayPresetDuration could skip scheduling the auto-stop for a positive duration, looping forever.
		// RCR: HapticsService.cs PlayPresetDuration — `if (duration > 0f)` → `> 0.15f` → RED (IsPlaying still True after 0.25s); narrowed so the 0.2s/0.5s siblings stay green.
		public IEnumerator PlayPresetDuration_Positive_AutoStopsAfterDuration()
		{
			_haptics.PlayPresetDuration(HapticPreset.Selection, 0.1f);
			Assert.IsTrue(_haptics.IsPlaying);
			Assert.AreEqual(0, _backend.StopCount);

			yield return new WaitForSecondsRealtime(0.25f);

			Assert.IsFalse(_haptics.IsPlaying);
			Assert.AreEqual(1, _backend.StopCount);
		}

		[UnityTest]
		// ADMIT: HapticsService.PlayCustom could schedule its auto-stop in milliseconds instead of seconds, stretching the stop 1000x.
		// RCR: HapticsService.cs PlayCustom — `ScheduleStop(durationMs / 1000f, ...)` → `ScheduleStop(durationMs, ...)` → RED (IsPlaying still True after 0.25s).
		public IEnumerator PlayCustom_AutoStopsAfterDurationMs()
		{
			_haptics.PlayCustom(0.7f, 100f);
			Assert.IsTrue(_haptics.IsPlaying);
			Assert.AreEqual(0, _backend.StopCount);

			yield return new WaitForSecondsRealtime(0.25f);

			Assert.IsFalse(_haptics.IsPlaying);
			Assert.AreEqual(1, _backend.StopCount);
		}

		[UnityTest]
		// ADMIT: An explicit StopCurrentHaptic must not be followed by a second backend Stop from the pending auto-stop coroutine.
		// RCR: none exists — the second Stop is blocked by both HapticsService.CancelPendingAutoStop/HapticsHost.Cancel and
		// OnAutoStop's `if (!_isPlaying) return` guard; disabling either leaves the other suppressing it (verified).
		// Double-covered, not single-line falsifiable.
		public IEnumerator StopCurrentHaptic_CancelsPendingAutoStop()
		{
			_haptics.PlayPresetDuration(HapticPreset.Warning, 0.2f);
			_haptics.StopCurrentHaptic();
			Assert.AreEqual(1, _backend.StopCount);
			Assert.IsFalse(_haptics.IsPlaying);

			yield return new WaitForSecondsRealtime(0.3f);

			// No second Stop should have fired from a pending auto-stop coroutine.
			Assert.AreEqual(1, _backend.StopCount);
		}

		[Test]
		// ADMIT: HapticsService.PlayCustom could pass an out-of-range intensity straight to the backend.
		// RCR: HapticsService.cs PlayCustom — `Mathf.Clamp01(intensity01)` → `Mathf.Abs(intensity01)` → RED (LastIntensity expected 1 was 2.5).
		public void PlayCustom_ClampsIntensity01()
		{
			_haptics.PlayCustom(2.5f, 100f);
			Assert.AreEqual(1f, _backend.LastIntensity);

			_haptics.PlayCustom(-1f, 100f);
			Assert.AreEqual(0f, _backend.LastIntensity);

			_haptics.PlayCustom(0.42f, 100f);
			Assert.AreEqual(0.42f, _backend.LastIntensity, 1e-6f);
		}

		[UnityTest]
		// ADMIT: destroying HapticsHost mid-countdown must not fire the auto-stop callback into a service
		// whose host is gone.
		// RCR: none exists - the callback is suppressed by both HapticsHost.OnDestroy's Cancel() and Unity's
		// own termination of the coroutine when the GameObject dies; removing Cancel() leaves the engine
		// teardown suppressing it (verified). Double-covered, not single-line falsifiable.
		public IEnumerator HapticsHost_OnDestroy_CancelsPendingAutoStop()
		{
			_haptics.PlayPresetDuration(HapticPreset.ImpactHeavy, 0.5f);

			var host = GameObject.Find("HapticsHost");
			Assert.IsNotNull(host, "HapticsHost should be spawned by the time PlayPresetDuration with positive duration returns");

			Object.Destroy(host);
			yield return null;

			yield return new WaitForSecondsRealtime(0.6f);

			// Host destroyed before the scheduled stop fired: no Stop callback should have run.
			// _isPlaying remains true on the service because the auto-stop coroutine never reached its callback;
			// caller would normally observe this and explicitly call StopCurrentHaptic().
			Assert.AreEqual(0, _backend.StopCount);
		}

		[UnityTest]
		// ADMIT: HapticsService.Dispose could release ownership without stopping active output or
		// destroying the lazily-created host, leaking a DontDestroyOnLoad object between sessions.
		// RCR: HapticsService.cs Dispose — replace UnityEngine.Object.Destroy with return → RED
		// (host remains after one frame).
		public IEnumerator Dispose_StopsOutput_DestroysHost_AndIsIdempotent()
		{
			_haptics.PlayPresetDuration(HapticPreset.Warning, 0.5f);
			var host = GameObject.Find("HapticsHost");
			Assert.IsNotNull(host);

			_haptics.Dispose();
			Assert.AreEqual(1, _backend.StopCount);
			Assert.IsFalse(_haptics.IsPlaying);
			Assert.DoesNotThrow(_haptics.Dispose);

			yield return null;

			Assert.IsTrue(host == null, "Dispose should destroy this service's own host");
		}

		private sealed class FakeHapticsBackend : IHapticsBackend
		{
			public bool IsSupportedValue;
			public int StopCount;
			public float LastIntensity;
			public float LastDurationMs;

			public bool IsSupported => IsSupportedValue;

			public void PlayPresetOneShot(HapticPreset preset) { }
			public void PlayPresetLoop(HapticPreset preset) { }

			public void PlayCustom(float intensity01, float durationMs)
			{
				LastIntensity = intensity01;
				LastDurationMs = durationMs;
			}

			public void Stop()
			{
				StopCount++;
			}
		}
	}
}

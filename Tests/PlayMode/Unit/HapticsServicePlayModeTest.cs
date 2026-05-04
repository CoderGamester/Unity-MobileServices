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

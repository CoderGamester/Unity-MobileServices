using System.Collections.Generic;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Haptics.Internal;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class HapticsServiceTest
	{
		private FakeHapticsBackend _backend;
		private HapticsService _haptics;

		[SetUp]
		public void Init()
		{
			_backend = new FakeHapticsBackend { IsSupportedValue = true };
			_haptics = new HapticsService(_backend);
		}

		[Test]
		// ADMIT: HapticsService.Enabled could read back false on a fresh service, so consumers gate haptics off by default.
		// RCR: HapticsService.cs Enabled.get — `get => _enabled` → `get => false` → RED (expected True was False). Also reddens DefaultCtor_InEditor_SelectsEditorBackend_NotSupported.
		public void Enabled_DefaultIsTrue()
		{
			Assert.IsTrue(_haptics.Enabled);
		}

		[Test]
		// ADMIT: HapticsService.Enabled's setter could act on a no-op assignment and stop a haptic that is legitimately still playing.
		// RCR: HapticsService.cs Enabled.set — call StopCurrentHaptic() inside the `_enabled == value` early-return branch → RED (StopCount expected 0 was 1).
		public void Enabled_SetSameValue_DoesNothing()
		{
			_haptics.PlayPreset(HapticPreset.Selection);
			_backend.Reset();

			_haptics.Enabled = true;

			Assert.AreEqual(0, _backend.StopCount);
		}

		[Test]
		// ADMIT: HapticsService.Enabled = false could leave the backend vibrating because the disable branch is inverted.
		// RCR: HapticsService.cs Enabled.set — `if (!_enabled)` → `if (_enabled)` → RED (StopCount expected 1 was 0).
		public void Enabled_SetFalseWhilePlaying_StopsBackend()
		{
			_haptics.PlayPreset(HapticPreset.Selection);
			_backend.Reset();

			_haptics.Enabled = false;

			Assert.IsFalse(_haptics.Enabled);
			Assert.AreEqual(1, _backend.StopCount);
			Assert.IsFalse(_haptics.IsPlaying);
		}

		[Test]
		// ADMIT: HapticsService.IsSupported could stop delegating to the backend and hard-code a capability answer.
		// RCR: HapticsService.cs IsSupported — `=> _backend.IsSupported` → `=> false` → RED (expected True was False for a supported backend).
		public void IsSupported_DelegatesToBackend()
		{
			_backend.IsSupportedValue = false;
			Assert.IsFalse(_haptics.IsSupported);

			_backend.IsSupportedValue = true;
			Assert.IsTrue(_haptics.IsSupported);
		}

		[Test]
		public void IsPlaying_FalseInitially()
		{
			Assert.IsFalse(_haptics.IsPlaying);
		}

		[Test]
		public void DefaultCtor_InEditor_SelectsEditorBackend_NotSupported()
		{
			var haptics = new HapticsService();

			Assert.IsTrue(haptics.Enabled);
			Assert.IsFalse(haptics.IsSupported);
			Assert.IsFalse(haptics.IsPlaying);
		}

		[Test]
		// ADMIT: HapticsService.PlayPresetDuration could lose its HapticPreset.None short-circuit and fire a real haptic for 'no haptic'.
		// RCR: HapticsService.cs PlayPresetDuration — `if (!_enabled || preset == HapticPreset.None)` → `if (!_enabled)` → RED (OneShotCount expected 0 was 1).
		public void PlayPreset_None_NoBackendCall()
		{
			_haptics.PlayPreset(HapticPreset.None);

			Assert.AreEqual(0, _backend.OneShotCount);
			Assert.AreEqual(0, _backend.LoopCount);
			Assert.AreEqual(0, _backend.CustomCount);
			Assert.IsFalse(_haptics.IsPlaying);
		}

		[Test]
		// ADMIT: HapticsService.PlayPresetDuration could lose its Enabled check and vibrate after the user turned haptics off.
		// RCR: HapticsService.cs PlayPresetDuration — `if (!_enabled || preset == HapticPreset.None)` → `if (preset == HapticPreset.None)` → RED (OneShotCount expected 0 was 1).
		public void PlayPreset_Disabled_NoBackendCall()
		{
			_haptics.Enabled = false;
			_backend.Reset();

			_haptics.PlayPreset(HapticPreset.Selection);

			Assert.AreEqual(0, _backend.OneShotCount);
			Assert.IsFalse(_haptics.IsPlaying);
		}

		[Test]
		// ADMIT: HapticsService.PlayPreset could forward a non-zero duration and turn the natural one-shot sugar into a loop.
		// RCR: HapticsService.cs PlayPreset — `PlayPresetDuration(preset, 0f)` → `-1f` → RED (OneShotCount expected 1 was 0).
		public void PlayPreset_Natural_CallsOneShotAndIsPlayingTrue()
		{
			_haptics.PlayPreset(HapticPreset.Selection);

			Assert.AreEqual(1, _backend.OneShotCount);
			Assert.AreEqual(HapticPreset.Selection, _backend.LastPreset);
			Assert.IsTrue(_haptics.IsPlaying);
		}

		[Test]
		// ADMIT: HapticsService.PlayPresetDuration could route duration==0 to the looping backend call instead of the one-shot.
		// RCR: HapticsService.cs PlayPresetDuration — narrow `if (duration == 0f)` to `&& preset != HapticPreset.Success` → RED (OneShotCount expected 1 was 0); narrowed so PlayPreset_Natural stays green.
		public void PlayPresetDuration_Zero_CallsOneShot()
		{
			_haptics.PlayPresetDuration(HapticPreset.Success, 0f);

			Assert.AreEqual(1, _backend.OneShotCount);
			Assert.AreEqual(0, _backend.LoopCount);
		}

		[Test]
		// ADMIT: HapticsService.PlayPresetDuration's default duration could stop being the indefinite-loop sentinel -1.
		// RCR: HapticsService.cs PlayPresetDuration — default parameter `duration = -1f` → `= 0f` → RED (LoopCount expected 1 was 0).
		public void PlayPresetDuration_NegativeOrDefault_CallsLoopNoAutoStop()
		{
			_haptics.PlayPresetDuration(HapticPreset.Warning);

			Assert.AreEqual(1, _backend.LoopCount);
			Assert.AreEqual(HapticPreset.Warning, _backend.LastPreset);
			Assert.IsTrue(_haptics.IsPlaying);
		}

		[Test]
		// ADMIT: HapticsService.PlayPresetDuration could start a new output without stopping an active loop.
		// RCR: HapticsService.cs PlayPresetDuration — `StopCurrentHaptic()` → `CancelPendingAutoStop()` → RED (StopCount expected 1 was 0).
		public void PlayPresetDuration_WhilePlaying_StopsPreviousBackendBeforeNewPreset()
		{
			_haptics.PlayPresetDuration(HapticPreset.Warning);
			_backend.Reset();

			_haptics.PlayPreset(HapticPreset.Success);

			Assert.AreEqual(1, _backend.StopCount);
			Assert.AreEqual(1, _backend.OneShotCount);
			Assert.AreEqual("Stop,OneShot", string.Join(",", _backend.Operations));
			Assert.AreEqual(HapticPreset.Success, _backend.LastPreset);
		}

		[Test]
		// ADMIT: HapticsService.PlayCustom could accept durationMs == 0 and schedule a zero-length haptic plus a host coroutine.
		// RCR: HapticsService.cs PlayCustom — `durationMs <= 0f` → `durationMs < 0f` → RED (CustomCount expected 0 was 1).
		public void PlayCustom_NonPositiveDuration_NoOp()
		{
			// Stays in EditMode because the no-op path short-circuits BEFORE EnsureHost()
			// is called — no DontDestroyOnLoad attempt.
			_haptics.PlayCustom(0.5f, 0f);
			_haptics.PlayCustom(0.5f, -10f);

			Assert.AreEqual(0, _backend.CustomCount);
			Assert.IsFalse(_haptics.IsPlaying);
		}

		// PlayCustom_ClampsIntensity01 lives in HapticsServicePlayModeTest because PlayCustom with
		// a positive durationMs always spawns the HapticsHost (DontDestroyOnLoad), which is
		// illegal in EditMode.

		[Test]
		// ADMIT: HapticsService.StopCurrentHaptic could call the backend Stop when nothing is playing, cancelling another subsystem's vibration.
		// RCR: HapticsService.cs StopCurrentHaptic — `if (!_isPlaying)` → `if (!_isPlaying && false)` → RED (StopCount expected 0 was 1).
		public void StopCurrentHaptic_NotPlaying_DoesNotCallBackendStop()
		{
			_haptics.StopCurrentHaptic();

			Assert.AreEqual(0, _backend.StopCount);
		}

		[Test]
		// ADMIT: HapticsService.StopCurrentHaptic could skip the backend Stop and leave the device vibrating.
		// RCR: HapticsService.cs StopCurrentHaptic — gate `_backend.Stop()` on `_currentPreset != HapticPreset.Error` → RED (StopCount expected 1 was 0); gated so Enabled_SetFalseWhilePlaying stays green.
		public void StopCurrentHaptic_WhilePlaying_CallsBackendStopAndClearsState()
		{
			_haptics.PlayPreset(HapticPreset.Error);
			_backend.Reset();

			_haptics.StopCurrentHaptic();

			Assert.AreEqual(1, _backend.StopCount);
			Assert.IsFalse(_haptics.IsPlaying);
		}

		private sealed class FakeHapticsBackend : IHapticsBackend
		{
			public bool IsSupportedValue;
			public int OneShotCount;
			public int LoopCount;
			public int CustomCount;
			public int StopCount;
			public HapticPreset LastPreset;
			public float LastIntensity;
			public float LastDurationMs;
			public List<string> Operations = new List<string>();

			public bool IsSupported => IsSupportedValue;

			public void PlayPresetOneShot(HapticPreset preset)
			{
				OneShotCount++;
				LastPreset = preset;
				Operations.Add("OneShot");
			}

			public void PlayPresetLoop(HapticPreset preset)
			{
				LoopCount++;
				LastPreset = preset;
				Operations.Add("Loop");
			}

			public void PlayCustom(float intensity01, float durationMs)
			{
				CustomCount++;
				LastIntensity = intensity01;
				LastDurationMs = durationMs;
				Operations.Add("Custom");
			}

			public void Stop()
			{
				StopCount++;
				Operations.Add("Stop");
			}

			public void Reset()
			{
				OneShotCount = 0;
				LoopCount = 0;
				CustomCount = 0;
				StopCount = 0;
				LastPreset = HapticPreset.None;
				LastIntensity = 0f;
				LastDurationMs = 0f;
				Operations.Clear();
			}
		}
	}
}

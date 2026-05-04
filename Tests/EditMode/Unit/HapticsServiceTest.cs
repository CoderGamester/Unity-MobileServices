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
		public void Enabled_DefaultIsTrue()
		{
			Assert.IsTrue(_haptics.Enabled);
		}

		[Test]
		public void Enabled_SetSameValue_DoesNothing()
		{
			_haptics.PlayPreset(HapticPreset.Selection);
			_backend.Reset();

			_haptics.Enabled = true;

			Assert.AreEqual(0, _backend.StopCount);
		}

		[Test]
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
		public void PlayPreset_None_NoBackendCall()
		{
			_haptics.PlayPreset(HapticPreset.None);

			Assert.AreEqual(0, _backend.OneShotCount);
			Assert.AreEqual(0, _backend.LoopCount);
			Assert.AreEqual(0, _backend.CustomCount);
			Assert.IsFalse(_haptics.IsPlaying);
		}

		[Test]
		public void PlayPreset_Disabled_NoBackendCall()
		{
			_haptics.Enabled = false;
			_backend.Reset();

			_haptics.PlayPreset(HapticPreset.Selection);

			Assert.AreEqual(0, _backend.OneShotCount);
			Assert.IsFalse(_haptics.IsPlaying);
		}

		[Test]
		public void PlayPreset_Natural_CallsOneShotAndIsPlayingTrue()
		{
			_haptics.PlayPreset(HapticPreset.Selection);

			Assert.AreEqual(1, _backend.OneShotCount);
			Assert.AreEqual(HapticPreset.Selection, _backend.LastPreset);
			Assert.IsTrue(_haptics.IsPlaying);
		}

		[Test]
		public void PlayPresetDuration_Zero_CallsOneShot()
		{
			_haptics.PlayPresetDuration(HapticPreset.Success, 0f);

			Assert.AreEqual(1, _backend.OneShotCount);
			Assert.AreEqual(0, _backend.LoopCount);
		}

		[Test]
		public void PlayPresetDuration_NegativeOrDefault_CallsLoopNoAutoStop()
		{
			_haptics.PlayPresetDuration(HapticPreset.Warning);

			Assert.AreEqual(1, _backend.LoopCount);
			Assert.AreEqual(HapticPreset.Warning, _backend.LastPreset);
			Assert.IsTrue(_haptics.IsPlaying);
		}

		[Test]
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
		public void StopCurrentHaptic_NotPlaying_DoesNotCallBackendStop()
		{
			_haptics.StopCurrentHaptic();

			Assert.AreEqual(0, _backend.StopCount);
		}

		[Test]
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

			public bool IsSupported => IsSupportedValue;

			public void PlayPresetOneShot(HapticPreset preset)
			{
				OneShotCount++;
				LastPreset = preset;
			}

			public void PlayPresetLoop(HapticPreset preset)
			{
				LoopCount++;
				LastPreset = preset;
			}

			public void PlayCustom(float intensity01, float durationMs)
			{
				CustomCount++;
				LastIntensity = intensity01;
				LastDurationMs = durationMs;
			}

			public void Stop()
			{
				StopCount++;
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
			}
		}
	}
}

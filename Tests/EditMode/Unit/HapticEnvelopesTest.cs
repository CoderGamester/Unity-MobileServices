using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Haptics.Internal;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	/// <summary>
	/// Pins the per-preset envelope tables that the Mobile Services Explorer's haptic envelope graph
	/// and the runtime <see cref="AndroidHapticsBackend"/> both read from. Asserting on shape +
	/// expected first-row values catches accidental drift if someone edits the float tables.
	/// </summary>
	public class HapticEnvelopesTest
	{
		[Test]
		public void GetFloatEnvelope_None_ReturnsSingleZeroPair()
		{
			var (timings, amps) = HapticEnvelopes.GetFloatEnvelopeFor(HapticPreset.None);
			Assert.AreEqual(1, timings.Length);
			Assert.AreEqual(1, amps.Length);
			Assert.AreEqual(0f, timings[0]);
			Assert.AreEqual(0f, amps[0]);
		}

		[Test]
		public void GetFloatEnvelope_Selection_MatchesAndroidBackendTable()
		{
			var (timings, amps) = HapticEnvelopes.GetFloatEnvelopeFor(HapticPreset.Selection);
			Assert.AreEqual(1, timings.Length);
			Assert.AreEqual(0.04f, timings[0], 1e-6f);
			Assert.AreEqual(0.471f, amps[0], 1e-6f);
		}

		[Test]
		public void GetFloatEnvelope_Error_HasSevenSamples()
		{
			var (timings, amps) = HapticEnvelopes.GetFloatEnvelopeFor(HapticPreset.Error);
			Assert.AreEqual(7, timings.Length);
			Assert.AreEqual(7, amps.Length);
		}

		[Test]
		public void GetEnvelope_ConvertsToMillisecondsAnd0to255Amplitudes()
		{
			var (timingsMs, amplitudes) = HapticEnvelopes.GetEnvelopeFor(HapticPreset.Selection);
			Assert.AreEqual(1, timingsMs.Length);
			Assert.AreEqual(40L, timingsMs[0], "0.04s = 40ms");
			Assert.AreEqual(120, amplitudes[0], "0.471 * 255 = 120 (rounded).");
		}

		[Test]
		public void GetNaturalDurationSeconds_SumsEveryStep()
		{
			Assert.AreEqual(0.04f, HapticEnvelopes.GetNaturalDurationSeconds(HapticPreset.Selection), 1e-6f);
			// Success: 0.04 + 0.04 + 0.16
			Assert.AreEqual(0.24f, HapticEnvelopes.GetNaturalDurationSeconds(HapticPreset.Success), 1e-6f);
		}

		[TestCase(HapticPreset.Selection)]
		[TestCase(HapticPreset.Success)]
		[TestCase(HapticPreset.Warning)]
		[TestCase(HapticPreset.Error)]
		[TestCase(HapticPreset.ImpactLight)]
		[TestCase(HapticPreset.ImpactMedium)]
		[TestCase(HapticPreset.ImpactHeavy)]
		[TestCase(HapticPreset.ImpactRigid)]
		[TestCase(HapticPreset.ImpactSoft)]
		public void GetFloatEnvelope_TimingsAndAmpsHaveSameLength(HapticPreset preset)
		{
			var (timings, amps) = HapticEnvelopes.GetFloatEnvelopeFor(preset);
			Assert.AreEqual(timings.Length, amps.Length, $"{preset}: timings/amps length mismatch.");
		}

		[TestCase(HapticPreset.Selection)]
		[TestCase(HapticPreset.Success)]
		[TestCase(HapticPreset.Warning)]
		[TestCase(HapticPreset.Error)]
		[TestCase(HapticPreset.ImpactLight)]
		[TestCase(HapticPreset.ImpactMedium)]
		[TestCase(HapticPreset.ImpactHeavy)]
		[TestCase(HapticPreset.ImpactRigid)]
		[TestCase(HapticPreset.ImpactSoft)]
		public void GetFloatEnvelope_AmpsAreClamped01(HapticPreset preset)
		{
			var (_, amps) = HapticEnvelopes.GetFloatEnvelopeFor(preset);
			foreach (var amp in amps)
			{
				Assert.IsTrue(amp >= 0f && amp <= 1f, $"{preset}: amplitude {amp} outside [0, 1].");
			}
		}
	}
}

using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Haptics.Internal;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	/// <summary>
	/// Pins the per-preset envelope tables that the Device Simulator panel's haptic envelope graph
	/// and the runtime <see cref="AndroidHapticsBackend"/> both read from. Asserting on shape +
	/// expected first-row values catches accidental drift if someone edits the float tables.
	/// </summary>
	public class HapticEnvelopesTest
	{
		[Test]
		// ADMIT: HapticEnvelopes' default case could return a non-zero envelope for HapticPreset.None, making the envelope graph draw a phantom pulse.
		// RCR: HapticEnvelopes.cs GetFloatEnvelopeFor — default `new[] { 0.0f }` timings → `new[] { 1.0f }` → RED (timings[0] expected 0 was 1).
		public void GetFloatEnvelope_None_ReturnsSingleZeroPair()
		{
			var (timings, amps) = HapticEnvelopes.GetFloatEnvelopeFor(HapticPreset.None);
			Assert.AreEqual(1, timings.Length);
			Assert.AreEqual(1, amps.Length);
			Assert.AreEqual(0f, timings[0]);
			Assert.AreEqual(0f, amps[0]);
		}

		[Test]
		// ADMIT: HapticEnvelopes could drift the Selection amplitude away from the Android waveform table it is the single source of truth for.
		// RCR: HapticEnvelopes.cs GetFloatEnvelopeFor — Selection amp `0.471f` → `0.472f` → RED (amps[0] expected 0.471).
		public void GetFloatEnvelope_Selection_MatchesAndroidBackendTable()
		{
			var (timings, amps) = HapticEnvelopes.GetFloatEnvelopeFor(HapticPreset.Selection);
			Assert.AreEqual(1, timings.Length);
			Assert.AreEqual(0.04f, timings[0], 1e-6f);
			Assert.AreEqual(0.471f, amps[0], 1e-6f);
		}

		[Test]
		// ADMIT: HapticEnvelopes could shorten the Error preset's 7-step envelope, flattening its multi-tap character.
		// RCR: HapticEnvelopes.cs GetFloatEnvelopeFor — drop the last (0.04f, 0.157f) pair from the Error case → RED (timings.Length expected 7 was 6).
		public void GetFloatEnvelope_Error_HasSevenSamples()
		{
			var (timings, amps) = HapticEnvelopes.GetFloatEnvelopeFor(HapticPreset.Error);
			Assert.AreEqual(7, timings.Length);
			Assert.AreEqual(7, amps.Length);
		}

		[Test]
		// ADMIT: HapticEnvelopes.GetEnvelopeFor could use the wrong seconds→milliseconds scale for VibrationEffect.createWaveform.
		// RCR: HapticEnvelopes.cs GetEnvelopeFor — `timesSec[i] * 1000f` → `* 100f` → RED (timingsMs[0] expected 40 was 4).
		public void GetEnvelope_ConvertsToMillisecondsAnd0to255Amplitudes()
		{
			var (timingsMs, amplitudes) = HapticEnvelopes.GetEnvelopeFor(HapticPreset.Selection);
			Assert.AreEqual(1, timingsMs.Length);
			Assert.AreEqual(40L, timingsMs[0], "0.04s = 40ms");
			Assert.AreEqual(120, amplitudes[0], "0.471 * 255 = 120 (rounded).");
		}

		[Test]
		// ADMIT: HapticEnvelopes.GetNaturalDurationSeconds could report only the last step instead of the total, truncating HapticsService's CurrentDurationSeconds.
		// RCR: HapticEnvelopes.cs GetNaturalDurationSeconds — `total += timesSec[i]` → `total =` → RED (Success expected 0.24 was 0.16).
		public void GetNaturalDurationSeconds_SumsEveryStep()
		{
			Assert.AreEqual(0.04f, HapticEnvelopes.GetNaturalDurationSeconds(HapticPreset.Selection), 1e-6f);
			// Success: 0.04 + 0.04 + 0.16
			Assert.AreEqual(0.24f, HapticEnvelopes.GetNaturalDurationSeconds(HapticPreset.Success), 1e-6f);
		}

		[TestCase(HapticPreset.Selection)]
		// ADMIT: HapticEnvelopes could ship a preset whose timings and amplitudes arrays differ in length, which AndroidHapticsBackend would index out of range.
		// RCR: HapticEnvelopes.cs GetFloatEnvelopeFor — append a 4th timing to the Warning case → RED (Warning: timings/amps length mismatch, 4 vs 3).
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
		// ADMIT: HapticEnvelopes could ship an out-of-range amplitude that GetEnvelopeFor would clamp, silently flattening the preset.
		// RCR: HapticEnvelopes.cs GetFloatEnvelopeFor — ImpactHeavy amp `1.000f` → `1.500f` → RED (ImpactHeavy: amplitude 1.5 outside [0, 1]).
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

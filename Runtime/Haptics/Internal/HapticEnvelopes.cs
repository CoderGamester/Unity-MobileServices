using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics.Internal
{
	/// <summary>
	/// Single source of truth for per-preset haptic envelopes — read by both
	/// <see cref="AndroidHapticsBackend"/> and the editor envelope visualiser.
	/// </summary>
	internal static class HapticEnvelopes
	{
		/// <summary>Source-of-truth envelope for a preset, in seconds and <c>[0, 1]</c> amplitude.</summary>
		internal static (float[] timesSec, float[] amps) GetFloatEnvelopeFor(HapticPreset preset)
		{
			switch (preset)
			{
				case HapticPreset.Selection:
					return (new[] { 0.04f }, new[] { 0.471f });
				case HapticPreset.Success:
					return (new[] { 0.04f, 0.04f, 0.16f }, new[] { 0.157f, 0.0f, 1.000f });
				case HapticPreset.Warning:
					return (new[] { 0.12f, 0.12f, 0.04f }, new[] { 1.000f, 0.0f, 0.470f });
				case HapticPreset.Error:
					return (new[] { 0.08f, 0.04f, 0.08f, 0.04f, 0.16f, 0.04f, 0.04f },
					        new[] { 0.470f, 0.0f, 0.470f, 0.0f, 1.000f, 0.0f, 0.157f });
				case HapticPreset.ImpactLight:
					return (new[] { 0.04f }, new[] { 0.156f });
				case HapticPreset.ImpactMedium:
					return (new[] { 0.08f }, new[] { 0.471f });
				case HapticPreset.ImpactHeavy:
					return (new[] { 0.16f }, new[] { 1.000f });
				case HapticPreset.ImpactRigid:
					return (new[] { 0.04f }, new[] { 1.000f });
				case HapticPreset.ImpactSoft:
					return (new[] { 0.16f }, new[] { 0.156f });
				default:
					return (new[] { 0.0f }, new[] { 0.0f });
			}
		}

		/// <summary>
		/// Returns the runtime-ready <c>(long[] milliseconds, int[] 0..255 amplitudes)</c> for the
		/// given preset. Mirrors what <see cref="AndroidHapticsBackend"/> passes to
		/// <c>VibrationEffect.createWaveform</c>.
		/// </summary>
		internal static (long[] timingsMs, int[] amplitudes) GetEnvelopeFor(HapticPreset preset)
		{
			var (timesSec, amps) = GetFloatEnvelopeFor(preset);
			var timingsMs = new long[timesSec.Length];
			var amplitudes = new int[amps.Length];
			for (var i = 0; i < timesSec.Length; i++)
			{
				timingsMs[i] = (long)Mathf.Round(timesSec[i] * 1000f);
				amplitudes[i] = Mathf.Clamp(Mathf.RoundToInt(amps[i] * 255f), 0, 255);
			}
			return (timingsMs, amplitudes);
		}

		/// <summary>Total natural duration of the preset in seconds.</summary>
		internal static float GetNaturalDurationSeconds(HapticPreset preset)
		{
			var (timesSec, _) = GetFloatEnvelopeFor(preset);
			var total = 0f;
			for (var i = 0; i < timesSec.Length; i++)
			{
				total += timesSec[i];
			}
			return total;
		}
	}
}

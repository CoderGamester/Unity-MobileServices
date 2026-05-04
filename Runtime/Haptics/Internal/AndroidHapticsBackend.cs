#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics.Internal
{
	/// <summary>
	/// Android implementation built on <c>android.os.Vibrator.vibrate(VibrationEffect)</c> via JNI.
	/// Uses <c>VibrationEffect.createWaveform(long[] timings, int[] amplitudes, int repeat)</c> for
	/// preset playback. Preset envelopes were translated from the Lofelt time/amplitude pairs
	/// used by the demons reference; every line of code here is original.
	/// Requires API level 26 (Android 8.0) or higher.
	/// </summary>
	internal sealed class AndroidHapticsBackend : IHapticsBackend
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		private const int RepeatLoop  = 0;
		private const int RepeatNone  = -1;
		private const int DefaultAmplitude = -1; // VibrationEffect.DEFAULT_AMPLITUDE

		private AndroidJavaObject _vibrator;
		private AndroidJavaClass  _vibrationEffectClass;
		private bool _initialized;
#endif

		/// <inheritdoc />
		public bool IsSupported
		{
			get
			{
#if UNITY_ANDROID && !UNITY_EDITOR
				return SystemInfo.supportsVibration;
#else
				return false;
#endif
			}
		}

#if UNITY_ANDROID && !UNITY_EDITOR
		private bool EnsureInitialized()
		{
			if (_initialized)
			{
				return _vibrator != null;
			}

			_initialized = true;

			try
			{
				using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
				using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
				{
					_vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
				}

				_vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[GameLovers.MobileServices] Haptics init failed: {e.Message}");
				_vibrator = null;
				_vibrationEffectClass = null;
			}

			return _vibrator != null && _vibrationEffectClass != null;
		}

		private static (long[] timingsMs, int[] amplitudes) GetEnvelopeFor(HapticPreset preset)
		{
			// Time/amplitude pairs are in seconds and [0,1] amplitude (matching Lofelt's HapticPatterns
			// shape). Translated to (long[] millis, int[] 0..255 amplitudes) for VibrationEffect.
			float[] timesSec; float[] amps;
			switch (preset)
			{
				case HapticPreset.Selection:
					timesSec = new[] { 0.04f };
					amps     = new[] { 0.471f };
					break;
				case HapticPreset.Success:
					timesSec = new[] { 0.04f, 0.04f, 0.16f };
					amps     = new[] { 0.157f, 0.0f,  1.000f };
					break;
				case HapticPreset.Warning:
					timesSec = new[] { 0.12f, 0.12f, 0.04f };
					amps     = new[] { 1.000f, 0.0f,  0.470f };
					break;
				case HapticPreset.Error:
					timesSec = new[] { 0.08f, 0.04f, 0.08f, 0.04f, 0.16f, 0.04f, 0.04f };
					amps     = new[] { 0.470f, 0.0f, 0.470f, 0.0f, 1.000f, 0.0f, 0.157f };
					break;
				case HapticPreset.ImpactLight:
					timesSec = new[] { 0.04f };
					amps     = new[] { 0.156f };
					break;
				case HapticPreset.ImpactMedium:
					timesSec = new[] { 0.08f };
					amps     = new[] { 0.471f };
					break;
				case HapticPreset.ImpactHeavy:
					timesSec = new[] { 0.16f };
					amps     = new[] { 1.000f };
					break;
				case HapticPreset.ImpactRigid:
					timesSec = new[] { 0.04f };
					amps     = new[] { 1.000f };
					break;
				case HapticPreset.ImpactSoft:
					timesSec = new[] { 0.16f };
					amps     = new[] { 0.156f };
					break;
				default:
					timesSec = new[] { 0.0f };
					amps     = new[] { 0.0f };
					break;
			}

			var timingsMs  = new long[timesSec.Length];
			var amplitudes = new int [amps.Length];
			for (int i = 0; i < timesSec.Length; i++)
			{
				timingsMs[i]  = (long)Mathf.Round(timesSec[i] * 1000f);
				amplitudes[i] = Mathf.Clamp(Mathf.RoundToInt(amps[i] * 255f), 0, 255);
			}
			return (timingsMs, amplitudes);
		}

		private void PlayWaveform(HapticPreset preset, int repeatIndex)
		{
			if (!EnsureInitialized() || preset == HapticPreset.None)
			{
				return;
			}

			var (timingsMs, amplitudes) = GetEnvelopeFor(preset);
			try
			{
				using var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>(
					"createWaveform", timingsMs, amplitudes, repeatIndex);
				_vibrator.Call("vibrate", effect);
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[GameLovers.MobileServices] Haptics PlayWaveform failed: {e.Message}");
			}
		}
#endif

		/// <inheritdoc />
		public void PlayPresetOneShot(HapticPreset preset)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			PlayWaveform(preset, RepeatNone);
#endif
		}

		/// <inheritdoc />
		public void PlayPresetLoop(HapticPreset preset)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			PlayWaveform(preset, RepeatLoop);
#endif
		}

		/// <inheritdoc />
		public void PlayCustom(float intensity01, float durationMs)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (!EnsureInitialized() || durationMs <= 0f)
			{
				return;
			}

			var amplitude = Mathf.Clamp(Mathf.RoundToInt(intensity01 * 255f), 1, 255);
			var milliseconds = (long)Mathf.Round(durationMs);

			try
			{
				using var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>(
					"createOneShot", milliseconds, amplitude);
				_vibrator.Call("vibrate", effect);
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[GameLovers.MobileServices] Haptics PlayCustom failed: {e.Message}");
			}
#endif
		}

		/// <inheritdoc />
		public void Stop()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (!EnsureInitialized())
			{
				return;
			}

			try
			{
				_vibrator.Call("cancel");
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[GameLovers.MobileServices] Haptics Stop failed: {e.Message}");
			}
#endif
		}
	}
}

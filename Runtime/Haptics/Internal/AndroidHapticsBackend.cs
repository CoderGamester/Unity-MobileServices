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

		private void PlayWaveform(HapticPreset preset, int repeatIndex)
		{
			if (!EnsureInitialized() || preset == HapticPreset.None)
			{
				return;
			}

			// Envelope tables live in HapticEnvelopes so the editor explorer can reuse the
			// exact same (timings, amplitudes) the device receives.
			var (timingsMs, amplitudes) = HapticEnvelopes.GetEnvelopeFor(preset);
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

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics.Internal
{
	/// <summary>JNI haptics backend using Android <c>VibrationEffect</c> on API level 26 or newer.</summary>
	internal sealed class AndroidHapticsBackend : IHapticsBackend
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		private const int MinimumVibrationEffectApi = 26;
		private const int RepeatLoop = 0;
		private const int RepeatNone = -1;
		private static bool _unsupportedApiWarningShown;

		private AndroidJavaObject _vibrator;
		private AndroidJavaClass _vibrationEffectClass;
		private bool _initialized;
#endif

		/// <inheritdoc />
		public bool IsSupported
		{
			get
			{
#if UNITY_ANDROID && !UNITY_EDITOR
				if (GetApiLevel() < MinimumVibrationEffectApi)
				{
					WarnUnsupportedApiOnce();
					return false;
				}
				return SystemInfo.supportsVibration;
#else
				return false;
#endif
			}
		}

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
			if (!IsSupported || !EnsureInitialized() || durationMs <= 0f)
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
			catch (System.Exception exception)
			{
				Debug.LogError($"[GameLovers.MobileServices] Haptics PlayCustom failed: {exception.Message}");
			}
#endif
		}

		/// <inheritdoc />
		public void Stop()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (!IsSupported || !EnsureInitialized())
			{
				return;
			}

			try
			{
				_vibrator.Call("cancel");
			}
			catch (System.Exception exception)
			{
				Debug.LogError($"[GameLovers.MobileServices] Haptics Stop failed: {exception.Message}");
			}
#endif
		}

#if UNITY_ANDROID && !UNITY_EDITOR
		private static int GetApiLevel()
		{
			try
			{
				using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
				{
					return version.GetStatic<int>("SDK_INT");
				}
			}
			catch
			{
				// Treat an unavailable API query as unsupported rather than attempting to load VibrationEffect.
				return 0;
			}
		}

		private static void WarnUnsupportedApiOnce()
		{
			if (_unsupportedApiWarningShown)
			{
				return;
			}

			_unsupportedApiWarningShown = true;
			Debug.LogWarning(
				"[GameLovers.MobileServices] Android haptics require API level 26 or newer; haptics are disabled on this device.");
		}

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
			catch (System.Exception exception)
			{
				Debug.LogError($"[GameLovers.MobileServices] Haptics init failed: {exception.Message}");
				_vibrator = null;
				_vibrationEffectClass = null;
			}

			return _vibrator != null && _vibrationEffectClass != null;
		}

		private void PlayWaveform(HapticPreset preset, int repeatIndex)
		{
			if (!IsSupported || !EnsureInitialized() || preset == HapticPreset.None)
			{
				return;
			}

			// Envelope tables stay shared with the editor's visualizer so device playback is represented exactly.
			var (timingsMs, amplitudes) = HapticEnvelopes.GetEnvelopeFor(preset);
			try
			{
				using var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>(
					"createWaveform", timingsMs, amplitudes, repeatIndex);
				_vibrator.Call("vibrate", effect);
			}
			catch (System.Exception exception)
			{
				Debug.LogError($"[GameLovers.MobileServices] Haptics PlayWaveform failed: {exception.Message}");
			}
		}
#endif
	}
}

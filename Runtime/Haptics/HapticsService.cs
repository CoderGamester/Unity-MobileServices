using GameLovers.MobileServices.Haptics.Internal;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics
{
	/// <inheritdoc />
	public sealed class HapticsService : IHapticsService
	{
		private readonly IHapticsBackend _backend;

		// HapticsHost MonoBehaviour is spawned lazily on the first Play* call so this service can
		// safely be constructed during DI bootstrap (before any GameObject scenes exist).
		private HapticsHost _host;

		private bool _enabled = true;
		private bool _isPlaying;
		private HapticPreset _currentPreset;
		private float _currentDurationSeconds;

		public HapticsService() : this(CreateDefaultBackend()) { }

		internal HapticsService(IHapticsBackend backend)
		{
			_backend = backend;
		}

		/// <summary>
		/// The preset most recently passed to a <c>Play*</c> call. Reads <see cref="HapticPreset.None"/>
		/// when no haptic is currently playing or when the last call was <see cref="PlayCustom"/>.
		/// </summary>
		/// <remarks>Editor introspection accessor — not part of the public surface.</remarks>
		internal HapticPreset CurrentPreset => _isPlaying ? _currentPreset : HapticPreset.None;

		/// <summary>
		/// Real-time-seconds duration scheduled for the active haptic. <c>0</c> when nothing is playing,
		/// the preset's natural one-shot duration when invoked via <see cref="PlayPreset"/>,
		/// <c>-1</c> for an indefinite loop, or the explicit positive duration passed to
		/// <see cref="PlayPresetDuration"/> / <see cref="PlayCustom"/>.
		/// </summary>
		/// <remarks>Editor introspection accessor — not part of the public surface.</remarks>
		internal float CurrentDurationSeconds => _isPlaying ? _currentDurationSeconds : 0f;

		/// <summary>The backend selected for the current platform.</summary>
		/// <remarks>Editor introspection accessor — not part of the public surface.</remarks>
		internal IHapticsBackend Backend => _backend;

		/// <inheritdoc />
		public bool Enabled
		{
			get => _enabled;
			set
			{
				if (_enabled == value)
				{
					return;
				}
				_enabled = value;
				if (!_enabled)
				{
					StopCurrentHaptic();
				}
			}
		}

		/// <inheritdoc />
		public bool IsSupported => _backend.IsSupported;

		/// <inheritdoc />
		public bool IsPlaying => _isPlaying;

		/// <inheritdoc />
		public void PlayPreset(HapticPreset preset)
		{
			PlayPresetDuration(preset, 0f);
		}

		/// <inheritdoc />
		public void PlayPresetDuration(HapticPreset preset, float duration = -1f)
		{
			if (!_enabled || preset == HapticPreset.None)
			{
				return;
			}

			CancelPendingAutoStop();

			if (duration == 0f)
			{
				_backend.PlayPresetOneShot(preset);
				_isPlaying = true;
				_currentPreset = preset;
				_currentDurationSeconds = HapticEnvelopes.GetNaturalDurationSeconds(preset);
				return;
			}

			_backend.PlayPresetLoop(preset);
			_isPlaying = true;
			_currentPreset = preset;
			_currentDurationSeconds = duration;

			if (duration > 0f)
			{
				EnsureHost().ScheduleStop(duration, OnAutoStop);
			}
		}

		/// <inheritdoc />
		public void PlayCustom(float intensity01, float durationMs)
		{
			if (!_enabled || durationMs <= 0f)
			{
				return;
			}

			CancelPendingAutoStop();

			intensity01 = Mathf.Clamp01(intensity01);
			_backend.PlayCustom(intensity01, durationMs);
			_isPlaying = true;
			_currentPreset = HapticPreset.None;
			_currentDurationSeconds = durationMs / 1000f;

			EnsureHost().ScheduleStop(durationMs / 1000f, OnAutoStop);
		}

		/// <inheritdoc />
		public void StopCurrentHaptic()
		{
			CancelPendingAutoStop();
			if (!_isPlaying)
			{
				return;
			}
			_backend.Stop();
			_isPlaying = false;
			_currentPreset = HapticPreset.None;
			_currentDurationSeconds = 0f;
		}

		private void OnAutoStop()
		{
			if (!_isPlaying)
			{
				return;
			}
			_backend.Stop();
			_isPlaying = false;
			_currentPreset = HapticPreset.None;
			_currentDurationSeconds = 0f;
		}

		private void CancelPendingAutoStop()
		{
			if (_host != null)
			{
				_host.Cancel();
			}
		}

		private HapticsHost EnsureHost()
		{
			if (_host != null)
			{
				return _host;
			}

			var go = new GameObject("HapticsHost");
			Object.DontDestroyOnLoad(go);
			_host = go.AddComponent<HapticsHost>();
			return _host;
		}

		private static IHapticsBackend CreateDefaultBackend()
		{
#if UNITY_EDITOR
			return new EditorHapticsBackend();
#elif UNITY_IOS
			return new IosHapticsBackend();
#elif UNITY_ANDROID
			return new AndroidHapticsBackend();
#else
			return new NoOpHapticsBackend();
#endif
		}
	}
}

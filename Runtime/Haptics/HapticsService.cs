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

		public HapticsService() : this(CreateDefaultBackend()) { }

		internal HapticsService(IHapticsBackend backend)
		{
			_backend = backend;
		}

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
				return;
			}

			_backend.PlayPresetLoop(preset);
			_isPlaying = true;

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
		}

		private void OnAutoStop()
		{
			if (!_isPlaying)
			{
				return;
			}
			_backend.Stop();
			_isPlaying = false;
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

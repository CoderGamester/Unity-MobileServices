// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics.Internal
{
	/// <summary>
	/// Fallback backend for platforms without haptic support (desktop, WebGL, etc.).
	/// All members are no-ops; <see cref="IsSupported"/> is always <c>false</c>.
	/// </summary>
	internal sealed class NoOpHapticsBackend : IHapticsBackend
	{
		/// <inheritdoc />
		public bool IsSupported => false;

		/// <inheritdoc />
		public void PlayPresetOneShot(HapticPreset preset) { }

		/// <inheritdoc />
		public void PlayPresetLoop(HapticPreset preset) { }

		/// <inheritdoc />
		public void PlayCustom(float intensity01, float durationMs) { }

		/// <inheritdoc />
		public void Stop() { }
	}
}

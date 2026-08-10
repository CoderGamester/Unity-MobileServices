// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics.Internal
{
	/// <summary>
	/// Platform-specific implementation of haptic playback. Selected at construction time by
	/// <see cref="HapticsService"/> based on the current build/runtime platform.
	/// </summary>
	internal interface IHapticsBackend
	{
		/// <summary>
		/// True when the underlying device + OS can produce at least basic vibration; false otherwise.
		/// </summary>
		bool IsSupported { get; }

		/// <summary>Play the preset's natural one-shot duration.</summary>
		void PlayPresetOneShot(HapticPreset preset);

		/// <summary>
		/// Start looping the preset; loop continues until <see cref="Stop"/> or auto-stop coroutine fires.
		/// </summary>
		void PlayPresetLoop(HapticPreset preset);

		/// <summary>
		/// Play a single custom-intensity haptic. Intensity is in <c>[0, 1]</c>; duration in milliseconds.
		/// </summary>
		void PlayCustom(float intensity01, float durationMs);

		/// <summary>Stop all active vibration immediately.</summary>
		void Stop();
	}
}

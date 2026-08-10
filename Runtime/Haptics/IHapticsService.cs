// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics
{
	/// <summary>
	/// Cross-platform haptic feedback. Built directly on iOS <c>UI*FeedbackGenerator</c> (iOS 10+) and
	/// Android <c>VibrationEffect.createWaveform</c> (API 26+) — no third-party plugin required.
	/// On Editor / unsupported platforms every call is a safe no-op.
	/// </summary>
	public interface IHapticsService
	{
		/// <summary>
		/// Master toggle. When <c>false</c>, every <c>Play*</c> call returns immediately without
		/// touching native. Setting this to <c>false</c> while a haptic is active also calls
		/// <see cref="StopCurrentHaptic"/> internally.
		/// </summary>
		bool Enabled { get; set; }

		/// <summary>
		/// True when the device can do at least basic vibration (Android: <c>SystemInfo.supportsVibration</c>;
		/// iOS: device family supports <c>UIFeedbackGenerator</c>; otherwise false).
		/// </summary>
		bool IsSupported { get; }

		/// <summary>
		/// True between any <c>Play*</c> call and the matching stop (auto or manual).
		/// </summary>
		bool IsPlaying { get; }

		/// <summary>
		/// Plays a one-shot preset using its natural duration. Convenience for
		/// <see cref="PlayPresetDuration"/> with <c>duration = 0f</c>.
		/// </summary>
		void PlayPreset(HapticPreset preset);

		/// <summary>
		/// Plays a preset with explicit duration semantics:
		/// <list type="bullet">
		/// <item><c>duration == 0f</c> — play the preset's natural one-shot duration.</item>
		/// <item><c>duration &lt; 0f</c> (default <c>-1f</c>) — loop indefinitely. Caller MUST
		/// invoke <see cref="StopCurrentHaptic"/> to end it.</item>
		/// <item><c>duration &gt; 0f</c> — loop the preset and auto-stop after <paramref name="duration"/>
		/// real-time seconds (unaffected by <c>Time.timeScale</c>).</item>
		/// </list>
		/// </summary>
		void PlayPresetDuration(HapticPreset preset, float duration = -1f);

		/// <summary>
		/// Plays a single custom-intensity haptic and auto-stops after <paramref name="durationMs"/>.
		/// <paramref name="intensity01"/> is clamped to <c>[0, 1]</c>.
		/// </summary>
		void PlayCustom(float intensity01, float durationMs);

		/// <summary>
		/// Stops any active haptic immediately, regardless of which <c>Play*</c> started it.
		/// Safe to call when nothing is playing (no-op). Also cancels any pending auto-stop scheduled
		/// by <see cref="PlayPresetDuration"/> with <c>duration &gt; 0f</c> or by <see cref="PlayCustom"/>.
		/// </summary>
		void StopCurrentHaptic();
	}
}

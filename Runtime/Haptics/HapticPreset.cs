// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics
{
	/// <summary>
	/// Catalogue of cross-platform haptic feedback presets.
	/// Each preset maps to platform-native primitives (iOS UIFeedbackGenerator family / Android VibrationEffect waveform).
	/// </summary>
	public enum HapticPreset
	{
		None = 0, // No haptic. Calls are short-circuited.

		Selection = 1, // Crisp tick suitable for picker / discrete value changes.

		Success = 2, // Two-tap success notification (ascending).

		Warning = 3, // Single warning notification.

		Error = 4, // Multi-tap error notification.

		ImpactLight = 5, // Soft, low-amplitude impact.

		ImpactMedium = 6, // Default impact strength.

		ImpactHeavy = 7, // Strong impact for major hits.

		ImpactRigid = 8, // Sharp, short impact (snappy).

		ImpactSoft = 9, // Gentle, longer impact (cushioned).
	}
}

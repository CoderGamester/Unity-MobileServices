// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics
{
	/// <summary>
	/// Catalogue of cross-platform haptic feedback presets.
	/// Each preset maps to platform-native primitives (iOS UIFeedbackGenerator family / Android VibrationEffect waveform).
	/// </summary>
	public enum HapticPreset
	{
		/// <summary>No haptic. Calls are short-circuited.</summary>
		None = 0,

		/// <summary>Crisp tick suitable for picker / discrete value changes.</summary>
		Selection = 1,

		/// <summary>Two-tap success notification (ascending).</summary>
		Success = 2,

		/// <summary>Single warning notification.</summary>
		Warning = 3,

		/// <summary>Multi-tap error notification.</summary>
		Error = 4,

		/// <summary>Soft, low-amplitude impact.</summary>
		ImpactLight = 5,

		/// <summary>Default impact strength.</summary>
		ImpactMedium = 6,

		/// <summary>Strong impact for major hits.</summary>
		ImpactHeavy = 7,

		/// <summary>Sharp, short impact (snappy).</summary>
		ImpactRigid = 8,

		/// <summary>Gentle, longer impact (cushioned).</summary>
		ImpactSoft = 9,
	}
}

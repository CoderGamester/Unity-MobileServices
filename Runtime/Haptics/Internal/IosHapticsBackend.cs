#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics.Internal
{
	/// <summary>
	/// iOS implementation built directly on UIKit feedback generators. Maps presets as follows:
	/// <list type="bullet">
	/// <item><see cref="HapticPreset.Selection"/> → <c>UISelectionFeedbackGenerator</c></item>
	/// <item><see cref="HapticPreset.Success"/> / <see cref="HapticPreset.Warning"/> /
	///       <see cref="HapticPreset.Error"/> → <c>UINotificationFeedbackGenerator</c></item>
	/// <item><see cref="HapticPreset.ImpactLight"/> / <see cref="HapticPreset.ImpactMedium"/> /
	///       <see cref="HapticPreset.ImpactHeavy"/> / <see cref="HapticPreset.ImpactRigid"/> /
	///       <see cref="HapticPreset.ImpactSoft"/> → <c>UIImpactFeedbackGenerator</c> with the matching style</item>
	/// </list>
	/// Looping is performed natively by re-firing the chosen generator on an <c>NSTimer</c> until <see cref="Stop"/>.
	/// </summary>
	internal sealed class IosHapticsBackend : IHapticsBackend
	{
#if UNITY_IOS && !UNITY_EDITOR
		[DllImport("__Internal")] private static extern void _GameLoversHapticsPreset(int presetId);
		[DllImport("__Internal")] private static extern void _GameLoversHapticsLoopStart(int presetId);
		[DllImport("__Internal")] private static extern void _GameLoversHapticsCustom(float intensity, float durationMs);
		[DllImport("__Internal")] private static extern void _GameLoversHapticsStop();
#endif

		/// <inheritdoc />
		public bool IsSupported
		{
			get
			{
#if UNITY_IOS && !UNITY_EDITOR
				return SystemInfo.deviceType == DeviceType.Handheld;
#else
				return false;
#endif
			}
		}

		/// <inheritdoc />
		public void PlayPresetOneShot(HapticPreset preset)
		{
#if UNITY_IOS && !UNITY_EDITOR
			_GameLoversHapticsPreset((int)preset);
#endif
		}

		/// <inheritdoc />
		public void PlayPresetLoop(HapticPreset preset)
		{
#if UNITY_IOS && !UNITY_EDITOR
			_GameLoversHapticsLoopStart((int)preset);
#endif
		}

		/// <inheritdoc />
		public void PlayCustom(float intensity01, float durationMs)
		{
#if UNITY_IOS && !UNITY_EDITOR
			_GameLoversHapticsCustom(intensity01, durationMs);
#endif
		}

		/// <inheritdoc />
		public void Stop()
		{
#if UNITY_IOS && !UNITY_EDITOR
			_GameLoversHapticsStop();
#endif
		}
	}
}

using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics.Internal
{
	/// <summary>
	/// Editor backend. Optionally logs calls to the Unity console for visibility while developing;
	/// no native haptic is produced. <see cref="IsSupported"/> is always <c>false</c> so caller code
	/// that gates on capability behaves the same as on a real unsupported device.
	/// </summary>
	internal sealed class EditorHapticsBackend : IHapticsBackend
	{
		/// <summary>Controls whether Editor haptics calls write diagnostic messages to the Unity console.</summary>
		internal static bool DebugLoggingEnabled { get; set; }

		/// <inheritdoc />
		public bool IsSupported => false;

		/// <inheritdoc />
		public void PlayPresetOneShot(HapticPreset preset)
		{
			if (!DebugLoggingEnabled) return;
			Debug.Log($"[Haptics] PlayPresetOneShot({preset})");
		}

		/// <inheritdoc />
		public void PlayPresetLoop(HapticPreset preset)
		{
			if (!DebugLoggingEnabled) return;
			Debug.Log($"[Haptics] PlayPresetLoop({preset})");
		}

		/// <inheritdoc />
		public void PlayCustom(float intensity01, float durationMs)
		{
			if (!DebugLoggingEnabled) return;
			Debug.Log($"[Haptics] PlayCustom(intensity={intensity01:0.00}, durationMs={durationMs:0})");
		}

		/// <inheritdoc />
		public void Stop()
		{
			if (!DebugLoggingEnabled) return;
			Debug.Log("[Haptics] Stop");
		}
	}
}

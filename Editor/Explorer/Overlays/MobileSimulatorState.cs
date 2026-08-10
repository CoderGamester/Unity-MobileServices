using System;
using System.Collections.Generic;
using UnityEditor;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Overlays
{
	/// <summary>
	/// Platform skin for the simulator overlay canvas. Auto-synced from the selected Device
	/// Simulator device profile by <c>MobileServicesDeviceSimulatorPlugin</c>; see
	/// <see cref="MobileSimulatorState.Platform"/>.
	/// </summary>
	public enum SimulatedPlatform
	{
		iOS,
		Android,
	}

	/// <summary>
	/// Style/role of an alert button rendered by the simulator (mirrors <c>AlertButtonStyle</c>
	/// without taking a direct dependency on the runtime enum so the overlay file can render
	/// out-of-the-box without a runtime reference, while staying type-correct at the call site).
	/// </summary>
	public enum SimulatedAlertButtonStyle
	{
		Default,
		Destructive,
		Cancel,
	}

	/// <summary>
	/// Plain payload describing one mock dialog button surfaced by the overlay.
	/// </summary>
	public sealed class SimulatedAlertButton
	{
		public string Text;
		public SimulatedAlertButtonStyle Style;
		public Action OnClicked;
	}

	/// <summary>
	/// Specification of one mock alert dialog. Whether it renders as a centered modal or a bottom
	/// action sheet is decided by <see cref="IsActionSheet"/> and the active platform; on Android
	/// both shapes collapse onto the same Material 3 dialog mock (no native sheet idiom).
	/// </summary>
	public sealed class SimulatedAlertSpec
	{
		public string Title;
		public string Message;
		public bool IsActionSheet;
		public List<SimulatedAlertButton> Buttons = new List<SimulatedAlertButton>();
	}

	/// <summary>Payload for a simulated toast.</summary>
	public sealed class SimulatedToastSpec
	{
		public string Message;
		public bool IsLongDuration;
	}

	/// <summary>Payload for a simulated share sheet.</summary>
	public sealed class SimulatedShareSpec
	{
		public string Text;
		public string Url;
		public string ImagePath;
		public string Title;
	}

	/// <summary>Payload for a simulated heads-up notification banner.</summary>
	public sealed class SimulatedNotificationBannerSpec
	{
		public string ChannelName;
		public string Title;
		public string Body;
		public string SubTitle;
	}

	/// <summary>Payload for a simulated permission or ATT prompt, including the callback that resolves it.</summary>
	public sealed class SimulatedPermissionDialogSpec
	{
		public string TypeName;          // e.g. "Camera" / "Photo Library"
		public string UsageDescription;  // Project-configured NS*UsageDescription text
		public bool IsAtt;
		public Action<bool> OnResolved;  // true = allow / false = deny
	}

	/// <summary>
	/// Editor-only broker decoupling the Device Simulator plugin and <c>EditorPlatformSimulator</c>
	/// from the in-Game-view overlay that paints the mock dialogs. See <c>docs/explorer.md</c>.
	/// </summary>
	/// <remarks>
	/// There is now a single renderer surface (the <c>MobileSimulatorRuntimeOverlay</c>), so push
	/// calls are simple broadcasts with no per-target routing, and the platform skin is a single
	/// <see cref="Platform"/> value (auto-synced from the Device Simulator device profile).
	/// </remarks>
	public static class MobileSimulatorState
	{
		private const string PlatformPrefKey = "GameLovers.MobileServicesSimulator.Platform";
		private const string EnabledPrefKey = "GameLovers.MobileServicesSimulator.Enabled";

		private static SimulatedPlatform _platform = SimulatedPlatform.iOS;
		private static bool _enabled = true;
		private static bool _initialized;

		// ---- Enabled (master switch) ----

		/// <summary>
		/// Fires when <see cref="Enabled"/> changes — the master switch that gates every plugin
		/// control and shows/hides the in-Game-view simulator banner.
		/// </summary>
		public static event Action<bool> EnabledChanged;

		/// <summary>
		/// Master switch for the editor simulator. When on, the plugin's controls are interactive
		/// and the in-Game-view "[EDITOR SIMULATOR]" banner is shown; when off, the controls are
		/// greyed out and the banner is hidden. Persisted to <see cref="EditorPrefs"/>.
		/// </summary>
		public static bool Enabled
		{
			get
			{
				EnsureInitialized();
				return _enabled;
			}
			set
			{
				EnsureInitialized();
				if (_enabled == value)
				{
					return;
				}
				_enabled = value;
				EditorPrefs.SetBool(EnabledPrefKey, value);
				EnabledChanged?.Invoke(value);
			}
		}

		// ---- Platform ----

		/// <summary>
		/// Fires when the <see cref="Platform"/> changes — the platform skin used by the
		/// in-Game-view overlay (auto-synced from <c>Application.platform</c> by the Device
		/// Simulator plugin's poll).
		/// </summary>
		public static event Action<SimulatedPlatform> PlatformChanged;

		/// <summary>Platform skin for the simulator overlay. Persisted to <see cref="EditorPrefs"/>.</summary>
		public static SimulatedPlatform Platform
		{
			get
			{
				EnsureInitialized();
				return _platform;
			}
			set
			{
				EnsureInitialized();
				if (_platform == value)
				{
					return;
				}
				_platform = value;
				EditorPrefs.SetInt(PlatformPrefKey, (int)value);
				PlatformChanged?.Invoke(value);
			}
		}

		// ---- Overlay payload streams ----

		/// <summary>Raised when an alert mock should be painted.</summary>
		public static event Action<SimulatedAlertSpec> AlertRequested;
		/// <summary>Raised when a toast mock should be painted.</summary>
		public static event Action<SimulatedToastSpec> ToastRequested;
		/// <summary>Raised when a share-sheet mock should be painted.</summary>
		public static event Action<SimulatedShareSpec> ShareRequested;
		/// <summary>Raised when the store review mock should be painted.</summary>
		public static event Action ReviewRequested;
		/// <summary>Raised when a notification-banner mock should be painted.</summary>
		public static event Action<SimulatedNotificationBannerSpec> NotificationBannerRequested;
		/// <summary>Raised when a permission or ATT prompt mock should be painted.</summary>
		public static event Action<SimulatedPermissionDialogSpec> PermissionDialogRequested;
		/// <summary>Raised when every visible mock should be cleared.</summary>
		public static event Action DismissAllRequested;

		// ---- Push entry points ----

		/// <summary>Broadcasts an alert mock to the overlay.</summary>
		public static void PushAlert(SimulatedAlertSpec spec) => AlertRequested?.Invoke(spec);
		/// <summary>Broadcasts a toast mock to the overlay.</summary>
		public static void PushToast(SimulatedToastSpec spec) => ToastRequested?.Invoke(spec);
		/// <summary>Broadcasts a share-sheet mock to the overlay.</summary>
		public static void PushShare(SimulatedShareSpec spec) => ShareRequested?.Invoke(spec);
		/// <summary>Broadcasts the store review mock to the overlay.</summary>
		public static void PushReview() => ReviewRequested?.Invoke();
		/// <summary>Broadcasts a notification-banner mock to the overlay.</summary>
		public static void PushNotificationBanner(SimulatedNotificationBannerSpec spec) =>
			NotificationBannerRequested?.Invoke(spec);
		/// <summary>Broadcasts a permission or ATT prompt mock to the overlay.</summary>
		public static void PushPermissionDialog(SimulatedPermissionDialogSpec spec) =>
			PermissionDialogRequested?.Invoke(spec);
		/// <summary>Clears every visible mock.</summary>
		public static void PushDismissAll() => DismissAllRequested?.Invoke();

		private static void EnsureInitialized()
		{
			if (_initialized)
			{
				return;
			}
			_initialized = true;
			_platform = (SimulatedPlatform)EditorPrefs.GetInt(PlatformPrefKey, (int)SimulatedPlatform.iOS);
			_enabled = EditorPrefs.GetBool(EnabledPrefKey, true);
		}
	}
}

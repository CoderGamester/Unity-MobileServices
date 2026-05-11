using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Overlays
{
	/// <summary>
	/// Platform skin available to the truth-mirror simulator surfaces. Each surface (standalone
	/// window vs runtime overlay) carries its own platform value — see
	/// <see cref="MobileSimulatorState.WindowPlatform"/> and <see cref="MobileSimulatorState.OverlayPlatform"/>.
	/// </summary>
	public enum SimulatedPlatform
	{
		iOS,
		Android,
	}

	/// <summary>
	/// Routing flag for <see cref="MobileSimulatorState"/> push calls. Producers say which
	/// renderer surface(s) a mock should appear on; renderers filter on the bit that matches
	/// their own surface.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="All"/> on every <c>Push*</c> overload so existing call sites
	/// (Explorer tabs, <c>EditorPlatformSimulator</c>) keep their broadcast semantics. The
	/// Device Simulator plugin opts into the narrower <see cref="RuntimeOverlay"/> scope so its
	/// mocks paint only inside Unity's Game / Simulator view, never in the standalone
	/// <c>MobileSimulatorWindow</c>.
	/// </remarks>
	[Flags]
	public enum SimulatorTarget
	{
		None             = 0,
		StandaloneWindow = 1 << 0,
		RuntimeOverlay   = 1 << 1,
		All              = StandaloneWindow | RuntimeOverlay,
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

	public sealed class SimulatedToastSpec
	{
		public string Message;
		public bool IsLongDuration;
	}

	public sealed class SimulatedShareSpec
	{
		public string Text;
		public string Url;
		public string ImagePath;
		public string Title;
	}

	public sealed class SimulatedNotificationBannerSpec
	{
		public string ChannelName;
		public string Title;
		public string Body;
		public string SubTitle;
	}

	public sealed class SimulatedPermissionDialogSpec
	{
		public string TypeName;          // e.g. "Camera" / "Photo Library"
		public string UsageDescription;  // Project-configured NS*UsageDescription text
		public bool IsAtt;
		public Action<bool> OnResolved;  // true = allow / false = deny
	}

	/// <summary>
	/// Editor-only broker decoupling the Explorer tabs and <c>EditorPlatformSimulator</c> from the
	/// truth-mirror windows that paint the mock dialogs. See <c>docs/explorer.md</c>.
	/// </summary>
	/// <remarks>
	/// <para><b>Per-target routing.</b> Every push carries a <see cref="SimulatorTarget"/>
	/// flag (default <see cref="SimulatorTarget.All"/>) and every renderer subscribes to one
	/// surface — <see cref="SimulatorTarget.StandaloneWindow"/> for the dockable
	/// <c>MobileSimulatorWindow</c>, <see cref="SimulatorTarget.RuntimeOverlay"/> for the
	/// in-Game-view overlay. The platform skin is also split per surface
	/// (<see cref="WindowPlatform"/> / <see cref="OverlayPlatform"/>) so the Explorer dropdown
	/// and the Device Simulator plugin's auto-sync can drive their own surfaces independently
	/// without one stealing the wheel from the other.</para>
	/// </remarks>
	public static class MobileSimulatorState
	{
		private const string WindowPlatformPrefKey  = "GameLovers.MobileServicesExplorer.SimulatedPlatform.Window";
		private const string OverlayPlatformPrefKey = "GameLovers.MobileServicesExplorer.SimulatedPlatform.Overlay";

		private static SimulatedPlatform _windowPlatform  = SimulatedPlatform.iOS;
		private static SimulatedPlatform _overlayPlatform = SimulatedPlatform.iOS;
		private static bool _initialized;

		// ---- Platform (per-surface) ----

		/// <summary>
		/// Fires when the <see cref="WindowPlatform"/> changes — the platform skin used by the
		/// dockable <c>MobileSimulatorWindow</c> (driven by the Explorer's
		/// <c>Render as: iOS | Android</c> dropdown).
		/// </summary>
		public static event Action<SimulatedPlatform> WindowPlatformChanged;

		/// <summary>
		/// Fires when the <see cref="OverlayPlatform"/> changes — the platform skin used by the
		/// in-Game-view <c>MobileSimulatorRuntimeOverlay</c> (auto-synced from
		/// <see cref="Application.platform"/> by the Device Simulator plugin's poll).
		/// </summary>
		public static event Action<SimulatedPlatform> OverlayPlatformChanged;

		/// <summary>Platform skin for the dockable Simulator window. Persisted to <see cref="EditorPrefs"/>.</summary>
		public static SimulatedPlatform WindowPlatform
		{
			get
			{
				EnsureInitialized();
				return _windowPlatform;
			}
			set
			{
				EnsureInitialized();
				if (_windowPlatform == value)
				{
					return;
				}
				_windowPlatform = value;
				EditorPrefs.SetInt(WindowPlatformPrefKey, (int)value);
				WindowPlatformChanged?.Invoke(value);
			}
		}

		/// <summary>Platform skin for the runtime overlay. Persisted to <see cref="EditorPrefs"/>.</summary>
		public static SimulatedPlatform OverlayPlatform
		{
			get
			{
				EnsureInitialized();
				return _overlayPlatform;
			}
			set
			{
				EnsureInitialized();
				if (_overlayPlatform == value)
				{
					return;
				}
				_overlayPlatform = value;
				EditorPrefs.SetInt(OverlayPlatformPrefKey, (int)value);
				OverlayPlatformChanged?.Invoke(value);
			}
		}

		// ---- Overlay payload streams (per-target) ----

		// Each event's first arg is the SimulatorTarget mask the producer addressed; subscribers
		// short-circuit unless their own surface bit is set.
		public static event Action<SimulatorTarget, SimulatedAlertSpec> AlertRequested;
		public static event Action<SimulatorTarget, SimulatedToastSpec> ToastRequested;
		public static event Action<SimulatorTarget, SimulatedShareSpec> ShareRequested;
		public static event Action<SimulatorTarget> ReviewRequested;
		public static event Action<SimulatorTarget, SimulatedNotificationBannerSpec> NotificationBannerRequested;
		public static event Action<SimulatorTarget, SimulatedPermissionDialogSpec> PermissionDialogRequested;
		public static event Action<SimulatorTarget> DismissAllRequested;

		// ---- Push entry points ----

		public static void PushAlert(SimulatedAlertSpec spec, SimulatorTarget targets = SimulatorTarget.All) =>
			AlertRequested?.Invoke(targets, spec);
		public static void PushToast(SimulatedToastSpec spec, SimulatorTarget targets = SimulatorTarget.All) =>
			ToastRequested?.Invoke(targets, spec);
		public static void PushShare(SimulatedShareSpec spec, SimulatorTarget targets = SimulatorTarget.All) =>
			ShareRequested?.Invoke(targets, spec);
		public static void PushReview(SimulatorTarget targets = SimulatorTarget.All) =>
			ReviewRequested?.Invoke(targets);
		public static void PushNotificationBanner(SimulatedNotificationBannerSpec spec, SimulatorTarget targets = SimulatorTarget.All) =>
			NotificationBannerRequested?.Invoke(targets, spec);
		public static void PushPermissionDialog(SimulatedPermissionDialogSpec spec, SimulatorTarget targets = SimulatorTarget.All) =>
			PermissionDialogRequested?.Invoke(targets, spec);
		public static void PushDismissAll(SimulatorTarget targets = SimulatorTarget.All) =>
			DismissAllRequested?.Invoke(targets);

		private static void EnsureInitialized()
		{
			if (_initialized)
			{
				return;
			}
			_initialized = true;
			_windowPlatform  = (SimulatedPlatform)EditorPrefs.GetInt(WindowPlatformPrefKey,  (int)SimulatedPlatform.iOS);
			_overlayPlatform = (SimulatedPlatform)EditorPrefs.GetInt(OverlayPlatformPrefKey, (int)SimulatedPlatform.iOS);
		}
	}
}

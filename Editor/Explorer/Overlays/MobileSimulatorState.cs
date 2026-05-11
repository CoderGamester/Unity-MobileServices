using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Overlays
{
	/// <summary>
	/// Platform skin currently selected by the Mobile Services Explorer's top-row toggle.
	/// Drives the truth-mirror overlay's USS swap (iOS / Android).
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
	/// truth-mirror window that paints the mock dialogs. See <c>docs/explorer.md</c>.
	/// </summary>
	public static class MobileSimulatorState
	{
		private const string PlatformPrefKey = "GameLovers.MobileServicesExplorer.SimulatedPlatform";

		private static SimulatedPlatform _platform = SimulatedPlatform.iOS;
		private static bool _initialized;
		private static bool _isPluginConnected;

		// ---- Platform ----

		/// <summary>Fires when the user flips the Explorer's <c>Render as: iOS | Android</c> toggle.</summary>
		public static event Action<SimulatedPlatform> PlatformChanged;

		// ---- DeviceSimulator plugin presence ----

		/// <summary>
		/// Fires when a <c>MobileServicesDeviceSimulatorPlugin</c> instance is created or destroyed
		/// inside Unity's Device Simulator window. Consumers (e.g. the Explorer header) use this to
		/// hand control of the platform skin to the plugin (which auto-syncs from the device profile)
		/// instead of letting the user pick it manually.
		/// </summary>
		public static event Action<bool> PluginConnectedChanged;

		/// <summary>
		/// <c>true</c> while at least one <c>MobileServicesDeviceSimulatorPlugin</c> is alive (the
		/// user has Unity's Simulator view open with our plugin enabled in it).
		/// </summary>
		public static bool IsActivePluginConnected
		{
			get => _isPluginConnected;
			internal set
			{
				if (_isPluginConnected == value)
				{
					return;
				}
				_isPluginConnected = value;
				PluginConnectedChanged?.Invoke(value);
			}
		}

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

		public static event Action<SimulatedAlertSpec> AlertRequested;
		public static event Action<SimulatedToastSpec> ToastRequested;
		public static event Action<SimulatedShareSpec> ShareRequested;
		public static event Action ReviewRequested;
		public static event Action<SimulatedNotificationBannerSpec> NotificationBannerRequested;
		public static event Action<SimulatedPermissionDialogSpec> PermissionDialogRequested;
		public static event Action DismissAllRequested;

		// ---- Push entry points ----

		public static void PushAlert(SimulatedAlertSpec spec) => AlertRequested?.Invoke(spec);
		public static void PushToast(SimulatedToastSpec spec) => ToastRequested?.Invoke(spec);
		public static void PushShare(SimulatedShareSpec spec) => ShareRequested?.Invoke(spec);
		public static void PushReview() => ReviewRequested?.Invoke();
		public static void PushNotificationBanner(SimulatedNotificationBannerSpec spec) =>
			NotificationBannerRequested?.Invoke(spec);
		public static void PushPermissionDialog(SimulatedPermissionDialogSpec spec) =>
			PermissionDialogRequested?.Invoke(spec);
		public static void PushDismissAll() => DismissAllRequested?.Invoke();

		private static void EnsureInitialized()
		{
			if (_initialized)
			{
				return;
			}
			_initialized = true;
			_platform = (SimulatedPlatform)EditorPrefs.GetInt(PlatformPrefKey, (int)SimulatedPlatform.iOS);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Explorer.Overlays;
using GameLovers.MobileServices.Editor.Settings;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Simulation
{
	/// <summary>
	/// Editor-only façade for driving platform state (battery, connectivity, safe area, deep links,
	/// permissions, ATT) from edit-mode tests and the Device Simulator panel. See
	/// <c>docs/explorer.md</c> for the comparison with Unity's Device Simulator.
	/// </summary>
	public static class EditorPlatformSimulator
	{
		// ---- Device state ----

		/// <summary>
		/// Flips the simulator's low-power-mode override AND fans the change through every
		/// <see cref="BatteryService"/> instance you pass — mirroring what
		/// <c>NSProcessInfoPowerStateDidChangeNotification</c> would do on a real iOS device.
		/// </summary>
		public static void SetIosLowPowerMode(bool enabled, params BatteryService[] services)
		{
			BatteryService.EditorLowPowerModeOverride = enabled;
			if (services != null)
			{
				foreach (var s in services)
				{
					s?.SimulateLowPowerModeChanged();
				}
			}
		}

		/// <summary>
		/// Pushes a safe-area override that <see cref="SafeAreaService"/> will report on its next
		/// <c>LateUpdate</c> diff. Each service you pass is forced to diff immediately so the
		/// Explorer surfaces the change without waiting for the host's poll.
		/// </summary>
		public static void SetSafeArea(Rect safeArea, params SafeAreaService[] services)
		{
			SafeAreaService.EditorSafeAreaOverride = safeArea;
			if (services != null)
			{
				foreach (var s in services)
				{
					s?.SimulateSafeAreaChanged();
				}
			}
		}

		/// <summary>Clears the safe-area override and restores the live <c>Screen.safeArea</c> read.</summary>
		public static void ClearSafeAreaOverride(params SafeAreaService[] services)
		{
			SafeAreaService.EditorSafeAreaOverride = null;
			if (services != null)
			{
				foreach (var s in services)
				{
					s?.SimulateSafeAreaChanged();
				}
			}
		}

		/// <summary>
		/// Overrides <c>SystemInfo.batteryLevel</c> exposure on the next poll. The package does
		/// not currently fan a battery-level event from the simulator (the runtime relies on
		/// <c>SystemInfo</c> directly); the Device Simulator panel shows the value via the live snapshot.
		/// </summary>
		public static void SetBatteryLevel(float level01)
		{
			level01 = Mathf.Clamp01(level01);
			SimulatedDeviceState.BatteryLevel = level01;
		}

		/// <summary>Same caveat as <see cref="SetBatteryLevel"/>.</summary>
		public static void SetBatteryStatus(BatteryStatus status)
		{
			SimulatedDeviceState.BatteryStatus = status;
		}

		// ---- Deep link ----

		/// <summary>
		/// Mimics the OS handing the app a runtime deep link (post-launch). Supersedes any pending
		/// cold-start link and fans the URI through every <c>OnLinkActivated</c> subscriber.
		/// </summary>
		public static void SimulateDeepLink(Uri uri, params DeepLinkService[] services)
		{
			if (uri == null || services == null)
			{
				return;
			}
			foreach (var s in services)
			{
				s?.SimulateLinkActivated(uri);
			}
		}

		// ---- Engage / disengage (install the OS-faithful overrides) ----

		/// <summary>
		/// Installs the editor overrides that make <see cref="PermissionsService"/> and
		/// <see cref="AttService"/> behave like the real OS: <see cref="IPermissionsService.Check"/> /
		/// <see cref="IAttService.CurrentStatus"/> read the persisted simulated decision, and the first
		/// <see cref="IPermissionsService.RequestAsync"/> / <see cref="IAttService.RequestAuthorizationAsync"/>
		/// for a <c>NotDetermined</c> entry shows a prompt in the in-Game-view overlay. Called by the
		/// Device Simulator panel while it is open and the master switch is on.
		/// </summary>
		public static void Engage()
		{
			_engaged = true;
			PermissionsService.EditorCheckOverride = ReadPermissionStore;
			PermissionsService.EditorRequestAsyncOverride = RequestPermissionAsync;
			AttService.EditorCurrentStatusOverride = ReadAttStore();
			AttService.EditorRequestAsyncOverride = RequestAttAsync;
		}

		/// <summary>
		/// Removes every editor override so a non-engaged editor session (and headless test runs)
		/// keeps the default <see cref="PermissionStatus.Granted"/> / <see cref="AttStatus.Authorized"/>
		/// short-circuit. Clears any prompt awaiting a decision.
		/// </summary>
		public static void Disengage()
		{
			_engaged = false;
			PermissionsService.EditorCheckOverride = null;
			PermissionsService.EditorRequestOverride = null;
			PermissionsService.EditorRequestAsyncOverride = null;
			AttService.EditorCurrentStatusOverride = null;
			AttService.EditorRequestResultOverride = null;
			AttService.EditorRequestAsyncOverride = null;
			_pendingPermissionResolvers.Clear();
			_pendingAttResolver = null;
		}

		// ---- Permissions (the "Settings" surface + reset) ----

		/// <summary>
		/// Sets the persisted simulated decision for <paramref name="permission"/> — the editor
		/// equivalent of the user changing it in the OS Settings app. Setting
		/// <see cref="PermissionStatus.NotDetermined"/> re-arms the first-time prompt.
		/// </summary>
		public static void SetPermissionState(AppPermission permission, PermissionStatus status) =>
			WritePermissionStore(permission, status);

		/// <summary>Reads the persisted simulated decision for <paramref name="permission"/>.</summary>
		public static PermissionStatus GetPermissionState(AppPermission permission) =>
			ReadPermissionStore(permission);

		/// <summary>
		/// Resets every permission to <see cref="PermissionStatus.NotDetermined"/> — the editor
		/// equivalent of a reinstall / "Reset Location &amp; Privacy".
		/// </summary>
		public static void ResetAllPermissions()
		{
			foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
			{
				WritePermissionStore(p, PermissionStatus.NotDetermined);
			}
		}

		/// <summary>True while a permission prompt is awaiting a decision (e.g. for a panel fallback).</summary>
		public static bool HasPendingPermissionPrompt(AppPermission permission) =>
			_pendingPermissionResolvers.ContainsKey(permission);

		/// <summary>
		/// Resolves a pending permission prompt from outside the overlay (the Device Simulator panel
		/// fallback), since overlay clicks are unreliable in the edit-mode Game view.
		/// </summary>
		public static void ResolvePendingPermissionPrompt(AppPermission permission, bool allow)
		{
			if (!_pendingPermissionResolvers.TryGetValue(permission, out var resolve))
			{
				return;
			}
			MobileSimulatorState.PushDismissAll();
			resolve(allow);
		}

		// ---- ATT (the "Settings" surface + reset) ----

		/// <summary>
		/// Sets the persisted simulated ATT decision. Setting <see cref="AttStatus.NotDetermined"/>
		/// re-arms the first-time prompt.
		/// </summary>
		public static void SetAttState(AttStatus status) => WriteAttStore(status);

		/// <summary>Reads the persisted simulated ATT decision.</summary>
		public static AttStatus GetAttState() => ReadAttStore();

		/// <summary>Resets ATT to <see cref="AttStatus.NotDetermined"/> (reinstall / Reset Privacy).</summary>
		public static void ResetAtt() => WriteAttStore(AttStatus.NotDetermined);

		/// <summary>True while the ATT prompt is awaiting a decision (e.g. for a panel fallback).</summary>
		public static bool HasPendingAttPrompt => _pendingAttResolver != null;

		/// <summary>Resolves a pending ATT prompt from outside the overlay (panel fallback).</summary>
		public static void ResolvePendingAttPrompt(bool allow)
		{
			var resolve = _pendingAttResolver;
			if (resolve == null)
			{
				return;
			}
			MobileSimulatorState.PushDismissAll();
			resolve(allow);
		}

		// ---- Overlay dismissal ----

		/// <summary>Closes any active simulator-overlay dialog without firing a button callback.</summary>
		public static void DismissAllOverlays() => MobileSimulatorState.PushDismissAll();

		// ---- Internals ----

		private const string PermStorePrefix = "GameLovers.MobileServicesSimulator.Perm.";
		private const string AttStoreKey = "GameLovers.MobileServicesSimulator.Att";

		private static bool _engaged;
		private static readonly Dictionary<AppPermission, Action<bool>> _pendingPermissionResolvers =
			new Dictionary<AppPermission, Action<bool>>();
		private static Action<bool> _pendingAttResolver;

		private static PermissionStatus ReadPermissionStore(AppPermission permission) =>
			(PermissionStatus)EditorPrefs.GetInt(PermStorePrefix + permission, (int)PermissionStatus.NotDetermined);

		private static void WritePermissionStore(AppPermission permission, PermissionStatus status) =>
			EditorPrefs.SetInt(PermStorePrefix + permission, (int)status);

		private static AttStatus ReadAttStore() =>
			(AttStatus)EditorPrefs.GetInt(AttStoreKey, (int)AttStatus.NotDetermined);

		private static void WriteAttStore(AttStatus status)
		{
			EditorPrefs.SetInt(AttStoreKey, (int)status);
			// CurrentStatus reads a value (not a func), so keep it synced to the store live.
			if (_engaged)
			{
				AttService.EditorCurrentStatusOverride = status;
			}
		}

		private static Task<PermissionStatus> RequestPermissionAsync(AppPermission permission)
		{
			var current = ReadPermissionStore(permission);
			if (current != PermissionStatus.NotDetermined)
			{
				// Already decided — the OS returns the cached decision without re-prompting.
				return Task.FromResult(current);
			}

			var tcs = new TaskCompletionSource<PermissionStatus>();
			void Resolve(bool allow)
			{
				_pendingPermissionResolvers.Remove(permission);
				var result = allow ? PermissionStatus.Granted : PermissionStatus.Denied;
				WritePermissionStore(permission, result);
				tcs.TrySetResult(result);
			}
			_pendingPermissionResolvers[permission] = Resolve;

			MobileSimulatorState.PushPermissionDialog(new SimulatedPermissionDialogSpec
			{
				TypeName = Humanize(permission),
				UsageDescription = MobileServicesSettings.instance.GetUsageDescriptionEn(permission),
				IsAtt = false,
				OnResolved = Resolve,
			});
			return tcs.Task;
		}

		private static Task<AttStatus> RequestAttAsync()
		{
			// ATT applies on iOS only — Android / other skins behave like the real AttService.
			if (MobileSimulatorState.Platform != SimulatedPlatform.iOS)
			{
				return Task.FromResult(AttStatus.Authorized);
			}

			var current = ReadAttStore();
			if (current != AttStatus.NotDetermined)
			{
				return Task.FromResult(current);
			}

			var tcs = new TaskCompletionSource<AttStatus>();
			void Resolve(bool allow)
			{
				_pendingAttResolver = null;
				var result = allow ? AttStatus.Authorized : AttStatus.Denied;
				WriteAttStore(result);
				tcs.TrySetResult(result);
			}
			_pendingAttResolver = Resolve;

			MobileSimulatorState.PushPermissionDialog(new SimulatedPermissionDialogSpec
			{
				IsAtt = true,
				UsageDescription = MobileServicesSettings.instance.GetAttUsageDescriptionEn(),
				OnResolved = Resolve,
			});
			return tcs.Task;
		}

		private static string Humanize(AppPermission permission)
		{
			switch (permission)
			{
				case AppPermission.LocationWhenInUse:
				case AppPermission.LocationAlways:
					return "Location";
				case AppPermission.PhotoLibrary:
				case AppPermission.PhotoLibraryAddOnly:
					return "Photos";
				default:
					return permission.ToString();
			}
		}
	}

	/// <summary>
	/// Static carrier for simulator-driven device snapshot values that the Device Simulator panel
	/// surfaces directly (the runtime <c>BatteryService</c> reads <c>SystemInfo</c> live and cannot be
	/// re-routed in the editor without a full poll re-implementation; the panel renders this
	/// override alongside the real read as the simulator hint).
	/// </summary>
	public static class SimulatedDeviceState
	{
		public static float? BatteryLevel;
		public static BatteryStatus? BatteryStatus;
	}
}

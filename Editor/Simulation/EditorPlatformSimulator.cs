using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Explorer.Overlays;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Simulation
{
	/// <summary>
	/// Editor-only façade for driving platform state (battery, connectivity, safe area, deep links,
	/// permissions, ATT) from edit-mode tests and the Mobile Services Explorer. See
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
		/// <c>SystemInfo</c> directly); the Explorer shows the value via the live snapshot.
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

		/// <summary>
		/// Sets the connectivity override and drives the diff on every passed
		/// <see cref="ConnectivityService"/>, firing <c>OnStatusChanged</c> if it transitions.
		/// </summary>
		public static void SetConnectivity(NetworkReachability reachability, params ConnectivityService[] services)
		{
			ConnectivityService.EditorReachabilityOverride = reachability;
			if (services != null)
			{
				foreach (var s in services)
				{
					s?.SimulateStatusChanged();
				}
			}
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

		// ---- Permissions ----

		/// <summary>
		/// Queues a result that the next call to <see cref="IPermissionsService.RequestAsync"/>
		/// will resolve to. Set <paramref name="status"/> to <c>null</c> to clear the override and
		/// restore the editor default (<see cref="PermissionStatus.Granted"/>).
		/// </summary>
		public static void QueuePermissionResult(AppPermission permission, PermissionStatus? status)
		{
			if (status == null)
			{
				_queuedPermissionResults.Remove(permission);
				RebuildPermissionRequestOverride();
				return;
			}
			_queuedPermissionResults[permission] = status.Value;
			RebuildPermissionRequestOverride();
		}

		/// <summary>
		/// Overrides <see cref="IPermissionsService.Check"/> reads for a single permission. Pass
		/// <c>null</c> to clear the override for that permission.
		/// </summary>
		public static void SetPermissionCheckResult(AppPermission permission, PermissionStatus? status)
		{
			if (status == null)
			{
				_checkOverrides.Remove(permission);
				RebuildPermissionCheckOverride();
				return;
			}
			_checkOverrides[permission] = status.Value;
			RebuildPermissionCheckOverride();
		}

		// ---- ATT ----

		/// <summary>
		/// Sets the result that the next <see cref="IAttService.RequestAuthorizationAsync"/> will
		/// resolve to and the value <see cref="IAttService.CurrentStatus"/> reads in the editor.
		/// Pass <c>null</c> to clear both overrides.
		/// </summary>
		public static void QueueAttResult(AttStatus? status)
		{
			AttService.EditorCurrentStatusOverride = status;
			AttService.EditorRequestResultOverride = status;
		}

		// ---- Overlay dismissal ----

		/// <summary>Closes any active simulator-overlay dialog without firing a button callback.</summary>
		public static void DismissAllOverlays() => MobileSimulatorState.PushDismissAll();

		// ---- Internals ----

		private static readonly Dictionary<AppPermission, PermissionStatus> _queuedPermissionResults =
			new Dictionary<AppPermission, PermissionStatus>();
		private static readonly Dictionary<AppPermission, PermissionStatus> _checkOverrides =
			new Dictionary<AppPermission, PermissionStatus>();

		private static void RebuildPermissionRequestOverride()
		{
			if (_queuedPermissionResults.Count == 0)
			{
				PermissionsService.EditorRequestOverride = null;
				return;
			}

			PermissionsService.EditorRequestOverride = p =>
				_queuedPermissionResults.TryGetValue(p, out var v) ? v : PermissionStatus.Granted;
		}

		private static void RebuildPermissionCheckOverride()
		{
			if (_checkOverrides.Count == 0)
			{
				PermissionsService.EditorCheckOverride = null;
				return;
			}

			PermissionsService.EditorCheckOverride = p =>
				_checkOverrides.TryGetValue(p, out var v) ? v : PermissionStatus.Granted;
		}
	}

	/// <summary>
	/// Static carrier for simulator-driven device snapshot values that the Explorer surfaces
	/// directly (the runtime <c>BatteryService</c> reads <c>SystemInfo</c> live and cannot be
	/// re-routed in the editor without a full poll re-implementation; the explorer renders this
	/// override alongside the real read as the simulator hint).
	/// </summary>
	public static class SimulatedDeviceState
	{
		public static float? BatteryLevel;
		public static BatteryStatus? BatteryStatus;
	}
}

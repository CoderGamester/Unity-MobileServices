using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Wraps Unity's <c>SystemInfo.batteryLevel</c> / <c>SystemInfo.batteryStatus</c> with change events,
	/// plus iOS / Android low-power-mode awareness.
	/// </summary>
	public interface IBatteryService
	{
		/// <summary>Current battery charge in <c>[0, 1]</c>; <c>-1</c> if unknown.</summary>
		float Level { get; }

		/// <summary>Current charging status (<c>Charging</c>, <c>Discharging</c>, <c>NotCharging</c>, <c>Full</c>, <c>Unknown</c>).</summary>
		BatteryStatus Status { get; }

		/// <summary>True when the OS reports its low-power / battery-saver mode is active.</summary>
		bool IsLowPowerMode { get; }

		/// <summary>Fired when <see cref="Level"/> changes by more than ~1%.</summary>
		event Action OnLevelChanged;

		/// <summary>Fired when <see cref="Status"/> transitions.</summary>
		event Action OnStatusChanged;

		/// <summary>Fired when <see cref="IsLowPowerMode"/> transitions.</summary>
		event Action OnLowPowerModeChanged;
	}
}

using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Wraps <c>Screen.safeArea</c> with change events for orientation, notch / dynamic-island
	/// reveal, and any other runtime safe-area shifts. Fires on diff only.
	/// </summary>
	public interface ISafeAreaService
	{
		/// <summary>Latest known safe area in screen pixels (cached <c>Screen.safeArea</c>).</summary>
		Rect SafeArea { get; }

		/// <summary>Fired when the safe area changes (orientation, notch reveal, etc.).</summary>
		event Action<Rect> OnSafeAreaChanged;
	}
}

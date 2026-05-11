using System;
using GameLovers.MobileServices.Device.Internal;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class SafeAreaService : ISafeAreaService, IDisposable
	{
		private readonly DeviceServicesHost _host;

		private Rect _lastSafeArea;
		private Vector2Int _lastResolution;

#if UNITY_EDITOR
		// Editor-only simulator override. When set, the LateUpdate poll reports this rect
		// instead of Screen.safeArea so the Explorer can drive notch/dynamic-island previews.
		internal static Rect? EditorSafeAreaOverride;
#endif

		/// <inheritdoc />
		public Rect SafeArea => _lastSafeArea;

		/// <inheritdoc />
		public event Action<Rect> OnSafeAreaChanged;

		/// <summary>Default ctor uses the package-wide singleton host (<see cref="DeviceServicesHost.Instance"/>).</summary>
		public SafeAreaService() : this(DeviceServicesHost.Instance) { }

		/// <summary>
		/// Test/DI overload that accepts an explicit host. Used by <see cref="DeviceService"/> to
		/// share a single host instance across the umbrella's children, and by tests that want
		/// deterministic host lifetime.
		/// </summary>
		internal SafeAreaService(DeviceServicesHost host)
		{
			_host = host;
			_lastSafeArea = Screen.safeArea;
			_lastResolution = new Vector2Int(Screen.width, Screen.height);
			_host.RegisterLateUpdate(Tick);
		}

		public void Dispose()
		{
			_host.UnregisterLateUpdate(Tick);
		}

		private void Tick()
		{
#if UNITY_EDITOR
			var current = EditorSafeAreaOverride ?? Screen.safeArea;
#else
			var current = Screen.safeArea;
#endif
			var resolution = new Vector2Int(Screen.width, Screen.height);

			if (current == _lastSafeArea && resolution == _lastResolution)
			{
				return;
			}

			_lastSafeArea = current;
			_lastResolution = resolution;
			OnSafeAreaChanged?.Invoke(current);
		}

#if UNITY_EDITOR
		/// <summary>
		/// Editor-only simulator hook. Forces an immediate diff against
		/// <see cref="EditorSafeAreaOverride"/> so the Explorer's "Set Safe Area" affordance
		/// surfaces the change without waiting for the next LateUpdate tick.
		/// </summary>
		internal void SimulateSafeAreaChanged()
		{
			Tick();
		}
#endif
	}
}

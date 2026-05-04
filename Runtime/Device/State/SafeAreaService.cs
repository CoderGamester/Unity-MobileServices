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
			var current = Screen.safeArea;
			var resolution = new Vector2Int(Screen.width, Screen.height);

			if (current == _lastSafeArea && resolution == _lastResolution)
			{
				return;
			}

			_lastSafeArea = current;
			_lastResolution = resolution;
			OnSafeAreaChanged?.Invoke(current);
		}
	}
}

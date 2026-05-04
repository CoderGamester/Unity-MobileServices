using System;
using GameLovers.MobileServices.Device.Internal;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class ConnectivityService : IConnectivityService, IDisposable
	{
		private readonly DeviceServicesHost _host;

		private NetworkReachability _lastStatus;

		/// <inheritdoc />
		public NetworkReachability Status => Application.internetReachability;

		/// <inheritdoc />
		public event Action<NetworkReachability> OnStatusChanged;

		/// <summary>Default ctor uses the package-wide singleton host (<see cref="DeviceServicesHost.Instance"/>).</summary>
		public ConnectivityService() : this(DeviceServicesHost.Instance) { }

		/// <summary>
		/// Test/DI overload that accepts an explicit host. Used by <see cref="DeviceService"/> to
		/// share a single host instance across the umbrella's children, and by tests that want
		/// deterministic host lifetime.
		/// </summary>
		internal ConnectivityService(DeviceServicesHost host)
		{
			_host = host;
			_lastStatus = Status;
			_host.RegisterSecondTick(Tick);
			_host.RegisterFocusChanged(OnFocusChanged);
		}

		public void Dispose()
		{
			_host.UnregisterSecondTick(Tick);
			_host.UnregisterFocusChanged(OnFocusChanged);
		}

		private void OnFocusChanged(bool focused)
		{
			if (focused)
			{
				Tick();
			}
		}

		private void Tick()
		{
			var current = Status;
			if (current == _lastStatus)
			{
				return;
			}
			_lastStatus = current;
			OnStatusChanged?.Invoke(current);
		}
	}
}

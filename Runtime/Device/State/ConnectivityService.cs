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

#if UNITY_EDITOR
		// Editor-only simulator override. When set, Status reads this instead of the live
		// Application.internetReachability so the Mobile Services Explorer's Device tab can
		// preview connectivity transitions without needing a real network state change.
		internal static NetworkReachability? EditorReachabilityOverride;
#endif

		/// <inheritdoc />
		public NetworkReachability Status =>
#if UNITY_EDITOR
			EditorReachabilityOverride ?? Application.internetReachability;
#else
			Application.internetReachability;
#endif

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

#if UNITY_EDITOR
		/// <summary>
		/// Editor-only simulator hook. Runs the diff path immediately so the Explorer's
		/// "Set Connectivity" button surfaces transitions without waiting for the host's
		/// per-second tick.
		/// </summary>
		internal void SimulateStatusChanged()
		{
			Tick();
		}
#endif
	}
}

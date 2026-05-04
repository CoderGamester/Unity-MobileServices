using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Best-effort wrapper around <c>Application.internetReachability</c> with change events.
	/// "Best effort" because <c>internetReachability</c> only reports interface state, not actual
	/// internet access — for hard guarantees, hit a real endpoint.
	/// </summary>
	public interface IConnectivityService
	{
		/// <summary>Latest known reachability snapshot.</summary>
		NetworkReachability Status { get; }

		/// <summary>Fired when <see cref="Status"/> transitions.</summary>
		event Action<NetworkReachability> OnStatusChanged;
	}
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device.Internal
{
	/// <summary>
	/// Internal MonoBehaviour that receives async permission results from the iOS native bridge via
	/// <c>UnitySendMessage</c>. Resolves the matching pending <see cref="TaskCompletionSource{T}"/>.
	/// </summary>
	internal sealed class PermissionsCallbackReceiver : MonoBehaviour
	{
		private static PermissionsCallbackReceiver _instance;
		private readonly Dictionary<int, TaskCompletionSource<PermissionStatus>> _pending =
			new Dictionary<int, TaskCompletionSource<PermissionStatus>>();
		private int _nextId = 1;

		/// <summary>The receiver GameObject the iOS bridge addresses by name, created on first use.</summary>
		public static PermissionsCallbackReceiver Instance
		{
			get
			{
				if (_instance != null)
				{
					return _instance;
				}

				var go = new GameObject("PermissionsCallbackReceiver");
				DontDestroyOnLoad(go);
				_instance = go.AddComponent<PermissionsCallbackReceiver>();
				return _instance;
			}
		}

		/// <summary>Registers a TCS and returns the request id to pass to the native bridge.</summary>
		public int Register(TaskCompletionSource<PermissionStatus> tcs)
		{
			var id = _nextId++;
			_pending[id] = tcs;
			return id;
		}

		// Native iOS bridge calls UnitySendMessage("PermissionsCallbackReceiver", "OnPermissionResult", "<id>:<status>")
		// where status is the int value of PermissionStatus.
		// ReSharper disable once UnusedMember.Global
		// ReSharper disable once InconsistentNaming
		/// <summary>
		/// Resolves the pending request named in <paramref name="payload"/>, formatted
		/// <c>"&lt;id&gt;:&lt;status&gt;"</c>. Must stay public: Unity dispatches it by name.
		/// </summary>
		public void OnPermissionResult(string payload)
		{
			try
			{
				var sep = payload.IndexOf(':');
				if (sep <= 0)
				{
					return;
				}
				var idText = payload.Substring(0, sep);
				var statusText = payload.Substring(sep + 1);

				if (!int.TryParse(idText, out var id) ||
				    !int.TryParse(statusText, out var statusInt))
				{
					return;
				}

				if (!_pending.TryGetValue(id, out var tcs))
				{
					return;
				}

				_pending.Remove(id);

				var status = (PermissionStatus)statusInt;
				tcs.TrySetResult(status);
			}
			catch (Exception e)
			{
				Debug.LogError($"[GameLovers.MobileServices] PermissionsCallbackReceiver failed to parse '{payload}': {e.Message}");
			}
		}

		private void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
			}

			foreach (var tcs in _pending.Values)
			{
				tcs.TrySetCanceled();
			}
			_pending.Clear();
		}
	}
}

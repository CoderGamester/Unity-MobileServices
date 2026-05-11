using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GameLovers.MobileServices.Device.Internal;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class AttService : IAttService
	{
#if UNITY_IOS && !UNITY_EDITOR
		[DllImport("__Internal")] private static extern int  _GameLoversAttCurrentStatus();
		[DllImport("__Internal")] private static extern void _GameLoversAttRequestAuthorization(int requestId, string callbackGameObject, string callbackMethod);
#endif

#if UNITY_EDITOR
		// Editor-only override hooks consumed by EditorPlatformSimulator. When set, the editor
		// short-circuit paths consult these instead of returning the default Authorized.
		internal static AttStatus? EditorCurrentStatusOverride;
		internal static AttStatus? EditorRequestResultOverride;
#endif

		/// <inheritdoc />
		public AttStatus CurrentStatus
		{
			get
			{
#if UNITY_IOS && !UNITY_EDITOR
				return (AttStatus)_GameLoversAttCurrentStatus();
#elif UNITY_EDITOR
				return EditorCurrentStatusOverride ?? AttStatus.Authorized;
#else
				return AttStatus.Authorized;
#endif
			}
		}

		/// <inheritdoc />
		public Task<AttStatus> RequestAuthorizationAsync()
		{
#if UNITY_IOS && !UNITY_EDITOR
			var tcs = new TaskCompletionSource<AttStatus>();
			var id = AttCallbackReceiver.Instance.Register(tcs);
			_GameLoversAttRequestAuthorization(id, "AttCallbackReceiver", "OnAttResult");
			return tcs.Task;
#elif UNITY_EDITOR
			return Task.FromResult(EditorRequestResultOverride ?? AttStatus.Authorized);
#else
			return Task.FromResult(AttStatus.Authorized);
#endif
		}
	}
}

namespace GameLovers.MobileServices.Device.Internal
{
	/// <summary>
	/// Internal MonoBehaviour that receives ATT results from the iOS bridge via <c>UnitySendMessage</c>.
	/// Mirrors the shape of <see cref="PermissionsCallbackReceiver"/> so each subsystem owns its own
	/// payload format and we don't have to multiplex.
	/// </summary>
	internal sealed class AttCallbackReceiver : MonoBehaviour
	{
		private static AttCallbackReceiver _instance;
		private readonly Dictionary<int, TaskCompletionSource<AttStatus>> _pending =
			new Dictionary<int, TaskCompletionSource<AttStatus>>();
		private int _nextId = 1;

		public static AttCallbackReceiver Instance
		{
			get
			{
				if (_instance != null)
				{
					return _instance;
				}

				var go = new GameObject("AttCallbackReceiver");
				DontDestroyOnLoad(go);
				_instance = go.AddComponent<AttCallbackReceiver>();
				return _instance;
			}
		}

		public int Register(TaskCompletionSource<AttStatus> tcs)
		{
			var id = _nextId++;
			_pending[id] = tcs;
			return id;
		}

		// Native iOS bridge calls UnitySendMessage("AttCallbackReceiver", "OnAttResult", "<id>:<status>")
		// where status is the int value of AttStatus.
		// ReSharper disable once UnusedMember.Global
		// ReSharper disable once InconsistentNaming
		public void OnAttResult(string payload)
		{
			try
			{
				var sep = payload.IndexOf(':');
				if (sep <= 0) return;
				var idText = payload.Substring(0, sep);
				var statusText = payload.Substring(sep + 1);

				if (!int.TryParse(idText, out var id) || !int.TryParse(statusText, out var statusInt))
				{
					return;
				}

				if (!_pending.TryGetValue(id, out var tcs))
				{
					return;
				}

				_pending.Remove(id);
				tcs.TrySetResult((AttStatus)statusInt);
			}
			catch (Exception e)
			{
				Debug.LogError($"[GameLovers.MobileServices] AttCallbackReceiver failed to parse '{payload}': {e.Message}");
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

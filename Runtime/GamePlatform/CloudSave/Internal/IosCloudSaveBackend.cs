#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>Game Center saved-games backend over <c>Plugins/iOS/CloudSave.m</c>.</summary>
	internal sealed class IosCloudSaveBackend : ICloudSaveBackend
	{
		private delegate void CloudSaveCallback(int requestId, string result);

		private static readonly Dictionary<int, AwaitableCompletionSource<string>> _requests = new Dictionary<int, AwaitableCompletionSource<string>>();
		private static int _nextRequestId = 1;

		public bool IsAvailable => _GameLoversCloudSaveIsAvailable() != 0;

		public async Awaitable<IReadOnlyList<CloudSaveSlot>> ListAsync()
		{
			var reply = await Request(id => _GameLoversCloudSaveList(id, OnNativeResult));
			return CloudSaveProtocol.ParseSlots(CloudSaveProtocol.ReadPayload(reply));
		}

		public async Awaitable<IReadOnlyList<CloudSaveVersion>> LoadVersionsAsync(string slot)
		{
			var reply = await Request(id => _GameLoversCloudSaveLoadVersions(slot, id, OnNativeResult));
			return CloudSaveProtocol.ParseVersions(CloudSaveProtocol.ReadPayload(reply));
		}

		public async Awaitable ResolveAsync(string slot, CloudSaveVersion chosen)
		{
			var encoded = Convert.ToBase64String(chosen.Data);
			CloudSaveProtocol.ReadPayload(await Request(id => _GameLoversCloudSaveResolve(slot, encoded, id, OnNativeResult)));
		}

		public async Awaitable SaveAsync(string slot, byte[] data, string description)
		{
			// Game Center saved games carry no description, so it is not sent.
			var encoded = Convert.ToBase64String(data);
			CloudSaveProtocol.ReadPayload(await Request(id => _GameLoversCloudSaveSave(slot, encoded, id, OnNativeResult)));
		}

		public async Awaitable DeleteAsync(string slot)
		{
			CloudSaveProtocol.ReadPayload(await Request(id => _GameLoversCloudSaveDelete(slot, id, OnNativeResult)));
		}

		[DllImport("__Internal")] private static extern int _GameLoversCloudSaveIsAvailable();
		[DllImport("__Internal")] private static extern void _GameLoversCloudSaveList(int requestId, CloudSaveCallback callback);
		[DllImport("__Internal")] private static extern void _GameLoversCloudSaveLoadVersions(string slot, int requestId, CloudSaveCallback callback);
		[DllImport("__Internal")] private static extern void _GameLoversCloudSaveResolve(string slot, string base64, int requestId, CloudSaveCallback callback);
		[DllImport("__Internal")] private static extern void _GameLoversCloudSaveSave(string slot, string base64, int requestId, CloudSaveCallback callback);
		[DllImport("__Internal")] private static extern void _GameLoversCloudSaveDelete(string slot, int requestId, CloudSaveCallback callback);

		private static Awaitable<string> Request(Action<int> start)
		{
			var completion = new AwaitableCompletionSource<string>();
			int id;
			lock (_requests)
			{
				id = _nextRequestId++;
				_requests[id] = completion;
			}

			start(id);
			return NativeCompletion.ToMainThread(completion);
		}

		[AOT.MonoPInvokeCallback(typeof(CloudSaveCallback))]
		private static void OnNativeResult(int requestId, string result)
		{
			AwaitableCompletionSource<string> completion;
			lock (_requests)
			{
				if (!_requests.TryGetValue(requestId, out completion)) return;
				_requests.Remove(requestId);
			}

			completion.TrySetResult(result);
		}
	}
}
#endif

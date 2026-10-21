using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class BackgroundTaskService : IBackgroundTaskService
	{
#if UNITY_IOS && !UNITY_EDITOR
		private delegate void ExpirationCallback(int taskId);

		private static readonly object _callbackLock = new object();
		private static readonly Dictionary<int, Action> _expirationCallbacks = new Dictionary<int, Action>();

		[DllImport("__Internal")] private static extern int _GameLoversBackgroundTaskBegin(string name, ExpirationCallback callback);
		[DllImport("__Internal")] private static extern void _GameLoversBackgroundTaskEnd(int taskId);
		[DllImport("__Internal")] private static extern double _GameLoversBackgroundTaskRemaining();
		[DllImport("__Internal")] private static extern int _GameLoversBackgroundTaskIsExpired(int taskId);
#endif

		/// <inheritdoc />
		public BackgroundTaskHandle Begin(string name, Action onExpiring = null)
		{
			var label = string.IsNullOrEmpty(name) ? "GameLovers.BackgroundTask" : name;
#if UNITY_IOS && !UNITY_EDITOR
			var taskId = _GameLoversBackgroundTaskBegin(label, OnNativeExpired);
			if (taskId == 0)
			{
				// The system refused the task (for example the application is already suspended).
				return new BackgroundTaskHandle(label, () => { }, () => TimeSpan.Zero, () => true);
			}

			if (onExpiring != null)
			{
				lock (_callbackLock) _expirationCallbacks[taskId] = onExpiring;
			}

			return new BackgroundTaskHandle(
				label,
				() =>
				{
					lock (_callbackLock) _expirationCallbacks.Remove(taskId);
					_GameLoversBackgroundTaskEnd(taskId);
				},
				() =>
				{
					var seconds = _GameLoversBackgroundTaskRemaining();
					return seconds < 0 ? TimeSpan.MaxValue : TimeSpan.FromSeconds(seconds);
				},
				() => _GameLoversBackgroundTaskIsExpired(taskId) != 0);
#else
			return new BackgroundTaskHandle(label, () => { }, () => TimeSpan.MaxValue, () => false);
#endif
		}

#if UNITY_IOS && !UNITY_EDITOR
		// Runs on the iOS main thread from the system's expiration handler; the native layer ends the task afterwards.
		[AOT.MonoPInvokeCallback(typeof(ExpirationCallback))]
		private static void OnNativeExpired(int taskId)
		{
			Action callback;
			lock (_callbackLock) _expirationCallbacks.TryGetValue(taskId, out callback);
			try
			{
				callback?.Invoke();
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogError($"[GameLovers.MobileServices] Background task expiration callback failed: {exception.Message}");
			}
		}
#endif
	}
}

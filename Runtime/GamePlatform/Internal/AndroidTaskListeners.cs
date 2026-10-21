#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>Attaches managed callbacks to Google Play Services <c>Task</c> objects through JNI.</summary>
	internal static class AndroidTaskListeners
	{
		private static readonly List<AndroidJavaProxy> _listeners = new List<AndroidJavaProxy>();

		/// <summary>
		/// Runs <paramref name="onComplete"/> when <paramref name="task"/> finishes. Callbacks arrive on Android's UI
		/// thread; the proxy is kept alive until it has run once.
		/// </summary>
		public static void Listen(AndroidJavaObject task, Action<AndroidJavaObject> onComplete)
		{
			CompleteListener listener = null;
			listener = new CompleteListener(finished =>
			{
				lock (_listeners) _listeners.Remove(listener);
				try
				{
					onComplete(finished);
				}
				catch (Exception exception)
				{
					Debug.LogError($"[GameLovers.MobileServices] Play Games callback failed: {exception.Message}");
				}
			});
			lock (_listeners) _listeners.Add(listener);
			task.Call<AndroidJavaObject>("addOnCompleteListener", listener);
		}

		/// <summary>The failure message of a finished <paramref name="task"/>.</summary>
		public static string DescribeFailure(AndroidJavaObject task)
		{
			using var exception = task.Call<AndroidJavaObject>("getException");
			return exception == null ? "The task failed." : exception.Call<string>("toString");
		}

		private sealed class CompleteListener : AndroidJavaProxy
		{
			private readonly Action<AndroidJavaObject> _onComplete;

			public CompleteListener(Action<AndroidJavaObject> onComplete) : base("com.google.android.gms.tasks.OnCompleteListener")
			{
				_onComplete = onComplete;
			}

			// ReSharper disable once InconsistentNaming
			public void onComplete(AndroidJavaObject task) => _onComplete(task);
		}
	}
}
#endif

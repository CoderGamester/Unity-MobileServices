using System;
using System.IO;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
#elif UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.NativeUi
{
	/// <summary>
	/// The kind of media <see cref="NativeMediaService.SaveToGalleryAsync"/> saves.
	/// </summary>
	public enum MediaType
	{
		Image, // A still image such as a PNG or JPEG.
		Video // A video file such as an MP4.
	}

	/// <summary>
	/// Saves files to the device gallery and picks an image with the operating system's own picker. Neither call
	/// asks the player for a permission, except that iOS shows its add-only photo prompt the first time something is saved.
	/// </summary>
	public static class NativeMediaService
	{
#if UNITY_EDITOR
		/// <summary>Editor simulator hook that stands in for the gallery; true when the file was saved.</summary>
		internal static Func<string, MediaType, bool> EditorSaveToGalleryOverride;

		/// <summary>Editor simulator hook that stands in for the picker; returns the chosen path or null.</summary>
		internal static Func<string> EditorPickImageOverride;
#endif

#if UNITY_IOS && !UNITY_EDITOR
		private delegate void MediaCallback(int requestId, string result);

		private static readonly Dictionary<int, AwaitableCompletionSource<string>> _requests = new Dictionary<int, AwaitableCompletionSource<string>>();
		private static int _nextRequestId = 1;

		[DllImport("__Internal", EntryPoint = "_GameLoversMediaSave")]
		private static extern void MediaSave(string path, int isVideo, int requestId, MediaCallback callback);

		[DllImport("__Internal", EntryPoint = "_GameLoversMediaPickImage")]
		private static extern void MediaPickImage(int requestId, MediaCallback callback);
#elif UNITY_ANDROID && !UNITY_EDITOR
		private static readonly List<AndroidJavaProxy> _androidCallbacks = new List<AndroidJavaProxy>();
#endif

		/// <summary>
		/// Copies a file into the shared gallery. On iOS the first save shows the add-only photo prompt and needs the
		/// <c>PhotoLibraryAddOnly</c> usage description in the Mobile Services Config. On Android 10 and newer no permission
		/// is needed; older versions need the <c>Write External Storage Legacy</c> config toggle.
		/// </summary>
		/// <param name="filePath">An absolute path to an existing image or video file.</param>
		/// <param name="mediaType">Whether the file is an image or a video.</param>
		/// <returns>True when the file was saved.</returns>
		public static Awaitable<bool> SaveToGalleryAsync(string filePath, MediaType mediaType = MediaType.Image)
		{
			var completion = new AwaitableCompletionSource<string>();
			if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
			{
				Debug.LogWarning($"[GameLovers.MobileServices] SaveToGallery needs an existing file; '{filePath}' was not found.");
				completion.SetResult(null);
				return ToSaved(completion);
			}

#if UNITY_EDITOR
			var simulatedSave = EditorSaveToGalleryOverride;
			if (simulatedSave == null)
			{
				Debug.Log($"[GameLovers.MobileServices] SaveToGallery is not available in the editor unless the Mobile Services simulator is engaged ({filePath}).");
				completion.SetResult(null);
				return ToSaved(completion);
			}

			completion.SetResult(simulatedSave(filePath, mediaType) ? "ok" : null);
#elif UNITY_IOS
			MediaSave(filePath, mediaType == MediaType.Video ? 1 : 0, Register(completion), OnNativeResult);
#elif UNITY_ANDROID
			StartAndroidSave(filePath, mediaType, completion);
#else
			Debug.LogWarning("[GameLovers.MobileServices] SaveToGallery is only available on iOS and Android.");
			completion.SetResult(null);
#endif
			return ToSaved(completion);
		}

		/// <summary>
		/// Opens the system image picker and returns a copy of the chosen image. The copy lives in a temporary or cache
		/// folder the app owns, so it can be loaded straight away; delete it when done. Needs no permission.
		/// </summary>
		/// <returns>The absolute path of the copied image, or null when the player cancelled.</returns>
		public static Awaitable<string> PickImageAsync()
		{
			var completion = new AwaitableCompletionSource<string>();
#if UNITY_EDITOR
			var simulatedPick = EditorPickImageOverride;
			if (simulatedPick == null)
			{
				Debug.Log("[GameLovers.MobileServices] PickImage is not available in the editor unless the Mobile Services simulator is engaged.");
				completion.SetResult(null);
				return NativeCompletion.ToMainThread(completion);
			}

			completion.SetResult(simulatedPick());
#elif UNITY_IOS
			MediaPickImage(Register(completion), OnNativeResult);
#elif UNITY_ANDROID
			StartAndroidPick(completion);
#else
			Debug.LogWarning("[GameLovers.MobileServices] PickImage is only available on iOS and Android.");
			completion.SetResult(null);
#endif
			return NativeCompletion.ToMainThread(completion);
		}

		private static async Awaitable<bool> ToSaved(AwaitableCompletionSource<string> completion)
		{
			return await NativeCompletion.ToMainThread(completion) != null;
		}

#if UNITY_IOS && !UNITY_EDITOR
		private static int Register(AwaitableCompletionSource<string> completion)
		{
			lock (_requests)
			{
				var id = _nextRequestId++;
				_requests[id] = completion;
				return id;
			}
		}

		[AOT.MonoPInvokeCallback(typeof(MediaCallback))]
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
#elif UNITY_ANDROID && !UNITY_EDITOR
		private static void StartAndroidSave(string filePath, MediaType mediaType, AwaitableCompletionSource<string> completion)
		{
			try
			{
				using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
				using var media = new AndroidJavaClass("com.gamelovers.mobileservices.GameLoversMedia");
				var callback = new ResultProxy("com.gamelovers.mobileservices.GameLoversMedia$ResultCallback", "onResult", completion);
				Retain(callback);
				media.CallStatic("saveToGallery", activity, filePath, mediaType == MediaType.Video, GetAlbumName(), callback);
			}
			catch (Exception e)
			{
				Debug.LogError($"[GameLovers.MobileServices] SaveToGallery failed: {e.Message}");
				completion.TrySetResult(null);
			}
		}

		private static void StartAndroidPick(AwaitableCompletionSource<string> completion)
		{
			try
			{
				using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
				using var picker = new AndroidJavaClass("com.gamelovers.mobileservices.GameLoversMediaPickerActivity");
				var callback = new ResultProxy("com.gamelovers.mobileservices.GameLoversMediaPickerActivity$PathCallback", "onPicked", completion);
				Retain(callback);
				picker.CallStatic("start", activity, callback);
			}
			catch (Exception e)
			{
				Debug.LogError($"[GameLovers.MobileServices] PickImage failed: {e.Message}. " +
					"Turn on 'Include Media Picker' in the Mobile Services Config and rebuild.");
				completion.TrySetResult(null);
			}
		}

		private static string GetAlbumName()
		{
			var name = string.IsNullOrWhiteSpace(Application.productName) ? "Game" : Application.productName.Trim();
			foreach (var invalid in new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
			{
				name = name.Replace(invalid, '-');
			}
			return name;
		}

		private static void Retain(AndroidJavaProxy callback)
		{
			lock (_androidCallbacks) _androidCallbacks.Add(callback);
		}

		private static void Release(AndroidJavaProxy callback)
		{
			lock (_androidCallbacks) _androidCallbacks.Remove(callback);
		}

		private sealed class ResultProxy : AndroidJavaProxy
		{
			private readonly string _methodName;
			private readonly AwaitableCompletionSource<string> _completion;

			public ResultProxy(string javaInterface, string methodName, AwaitableCompletionSource<string> completion) : base(javaInterface)
			{
				_methodName = methodName;
				_completion = completion;
			}

			public override AndroidJavaObject Invoke(string methodName, object[] args)
			{
				if (methodName != _methodName) return base.Invoke(methodName, args);
				Release(this);
				_completion.TrySetResult(args != null && args.Length > 0 ? args[0] as string : null);
				return null;
			}
		}
#endif
	}
}

using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device.Internal
{
	/// <summary>Sets and clears the OS shortcuts and, on iOS, hands taps to the deep-link service.</summary>
	internal static class AppShortcutsBridge
	{
#if UNITY_IOS && !UNITY_EDITOR
		[DllImport("__Internal")] private static extern void _GameLoversShortcutsSet(string json);
		[DllImport("__Internal")] private static extern void _GameLoversShortcutsClear();
		[DllImport("__Internal")] private static extern IntPtr _GameLoversShortcutTakePending();
#endif

		/// <summary>Publishes the shortcuts to the OS, replacing the previous set.</summary>
		public static void Set(IReadOnlyList<AppShortcut> shortcuts)
		{
#if UNITY_EDITOR
			Debug.Log($"[GameLovers.MobileServices] SetShortcuts is not available in the editor ({shortcuts.Count} shortcut(s)); " +
				"drive taps with EditorPlatformSimulator.SimulateDeepLink.");
#elif UNITY_IOS
			var items = new ShortcutItem[shortcuts.Count];
			for (var i = 0; i < items.Length; i++)
			{
				items[i] = new ShortcutItem { id = shortcuts[i].Id, title = shortcuts[i].Title, subtitle = shortcuts[i].Subtitle ?? string.Empty, uri = shortcuts[i].Uri };
			}
			_GameLoversShortcutsSet(JsonUtility.ToJson(new ShortcutItems { items = items }));
#elif UNITY_ANDROID
			SetAndroid(shortcuts);
#else
			Debug.LogWarning("[GameLovers.MobileServices] Shortcuts are only available on iOS and Android.");
#endif
		}

		/// <summary>Removes every shortcut the app published.</summary>
		public static void Clear()
		{
#if UNITY_EDITOR
			Debug.Log("[GameLovers.MobileServices] ClearShortcuts is not available in the editor.");
#elif UNITY_IOS
			_GameLoversShortcutsClear();
#elif UNITY_ANDROID
			CallAndroid("clear", null);
#endif
		}

		/// <summary>Takes the deep-link URI of a shortcut tapped since the last call, or null. iOS only.</summary>
		public static string TakePendingLink()
		{
#if UNITY_IOS && !UNITY_EDITOR
			var pointer = _GameLoversShortcutTakePending();
			return pointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer);
#else
			return null;
#endif
		}

#if UNITY_ANDROID && !UNITY_EDITOR
		private static void SetAndroid(IReadOnlyList<AppShortcut> shortcuts)
		{
			if (AndroidApiLevel.Current < 25)
			{
				Debug.LogWarning("[GameLovers.MobileServices] Shortcuts need Android 7.1 (API 25) or newer.");
				return;
			}

			var ids = new string[shortcuts.Count];
			var titles = new string[shortcuts.Count];
			var subtitles = new string[shortcuts.Count];
			var uris = new string[shortcuts.Count];
			for (var i = 0; i < ids.Length; i++)
			{
				ids[i] = shortcuts[i].Id;
				titles[i] = shortcuts[i].Title;
				subtitles[i] = shortcuts[i].Subtitle ?? string.Empty;
				uris[i] = shortcuts[i].Uri;
			}
			CallAndroid("set", new object[] { ids, titles, subtitles, uris });
		}

		private static void CallAndroid(string method, object[] arguments)
		{
			try
			{
				using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
				using var shortcuts = new AndroidJavaClass("com.gamelovers.mobileservices.GameLoversShortcuts");
				var all = new object[(arguments?.Length ?? 0) + 1];
				all[0] = activity;
				arguments?.CopyTo(all, 1);
				shortcuts.CallStatic(method, all);
			}
			catch (Exception exception)
			{
				Debug.LogError($"[GameLovers.MobileServices] Shortcuts failed: {exception.Message}");
			}
		}
#endif

#if UNITY_IOS && !UNITY_EDITOR
		[Serializable]
		private sealed class ShortcutItems
		{
			public ShortcutItem[] items;
		}

		[Serializable]
		private sealed class ShortcutItem
		{
			public string id;
			public string title;
			public string subtitle;
			public string uri;
		}
#endif
	}
}

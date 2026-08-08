using System;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLovers.MobileServices.NativeUi
{
	/// <summary>
	/// The OS button styles an alert can use, in iOS's vocabulary.
	/// </summary>
	public enum AlertButtonStyle
	{
		Default,
		Destructive,
		Cancel
	}

	/// <summary>
	/// One alert button: its label, its OS style, and the callback fired when the user picks it.
	/// </summary>
	public struct AlertButton
	{
		public string Text;
		public AlertButtonStyle Style;
		public Action Callback;
	}

	/// <summary>
	/// This service provides the functionality to call native UI screens
	/// </summary>
	public static class NativeUiService
	{
#if UNITY_IOS
		/// <summary>Native iOS callback signature; the button is identified by its text.</summary>
		internal delegate void AlertButtonDelegate(string buttonText);

		private static AlertButton[] _currentButtons;
#endif

#if UNITY_EDITOR
		// Editor-only override hook consumed by EditorPlatformSimulator so the Device Simulator can model
		// the real "shown when requested" review flow in the editor (mirroring the Permissions / ATT
		// EditorRequest*Override hooks). When set, RequestReview routes here instead of logging; when
		// unset, the editor branch keeps its plain Debug.Log no-op. Player builds carry none of this.
		internal static System.Action EditorRequestReviewOverride;
#endif

		/// <summary>
		/// Shows an alert native OS message popup with the given <paramref name="title"/>, <paramref name="message"/>
		/// and the <paramref name="buttons"/> ordered from left to right.
		/// If on iOS device, it can be set the pop up to be visible as an alert sheet depending on the given <paramref name="isAlertSheet"/>
		/// </summary>
		/// <exception cref="SystemException">
		/// Thrown if the current platform is not iOS nor Android
		/// </exception>
		public static void ShowAlertPopUp(bool isAlertSheet, string title, string message, params AlertButton[] buttons)
		{
#if UNITY_EDITOR
			Debug.Log($"Show Alert Pop Up is not available in the editor and was triggered with: {title} - {message}");
#elif UNITY_IOS
			_currentButtons = buttons ?? throw new ArgumentException("The buttons count must be higher than zero");

			var buttonsText = new string[buttons.Length];
			var buttonsStyle = new int[buttons.Length];

			for (var i = 0; i < buttons.Length; i++)
			{
				buttonsText[i] = buttons[i].Text;
				buttonsStyle[i] = (int) buttons[i].Style;
			}

			AlertMessage(isAlertSheet, title, message, buttonsText, buttonsStyle, buttons.Length, AlertButtonCallback);
#elif UNITY_ANDROID
			using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
			using (var unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
			using (var alertDialogBuilder = new AndroidJavaObject("android.app.AlertDialog$Builder", unityActivity))
			using (var alertDialog = alertDialogBuilder.Call<AndroidJavaObject>("create"))
			{
				alertDialog.Call("setTitle", title);
				alertDialog.Call("setMessage", message);

				for (var i = 0; i < buttons.Length; i++)
				{
					alertDialog.Call("setButton", ConvertToAndroidStyle(buttons[i].Style),
						buttons[i].Text, new AndroidButtonCallback(buttons[i].Callback));
				}

				alertDialog.Call("show");
			}
#else
			throw new SystemException("Show an alert Pop Up is only available for iOS and Android platforms");
#endif
		}

		/// <summary>
		/// Shows a toast native OS message popup with the given <paramref name="message"/>.
		/// This toast will be available on the screen for 3.5sec or 2sec depending
		/// on the given <paramref name="isLongDuration"/> information
		/// </summary>
		/// <exception cref="SystemException">
		/// Thrown if the current platform is not iOS nor Android
		/// </exception>
		public static void ShowToastMessage(string message, bool isLongDuration)
		{
#if UNITY_EDITOR
			Debug.Log($"Show Toast message is not available in the editor and was triggered with: {message}");
#elif UNITY_IOS
			ToastMessage(message, isLongDuration);
#elif UNITY_ANDROID
			using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
			using (var unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
			using (var toastClass = new AndroidJavaClass("android.widget.Toast"))
			{
				var duration = isLongDuration ? toastClass.GetStatic<int>("LENGTH_LONG") : toastClass.GetStatic<int>("LENGTH_SHORT");
				var toast = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity, message, duration);

				toast.Call("show");

				toast.Dispose();
			}
#else
			throw new SystemException("Show a Toast message is only available for iOS and Android platforms");
#endif
		}

		/// <summary>
		/// Requests an OS-mediated app rating prompt. iOS uses <c>SKStoreReviewController</c>; Android uses
		/// the Play In-App Review API. Both platforms throttle requests internally, so calling this
		/// frequently does NOT spam the user — the OS decides whether to actually show the prompt.
		/// On Editor / unsupported platforms this is a safe no-op.
		/// </summary>
		/// <remarks>
		/// Fire-and-forget: neither platform exposes a "was actually shown" signal (the OS may silently
		/// suppress the prompt under its own throttling quota — that is normal and is NOT an error).
		/// Because there is no success callback, this logs when the prompt is requested. When the
		/// platform DOES surface an error (Android's Play flow cannot run — Play Core library missing,
		/// the request flow failed, or launch threw), it is logged as a warning / error.
		/// On Android the Play In-App Review library is auto-injected at build time by
		/// <c>MobileServicesBuildPostprocessor</c> (Tools &gt; GameLovers &gt; Mobile Services &gt; Select Mobile Services Config).
		/// If you opt out of that injection you must add it yourself to <c>mainTemplate.gradle</c>:
		/// <c>implementation 'com.google.android.play:review:2.0.2'</c> (or newer). This never throws.
		/// </remarks>
		public static void RequestReview()
		{
#if UNITY_EDITOR
			if (EditorRequestReviewOverride != null)
			{
				EditorRequestReviewOverride.Invoke();
			}
			else
			{
				Debug.Log("[GameLovers.MobileServices] RequestReview() is a no-op in the editor unless the Mobile Services Device Simulator is enabled.");
			}
#elif UNITY_IOS
			// SKStoreReviewController gives no success/error callback — log that we requested it.
			Debug.Log("[GameLovers.MobileServices] Requested App Store review prompt (SKStoreReviewController). The OS decides whether to actually show it; there is no callback.");
			RequestReviewNative();
#elif UNITY_ANDROID
			RequestReviewAndroid();
#endif
		}

		/// <summary>
		/// Opens the OS share sheet with the given content. Any combination of <paramref name="text"/>,
		/// <paramref name="url"/>, and <paramref name="imagePath"/> may be supplied; nulls are skipped.
		/// <paramref name="imagePath"/> must be an absolute filesystem path. <paramref name="title"/> is
		/// used as the chooser title (Android) and is ignored on iOS.
		/// On Editor / unsupported platforms this is a safe no-op.
		/// </summary>
		public static void Share(string text, string url = null, string imagePath = null, string title = null)
		{
#if UNITY_EDITOR
			Debug.Log($"Share is not available in the editor (text='{text}', url='{url}', imagePath='{imagePath}').");
#elif UNITY_IOS
			ShareNative(text ?? string.Empty, url ?? string.Empty, imagePath ?? string.Empty);
#elif UNITY_ANDROID
			ShareAndroid(text, url, imagePath, title);
#endif
		}

#if UNITY_ANDROID
		private static void RequestReviewAndroid()
		{
			try
			{
				using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
				using var managerFactory = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory");
				using var manager = managerFactory.CallStatic<AndroidJavaObject>("create", activity);
				using var requestTask = manager.Call<AndroidJavaObject>("requestReviewFlow");
				requestTask.Call<AndroidJavaObject>("addOnCompleteListener", new ReviewFlowListener(activity, manager));
			}
			catch (Exception e)
			{
				Debug.LogError($"[GameLovers.MobileServices] RequestReview failed: {e.Message}. " +
					"Ensure 'com.google.android.play:review' is on the gradle classpath.");
			}
		}

		private static void ShareAndroid(string text, string url, string imagePath, string title)
		{
			try
			{
				using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
				using var intentClass = new AndroidJavaClass("android.content.Intent");
				using var intent = new AndroidJavaObject("android.content.Intent");

				intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));

				var hasImage = !string.IsNullOrEmpty(imagePath);
				if (hasImage)
				{
					intent.Call<AndroidJavaObject>("setType", "image/*");
					using var uriClass = new AndroidJavaClass("android.net.Uri");
					using var fileClass = new AndroidJavaObject("java.io.File", imagePath);
					using var imageUri = uriClass.CallStatic<AndroidJavaObject>("fromFile", fileClass);
					intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), imageUri);
					intent.Call<AndroidJavaObject>("addFlags", intentClass.GetStatic<int>("FLAG_GRANT_READ_URI_PERMISSION"));
				}
				else
				{
					intent.Call<AndroidJavaObject>("setType", "text/plain");
				}

				var combinedText = string.IsNullOrEmpty(url) ? text : (string.IsNullOrEmpty(text) ? url : text + " " + url);
				if (!string.IsNullOrEmpty(combinedText))
				{
					intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), combinedText);
				}

				using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, title ?? string.Empty);
				activity.Call("startActivity", chooser);
			}
			catch (Exception e)
			{
				Debug.LogError($"[GameLovers.MobileServices] Share failed: {e.Message}");
			}
		}

		private class ReviewFlowListener : AndroidJavaProxy
		{
			private readonly AndroidJavaObject _activity;
			private readonly AndroidJavaObject _manager;

			// Modern Play In-App Review (v2.x of com.google.android.play:review) uses Google Play Services Tasks.
			// The legacy com.google.android.play.core.tasks.OnCompleteListener applies to the deprecated
			// monolithic com.google.android.play:core library only — do not use it here.
			public ReviewFlowListener(AndroidJavaObject activity, AndroidJavaObject manager)
				: base("com.google.android.gms.tasks.OnCompleteListener")
			{
				_activity = activity;
				_manager = manager;
			}

			// ReSharper disable once InconsistentNaming
			public void onComplete(AndroidJavaObject task)
			{
				try
				{
					if (!task.Call<bool>("isSuccessful"))
					{
						Debug.LogWarning("[GameLovers.MobileServices] requestReviewFlow returned an unsuccessful task — the review prompt cannot be shown.");
						return;
					}

					using var reviewInfo = task.Call<AndroidJavaObject>("getResult");
					using var launchTask = _manager.Call<AndroidJavaObject>("launchReviewFlow", _activity, reviewInfo);
					_ = launchTask;
					// launchReviewFlow's task completes when the flow finishes, but the OS gives no
					// "was actually displayed" signal (it may be suppressed by quota — not an error).
					Debug.Log("[GameLovers.MobileServices] Launched Play In-App Review flow. The OS decides whether to actually show it; there is no shown callback.");
				}
				catch (Exception e)
				{
					Debug.LogError($"[GameLovers.MobileServices] launchReviewFlow failed: {e.Message}");
				}
			}
		}
#endif

#if UNITY_IOS
		[System.Runtime.InteropServices.DllImport("__Internal", EntryPoint = "_GameLoversAlertMessage")]
		private static extern void AlertMessage(bool isSheet, string title, string message, string[] buttonsText,
			int[] buttonsStyle, int buttonsLength, AlertButtonDelegate alertButtonCallback);

		[System.Runtime.InteropServices.DllImport("__Internal", EntryPoint = "_GameLoversToastMessage")]
		private static extern void ToastMessage(string message, bool isLongDuration);

		[System.Runtime.InteropServices.DllImport("__Internal", EntryPoint = "_GameLoversRequestReview")]
		private static extern void RequestReviewNative();

		[System.Runtime.InteropServices.DllImport("__Internal", EntryPoint = "_GameLoversShare")]
		private static extern void ShareNative(string text, string url, string imagePath);

		[AOT.MonoPInvokeCallback(typeof(AlertButtonDelegate))]
		private static void AlertButtonCallback(string buttonText)
		{
			if (_currentButtons == null)
			{
				return;
			}

			foreach (var button in _currentButtons)
			{
				if (button.Text == buttonText)
				{
					button.Callback?.Invoke();
					break;
				}
			}
		}
#elif UNITY_ANDROID
		private class AndroidButtonCallback : AndroidJavaProxy
		{
			private readonly Action _callback;

			public AndroidButtonCallback(Action callback) : base("android.content.DialogInterface$OnClickListener")
			{
				_callback = callback;
			}

			// ReSharper disable once InconsistentNaming
			public void onClick(AndroidJavaObject dialog, int which)
			{
				dialog.Call("dismiss");

				_callback();
			}
		}

		private static int ConvertToAndroidStyle(AlertButtonStyle style)
		{
			switch (style)
			{
				case AlertButtonStyle.Default:
					return -3;
				case AlertButtonStyle.Destructive:
					return -1;
				case AlertButtonStyle.Cancel:
					return -2;
				default:
					throw new ArgumentOutOfRangeException(nameof(style), style, "Wrong given style");
			}
		}
#endif
	}
}

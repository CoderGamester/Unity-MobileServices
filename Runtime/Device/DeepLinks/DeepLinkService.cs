using System;
using GameLovers.MobileServices.Device.Internal;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class DeepLinkService : IDeepLinkService, IDisposable
	{
		private Action<Uri> _onLinkActivated;
		private Uri _pendingColdStartLink;
#if UNITY_IOS && !UNITY_EDITOR
		private readonly DeviceServicesHost _shortcutHost;
#endif

		/// <inheritdoc />
		public Uri PendingColdStartLink => _pendingColdStartLink;

		/// <inheritdoc />
		public event Action<Uri> OnLinkActivated
		{
			add
			{
				_onLinkActivated += value;
				if (_pendingColdStartLink == null)
				{
					return;
				}
				var pending = _pendingColdStartLink;
				_pendingColdStartLink = null;
				value(pending);
			}
			remove => _onLinkActivated -= value;
		}

		public DeepLinkService()
		{
			Application.deepLinkActivated += OnDeepLinkActivated;

			// If the app was cold-launched with a deep link, Application.absoluteURL is non-empty
			// before any subscriber attaches. Capture it here and replay on the first subscription.
			if (!string.IsNullOrEmpty(Application.absoluteURL))
			{
				_pendingColdStartLink = TryParse(Application.absoluteURL);
			}

#if UNITY_IOS && !UNITY_EDITOR
			// iOS delivers a tapped home-screen shortcut natively, not through Unity's deep-link callback, so it is collected
			// at start-up (a cold start) and whenever the app returns to the foreground.
			_shortcutHost = DeviceServicesHost.Instance;
			_shortcutHost.RegisterFocusChanged(OnShortcutFocusChanged);
			DeliverPendingShortcut();
#endif
		}

		/// <inheritdoc />
		public void Dispose()
		{
			Application.deepLinkActivated -= OnDeepLinkActivated;
#if UNITY_IOS && !UNITY_EDITOR
			if (_shortcutHost != null) _shortcutHost.UnregisterFocusChanged(OnShortcutFocusChanged);
#endif
			_onLinkActivated = null;
			_pendingColdStartLink = null;
		}

#if UNITY_EDITOR
		/// <summary>
		/// Editor-only simulator hook. Mimics what <c>Application.deepLinkActivated</c> would do
		/// when the OS hands the app a link at runtime — supersedes any pending cold-start link
		/// and dispatches to all current subscribers.
		/// </summary>
		internal void SimulateLinkActivated(Uri uri)
		{
			if (uri == null)
			{
				return;
			}
			_pendingColdStartLink = null;
			_onLinkActivated?.Invoke(uri);
		}
#endif

#if UNITY_IOS && !UNITY_EDITOR
		private void OnShortcutFocusChanged(bool focused)
		{
			if (focused) DeliverPendingShortcut();
		}

		private void DeliverPendingShortcut()
		{
			var uri = TryParse(AppShortcutsBridge.TakePendingLink());
			if (uri == null)
			{
				return;
			}

			// Unlike a runtime link, a shortcut that arrives before anyone subscribed is kept for the first subscriber.
			if (_onLinkActivated == null)
			{
				_pendingColdStartLink = uri;
				return;
			}
			_onLinkActivated.Invoke(uri);
		}
#endif

		private void OnDeepLinkActivated(string url)
		{
			var parsed = TryParse(url);
			if (parsed == null)
			{
				return;
			}

			// Runtime delivery supersedes / consumes the pending cold-start link.
			_pendingColdStartLink = null;
			_onLinkActivated?.Invoke(parsed);
		}

		private static Uri TryParse(string url)
		{
			if (string.IsNullOrEmpty(url))
			{
				return null;
			}
			return Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed : null;
		}
	}
}

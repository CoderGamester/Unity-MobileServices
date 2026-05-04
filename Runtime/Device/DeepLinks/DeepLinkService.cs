using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class DeepLinkService : IDeepLinkService, IDisposable
	{
		private Action<Uri> _onLinkActivated;
		private Uri _pendingColdStartLink;

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
		}

		public void Dispose()
		{
			Application.deepLinkActivated -= OnDeepLinkActivated;
			_onLinkActivated = null;
			_pendingColdStartLink = null;
		}

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

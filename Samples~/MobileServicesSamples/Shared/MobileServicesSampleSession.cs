using System;
using GameLovers.MobileServices.Device;
using UnityEngine;
using UnityEngine.SceneManagement;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples
{
	/// <summary>Owns the sample player's deep-link lifetime across scene transitions.</summary>
	public sealed class MobileServicesSampleSession : MonoBehaviour, IDeepLinkService
	{
		private static MobileServicesSampleSession _instance;

		private DeepLinkService _source;
		private Action<Uri> _onLinkActivated;
		private Uri _pendingLink;
		private Uri _lastColdStartLink;

		/// <inheritdoc />
		public Uri PendingColdStartLink => _pendingLink;

		/// <summary>Returns the URL that cold-launched the current sample player, if any.</summary>
		public Uri LastColdStartLink => _lastColdStartLink;

		/// <inheritdoc />
		public event Action<Uri> OnLinkActivated
		{
			add
			{
				_onLinkActivated += value;
				DeliverPendingLink(value);
			}
			remove => _onLinkActivated -= value;
		}

		private void Awake()
		{
			if (_instance != null && !ReferenceEquals(_instance, this))
			{
				Destroy(gameObject);
				return;
			}

			_instance = this;
			DontDestroyOnLoad(gameObject);
			_source = new DeepLinkService();
			_lastColdStartLink = _source.PendingColdStartLink;
			_source.OnLinkActivated += ReceiveLink;
		}

		private void OnDestroy()
		{
			if (!ReferenceEquals(_instance, this)) return;

			_source?.Dispose();
			_source = null;
			_onLinkActivated = null;
			_pendingLink = null;
			_instance = null;
		}

		/// <summary>Returns the one sample session, creating it before a page binds its UI.</summary>
		public static MobileServicesSampleSession GetOrCreate()
		{
			if (_instance != null) return _instance;

			var host = new GameObject(nameof(MobileServicesSampleSession));
			_instance = host.AddComponent<MobileServicesSampleSession>();
			return _instance;
		}

		private void ReceiveLink(Uri uri)
		{
			if (uri == null) return;

			_pendingLink = uri;
			if (MobileServicesSamplePages.TryGetPage(SceneManager.GetActiveScene().name, out var page) &&
			    page == MobileServicesSamplePage.Links)
			{
				DeliverPendingLink(_onLinkActivated);
				return;
			}

			MobileServicesSampleNavigation.TryNavigate(MobileServicesSamplePage.Links);
		}

		private void DeliverPendingLink(Action<Uri> subscriber)
		{
			if (_pendingLink == null || subscriber == null) return;

			var pending = _pendingLink;
			_pendingLink = null;
			subscriber(pending);
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaticState()
		{
			_instance = null;
		}
	}
}

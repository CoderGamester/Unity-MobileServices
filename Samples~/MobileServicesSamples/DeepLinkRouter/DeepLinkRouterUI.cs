using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Samples;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.DeepLinkRouter
{
	/// <summary>Demonstrates raw links, synchronous route configuration and cold-start replay.</summary>
	public sealed class DeepLinkRouterUI : MonoBehaviour
	{
		private UIDocument _document;
		private MobileServicesSampleSession _session;
		private IDeepLinkService _deepLink;
		private IHapticsService _haptics;
		private GameLovers.MobileServices.Device.DeepLinkRouter _router;
		private VisualElement _boundRoot;
		private Label _log;
		private Label _coldStart;
		private readonly List<string> _logEntries = new List<string>();

		private void Awake()
		{
			_document = GetComponent<UIDocument>();
			EnsureRuntimeDependencies();
		}

		private void Start()
		{
			EnsureRuntimeDependencies();
			_deepLink.OnLinkActivated += OnRawLink;
			EnsureUiBound();
			if (_session.LastColdStartLink != null) Log($"Cold-start replay received: {_session.LastColdStartLink}");
			Log("Ready. On a device, launch with the scheme above for the OS path.");
		}

		private void Update()
		{
			EnsureRuntimeDependencies();
			EnsureUiBound();
			if (_coldStart != null)
				_coldStart.text = SampleStatusFormatter.Format(
					new SampleStatusEntry("Cold-start link", _session.LastColdStartLink));
		}

		private void OnDestroy()
		{
			_boundRoot?.UnregisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
			if (_deepLink != null) _deepLink.OnLinkActivated -= OnRawLink;
			_router?.Dispose();
			(_haptics as IDisposable)?.Dispose();
		}

		private void EnsureRuntimeDependencies()
		{
			if (_session == null) _session = MobileServicesSampleSession.GetOrCreate();
			if (_deepLink == null) _deepLink = _session;
			if (_haptics == null) _haptics = new HapticsService();
			if (_router != null) return;
			_session = MobileServicesSampleSession.GetOrCreate();
			_router = new GameLovers.MobileServices.Device.DeepLinkRouter(_deepLink, router =>
			{
				router.MapRoute("/promo/:id", (uri, parameters) =>
					Log($"Routed promo: id={parameters["id"]}, uri={uri}"));
				router.MapRoute("/profile/:user", (uri, parameters) =>
					Log($"Routed profile: user={parameters["user"]}, uri={uri}"));
				router.MapRoute("/settings", (uri, _) => Log($"Routed settings: {uri}"));
			});
		}

		private void EnsureUiBound()
		{
			EnsureRuntimeDependencies();
			if (_document == null) _document = GetComponent<UIDocument>();
			var root = _document == null ? null : _document.rootVisualElement;
			if (root == null) return;
			var coldStart = root.Q<Label>("cold-start");
			if (ReferenceEquals(_coldStart, coldStart)) return;

			_boundRoot?.UnregisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
			_boundRoot = root;
			_log = root.Q<Label>("log");
			_coldStart = coldStart;
			BindClickHaptics(root);
			var scheme = DeepLinkSampleScheme.FromIdentifier(Application.identifier);
			root.Q<Label>("scheme").text = SampleStatusFormatter.Format(
				new SampleStatusEntry("URL scheme", scheme),
				new SampleStatusEntry("Bundle identifier", Application.identifier));
			root.Q<Button>("promo")?.RegisterCallback<ClickEvent>(_ => Dispatch($"{scheme}://promo/spring2026", true));
			root.Q<Button>("profile")?.RegisterCallback<ClickEvent>(_ => Dispatch($"{scheme}://profile/abc123", true));
			root.Q<Button>("settings")?.RegisterCallback<ClickEvent>(_ => Dispatch($"{scheme}://settings", true));
			root.Q<Button>("unmatched")?.RegisterCallback<ClickEvent>(_ => Dispatch($"{scheme}://unknown/path", true));
			root.Q<Button>("raw")?.RegisterCallback<ClickEvent>(_ =>
			{
				var uri = new Uri($"{scheme}://promo/raw-only");
				Log($"Raw simulation (router bypassed): {uri}");
			});
			RefreshLog();
		}

		private void Dispatch(string value, bool routerOnly)
		{
			var uri = new Uri(value);
			var handled = _router.TryDispatch(uri);
			if (!handled) Log($"Unmatched route: {uri}");
			if (routerOnly && handled) Log($"Router-only simulation handled {uri}");
		}

		private void OnRawLink(Uri uri) => Log($"Raw link event: {uri}");

		private void BindClickHaptics(VisualElement root)
		{
			root.RegisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
		}

		private void OnButtonClick(ClickEvent evt)
		{
			var target = evt.target as VisualElement;
			var button = target as Button ?? target?.GetFirstAncestorOfType<Button>();
			if (button != null && button.enabledInHierarchy)
			{
				_haptics.PlayPreset(HapticPreset.Selection);
			}
		}

		private void Log(string message)
		{
			_logEntries.Insert(0, message);
			if (_logEntries.Count > 12) _logEntries.RemoveAt(_logEntries.Count - 1);
			RefreshLog();
		}

		private void RefreshLog()
		{
			if (_log != null) _log.text = string.Join("\n", _logEntries);
		}

	}
}

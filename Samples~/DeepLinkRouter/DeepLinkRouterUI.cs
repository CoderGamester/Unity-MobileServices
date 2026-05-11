using System;
using GameLovers.MobileServices.Device;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.DeepLinkRouter
{
	/// <summary>
	/// <see cref="IDeepLinkRouter"/> pattern sample with cold-start replay demonstration. See the
	/// per-sample <c>README.md</c>.
	/// </summary>
	public sealed class DeepLinkRouterUI : MonoBehaviour
	{
		private DeepLinkService _deepLink;
		private GameLovers.MobileServices.Device.DeepLinkRouter _router;
		private Text _log;
		private Text _coldStartLabel;

		private void Awake()
		{
			_deepLink = new DeepLinkService();
			_router = new GameLovers.MobileServices.Device.DeepLinkRouter(_deepLink);

			_router.MapRoute("/promo/:id", (uri, p) =>
				Log($"[promo] id={p["id"]} (full: {uri})"));
			_router.MapRoute("/profile/:userId", (uri, p) =>
				Log($"[profile] userId={p["userId"]} (full: {uri})"));
			_router.MapRoute("/settings", (uri, p) =>
				Log($"[settings] (full: {uri})"));
		}

		private void Start()
		{
			BuildUi();
			// Cold-start link replay path — if the OS handed the app a launch URL,
			// the DeepLinkService will replay it to the first subscriber. The router IS the
			// first subscriber (constructed in Awake), so the replay automatically dispatches.
			if (_deepLink.PendingColdStartLink != null)
			{
				Log($"Pending cold-start link queued: {_deepLink.PendingColdStartLink}");
			}
		}

		private void OnDestroy()
		{
			_router?.Dispose();
			_deepLink?.Dispose();
		}

		private void Update()
		{
			if (_coldStartLabel == null) return;
			_coldStartLabel.text = _deepLink.PendingColdStartLink != null
				? $"Cold-start: {_deepLink.PendingColdStartLink}"
				: "Cold-start: (none — link was already consumed or absent)";
		}

		private void BuildUi()
		{
			var canvasGo = new GameObject("Canvas");
			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			canvasGo.AddComponent<GraphicRaycaster>();

			var layoutGo = new GameObject("Layout", typeof(RectTransform), typeof(VerticalLayoutGroup));
			layoutGo.transform.SetParent(canvas.transform, false);
			var rt = (RectTransform)layoutGo.transform;
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = new Vector2(16, 16);
			rt.offsetMax = new Vector2(-16, -16);
			var v = layoutGo.GetComponent<VerticalLayoutGroup>();
			v.spacing = 8;
			v.childForceExpandHeight = false;
			v.childForceExpandWidth = true;

			AddHeader(layoutGo.transform, "Deep Link Router");
			AddLabel(layoutGo.transform,
				"Registered routes: /promo/:id  /profile/:userId  /settings");
			_coldStartLabel = AddLabel(layoutGo.transform, "Cold-start: …");

			AddSectionHeader(layoutGo.transform, "Try without launching from the OS");
			AddButton(layoutGo.transform, "Dispatch myapp://promo/spring2026", () =>
				_router.TryDispatch(new Uri("myapp://promo/spring2026")));
			AddButton(layoutGo.transform, "Dispatch myapp://profile/abc123", () =>
				_router.TryDispatch(new Uri("myapp://profile/abc123")));
			AddButton(layoutGo.transform, "Dispatch myapp://settings", () =>
				_router.TryDispatch(new Uri("myapp://settings")));
			AddButton(layoutGo.transform, "Dispatch unmatched URI", () =>
			{
				var ok = _router.TryDispatch(new Uri("myapp://unknown/path"));
				Log($"Unmatched dispatch returned: {ok}");
			});

			_log = AddLabel(layoutGo.transform, "Log:");
		}

		private void Log(string message)
		{
			Debug.Log($"[DeepLinkRouter] {message}");
			if (_log == null) return;
			var lines = _log.text?.Split('\n') ?? Array.Empty<string>();
			var keep = Math.Max(0, lines.Length - 8);
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("Log:");
			for (var i = keep; i < lines.Length; i++) sb.AppendLine(lines[i]);
			sb.AppendLine(message);
			_log.text = sb.ToString();
		}

		// ---- UI helpers ----
		private static void AddHeader(Transform parent, string text)
		{
			var t = AddLabel(parent, text);
			t.fontSize = 22;
			t.fontStyle = FontStyle.Bold;
		}

		private static void AddSectionHeader(Transform parent, string text)
		{
			var t = AddLabel(parent, text);
			t.fontSize = 16;
			t.fontStyle = FontStyle.Bold;
			t.color = new Color(0.8f, 0.9f, 1f);
		}

		private static Text AddLabel(Transform parent, string text)
		{
			var go = new GameObject("Label", typeof(Text));
			go.transform.SetParent(parent, false);
			var t = go.GetComponent<Text>();
			t.text = text;
			t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			t.fontSize = 13;
			t.color = Color.white;
			t.alignment = TextAnchor.UpperLeft;
			go.AddComponent<LayoutElement>().minHeight = 18;
			return t;
		}

		private static Button AddButton(Transform parent, string label, Action onClick)
		{
			var go = new GameObject(label, typeof(Image), typeof(Button));
			go.transform.SetParent(parent, false);
			go.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.6f, 0.85f);
			var btn = go.GetComponent<Button>();
			btn.onClick.AddListener(() => onClick?.Invoke());
			go.AddComponent<LayoutElement>().minHeight = 36;

			var textGo = new GameObject("Text", typeof(Text));
			textGo.transform.SetParent(go.transform, false);
			var rt = (RectTransform)textGo.transform;
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			var t = textGo.GetComponent<Text>();
			t.text = label;
			t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			t.fontSize = 13;
			t.color = Color.white;
			t.alignment = TextAnchor.MiddleCenter;
			return btn;
		}
	}
}

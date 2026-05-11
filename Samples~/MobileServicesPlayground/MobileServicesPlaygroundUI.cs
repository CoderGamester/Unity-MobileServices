using System;
using System.Threading.Tasks;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Gestures;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.NativeUi;
using GameLovers.MobileServices.Notifications;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.MobileServicesPlayground
{
	/// <summary>
	/// Kitchen-sink playground wiring every Mobile Services subsystem into a single canvas.
	/// See the per-sample <c>README.md</c>.
	/// </summary>
	public sealed class MobileServicesPlaygroundUI : MonoBehaviour
	{
		[Header("Notification channel id (must be registered before scheduling)")]
		[SerializeField] private string _channelId = "default";

		private IDeviceService _device;
		private IHapticsService _haptics;
		private INotificationService _notifications;
		private GestureController _gestures;

		private Text _log;
		private Text _deviceLabel;
		private Text _safeAreaLabel;
		private RectTransform _safeAreaPanel;

		private void Awake()
		{
			_device = new DeviceService();
			_haptics = new HapticsService();
			_notifications = new MobileNotificationService(
				new GameNotificationChannel(_channelId, "Default", "Playground demo channel"));
		}

		private void Start()
		{
			BuildUi();
			WireDeviceEvents();
			WireDeepLink();
		}

		private void OnDestroy()
		{
			(_device as IDisposable)?.Dispose();
			if (_gestures != null)
			{
				_gestures.Swiped -= OnSwiped;
				_gestures.Tapped -= OnTapped;
			}
		}

		private void Update()
		{
			// Sample applies safe-area padding to the colored panel every frame for snappy
			// orientation/notch response. Production code usually subscribes to
			// IDeviceService.SafeArea.OnSafeAreaChanged instead.
			if (_safeAreaPanel != null)
			{
				var area = _device.SafeArea.SafeArea;
				var w = Screen.width;
				var h = Screen.height;
				_safeAreaPanel.anchorMin = new Vector2(area.xMin / w, area.yMin / h);
				_safeAreaPanel.anchorMax = new Vector2(area.xMax / w, area.yMax / h);
			}

			if (_deviceLabel != null)
			{
				_deviceLabel.text =
					$"Battery {_device.Battery.Level:P0} ({_device.Battery.Status}) | LPM {_device.Battery.IsLowPowerMode}\n" +
					$"Connectivity {_device.Connectivity.Status}\n" +
					$"KeepAwake {_device.ScreenWake.KeepAwake}\n" +
					$"ATT {_device.Att.CurrentStatus}";
			}
			if (_safeAreaLabel != null)
			{
				_safeAreaLabel.text = $"Safe area: {_device.SafeArea.SafeArea}";
			}
		}

		private void BuildUi()
		{
			var canvasGo = new GameObject("PlaygroundCanvas");
			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			canvasGo.AddComponent<GraphicRaycaster>();

			_safeAreaPanel = NewPanel("SafeAreaPanel", canvas.transform, new Color(0.1f, 0.18f, 0.25f, 0.9f));
			_safeAreaPanel.anchorMin = Vector2.zero;
			_safeAreaPanel.anchorMax = Vector2.one;
			_safeAreaPanel.offsetMin = Vector2.zero;
			_safeAreaPanel.offsetMax = Vector2.zero;

			var layoutGroup = NewVerticalLayout(_safeAreaPanel, padding: 8);

			AddHeader(layoutGroup.transform, "Mobile Services Playground");

			_safeAreaLabel = AddLabel(layoutGroup.transform, "Safe area: …");
			_deviceLabel = AddLabel(layoutGroup.transform, "Device state…");

			AddSectionHeader(layoutGroup.transform, "Native UI");
			AddButton(layoutGroup.transform, "Show Alert", () => NativeUiService.ShowAlertPopUp(
				false, "Delete Save?", "This cannot be undone.",
				new AlertButton { Text = "Cancel", Style = AlertButtonStyle.Cancel, Callback = () => Log("Alert: Cancel") },
				new AlertButton { Text = "Delete", Style = AlertButtonStyle.Destructive, Callback = () => Log("Alert: Delete") }));
			AddButton(layoutGroup.transform, "Show Toast", () => NativeUiService.ShowToastMessage("Item Collected!", false));
			AddButton(layoutGroup.transform, "Request Review", NativeUiService.RequestReview);
			AddButton(layoutGroup.transform, "Share", () => NativeUiService.Share("Check out my high score!", "https://example.com"));

			AddSectionHeader(layoutGroup.transform, "Haptics");
			foreach (HapticPreset preset in Enum.GetValues(typeof(HapticPreset)))
			{
				if (preset == HapticPreset.None) continue;
				var captured = preset;
				AddButton(layoutGroup.transform, preset.ToString(), () => { _haptics.PlayPreset(captured); Log($"Haptic: {captured}"); });
			}

			AddSectionHeader(layoutGroup.transform, "Notifications");
			AddButton(layoutGroup.transform, "Schedule in 5s", () =>
			{
				var n = _notifications.CreateNotification();
				n.Title = "Playground reward";
				n.Body = "Your scheduled notification!";
				n.Channel = _channelId;
				n.DeliveryTime = DateTime.Now.AddSeconds(5);
				_notifications.ScheduleNotification(n);
				Log("Scheduled in 5s");
			});
			AddButton(layoutGroup.transform, "Cancel all", () => { _notifications.CancelAllScheduledNotifications(); Log("Cancelled all"); });

			AddSectionHeader(layoutGroup.transform, "Permissions");
			foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
			{
				var captured = p;
				AddButton(layoutGroup.transform, $"Request {p}", async () =>
				{
					var result = await _device.Permissions.RequestAsync(captured);
					Log($"{captured}: {result}");
				});
			}

			AddSectionHeader(layoutGroup.transform, "ATT");
			AddButton(layoutGroup.transform, "Request ATT", async () =>
			{
				var result = await _device.Att.RequestAuthorizationAsync();
				Log($"ATT: {result}");
			});

			AddSectionHeader(layoutGroup.transform, "Other");
			AddButton(layoutGroup.transform, "Toggle KeepAwake", () => _device.ScreenWake.KeepAwake = !_device.ScreenWake.KeepAwake);
			AddButton(layoutGroup.transform, "Configure Audio (iOS)", _device.AudioSession.ConfigureForPlayback);

			_log = AddLabel(layoutGroup.transform, "Log:");
		}

		private void WireDeviceEvents()
		{
			_device.Battery.OnLevelChanged       += () => Log($"Battery {_device.Battery.Level:P0}");
			_device.Battery.OnStatusChanged      += () => Log($"Battery status {_device.Battery.Status}");
			_device.Battery.OnLowPowerModeChanged += () => Log($"LPM {_device.Battery.IsLowPowerMode}");
			_device.Connectivity.OnStatusChanged += s  => Log($"Connectivity {s}");
		}

		private void WireDeepLink()
		{
			_device.DeepLink.OnLinkActivated += uri => Log($"Deep link: {uri}");
		}

		private void Log(string message)
		{
			Debug.Log($"[Playground] {message}");
			if (_log == null) return;
			var lines = _log.text?.Split('\n') ?? Array.Empty<string>();
			var keep = Math.Max(0, lines.Length - 8);
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("Log:");
			for (var i = keep; i < lines.Length; i++)
			{
				sb.AppendLine(lines[i]);
			}
			sb.AppendLine(message);
			_log.text = sb.ToString();
		}

		private void OnSwiped(SwipeInput swipe) => Log($"Swiped {swipe.SwipeDirection} (vel {swipe.SwipeVelocity:F1})");
		private void OnTapped(TapInput tap)     => Log($"Tapped at {tap.PressPosition}");

		// ---- UI helpers ----

		private static RectTransform NewPanel(string name, Transform parent, Color color)
		{
			var go = new GameObject(name, typeof(Image));
			go.transform.SetParent(parent, false);
			go.GetComponent<Image>().color = color;
			return (RectTransform)go.transform;
		}

		private static VerticalLayoutGroup NewVerticalLayout(RectTransform parent, int padding)
		{
			var go = new GameObject("Layout");
			go.transform.SetParent(parent, false);
			var rt = go.AddComponent<RectTransform>();
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = new Vector2(padding, padding);
			rt.offsetMax = new Vector2(-padding, -padding);

			var sr = go.AddComponent<ScrollRect>();
			var contentGo = new GameObject("Content");
			contentGo.transform.SetParent(go.transform, false);
			var contentRt = contentGo.AddComponent<RectTransform>();
			contentRt.anchorMin = new Vector2(0, 1);
			contentRt.anchorMax = new Vector2(1, 1);
			contentRt.pivot = new Vector2(0.5f, 1f);
			contentRt.sizeDelta = new Vector2(0, 1200);

			var layout = contentGo.AddComponent<VerticalLayoutGroup>();
			layout.spacing = 4;
			layout.padding = new RectOffset(8, 8, 8, 8);
			layout.childForceExpandHeight = false;
			layout.childForceExpandWidth = true;
			contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			sr.content = contentRt;
			sr.horizontal = false;
			sr.vertical = true;
			return layout;
		}

		private static void AddHeader(Transform parent, string text)
		{
			var t = AddLabel(parent, text);
			t.fontSize = 24;
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
			var le = go.AddComponent<LayoutElement>();
			le.minHeight = 18;
			return t;
		}

		private static Button AddButton(Transform parent, string label, Action onClick)
		{
			var go = new GameObject(label, typeof(Image), typeof(Button));
			go.transform.SetParent(parent, false);
			go.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.6f, 0.85f);

			var btn = go.GetComponent<Button>();
			btn.onClick.AddListener(() => onClick?.Invoke());

			var le = go.AddComponent<LayoutElement>();
			le.minHeight = 36;

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

		private static Button AddButton(Transform parent, string label, Func<Task> onClick)
		{
			return AddButton(parent, label, () => _ = onClick());
		}
	}
}

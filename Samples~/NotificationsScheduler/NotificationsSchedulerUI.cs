using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Notifications;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.NotificationsScheduler
{
	/// <summary>
	/// Notifications lifecycle sample — channel CRUD, <see cref="OperatingMode"/> toggles, and the
	/// background-foreground round trip. See the per-sample <c>README.md</c>.
	/// </summary>
	public sealed class NotificationsSchedulerUI : MonoBehaviour
	{
		private INotificationService _service;
		private readonly List<GameNotificationChannel> _channels = new List<GameNotificationChannel>();
		private Text _log;
		private Text _channelsLabel;
		private Text _modeLabel;
		private OperatingMode _currentMode = OperatingMode.NoQueue;

		private void Awake()
		{
			_channels.Add(new GameNotificationChannel("default", "Default", "Default channel"));
			_service = new MobileNotificationService(_channels.ToArray());
		}

		private void Start()
		{
			BuildUi();
			_service.OnLocalNotificationDeliveredEvent += pending =>
				Log($"Delivered while foreground: {pending.Notification.Title}");
			_service.OnLocalNotificationExpiredEvent += pending =>
				Log($"Expired (queued + foregrounded): {pending.Notification.Title}");
		}

		private void OnDestroy()
		{
			// MobileNotificationService doesn't expose a Dispose; the host GameObject is
			// DontDestroyOnLoad. In a "reset game" flow you'd destroy the GameObject by name —
			// see the package gotchas in AGENTS.md §4. We leave the demo host in place.
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

			AddHeader(layoutGo.transform, "Notifications Scheduler");
			_modeLabel = AddLabel(layoutGo.transform, $"Mode: {_currentMode}");
			_channelsLabel = AddLabel(layoutGo.transform, BuildChannelsLabel());

			AddSectionHeader(layoutGo.transform, "Schedule");
			AddButton(layoutGo.transform, "Schedule in 5s",  () => ScheduleIn(TimeSpan.FromSeconds(5), reschedule: false));
			AddButton(layoutGo.transform, "Schedule in 30s", () => ScheduleIn(TimeSpan.FromSeconds(30), reschedule: false));
			AddButton(layoutGo.transform, "Schedule with Reschedule=true (30s)", () => ScheduleIn(TimeSpan.FromSeconds(30), reschedule: true));
			AddButton(layoutGo.transform, "Cancel all", () => { _service.CancelAllScheduledNotifications(); Log("Cancelled all"); });

			AddSectionHeader(layoutGo.transform, "Channels");
			AddButton(layoutGo.transform, "Add 'rewards' channel", () =>
			{
				if (TryAddChannel("rewards", "Rewards", "Daily reward reminders"))
				{
					Log("Added channel 'rewards'");
				}
			});

			AddSectionHeader(layoutGo.transform, "Operating mode");
			foreach (OperatingMode m in Enum.GetValues(typeof(OperatingMode)))
			{
				var captured = m;
				AddButton(layoutGo.transform, m.ToString(), () =>
				{
					_currentMode = captured;
					_modeLabel.text = $"Mode: {_currentMode}";
					Log($"Switched mode → {_currentMode}");
					// NOTE: MobileNotificationService routes its mode via the host MonoBehaviour;
					// you can extend this sample to call into the host directly if you need to
					// flip modes at runtime. The default is NoQueue.
				});
			}

			_log = AddLabel(layoutGo.transform, "Log:");
		}

		private string BuildChannelsLabel()
		{
			var sb = new System.Text.StringBuilder("Channels: ");
			foreach (var c in _channels) sb.Append(c.Id).Append(' ');
			return sb.ToString();
		}

		private bool TryAddChannel(string id, string name, string description)
		{
			foreach (var c in _channels) if (c.Id == id) return false;
			_channels.Add(new GameNotificationChannel(id, name, description));
			_channelsLabel.text = BuildChannelsLabel();
			return true;
		}

		private void ScheduleIn(TimeSpan span, bool reschedule)
		{
			var n = _service.CreateNotification();
			n.Title = $"Test +{span.TotalSeconds:F0}s";
			n.Body = $"Reschedule={reschedule}";
			n.Channel = _channels[0].Id;
			n.DeliveryTime = DateTime.Now + span;
			var pending = _service.ScheduleNotification(n);
			if (reschedule) pending.Reschedule = true;
			Log($"Scheduled {n.Title}");
		}

		private void Log(string message)
		{
			Debug.Log($"[Notifications Scheduler] {message}");
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
	}
}

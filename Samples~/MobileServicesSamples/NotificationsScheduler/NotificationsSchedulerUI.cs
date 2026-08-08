using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Notifications;
using GameLovers.MobileServices.Samples;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.NotificationsScheduler
{
	/// <summary>Schedule, inspect and cancel notifications without runtime channel CRUD.</summary>
	public sealed class NotificationsSchedulerUI : MonoBehaviour
	{
		private readonly List<GameNotificationChannel> _channels = new List<GameNotificationChannel>
		{
			new GameNotificationChannel("default", "Default", "General sample notifications"),
			new GameNotificationChannel("rewards", "Rewards", "Reward reminders")
		};

		private UIDocument _document;
		private INotificationService _service;
		private IDeviceService _device;
		private IHapticsService _haptics;
		private VisualElement _boundRoot;
		private VisualElement _pendingList;
		private Label _status;
		private Label _log;
		private DropdownField _channel;
		private DropdownField _mode;
		private readonly List<string> _logEntries = new List<string>();
		private string _selectedChannel = "default";

#if UNITY_EDITOR
		/// <summary>Exposes the sample-owned service to its editor simulation adapter.</summary>
		internal INotificationService EditorNotificationService => _service;
#endif

		private void Awake()
		{
			_document = GetComponent<UIDocument>();
			EnsureRuntimeDependencies();
		}

		private void Start()
		{
			EnsureRuntimeDependencies();
			_service.OnLocalNotificationDeliveredEvent += OnNotificationDelivered;
			_service.OnLocalNotificationExpiredEvent += OnNotificationExpired;
			EnsureUiBound();
		}

		private void Update()
		{
			EnsureUiBound();
		}

		private void OnDestroy()
		{
			_boundRoot?.UnregisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
			var service = _service;
			_service = null;
			if (service != null)
			{
				service.OnLocalNotificationDeliveredEvent -= OnNotificationDelivered;
				service.OnLocalNotificationExpiredEvent -= OnNotificationExpired;
			}
			(service as IDisposable)?.Dispose();
			(_device as IDisposable)?.Dispose();
			(_haptics as IDisposable)?.Dispose();
		}

		private void EnsureUiBound()
		{
			EnsureRuntimeDependencies();
			if (_document == null) _document = GetComponent<UIDocument>();
			var root = _document == null ? null : _document.rootVisualElement;
			if (root == null) return;
			var status = root.Q<Label>("permission-status");
			if (ReferenceEquals(_status, status)) return;

			_boundRoot?.UnregisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
			_boundRoot = root;
			var safeArea = root as SafeAreaContainer ?? root.Q<SafeAreaContainer>();
			safeArea?.SetSafeAreaService(_device.SafeArea);
			BindClickHaptics(root);
			_status = status;
			_log = root.Q<Label>("log");
			_pendingList = root.Q<VisualElement>("pending-list");
			_channel = root.Q<DropdownField>("channel");
			_mode = root.Q<DropdownField>("mode");
			if (_channel != null)
			{
				_channel.choices = new List<string> { "default", "rewards" };
				_channel.SetValueWithoutNotify(_selectedChannel);
				_channel.RegisterValueChangedCallback(evt => _selectedChannel = evt.newValue);
			}
			if (_mode != null)
			{
				_mode.choices = new List<string>(Enum.GetNames(typeof(OperatingMode)));
				_mode.SetValueWithoutNotify(_service.Mode.ToString());
				_mode.RegisterValueChangedCallback(evt =>
				{
					_service.Mode = (OperatingMode)Enum.Parse(typeof(OperatingMode), evt.newValue);
					Log($"Mode: {_service.Mode}");
				});
			}
			root.Q<Button>("permission-check")?.RegisterCallback<ClickEvent>(_ => RefreshPermissionStatus());
			root.Q<Button>("permission-request")?.RegisterCallback<ClickEvent>(async _ =>
			{
				var result = await _device.Permissions.RequestAsync(AppPermission.Notifications);
				Log($"Notification permission: {result}");
				RefreshPermissionStatus();
			});
			root.Q<Button>("schedule-5")?.RegisterCallback<ClickEvent>(_ => Schedule(TimeSpan.FromSeconds(5), false));
			root.Q<Button>("schedule-30")?.RegisterCallback<ClickEvent>(_ => Schedule(TimeSpan.FromSeconds(30), false));
			root.Q<Button>("schedule-reschedule")?.RegisterCallback<ClickEvent>(_ => Schedule(TimeSpan.FromSeconds(30), true));
			root.Q<Button>("cancel-all")?.RegisterCallback<ClickEvent>(_ =>
			{
				_service.CancelAllScheduledNotifications();
				RefreshPending();
				Log("Cancelled all scheduled notifications.");
			});
			root.Q<Button>("dismiss-all")?.RegisterCallback<ClickEvent>(_ =>
			{
				_service.DismissAllDisplayedNotifications();
				Log("Dismissed all displayed notifications.");
			});
			RefreshPermissionStatus();
			RefreshPending();
			RefreshLog();
		}

		private void EnsureRuntimeDependencies()
		{
			if (_service == null) _service = new MobileNotificationService(_channels.ToArray());
			if (_device == null) _device = new DeviceService();
			if (_haptics == null) _haptics = new HapticsService();
		}

		private void Schedule(TimeSpan delay, bool reschedule)
		{
			var notification = _service.CreateNotification();
			notification.Title = $"Sample notification (+{delay.TotalSeconds:F0}s)";
			notification.Body = reschedule ? "This notification is marked for rescheduling." : "This notification is a one-shot.";
			notification.Channel = _channel?.value ?? _selectedChannel;
			notification.DeliveryTime = DateTime.Now.Add(delay);
			var pending = _service.ScheduleNotification(notification);
			pending.Reschedule = reschedule;
			RefreshPending();
			Log($"Scheduled {notification.Title} on channel {notification.Channel}.");
		}

		private void RefreshPermissionStatus()
		{
			if (_status != null)
			{
				_status.text = SampleStatusFormatter.Format(
					new SampleStatusEntry("Notification permission", _device.Permissions.Check(AppPermission.Notifications)));
			}
		}

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

		private void RefreshPending()
		{
			if (_pendingList == null) return;
			_pendingList.Clear();
			if (_service.PendingNotifications.Count == 0)
			{
				_pendingList.Add(new Label("No pending notifications."));
				return;
			}
			foreach (var pending in _service.PendingNotifications)
			{
				var captured = pending;
				var row = new VisualElement();
				row.AddToClassList("pending-row");
				row.Add(new Label(SampleStatusFormatter.Format(
					new SampleStatusEntry("Title", pending.Notification.Title),
					new SampleStatusEntry("Delivery", pending.Notification.DeliveryTime?.ToString("g")),
					new SampleStatusEntry("Channel", pending.Notification.Channel),
					new SampleStatusEntry("Reschedule", SampleStatusFormatter.YesNo(pending.Reschedule)))));
				var cancel = new Button(() =>
				{
					if (captured.Notification.Id.HasValue) _service.CancelNotification(captured.Notification.Id.Value);
					RefreshPending();
					Log($"Cancelled {captured.Notification.Title}.");
				}) { text = "Cancel" };
				cancel.AddToClassList("sample-button");
				row.Add(cancel);
				_pendingList.Add(row);
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

		private void OnNotificationDelivered(PendingNotification notification) =>
			Log($"Delivered: {notification.Notification.Title}");

		private void OnNotificationExpired(PendingNotification notification) =>
			Log($"Expired: {notification.Notification.Title}");

	}
}

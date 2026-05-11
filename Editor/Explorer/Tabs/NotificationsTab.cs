using System;
using System.Text;
using GameLovers.MobileServices.Editor.Explorer.Overlays;
using GameLovers.MobileServices.Notifications;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Tabs
{
	/// <summary>Notifications tab — schedule test, list pending, surface banner mocks on the simulator.</summary>
	public sealed class NotificationsTab : MobileServiceTab
	{
		public override string DisplayName => "Notifications";
		protected override int RefreshIntervalMs => 500;

		private MobileNotificationService _service;
		private Label _statusLabel;
		private VisualElement _pendingList;
		private Label _channelsLabel;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			_statusLabel = new Label();
			scroll.Add(_statusLabel);

			scroll.Add(MakeSectionLabel("Channels"));
			_channelsLabel = new Label("(none — service not initialised yet)");
			_channelsLabel.AddToClassList("tab-empty-label");
			scroll.Add(_channelsLabel);

			scroll.Add(MakeSectionLabel("Schedule test"));
			var scheduleRow = new VisualElement();
			scheduleRow.style.flexDirection = FlexDirection.Row;
			scheduleRow.Add(MakePrimaryButton("In 1s", () => ScheduleTest(1)));
			scheduleRow.Add(MakePrimaryButton("In 5s", () => ScheduleTest(5)));
			scheduleRow.Add(MakePrimaryButton("In 30s", () => ScheduleTest(30)));
			scroll.Add(scheduleRow);

			scroll.Add(MakeSectionLabel("Pending"));
			_pendingList = new VisualElement();
			scroll.Add(_pendingList);

			var bar = MakeActionBar();
			bar.Add(MakePrimaryButton("Initialise (default channel)", InitialiseService));
			bar.Add(MakePrimaryDangerButton("Cancel All", () =>
			{
				if (_service == null) return;
				_service.CancelAllScheduledNotifications();
				Refresh();
			}));
			scroll.Add(bar);

			Add(scroll);
			Refresh();
		}

		protected override void Refresh()
		{
			if (_service == null)
			{
				_statusLabel.text = "Notifications service: (none — Initialise to spawn a host MonoBehaviour)";
				_pendingList.Clear();
				_pendingList.Add(MakeEmptyLabel("Service not initialised."));
				return;
			}

			var channels = _service.Channels;
			if (channels == null || channels.Count == 0)
			{
				_channelsLabel.text = "(no channels)";
			}
			else
			{
				var sb = new StringBuilder();
				for (var i = 0; i < channels.Count; i++)
				{
					if (i > 0) sb.Append(", ");
					sb.Append(channels[i].Id);
					sb.Append(" / ");
					sb.Append(channels[i].Name);
				}
				_channelsLabel.text = sb.ToString();
			}

			_statusLabel.text = $"Notifications service: mode={_service.CurrentMode}, pending={_service.PendingNotifications.Count}";
			_pendingList.Clear();
			if (_service.PendingNotifications.Count == 0)
			{
				_pendingList.Add(MakeEmptyLabel("No pending notifications."));
				return;
			}

			foreach (var pending in _service.PendingNotifications)
			{
				var row = MakeRow($"{pending.Notification.Title ?? "(no title)"}", pending.Notification.DeliveryTime?.ToString("u") ?? "(no time)");
				_pendingList.Add(row);
			}
		}

		protected override void OnExitingPlayMode()
		{
			_service = null;
		}

		private void InitialiseService()
		{
			if (!Application.isPlaying)
			{
				Debug.Log("[MobileServicesExplorer] Notifications service creates a DontDestroyOnLoad GameObject — requires Play mode.");
				return;
			}
			if (_service != null) return;

			_service = new MobileNotificationService(new GameNotificationChannel("default", "Default", "Default notifications"));
			Refresh();
		}

		private void ScheduleTest(int seconds)
		{
			if (!Application.isPlaying)
			{
				Debug.Log("[MobileServicesExplorer] Notifications scheduling requires Play mode.");
				return;
			}
			if (_service == null)
			{
				InitialiseService();
			}
			if (_service == null) return;

			var notification = _service.CreateNotification();
			notification.Title = $"Test in {seconds}s";
			notification.Body = $"Mock heads-up at {DateTime.Now.AddSeconds(seconds):HH:mm:ss}";
			notification.Channel = "default";
			notification.DeliveryTime = DateTime.Now.AddSeconds(seconds);
			_service.ScheduleNotification(notification);

			// Heads-up banner on the simulator at the simulated delivery moment.
			var deliverAtMs = seconds * 1000;
			schedule.Execute(() =>
			{
				MobileSimulatorState.PushNotificationBanner(new SimulatedNotificationBannerSpec
				{
					ChannelName = notification.Channel ?? "default",
					Title = notification.Title,
					Body = notification.Body,
				});
			}).StartingIn(deliverAtMs);
		}
	}
}

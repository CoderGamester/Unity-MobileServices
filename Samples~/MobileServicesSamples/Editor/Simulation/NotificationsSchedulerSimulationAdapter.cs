using System;
using GameLovers.MobileServices.Editor.Explorer.Overlays;
using GameLovers.MobileServices.Editor.Simulation;
using GameLovers.MobileServices.Notifications;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.NotificationsScheduler.Editor
{
	/// <summary>Registers the active Notifications Scheduler sample with the editor simulator.</summary>
	[InitializeOnLoad]
	internal static class NotificationsSchedulerSimulationRegistration
	{
		private static NotificationsSchedulerSimulationTarget _target;
		private static double _nextPoll;

		static NotificationsSchedulerSimulationRegistration()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			EditorApplication.update += Update;
		}

		private static void Update()
		{
			if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < _nextPoll)
			{
				return;
			}

			_nextPoll = EditorApplication.timeSinceStartup + 0.25d;
			var sample = UnityEngine.Object.FindAnyObjectByType<NotificationsSchedulerUI>();
			if (sample == null)
			{
				UnregisterTarget();
				return;
			}

			if (_target == null || !ReferenceEquals(_target.Sample, sample))
			{
				UnregisterTarget();
				_target = new NotificationsSchedulerSimulationTarget(sample);
				MobileNotificationSimulation.Register(_target);
			}
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
			{
				UnregisterTarget();
			}
		}

		private static void UnregisterTarget()
		{
			if (_target == null)
			{
				return;
			}

			MobileNotificationSimulation.Unregister(_target);
			_target = null;
		}
	}

	/// <summary>Adapts the sample-owned service to the package editor simulation contract.</summary>
	internal sealed class NotificationsSchedulerSimulationTarget : IMobileNotificationSimulationTarget
	{
		public NotificationsSchedulerUI Sample { get; }

		public string DisplayName => "Notifications Scheduler";

		public int PendingCount => Sample != null && Sample.EditorNotificationService != null
			? Sample.EditorNotificationService.PendingNotifications.Count
			: 0;

		public NotificationsSchedulerSimulationTarget(NotificationsSchedulerUI sample)
		{
			Sample = sample ?? throw new ArgumentNullException(nameof(sample));
		}

		public bool TryDeliverNext(out SimulatedNotificationBannerSpec spec) => TryDeliver(dueOnly: false, out spec);

		public bool TryDeliverDue(out SimulatedNotificationBannerSpec spec) => TryDeliver(dueOnly: true, out spec);

		private bool TryDeliver(bool dueOnly, out SimulatedNotificationBannerSpec spec)
		{
			spec = null;
			var service = Sample == null ? null : Sample.EditorNotificationService;
			if (service == null)
			{
				return false;
			}

			PendingNotification candidate = null;
			foreach (var pending in service.PendingNotifications)
			{
				var notification = pending.Notification;
				if (!notification.Id.HasValue)
				{
					continue;
				}

				if (dueOnly && (!notification.DeliveryTime.HasValue || notification.DeliveryTime.Value > DateTime.Now))
				{
					continue;
				}

				if (candidate == null || CompareDeliveryTime(notification, candidate.Notification) < 0)
				{
					candidate = pending;
				}
			}

			if (candidate == null)
			{
				return false;
			}

			spec = CreateBannerSpec(candidate.Notification);
			return MobileNotificationSimulation.TryDeliver(service, candidate.Notification.Id.Value);
		}

		private static int CompareDeliveryTime(IGameNotification left, IGameNotification right)
		{
			var leftTime = left.DeliveryTime ?? DateTime.MaxValue;
			var rightTime = right.DeliveryTime ?? DateTime.MaxValue;
			return leftTime.CompareTo(rightTime);
		}

		private static SimulatedNotificationBannerSpec CreateBannerSpec(IGameNotification notification)
		{
			return new SimulatedNotificationBannerSpec
			{
				ChannelName = string.IsNullOrWhiteSpace(notification.Channel) ? "Notifications" : notification.Channel,
				Title = string.IsNullOrWhiteSpace(notification.Title) ? "Notification" : notification.Title,
				Body = notification.Body ?? string.Empty,
				SubTitle = notification.Subtitle,
			};
		}
	}
}

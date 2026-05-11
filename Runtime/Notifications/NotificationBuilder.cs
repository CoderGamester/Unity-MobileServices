using System;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Notifications
{
	/// <summary>
	/// Fluent builder over <see cref="INotificationService.CreateNotification"/> +
	/// <see cref="INotificationService.ScheduleNotification"/>. Removes the 6-line property-assignment
	/// boilerplate from the common case:
	/// <code>
	/// service.Schedule()
	///     .In(TimeSpan.FromHours(24))
	///     .Title("Daily Reward")
	///     .Body("Your reward awaits!")
	///     .Channel("rewards")
	///     .BadgeIncrement()
	///     .Send();
	/// </code>
	/// </summary>
	public sealed class NotificationBuilder
	{
		private readonly INotificationService _service;
		private readonly IGameNotification _notification;
		private bool _badgeIncrement;

		internal NotificationBuilder(INotificationService service)
		{
			_service = service ?? throw new ArgumentNullException(nameof(service));
			_notification = service.CreateNotification();
		}

		public NotificationBuilder Title(string title) { _notification.Title = title; return this; }
		public NotificationBuilder Body(string body) { _notification.Body = body; return this; }
		public NotificationBuilder Subtitle(string subtitle) { _notification.Subtitle = subtitle; return this; }
		public NotificationBuilder Channel(string channelId) { _notification.Channel = channelId; return this; }
		public NotificationBuilder Id(int id) { _notification.Id = id; return this; }
		public NotificationBuilder BadgeNumber(int? badge) { _notification.BadgeNumber = badge; return this; }
		public NotificationBuilder SmallIcon(string smallIcon) { _notification.SmallIcon = smallIcon; return this; }
		public NotificationBuilder LargeIcon(string largeIcon) { _notification.LargeIcon = largeIcon; return this; }
		public NotificationBuilder AutoCancel(bool shouldAutoCancel = true) { _notification.ShouldAutoCancel = shouldAutoCancel; return this; }
		public NotificationBuilder At(DateTime deliveryTime) { _notification.DeliveryTime = deliveryTime; return this; }
		public NotificationBuilder In(TimeSpan delay) { _notification.DeliveryTime = DateTime.Now + delay; return this; }

		/// <summary>
		/// Marks the notification for badge-increment behaviour. The underlying
		/// <see cref="GameNotificationsMonoBehaviour"/> auto-increments badge numbers when none of the
		/// pending notifications have one set; calling this leaves <see cref="IGameNotification.BadgeNumber"/>
		/// unset so the auto-increment path engages.
		/// </summary>
		public NotificationBuilder BadgeIncrement()
		{
			_badgeIncrement = true;
			_notification.BadgeNumber = null;
			return this;
		}

		/// <summary>
		/// Schedules the built notification and returns the resulting <see cref="PendingNotification"/>.
		/// </summary>
		public PendingNotification Send()
		{
			// _badgeIncrement is reserved for future behaviour flips; the auto-increment path on the
			// host MonoBehaviour fires when BadgeNumber == null on every queued entry, so simply leaving
			// it null (the default once BadgeIncrement is called) is sufficient today.
			_ = _badgeIncrement;
			return _service.ScheduleNotification(_notification);
		}
	}

	/// <summary>
	/// Extension methods exposing the fluent builder on <see cref="INotificationService"/>.
	/// </summary>
	public static class NotificationServiceExtensions
	{
		/// <summary>
		/// Returns a new <see cref="NotificationBuilder"/> backed by this service. Each call constructs
		/// a fresh notification via <see cref="INotificationService.CreateNotification"/>.
		/// </summary>
		public static NotificationBuilder Schedule(this INotificationService service)
		{
			return new NotificationBuilder(service);
		}
	}
}

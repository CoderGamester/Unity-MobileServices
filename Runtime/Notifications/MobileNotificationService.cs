using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLovers.MobileServices.Notifications
{
	/// <summary>
	/// This service allows to schedule and handle notifications on the current platform
	/// </summary>
	public interface INotificationService
	{
		/// <summary>
		/// Event fired when a scheduled local notification is delivered while the app is in the foreground.
		/// </summary>
		event Action<PendingNotification> OnLocalNotificationDeliveredEvent;

		/// <summary>
		/// Event fired when a queued local notification is cancelled because the application is in the foreground
		/// when it was meant to be displayed.
		/// </summary>
		/// <seealso cref="OperatingMode.Queue"/>
		event Action<PendingNotification> OnLocalNotificationExpiredEvent;

		/// <summary>
		/// The queueing / delivery <see cref="OperatingMode"/> for the service. Defaults to
		/// <see cref="OperatingMode.NoQueue"/>. Settable at runtime; the new value takes effect on the
		/// next <see cref="ScheduleNotification"/> call and the next foreground/background transition.
		/// </summary>
		OperatingMode Mode { get; set; }

		/// <summary>
		/// Gets a collection of notifications that are scheduled or queued.
		/// </summary>
		IReadOnlyList<PendingNotification> PendingNotifications { get; }
		
		/// <summary>
		/// Create a new instance of a <see cref="IGameNotification"/> for this platform.
		/// </summary>
		/// <returns>A new platform-appropriate notification object.</returns>
		IGameNotification CreateNotification();

		/// <summary>
		/// Schedules a notification to be delivered.
		/// </summary>
		/// <param name="gameNotification">The notification to deliver.</param>
		/// <exception cref="ArgumentNullException"><paramref name="gameNotification"/> is null.</exception>
		/// <exception cref="InvalidOperationException"><paramref name="gameNotification"/> isn't of the correct type.</exception>
		PendingNotification ScheduleNotification(IGameNotification gameNotification);

		/// <summary>
		/// Cancels a scheduled notification.
		/// </summary>
		/// <param name="notificationId">The ID of a previously scheduled notification.</param>
		void CancelNotification(int notificationId);

		/// <summary>
		/// Dismiss a displayed notification.
		/// </summary>
		/// <param name="notificationId">The ID of a previously scheduled notification that is being displayed to the user.</param>
		void DismissNotification(int notificationId);

		/// <summary>
		/// Cancels all scheduled notifications.
		/// </summary>
		void CancelAllScheduledNotifications();

		/// <summary>
		/// Dismisses all displayed notifications.
		/// </summary>
		void DismissAllDisplayedNotifications();
	}
	
	/// <inheritdoc />
	public class MobileNotificationService : INotificationService
	{
		private readonly GameNotificationsMonoBehaviour _monoBehaviour;
		private readonly GameNotificationChannel[] _channels;

		/// <inheritdoc />
		public event Action<PendingNotification> OnLocalNotificationDeliveredEvent;
		/// <inheritdoc />
		public event Action<PendingNotification> OnLocalNotificationExpiredEvent;

		/// <inheritdoc />
		public IReadOnlyList<PendingNotification> PendingNotifications => _monoBehaviour.PendingNotifications;

		/// <inheritdoc />
		public OperatingMode Mode
		{
			get => _monoBehaviour.Mode;
			set => _monoBehaviour.Mode = value;
		}

		/// <summary>The queueing / delivery mode the host MonoBehaviour was configured with.</summary>
		/// <remarks>Editor introspection accessor — not part of the public surface.</remarks>
		internal OperatingMode CurrentMode => _monoBehaviour != null ? _monoBehaviour.Mode : OperatingMode.NoQueue;

		/// <summary>The channels passed to the constructor (Android default-channel-id resolution + Explorer display).</summary>
		/// <remarks>Editor introspection accessor — not part of the public surface.</remarks>
		internal IReadOnlyList<GameNotificationChannel> Channels => _channels;

		public MobileNotificationService(params GameNotificationChannel[] channels)
		{
			_channels = channels ?? Array.Empty<GameNotificationChannel>();
			_monoBehaviour = new GameObject("NotificationService").AddComponent<GameNotificationsMonoBehaviour>();
			_monoBehaviour.OnLocalNotificationDelivered = OnLocalNotificationDeliveredEvent;
			_monoBehaviour.OnLocalNotificationExpired = OnLocalNotificationExpiredEvent;

			_monoBehaviour.Initialize(channels);
			UnityEngine.Object.DontDestroyOnLoad(_monoBehaviour);
		}

		/// <inheritdoc />
		public IGameNotification CreateNotification()
		{
#if UNITY_EDITOR
			return new EditorGameNotification();
#else
			return _monoBehaviour.CreateNotification();
#endif
		}

		/// <inheritdoc />
		public PendingNotification ScheduleNotification(IGameNotification gameNotification)
		{
#if UNITY_EDITOR
			if (!gameNotification.Id.HasValue)
            {
                // Generate an ID for items that don't have one (just so they can be identified later)
                gameNotification.Id = Math.Abs(DateTime.Now.ToString("yyMMddHHmmssffffff").GetHashCode());
            }
			return new PendingNotification(gameNotification);
#else
			return _monoBehaviour.ScheduleNotification(gameNotification);
#endif
		}

		/// <inheritdoc />
		public void CancelNotification(int notificationId)
		{
			_monoBehaviour.CancelNotification(notificationId);
		}

		/// <inheritdoc />
		public void DismissNotification(int notificationId)
		{
			_monoBehaviour.DismissNotification(notificationId);
		}

		/// <inheritdoc />
		public void CancelAllScheduledNotifications()
		{
			_monoBehaviour.CancelAllNotifications();
		}

		/// <inheritdoc />
		public void DismissAllDisplayedNotifications()
		{
			_monoBehaviour.DismissAllNotifications();
		}
	}
}
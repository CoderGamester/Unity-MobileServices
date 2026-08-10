using System;

// ReSharper disable once CheckNamespace

namespace GameLovers.MobileServices.Notifications
{
    /// <summary>
    /// Any type that handles notifications for a specific game platform
    /// </summary>
    internal interface IGameNotificationsPlatform
    {
        /// <summary>
        /// Fired when a notification is received.
        /// </summary>
        event Action<IGameNotification> NotificationReceived;

        /// <summary>
        /// Create a new instance of a <see cref="IGameNotification"/> for this platform.
        /// </summary>
        IGameNotification CreateNotification();

        /// <summary>
        /// Schedules a notification to be delivered. Throws if <paramref name="gameNotification"/> is null,
        /// or is not the concrete notification type this platform implementation expects.
        /// </summary>
        void ScheduleNotification(IGameNotification gameNotification);

        /// <summary>
        /// Cancels a scheduled notification.
        /// </summary>
        void CancelNotification(int notificationId);

        /// <summary>
        /// Dismiss a displayed notification.
        /// </summary>
        void DismissNotification(int notificationId);

        /// <summary>
        /// Cancels all scheduled notifications.
        /// </summary>
        void CancelAllScheduledNotifications();

        /// <summary>
        /// Dismisses all displayed notifications.
        /// </summary>
        void DismissAllDisplayedNotifications();

        /// <summary>
        /// Performs any initialization or processing necessary on foregrounding the application.
        /// </summary>
        void OnForeground();

        /// <summary>
        /// Performs any processing necessary on backgrounding or closing the application.
        /// </summary>
        void OnBackground();
    }

    /// <summary>
    /// Any type that handles notifications for a specific game platform.
    /// </summary>
    /// <remarks>Has a concrete notification type</remarks>
    internal interface IGameNotificationsPlatform<TNotificationType> : IGameNotificationsPlatform
        where TNotificationType : IGameNotification
    {
        /// <summary>
        /// Create an instance of <typeparamref name="TNotificationType"/>.
        /// </summary>
        new TNotificationType CreateNotification();

        /// <summary>
        /// Schedule a notification to be delivered. Throws if <paramref name="notification"/> is null.
        /// </summary>
        void ScheduleNotification(TNotificationType notification);
    }
}

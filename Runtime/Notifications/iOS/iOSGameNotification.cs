#if UNITY_IOS
using System;
using Unity.Notifications.iOS;
using UnityEngine;
using UnityEngine.Assertions;

// ReSharper disable once CheckNamespace

namespace GameLovers.MobileServices.Notifications
{
    /// <summary>
    /// iOS implementation of <see cref="IGameNotification"/>.
    /// </summary>
    public class iOSGameNotification : IGameNotification
    {
        private readonly iOSNotification _internalNotification;

        /// <summary>
        /// Gets the internal notification object used by the mobile notifications system.
        /// </summary>
        public iOSNotification InternalNotification => _internalNotification;

        /// <inheritdoc />
        /// <remarks>
        /// Internally stored as a string. Gets parsed to an integer when retrieving.
        /// </remarks>
        /// <value>The identifier as an integer, or null if the identifier couldn't be parsed as a number.</value>
        public int? Id
        {
            get
            {
                if (!int.TryParse(_internalNotification.Identifier, out int value))
                {
                    Debug.LogWarning("Internal iOS notification's identifier isn't a number.");
                    return null;
                }

                return value;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _internalNotification.Identifier = value.Value.ToString();
            }
        }

        /// <inheritdoc />
        public string Title { get => _internalNotification.Title; set => _internalNotification.Title = value; }

        /// <inheritdoc />
        public string Body { get => _internalNotification.Body; set => _internalNotification.Body = value; }

        /// <inheritdoc />
        public string Subtitle { get => _internalNotification.Subtitle; set => _internalNotification.Subtitle = value; }

        /// <inheritdoc />
        /// <remarks>
        /// On iOS, this represents the notification's Category Identifier.
        /// </remarks>
        /// <value>The value of <see cref="CategoryIdentifier"/>.</value>
        public string Channel { get => CategoryIdentifier; set => CategoryIdentifier = value; }

        /// <inheritdoc />
        public int? BadgeNumber
        {
            get => _internalNotification.Badge != -1 ? _internalNotification.Badge : (int?)null;
            set => _internalNotification.Badge = value ?? -1;
        }

        /// <inheritdoc />
        public bool ShouldAutoCancel { get; set; }

        /// <inheritdoc />
        public bool Scheduled { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// <para>On iOS, setting this causes the notification to be delivered on a calendar time.</para>
        /// <para>If it has previously been manually set to a different type of trigger, or has not been set before,
        /// this returns null.</para>
        /// <para>The millisecond component of the provided DateTime is ignored.</para>
        /// </remarks>
        /// <value>A <see cref="DateTime"/> representing the delivery time of this message, or null if
        /// not set or the trigger isn't a <see cref="iOSNotificationCalendarTrigger"/>.</value>
        public DateTime? DeliveryTime
        {
            get
            {
                if (!(_internalNotification.Trigger is iOSNotificationCalendarTrigger calendarTrigger))
                {
                    return null;
                }

                DateTime now = DateTime.Now;
                var result = new DateTime
                    (
                    calendarTrigger.Year ?? now.Year,
                    calendarTrigger.Month ?? now.Month,
                    calendarTrigger.Day ?? now.Day,
                    calendarTrigger.Hour ?? now.Hour,
                    calendarTrigger.Minute ?? now.Minute,
                    calendarTrigger.Second ?? now.Second,
                    DateTimeKind.Local
                    );

                return result;
            }
            set
            {
                if (!value.HasValue)
                {
                    return;
                }

                DateTime date = value.Value.ToLocalTime();

                _internalNotification.Trigger = new iOSNotificationCalendarTrigger
                {
                    Year = date.Year,
                    Month = date.Month,
                    Day = date.Day,
                    Hour = date.Hour,
                    Minute = date.Minute,
                    Second = date.Second
                };
            }
        }

        /// <summary>
        /// The category identifier for this notification.
        /// </summary>
        public string CategoryIdentifier
        {
            get => _internalNotification.CategoryIdentifier;
            set => _internalNotification.CategoryIdentifier = value;
        }

        /// <summary>
        /// Does nothing on iOS.
        /// </summary>
        public string SmallIcon { get => null; set {} }

        /// <summary>
        /// Does nothing on iOS.
        /// </summary>
        public string LargeIcon { get => null; set {} }

        public iOSGameNotification()
        {
            _internalNotification = new iOSNotification
            {
                ShowInForeground = true // Deliver in foreground by default
            };
        }

        internal iOSGameNotification(iOSNotification _internalNotification)
        {
            this._internalNotification = _internalNotification;
        }

        /// <summary>
        /// Mark this notifications scheduled flag.
        /// </summary>
        internal void OnScheduled()
        {
            Assert.IsFalse(Scheduled);
            Scheduled = true;
        }
    }
}
#endif

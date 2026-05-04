using System;
using GameLovers.MobileServices.Notifications;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class EditorGameNotificationTest
	{
		[Test]
		public void AllProperties_RoundTripGettersAndSetters()
		{
			var deliveryTime = new DateTime(2030, 1, 2, 3, 4, 5);
			var notification = new EditorGameNotification
			{
				Id = 42,
				Title = "Title",
				Body = "Body",
				Subtitle = "Sub",
				Channel = "channel",
				BadgeNumber = 7,
				ShouldAutoCancel = true,
				DeliveryTime = deliveryTime,
				SmallIcon = "small",
				LargeIcon = "large",
			};

			Assert.AreEqual(42, notification.Id);
			Assert.AreEqual("Title", notification.Title);
			Assert.AreEqual("Body", notification.Body);
			Assert.AreEqual("Sub", notification.Subtitle);
			Assert.AreEqual("channel", notification.Channel);
			Assert.AreEqual(7, notification.BadgeNumber);
			Assert.IsTrue(notification.ShouldAutoCancel);
			Assert.AreEqual(deliveryTime, notification.DeliveryTime);
			Assert.AreEqual("small", notification.SmallIcon);
			Assert.AreEqual("large", notification.LargeIcon);
		}

		[Test]
		public void Scheduled_DefaultsFalse()
		{
			var notification = new EditorGameNotification();

			Assert.IsFalse(notification.Scheduled);
		}
	}
}

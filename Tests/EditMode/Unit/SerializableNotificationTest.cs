using System;
using GameLovers.MobileServices.Notifications;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class SerializableNotificationTest
	{
		private sealed class FakePlatform : IGameNotificationsPlatform
		{
			public event Action<IGameNotification> NotificationReceived { add { } remove { } }

			public IGameNotification CreateNotification() => new EditorGameNotification();
			public void ScheduleNotification(IGameNotification gameNotification) { }
			public void CancelNotification(int notificationId) { }
			public void DismissNotification(int notificationId) { }
			public void CancelAllScheduledNotifications() { }
			public void DismissAllDisplayedNotifications() { }
			public IGameNotification GetLastOpenedNotification() => null;
			public void OnForeground() { }
			public void OnBackground() { }
		}

		[Test]
		// ADMIT: SerializableNotificationConverter could drop the Data payload when queued notifications are saved and restored, so a rescheduled notification loses its deep link.
		// RCR: SerializableNotification.cs AsSerializableNotification — Data not saved → RED (Expected: "game://rewards/daily" But was: null). 2026-10-21
		public void RoundTrip_PreservesDataAndText()
		{
			var original = new EditorGameNotification
			{
				Id = 7,
				Title = "Reward ready",
				Body = "Claim it",
				Channel = "default",
				Data = "game://rewards/daily",
				DeliveryTime = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Local),
			};

			var restored = new PendingNotification(original).AsSerializableNotification().AsGameNotification(new FakePlatform());

			Assert.AreEqual("game://rewards/daily", restored.Data);
			Assert.AreEqual("Reward ready", restored.Title);
			Assert.AreEqual(7, restored.Id);
			Assert.AreEqual(original.DeliveryTime, restored.DeliveryTime);
		}
	}
}

using System;
using GameLovers.MobileServices.Notifications;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class PendingNotificationTest
	{
		[Test]
		// ADMIT: PendingNotification's ctor could stop rejecting a null IGameNotification with ArgumentNullException.
		// RCR: PendingNotification.cs PendingNotification(IGameNotification) — throw ArgumentException instead of ArgumentNullException → RED (wrong exception type).
		public void Ctor_NullNotification_ThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() => new PendingNotification(null));
		}

		[Test]
		// ADMIT: PendingNotification's ctor could store a different instance than the one it was handed.
		// RCR: PendingNotification.cs PendingNotification(IGameNotification) — store `new EditorGameNotification()` on the non-null branch → RED (AreSame fails). Also reddens NotificationBuilderTest.Send_CallsScheduleAndReturnsPending.
		public void Ctor_StoresNotificationReference()
		{
			var notification = new EditorGameNotification { Title = "ref" };

			var pending = new PendingNotification(notification);

			Assert.AreSame(notification, pending.Notification);
		}

		[Test]
		public void Reschedule_DefaultsFalse()
		{
			var pending = new PendingNotification(new EditorGameNotification());

			Assert.IsFalse(pending.Reschedule);
		}
	}
}

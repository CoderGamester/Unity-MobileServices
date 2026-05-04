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
		public void Ctor_NullNotification_ThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() => new PendingNotification(null));
		}

		[Test]
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

using System;
using GameLovers.MobileServices.Notifications;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class NotificationBuilderTest
	{
		private INotificationService _service;
		private IGameNotification _stub;

		[SetUp]
		public void Init()
		{
			_service = Substitute.For<INotificationService>();
			_stub = new StubNotification();
			_service.CreateNotification().Returns(_stub);
			_service.ScheduleNotification(Arg.Any<IGameNotification>())
				.Returns(call => new PendingNotification(call.Arg<IGameNotification>()));
		}

		[Test]
		public void Schedule_ReturnsNewBuilder()
		{
			var builder = _service.Schedule();
			Assert.IsNotNull(builder);
			_service.Received(1).CreateNotification();
		}

		[Test]
		public void Builder_TitleBodyChannel_AssignsAllFields()
		{
			_service.Schedule().Title("T").Body("B").Channel("c").Send();
			Assert.AreEqual("T", _stub.Title);
			Assert.AreEqual("B", _stub.Body);
			Assert.AreEqual("c", _stub.Channel);
		}

		[Test]
		public void Builder_SubtitleIdSmallIconLargeIcon_AssignsAllFields()
		{
			_service.Schedule().Subtitle("S").Id(42).SmallIcon("sm").LargeIcon("lg").Send();
			Assert.AreEqual("S", _stub.Subtitle);
			Assert.AreEqual(42, _stub.Id);
			Assert.AreEqual("sm", _stub.SmallIcon);
			Assert.AreEqual("lg", _stub.LargeIcon);
		}

		[Test]
		public void In_AssignsDeliveryTimeRelativeToNow()
		{
			var before = DateTime.Now;
			_service.Schedule().In(TimeSpan.FromHours(1)).Send();
			var after = DateTime.Now;
			Assert.IsTrue(_stub.DeliveryTime.HasValue);
			Assert.IsTrue(_stub.DeliveryTime.Value >= before.AddHours(1));
			Assert.IsTrue(_stub.DeliveryTime.Value <= after.AddHours(1).AddSeconds(1));
		}

		[Test]
		public void At_AssignsExactDeliveryTime()
		{
			var target = new DateTime(2030, 1, 1, 12, 0, 0);
			_service.Schedule().At(target).Send();
			Assert.AreEqual(target, _stub.DeliveryTime);
		}

		[Test]
		public void BadgeIncrement_ClearsBadgeNumber()
		{
			_service.Schedule().BadgeNumber(7).BadgeIncrement().Send();
			Assert.IsFalse(_stub.BadgeNumber.HasValue);
		}

		[Test]
		public void AutoCancel_DefaultTrue_AssignsTrue()
		{
			_service.Schedule().AutoCancel().Send();
			Assert.IsTrue(_stub.ShouldAutoCancel);
		}

		[Test]
		public void Send_CallsScheduleAndReturnsPending()
		{
			var pending = _service.Schedule().Title("X").Send();
			Assert.IsNotNull(pending);
			Assert.AreSame(_stub, pending.Notification);
			_service.Received(1).ScheduleNotification(_stub);
		}

		private sealed class StubNotification : IGameNotification
		{
			public int? Id { get; set; }
			public string Title { get; set; }
			public string Body { get; set; }
			public string Subtitle { get; set; }
			public string Channel { get; set; }
			public int? BadgeNumber { get; set; }
			public bool ShouldAutoCancel { get; set; }
			public DateTime? DeliveryTime { get; set; }
			public bool Scheduled => false;
			public string SmallIcon { get; set; }
			public string LargeIcon { get; set; }
		}
	}
}

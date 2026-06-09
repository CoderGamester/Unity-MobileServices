using System.Collections;
using GameLovers.MobileServices.Notifications;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	public class MobileNotificationServiceTest
	{
		private MobileNotificationService _service;

		[SetUp]
		public void Init()
		{
			var defaultChannel = new GameNotificationChannel("default", "Default", "default channel");
			_service = new MobileNotificationService(defaultChannel);
		}

		[TearDown]
		public void Cleanup()
		{
			var go = GameObject.Find("NotificationService");
			if (go != null)
			{
				Object.Destroy(go);
			}
		}

		[UnityTest]
		public IEnumerator Ctor_CreatesNotificationServiceGameObject_DontDestroyOnLoad()
		{
			yield return null;

			var go = GameObject.Find("NotificationService");
			Assert.IsNotNull(go, "MobileNotificationService should create a 'NotificationService' GameObject in its ctor");
			Assert.AreEqual("DontDestroyOnLoad", go.scene.name,
				"NotificationService GameObject should be marked DontDestroyOnLoad");
		}

		[Test]
		public void CreateNotification_InEditor_ReturnsEditorGameNotification()
		{
			var notification = _service.CreateNotification();

			Assert.IsInstanceOf<EditorGameNotification>(notification);
		}

		[Test]
		public void ScheduleNotification_InEditor_AssignsGeneratedIdWhenNull_AndReturnsPending()
		{
			var notification = _service.CreateNotification();
			Assert.IsFalse(notification.Id.HasValue);

			var pending = _service.ScheduleNotification(notification);

			Assert.IsNotNull(pending);
			Assert.AreSame(notification, pending.Notification);
			Assert.IsTrue(notification.Id.HasValue, "Editor scheduling should assign a generated id when none is provided");
			Assert.IsTrue(notification.Id.Value >= 0, "Generated id should be non-negative (Math.Abs of GetHashCode)");
		}

		[Test]
		public void ScheduleNotification_InEditor_PreservesProvidedId()
		{
			var notification = _service.CreateNotification();
			notification.Id = 12345;

			var pending = _service.ScheduleNotification(notification);

			Assert.AreEqual(12345, pending.Notification.Id);
		}

		[Test]
		public void CancelNotification_DismissNotification_DoNotThrow()
		{
			Assert.DoesNotThrow(() => _service.CancelNotification(1));
			Assert.DoesNotThrow(() => _service.DismissNotification(1));
		}

		[Test]
		public void CancelAllScheduledNotifications_DismissAllDisplayedNotifications_DoNotThrow()
		{
			Assert.DoesNotThrow(_service.CancelAllScheduledNotifications);
			Assert.DoesNotThrow(_service.DismissAllDisplayedNotifications);
		}

		[Test]
		public void Mode_SetValue_RoundTripsThroughHost()
		{
			Assert.AreEqual(OperatingMode.NoQueue, _service.Mode);

			_service.Mode = OperatingMode.Queue;
			Assert.AreEqual(OperatingMode.Queue, _service.Mode);

			_service.Mode = OperatingMode.NoQueue;
			Assert.AreEqual(OperatingMode.NoQueue, _service.Mode);
		}

		[Test]
		public void PendingNotifications_FreshService_IsEmpty()
		{
			Assert.IsNotNull(_service.PendingNotifications);
			Assert.AreEqual(0, _service.PendingNotifications.Count);
		}

		[Test]
		public void OnLocalNotificationDeliveredEvent_EditorSchedule_DoesNotFire()
		{
			var fireCount = 0;
			_service.OnLocalNotificationDeliveredEvent += _ => fireCount++;

			_service.ScheduleNotification(_service.CreateNotification());

			Assert.AreEqual(0, fireCount,
				"Delivery is platform-driven (manual-only per Tests/AGENTS.md §9); scheduling in the Editor must not synchronously fire the delivered event.");
		}

		[Test]
		public void OnLocalNotificationExpiredEvent_EditorSchedule_DoesNotFire()
		{
			var fireCount = 0;
			_service.OnLocalNotificationExpiredEvent += _ => fireCount++;

			_service.ScheduleNotification(_service.CreateNotification());

			Assert.AreEqual(0, fireCount,
				"Foreground-expiry is platform-driven (manual-only per Tests/AGENTS.md §9); scheduling in the Editor must not synchronously fire the expired event.");
		}
	}
}

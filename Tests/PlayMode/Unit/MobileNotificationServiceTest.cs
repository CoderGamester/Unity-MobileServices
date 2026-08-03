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
		// ADMIT: MobileNotificationService's ctor could leave its host GameObject in the active scene, destroying it on the first scene load.
		// RCR: MobileNotificationService.cs MobileNotificationService(params) — drop `DontDestroyOnLoad(_monoBehaviour)` → RED (go.scene.name expected 'DontDestroyOnLoad').
		public IEnumerator Ctor_CreatesNotificationServiceGameObject_DontDestroyOnLoad()
		{
			yield return null;

			var go = GameObject.Find("NotificationService");
			Assert.IsNotNull(go, "MobileNotificationService should create a 'NotificationService' GameObject in its ctor");
			Assert.AreEqual("DontDestroyOnLoad", go.scene.name,
				"NotificationService GameObject should be marked DontDestroyOnLoad");
		}

		[Test]
		// ADMIT: MobileNotificationService.CreateNotification could stop returning the Editor stand-in, so Editor callers get null.
		// RCR: MobileNotificationService.cs CreateNotification — editor branch `return new EditorGameNotification()` → `return null` → RED (expected instance of EditorGameNotification, was null). Also crashes the four siblings that dereference the created notification.
		public void CreateNotification_InEditor_ReturnsEditorGameNotification()
		{
			var notification = _service.CreateNotification();

			Assert.IsInstanceOf<EditorGameNotification>(notification);
		}

		[Test]
		// ADMIT: MobileNotificationService.ScheduleNotification could skip generating an id, leaving Editor-scheduled notifications unidentifiable.
		// RCR: MobileNotificationService.cs ScheduleNotification — `if (!gameNotification.Id.HasValue)` → `&& false` → RED (Id expected to have a value).
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
		// ADMIT: MobileNotificationService.ScheduleNotification could overwrite a caller-supplied id with a generated one.
		// RCR: MobileNotificationService.cs ScheduleNotification — `if (!gameNotification.Id.HasValue)` → `if (true)` → RED (Id expected 12345); sibling AssignsGeneratedIdWhenNull stays green.
		public void ScheduleNotification_InEditor_PreservesProvidedId()
		{
			var notification = _service.CreateNotification();
			notification.Id = 12345;

			var pending = _service.ScheduleNotification(notification);

			Assert.AreEqual(12345, pending.Notification.Id);
		}

		[Test]
		// ADMIT: GameNotificationsMonoBehaviour.CancelNotification could invert its Initialized guard and throw on an initialized host.
		// RCR: GameNotificationsMonoBehaviour.cs CancelNotification — `if (!Initialized)` → `if (Initialized)` → RED (InvalidOperationException 'Must call Initialize() first' where none expected).
		public void CancelNotification_DismissNotification_DoNotThrow()
		{
			Assert.DoesNotThrow(() => _service.CancelNotification(1));
			Assert.DoesNotThrow(() => _service.DismissNotification(1));
		}

		[Test]
		// ADMIT: GameNotificationsMonoBehaviour.CancelAllNotifications could invert its Initialized guard and throw on an initialized host.
		// RCR: GameNotificationsMonoBehaviour.cs CancelAllNotifications — `if (!Initialized)` → `if (Initialized)` → RED (InvalidOperationException 'Must call Initialize() first' where none expected).
		public void CancelAllScheduledNotifications_DismissAllDisplayedNotifications_DoNotThrow()
		{
			Assert.DoesNotThrow(_service.CancelAllScheduledNotifications);
			Assert.DoesNotThrow(_service.DismissAllDisplayedNotifications);
		}

		[Test]
		// ADMIT: MobileNotificationService.Mode's setter could drop the value instead of writing it to the host MonoBehaviour.
		// RCR: MobileNotificationService.cs Mode.set — `= value` → `= OperatingMode.NoQueue` → RED (Mode expected Queue was NoQueue).
		public void Mode_SetValue_RoundTripsThroughHost()
		{
			Assert.AreEqual(OperatingMode.NoQueue, _service.Mode);

			_service.Mode = OperatingMode.Queue;
			Assert.AreEqual(OperatingMode.Queue, _service.Mode);

			_service.Mode = OperatingMode.NoQueue;
			Assert.AreEqual(OperatingMode.NoQueue, _service.Mode);
		}

		[Test]
		// ADMIT: MobileNotificationService.PendingNotifications could surface entries a fresh service never scheduled.
		// RCR: MobileNotificationService.cs PendingNotifications — return a one-element list instead of the host's list → RED (Count expected 0 was 1).
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

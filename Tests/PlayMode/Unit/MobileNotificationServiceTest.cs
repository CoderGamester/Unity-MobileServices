using System;
using System.Collections;
using System.Linq;
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
				UnityEngine.Object.Destroy(go);
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

		[UnityTest]
		// ADMIT: MobileNotificationService's Editor scheduling path could return a wrapper without
		// registering it, making PendingNotifications disagree with the returned handle.
		// RCR: MobileNotificationService.cs ScheduleNotification — return a new PendingNotification
		// directly instead of RegisterPendingNotification → RED (pending count expected 1, was 0).
		public IEnumerator ScheduleNotification_InEditor_AddsReturnedPendingToCollection()
		{
			var pending = _service.ScheduleNotification(_service.CreateNotification());

			Assert.AreEqual(1, _service.PendingNotifications.Count);
			Assert.AreSame(pending, _service.PendingNotifications[0]);
			yield return null;
		}

#if UNITY_EDITOR
		[Test]
		// ADMIT: MobileNotificationService needs an explicit editor delivery path so the simulator can drive the sample's real service without changing scheduling semantics.
		// RCR: MobileNotificationService.cs TrySimulateDelivery — return false before removing/raising → RED (delivery count expected 1, pending count expected 0).
		public void TrySimulateDelivery_RemovesPendingAndRaisesDeliveredEventOnce()
		{
			var notification = _service.CreateNotification();
			notification.Id = 4321;
			notification.Title = "Simulator delivery";
			var pending = _service.ScheduleNotification(notification);
			PendingNotification delivered = null;
			var deliveryCount = 0;
			_service.OnLocalNotificationDeliveredEvent += received =>
			{
				delivered = received;
				deliveryCount++;
			};

			Assert.IsTrue(_service.TrySimulateDelivery(notification.Id.Value));
			Assert.AreEqual(1, deliveryCount);
			Assert.AreSame(pending, delivered);
			Assert.AreEqual(0, _service.PendingNotifications.Count);
			Assert.IsFalse(_service.TrySimulateDelivery(notification.Id.Value));
			Assert.AreEqual(1, deliveryCount);
		}
#endif

		[UnityTest]
		// ADMIT: MobileNotificationService.Dispose could leave its DontDestroyOnLoad host alive or
		// perform work twice when ownership is released by both MobileService and the caller.
		// RCR: MobileNotificationService.cs Dispose — replace UnityEngine.Object.Destroy with return → RED
		// (host remains after one frame).
		public IEnumerator Dispose_DestroysOwnHost_AndIsIdempotent()
		{
			var host = ResolveHost();
			_service.Dispose();
			Assert.DoesNotThrow(_service.Dispose);

			yield return null;

			Assert.IsTrue(host == null, "Dispose should destroy this service's own host");
		}

		[Test]
		// ADMIT: MobileNotificationService could expose a partially destroyed host after disposal,
		// producing inconsistent success or MissingReferenceException results across its public API.
		// RCR: MobileNotificationService.cs ThrowIfDisposed — replace the throw with return → RED
		// (PendingNotifications expected ObjectDisposedException, but no exception was thrown). 2026-08-09
		public void PublicOperations_AfterDispose_ThrowObjectDisposedException()
		{
			var notification = _service.CreateNotification();
			_service.Dispose();

			Assert.Throws<ObjectDisposedException>(() => _ = _service.PendingNotifications);
			Assert.Throws<ObjectDisposedException>(() => _ = _service.Mode);
			Assert.Throws<ObjectDisposedException>(() => _service.Mode = OperatingMode.Queue);
			Assert.Throws<ObjectDisposedException>(() => _service.CreateNotification());
			Assert.Throws<ObjectDisposedException>(() => _service.ScheduleNotification(notification));
			Assert.Throws<ObjectDisposedException>(() => _service.CancelNotification(1));
			Assert.Throws<ObjectDisposedException>(() => _service.DismissNotification(1));
			Assert.Throws<ObjectDisposedException>(_service.CancelAllScheduledNotifications);
			Assert.Throws<ObjectDisposedException>(_service.DismissAllDisplayedNotifications);
#if UNITY_EDITOR
			Assert.Throws<ObjectDisposedException>(() => _service.TrySimulateDelivery(1));
#endif
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
		// ADMIT: Editor cancellation could return before removing the in-memory pending row when no native backend exists.
		// RCR: GameNotificationsMonoBehaviour.cs CancelNotification — restore the early `_platform == null` return → RED (pending count expected 0, was 1).
		public void CancelNotification_RemovesPendingNotificationWithoutNativeBackend()
		{
			var notification = _service.CreateNotification();
			var pending = _service.ScheduleNotification(notification);

			_service.CancelNotification(pending.Notification.Id.Value);

			Assert.AreEqual(0, _service.PendingNotifications.Count);
		}

		[Test]
		// ADMIT: Editor Cancel All could return before clearing the in-memory pending rows when no native backend exists.
		// RCR: GameNotificationsMonoBehaviour.cs CancelAllNotifications — restore the early `_platform == null` return → RED (pending count expected 0, was 2).
		public void CancelAllScheduledNotifications_ClearsPendingNotificationsWithoutNativeBackend()
		{
			_service.ScheduleNotification(_service.CreateNotification());
			_service.ScheduleNotification(_service.CreateNotification());

			_service.CancelAllScheduledNotifications();

			Assert.AreEqual(0, _service.PendingNotifications.Count);
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
		// ADMIT: MobileNotificationService.ScheduleNotification must not synthesise a delivery - delivery is
		// platform-driven, and a synchronous fire would double-count every scheduled notification.
		// RCR: MobileNotificationService.cs ScheduleNotification - invoke OnLocalNotificationDeliveredEvent on
		// the editor branch's returned PendingNotification -> RED (fireCount expected 0, was 1).
		public void OnLocalNotificationDeliveredEvent_EditorSchedule_DoesNotFire()
		{
			var fireCount = 0;
			_service.OnLocalNotificationDeliveredEvent += _ => fireCount++;

			_service.ScheduleNotification(_service.CreateNotification());

			Assert.AreEqual(0, fireCount,
				"Delivery is platform-driven (manual-only per Tests/AGENTS.md §9); scheduling in the Editor must not synchronously fire the delivered event.");
		}

		[Test]
		// ADMIT: MobileNotificationService.ScheduleNotification must not synthesise a foreground expiry -
		// expiry is platform-driven and only GameNotificationsMonoBehaviour may raise it.
		// RCR: MobileNotificationService.cs ScheduleNotification - invoke OnLocalNotificationExpiredEvent on
		// the editor branch's returned PendingNotification -> RED (fireCount expected 0, was 1).
		public void OnLocalNotificationExpiredEvent_EditorSchedule_DoesNotFire()
		{
			var fireCount = 0;
			_service.OnLocalNotificationExpiredEvent += _ => fireCount++;

			_service.ScheduleNotification(_service.CreateNotification());

			Assert.AreEqual(0, fireCount,
				"Foreground-expiry is platform-driven (manual-only per Tests/AGENTS.md §9); scheduling in the Editor must not synchronously fire the expired event.");
		}

		[UnityTest]
		// ADMIT: MobileNotificationService's ctor snapshots the still-null OnLocalNotificationDeliveredEvent into the
		// host's plain OnLocalNotificationDelivered field, so a consumer subscribing later is never reached.
		// RCR: MobileNotificationService.cs ctor — restore `= OnLocalNotificationDeliveredEvent` → RED
		// (Expected: same as PendingNotification, But was: null); the expired sibling stays green. 2026-08-03
		// Raised through the host field directly because GameNotificationsMonoBehaviour.OnNotificationReceived is
		// platform-driven and has no IGameNotificationsPlatform in the Editor.
		public IEnumerator OnLocalNotificationDeliveredEvent_SubscribedAfterCtor_ReachesSubscriber()
		{
			var host = ResolveHost();
			PendingNotification received = null;

			_service.OnLocalNotificationDeliveredEvent += notification => received = notification;

			yield return null;

			var delivered = new PendingNotification(_service.CreateNotification());

			host.OnLocalNotificationDelivered?.Invoke(delivered);

			Assert.AreSame(delivered, received,
				"A handler subscribed after construction must reach the host's delivered raise path");
		}

		[UnityTest]
		// ADMIT: MobileNotificationService's ctor snapshots the still-null OnLocalNotificationExpiredEvent into the
		// host's plain OnLocalNotificationExpired field, so GameNotificationsMonoBehaviour.Update's queue-expiry
		// raise reaches no consumer.
		// RCR: MobileNotificationService.cs ctor — restore `= OnLocalNotificationExpiredEvent` → RED
		// (Expected: same as PendingNotification, But was: null); the delivered sibling stays green. 2026-08-03
		public IEnumerator OnLocalNotificationExpiredEvent_SubscribedAfterCtor_ReachesSubscriber()
		{
			var host = ResolveHost();
			PendingNotification received = null;

			_service.OnLocalNotificationExpiredEvent += notification => received = notification;
			_service.Mode = OperatingMode.Queue;

			var notification = _service.CreateNotification();
			notification.DeliveryTime = DateTime.Now.AddSeconds(-1);

			var expired = host.ScheduleNotification(notification);

			yield return null;
			yield return null;

			Assert.AreSame(expired, received,
				"A handler subscribed after construction must reach the host's Update expiry raise path");
		}

		// Attribution guard: a previous test's host can outlive its deferred Destroy, so the host is matched by the
		// pending list instance this service exposes rather than by GameObject name.
		private GameNotificationsMonoBehaviour ResolveHost()
		{
			var matches = UnityEngine.Object
				.FindObjectsByType<GameNotificationsMonoBehaviour>()
				.Where(host => ReferenceEquals(host.PendingNotifications, _service.PendingNotifications))
				.ToArray();

			Assert.AreEqual(1, matches.Length, "Expected exactly one host backing the service under test");

			return matches[0];
		}
	}
}

using System;
using System.Collections;
using GameLovers.MobileServices.Notifications;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class GameNotificationsMonoBehaviourOpenedTest
	{
		private sealed class FakePlatform : IGameNotificationsPlatform
		{
			public IGameNotification LastOpened;

			public event Action<IGameNotification> NotificationReceived { add { } remove { } }

			public IGameNotification CreateNotification() => new EditorGameNotification();
			public void ScheduleNotification(IGameNotification gameNotification) { }
			public void CancelNotification(int notificationId) { }
			public void DismissNotification(int notificationId) { }
			public void CancelAllScheduledNotifications() { }
			public void DismissAllDisplayedNotifications() { }
			public IGameNotification GetLastOpenedNotification() => LastOpened;
			public void OnForeground() { }
			public void OnBackground() { }
		}

		private GameObject _host;
		private GameNotificationsMonoBehaviour _service;
		private FakePlatform _platform;
		private int _openedCount;
		private PendingNotification _lastOpened;

		[SetUp]
		public void Init()
		{
			_host = new GameObject("NotificationOpenedTestHost");
			_service = _host.AddComponent<GameNotificationsMonoBehaviour>();
			_platform = new FakePlatform();
			_openedCount = 0;
			_lastOpened = null;
			_service.OnNotificationOpened = notification =>
			{
				_openedCount++;
				_lastOpened = notification;
			};
		}

		[TearDown]
		public void Cleanup()
		{
			UnityEngine.Object.Destroy(_host);
		}

		private static EditorGameNotification Opened(int id, string data) =>
			new EditorGameNotification { Id = id, Title = "t" + id, Data = data };

		private void Refocus() => _host.SendMessage("OnApplicationFocus", true);

		[UnityTest]
		// ADMIT: GameNotificationsMonoBehaviour could report the cold-start notification as a fresh open too, so the game routes the same tap twice.
		// RCR: GameNotificationsMonoBehaviour.cs AttachPlatform — launch key not remembered → RED (Expected: 0 But was: 1). 2026-10-21
		public IEnumerator AttachPlatform_StartedFromNotification_ExposesLaunchWithoutRaisingOpened()
		{
			_platform.LastOpened = Opened(5, "game://launch");

			_service.AttachPlatform(_platform);
			Refocus();
			yield return null;

			Assert.IsNotNull(_service.LaunchNotification);
			Assert.AreEqual("game://launch", _service.LaunchNotification.Notification.Data);
			Assert.AreEqual(0, _openedCount);
		}

		[UnityTest]
		// ADMIT: GameNotificationsMonoBehaviour could expose a launch notification when the app was started normally.
		// RCR: GameNotificationsMonoBehaviour.cs LaunchNotification — default launch value set → RED (Expected: null But was: <GameLovers.MobileServices.Notifications.PendingNotification>). 2026-10-21
		public IEnumerator AttachPlatform_StartedNormally_HasNoLaunchNotification()
		{
			_service.AttachPlatform(_platform);
			yield return null;

			Assert.IsNull(_service.LaunchNotification);
		}

		[UnityTest]
		// ADMIT: GameNotificationsMonoBehaviour could miss a notification the player opens after startup, so taps while the app is running never route.
		// RCR: GameNotificationsMonoBehaviour.cs PublishOpenedNotification — Opened invocation removed → RED (Expected: 1 But was: 0). 2026-10-21
		public IEnumerator Refocus_NewNotificationOpened_RaisesOpenedOnce()
		{
			_service.AttachPlatform(_platform);
			_platform.LastOpened = Opened(9, "game://shop");

			Refocus();
			yield return null;

			Assert.AreEqual(1, _openedCount);
			Assert.AreEqual("game://shop", _lastOpened.Notification.Data);
		}

		[UnityTest]
		// ADMIT: GameNotificationsMonoBehaviour could raise Opened on every focus because the platforms keep returning the last opened notification.
		// RCR: GameNotificationsMonoBehaviour.cs PublishOpenedNotification — de-duplication disabled → RED (Expected: 1 But was: 3). 2026-10-21
		public IEnumerator Refocus_SameNotificationAgain_DoesNotRaiseOpenedAgain()
		{
			_service.AttachPlatform(_platform);
			_platform.LastOpened = Opened(9, "game://shop");

			Refocus();
			Refocus();
			Refocus();
			yield return null;

			Assert.AreEqual(1, _openedCount);
		}

		[UnityTest]
		// ADMIT: GameNotificationsMonoBehaviour could remember only the first opened notification, ignoring later taps on different ones.
		// RCR: GameNotificationsMonoBehaviour.cs PublishOpenedNotification — constant opened key → RED (Expected: 2 But was: 1). 2026-10-21
		public IEnumerator Refocus_DifferentNotificationOpened_RaisesOpenedForEach()
		{
			_service.AttachPlatform(_platform);
			_platform.LastOpened = Opened(1, "game://a");
			Refocus();
			_platform.LastOpened = Opened(2, "game://b");
			Refocus();
			yield return null;

			Assert.AreEqual(2, _openedCount);
			Assert.AreEqual("game://b", _lastOpened.Notification.Data);
		}
	}
}

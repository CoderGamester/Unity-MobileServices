using GameLovers.MobileServices.Notifications;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class OperatingModeTest
	{
		[Test]
		// ADMIT: OperatingMode.QueueAndClear could lose its ClearOnForegrounding bit, so foregrounding no longer clears the queue.
		// RCR: GameNotificationsMonoBehaviour.cs OperatingMode.QueueAndClear — `Queue | ClearOnForegrounding` → `Queue` → RED (ClearOnForegrounding bit expected set).
		public void QueueAndClear_HasQueueAndClearOnForegroundingFlags()
		{
			var mode = OperatingMode.QueueAndClear;

			Assert.IsTrue((mode & OperatingMode.Queue) == OperatingMode.Queue);
			Assert.IsTrue((mode & OperatingMode.ClearOnForegrounding) == OperatingMode.ClearOnForegrounding);
			Assert.IsFalse((mode & OperatingMode.RescheduleAfterClearing) == OperatingMode.RescheduleAfterClearing);
		}

		[Test]
		// ADMIT: OperatingMode.QueueClearAndReschedule could lose its RescheduleAfterClearing bit, so cleared notifications are never re-queued.
		// RCR: GameNotificationsMonoBehaviour.cs OperatingMode.QueueClearAndReschedule — drop `| RescheduleAfterClearing` → RED (RescheduleAfterClearing bit expected set).
		public void QueueClearAndReschedule_HasAllThreeFlags()
		{
			var mode = OperatingMode.QueueClearAndReschedule;

			Assert.IsTrue((mode & OperatingMode.Queue) == OperatingMode.Queue);
			Assert.IsTrue((mode & OperatingMode.ClearOnForegrounding) == OperatingMode.ClearOnForegrounding);
			Assert.IsTrue((mode & OperatingMode.RescheduleAfterClearing) == OperatingMode.RescheduleAfterClearing);
		}
	}
}

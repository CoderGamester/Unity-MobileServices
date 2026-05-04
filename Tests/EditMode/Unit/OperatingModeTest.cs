using GameLovers.MobileServices.Notifications;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class OperatingModeTest
	{
		[Test]
		public void QueueAndClear_HasQueueAndClearOnForegroundingFlags()
		{
			var mode = OperatingMode.QueueAndClear;

			Assert.IsTrue((mode & OperatingMode.Queue) == OperatingMode.Queue);
			Assert.IsTrue((mode & OperatingMode.ClearOnForegrounding) == OperatingMode.ClearOnForegrounding);
			Assert.IsFalse((mode & OperatingMode.RescheduleAfterClearing) == OperatingMode.RescheduleAfterClearing);
		}

		[Test]
		public void QueueClearAndReschedule_HasAllThreeFlags()
		{
			var mode = OperatingMode.QueueClearAndReschedule;

			Assert.IsTrue((mode & OperatingMode.Queue) == OperatingMode.Queue);
			Assert.IsTrue((mode & OperatingMode.ClearOnForegrounding) == OperatingMode.ClearOnForegrounding);
			Assert.IsTrue((mode & OperatingMode.RescheduleAfterClearing) == OperatingMode.RescheduleAfterClearing);
		}
	}
}

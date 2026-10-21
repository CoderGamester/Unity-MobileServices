using System;
using GameLovers.MobileServices.Device;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class BackgroundTaskHandleTest
	{
		private int _endCalls;
		private bool _expired;
		private TimeSpan _remaining;
		private BackgroundTaskHandle _handle;

		[SetUp]
		public void Init()
		{
			_endCalls = 0;
			_expired = false;
			_remaining = TimeSpan.FromSeconds(20);
			_handle = new BackgroundTaskHandle("upload", () => _endCalls++, () => _remaining, () => _expired);
		}

		[Test]
		// ADMIT: BackgroundTaskHandle.Dispose could end the system task twice, which iOS treats as a leaked or invalid task id.
		// RCR: BackgroundTaskHandle.cs Dispose — disposed guard disabled → RED (Expected: 1 But was: 2). 2026-10-21
		public void Dispose_CalledTwice_EndsTheTaskOnce()
		{
			_handle.Dispose();
			_handle.Dispose();

			Assert.AreEqual(1, _endCalls);
		}

		[Test]
		// ADMIT: BackgroundTaskHandle.IsActive could stay true after expiry or disposal, so protected work keeps running past the limit.
		// RCR: BackgroundTaskHandle.cs IsActive — disposal check removed → RED (Expected: False But was: True). 2026-10-21
		public void IsActive_TracksExpiryAndDisposal()
		{
			Assert.IsTrue(_handle.IsActive);

			_expired = true;
			Assert.IsFalse(_handle.IsActive);
			Assert.IsTrue(_handle.IsExpired);

			_expired = false;
			_handle.Dispose();
			Assert.IsFalse(_handle.IsActive);
			Assert.IsFalse(_handle.IsExpired);
		}

		[Test]
		// ADMIT: BackgroundTaskHandle.RemainingTime could keep reporting time left after the task ended.
		// RCR: BackgroundTaskHandle.cs RemainingTime — disposal short-circuit removed → RED (Expected: 00:00:00 But was: 00:00:20). 2026-10-21
		public void RemainingTime_ReportsPlatformValueUntilDisposed()
		{
			Assert.AreEqual(TimeSpan.FromSeconds(20), _handle.RemainingTime);

			_handle.Dispose();

			Assert.AreEqual(TimeSpan.Zero, _handle.RemainingTime);
		}

		[Test]
		// ADMIT: BackgroundTaskService.Begin could hand non-iOS platforms a handle that reports expiry, aborting work that has no limit.
		// RCR: BackgroundTaskService.cs Begin — fallback handle reports expired → RED (Expected: True But was: False). 2026-10-21
		public void Begin_WithoutPlatformSupport_ReturnsActiveUnlimitedHandle()
		{
			var handle = new BackgroundTaskService().Begin("sync");

			Assert.IsTrue(handle.IsActive);
			Assert.AreEqual(TimeSpan.MaxValue, handle.RemainingTime);
			Assert.AreEqual("sync", handle.Name);
			handle.Dispose();
		}

		[Test]
		// ADMIT: BackgroundTaskService.Begin could drop the diagnostic label when the caller passes none.
		// RCR: BackgroundTaskService.cs Begin — default label removed → RED (System.ArgumentException : The actual value must be a string or a non-null IEnumerable ...). 2026-10-21
		public void Begin_WithoutName_UsesADefaultLabel()
		{
			var handle = new BackgroundTaskService().Begin(null);

			Assert.IsNotEmpty(handle.Name);
			handle.Dispose();
		}
	}
}

using System.Threading.Tasks;
using GameLovers.MobileServices.Device;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class AttServiceTest
	{
		private AttService _service;

		[SetUp]
		public void Init()
		{
			_service = new AttService();
		}

		[Test]
		// ADMIT: AttService.CurrentStatus could return a non-Authorized default in the Editor, making consumers skip tracking init.
		// RCR: AttService.cs CurrentStatus — editor fallback `?? AttStatus.Authorized` → `?? AttStatus.Denied` → RED (expected Authorized was Denied).
		public void CurrentStatus_InEditor_IsAuthorized()
		{
			Assert.AreEqual(AttStatus.Authorized, _service.CurrentStatus);
		}

		[Test]
		// ADMIT: AttService.RequestAuthorizationAsync could return a non-Authorized completed Task in the Editor.
		// RCR: AttService.cs RequestAuthorizationAsync — `Task.FromResult(EditorRequestResultOverride ?? Authorized)` → `?? Denied` → RED (expected Authorized was Denied).
		public void RequestAuthorizationAsync_InEditor_ReturnsAuthorized()
		{
			Task<AttStatus> task = _service.RequestAuthorizationAsync();

			Assert.IsTrue(task.IsCompleted);
			Assert.AreEqual(AttStatus.Authorized, task.Result);
		}
	}
}

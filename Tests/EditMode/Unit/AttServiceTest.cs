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
		public void CurrentStatus_InEditor_IsAuthorized()
		{
			Assert.AreEqual(AttStatus.Authorized, _service.CurrentStatus);
		}

		[Test]
		public void RequestAuthorizationAsync_InEditor_ReturnsAuthorized()
		{
			Task<AttStatus> task = _service.RequestAuthorizationAsync();

			Assert.IsTrue(task.IsCompleted);
			Assert.AreEqual(AttStatus.Authorized, task.Result);
		}
	}
}

using System.Threading.Tasks;
using GameLovers.MobileServices.Device;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class PermissionsServiceTest
	{
		private PermissionsService _service;

		[SetUp]
		public void Init()
		{
			_service = new PermissionsService();
		}

		[Test]
		[TestCase(AppPermission.Camera)]
		[TestCase(AppPermission.Microphone)]
		[TestCase(AppPermission.LocationWhenInUse)]
		[TestCase(AppPermission.LocationAlways)]
		[TestCase(AppPermission.PhotoLibrary)]
		[TestCase(AppPermission.PhotoLibraryAddOnly)]
		[TestCase(AppPermission.Notifications)]
		public void Check_InEditor_AllPermissions_ReturnsGranted(AppPermission permission)
		{
			Assert.AreEqual(PermissionStatus.Granted, _service.Check(permission));
		}

		[Test]
		[TestCase(AppPermission.Camera)]
		[TestCase(AppPermission.Microphone)]
		[TestCase(AppPermission.LocationWhenInUse)]
		[TestCase(AppPermission.LocationAlways)]
		[TestCase(AppPermission.PhotoLibrary)]
		[TestCase(AppPermission.PhotoLibraryAddOnly)]
		[TestCase(AppPermission.Notifications)]
		public void RequestAsync_InEditor_AllPermissions_ReturnsGranted(AppPermission permission)
		{
			Task<PermissionStatus> task = _service.RequestAsync(permission);

			Assert.IsTrue(task.IsCompleted);
			Assert.AreEqual(PermissionStatus.Granted, task.Result);
		}
	}
}

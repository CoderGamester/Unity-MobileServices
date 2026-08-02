using System.Collections.Generic;
using System.Threading.Tasks;
using GameLovers.MobileServices.Device;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class MultiPermissionRequestTest
	{
		[Test]
		// ADMIT: IPermissionsService.RequestAsync(params) could key every result under one permission, collapsing the aggregate.
		// RCR: IPermissionsService.cs RequestAsync(params AppPermission[]) — `result[p]` → `result[AppPermission.Camera]` → RED (Count expected 3 was 1).
		public async Task RequestAsync_MultiplePermissions_AggregatesIntoDictionary()
		{
			IPermissionsService service = new PermissionsService();
			var result = await service.RequestAsync(AppPermission.Camera, AppPermission.Microphone, AppPermission.Notifications);

			Assert.AreEqual(3, result.Count);
			Assert.IsTrue(result.ContainsKey(AppPermission.Camera));
			Assert.IsTrue(result.ContainsKey(AppPermission.Microphone));
			Assert.IsTrue(result.ContainsKey(AppPermission.Notifications));
		}

		[Test]
		public async Task RequestAsync_NoPermissions_ReturnsEmptyDictionary()
		{
			IPermissionsService service = new PermissionsService();
			var result = await service.RequestAsync(new AppPermission[0]);
			Assert.AreEqual(0, result.Count);
		}

		[Test]
		// ADMIT: IPermissionsService.RequestAsync(params) could return a populated dictionary for a null argument.
		// RCR: IPermissionsService.cs RequestAsync(params AppPermission[]) — seed one entry in the `permissions == null` early return → RED (Count expected 0 was 1).
		public async Task RequestAsync_Null_ReturnsEmptyDictionary()
		{
			IPermissionsService service = new PermissionsService();
			var result = await service.RequestAsync((AppPermission[])null);
			Assert.AreEqual(0, result.Count);
		}

		[Test]
		// ADMIT: IPermissionsService.RequestAsync(params) could use Dictionary.Add and throw when the caller repeats a permission.
		// RCR: IPermissionsService.cs RequestAsync(params AppPermission[]) — `result[p] = ...` → `result.Add(p, ...)` → RED (ArgumentException: an item with the same key has already been added).
		public async Task RequestAsync_DuplicatePermission_LastValueWins()
		{
			IPermissionsService service = new PermissionsService();
			// The aggregated dictionary deduplicates by key — sequential awaits write the same key twice.
			var result = await service.RequestAsync(AppPermission.Camera, AppPermission.Camera);
			Assert.AreEqual(1, result.Count);
		}
	}
}

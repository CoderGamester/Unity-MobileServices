using System.Collections.Generic;
using System.Linq;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Device.Internal;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class AndroidPermissionPolicyTest
	{
		private const int Api28 = 28;
		private const int Api30 = 30;
		private const int Api32 = 32;
		private const int Api33 = 33;
		private const int Api34 = 34;

		private static PermissionStatus Resolve(
			AppPermission permission,
			int apiLevel,
			bool wasRequested = false,
			bool showRationale = false,
			params string[] granted)
		{
			var grants = new HashSet<string>(granted);
			return AndroidPermissionPolicy.Resolve(permission, apiLevel, grants.Contains, wasRequested, showRationale);
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.GetForegroundRequest could request only precise location, which Android 12+ ignores.
		// RCR: AndroidPermissionPolicy.cs GetForegroundRequest — location request without CoarseLocation → RED (Expected: equivalent to < "ACCESS_FINE_LOCATION", "ACCESS_COARSE_LOCATION" > But was: <...). 2026-10-21
		[TestCase(AppPermission.LocationWhenInUse)]
		[TestCase(AppPermission.LocationAlways)]
		public void GetForegroundRequest_Location_AsksPreciseAndApproximateTogether(AppPermission permission)
		{
			var request = AndroidPermissionPolicy.GetForegroundRequest(permission, Api33);

			CollectionAssert.AreEquivalent(
				new[] { AndroidPermissionPolicy.FineLocation, AndroidPermissionPolicy.CoarseLocation },
				request);
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.NeedsBackgroundRequest could fold background location into the foreground dialog, which Android 11+ rejects.
		// RCR: AndroidPermissionPolicy.cs GetForegroundRequest — BackgroundLocation added to the foreground request → RED (Expected: not collection containing "ACCESS_BACKGROUND_LOCATION" But was: < "ACCESS_FIN...). 2026-10-21
		public void GetForegroundRequest_LocationAlways_NeverIncludesBackgroundLocation()
		{
			var request = AndroidPermissionPolicy.GetForegroundRequest(AppPermission.LocationAlways, Api33);

			CollectionAssert.DoesNotContain(request, AndroidPermissionPolicy.BackgroundLocation);
			Assert.IsTrue(AndroidPermissionPolicy.NeedsBackgroundRequest(AppPermission.LocationAlways, Api30));
			Assert.IsFalse(AndroidPermissionPolicy.NeedsBackgroundRequest(AppPermission.LocationWhenInUse, Api30));
			Assert.IsFalse(AndroidPermissionPolicy.NeedsBackgroundRequest(AppPermission.LocationAlways, Api28));
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.GetForegroundRequest could ask for the API 33 photo permission on older devices where it does not exist.
		// RCR: AndroidPermissionPolicy.cs GetForegroundRequest — API 33 ternary branches swapped → RED (Expected: equivalent to < "READ_EXTERNAL_STORAGE" > But was: < "READ_MEDIA_IMAGES" >). 2026-10-21
		[TestCase(Api32, new[] { AndroidPermissionPolicy.ReadExternalStorage })]
		[TestCase(Api33, new[] { AndroidPermissionPolicy.ReadMediaImages })]
		[TestCase(Api34, new[] { AndroidPermissionPolicy.ReadMediaImages, AndroidPermissionPolicy.ReadMediaVisualUserSelected })]
		public void GetForegroundRequest_PhotoLibrary_UsesTheApiLevelPermission(int apiLevel, string[] expected)
		{
			CollectionAssert.AreEquivalent(expected, AndroidPermissionPolicy.GetForegroundRequest(AppPermission.PhotoLibrary, apiLevel));
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy could prompt for storage just to save to the gallery, which needs no permission from API 29.
		// RCR: AndroidPermissionPolicy.cs GetForegroundRequest — `apiLevel >= ApiLevelQ` inverted → RED (Expected: 0 But was: 1). 2026-10-21
		public void GetForegroundRequest_PhotoAddOnly_FromApi29_NeedsNoPermission()
		{
			Assert.AreEqual(0, AndroidPermissionPolicy.GetForegroundRequest(AppPermission.PhotoLibraryAddOnly, Api30).Count);
			CollectionAssert.AreEqual(
				new[] { AndroidPermissionPolicy.WriteExternalStorage },
				AndroidPermissionPolicy.GetForegroundRequest(AppPermission.PhotoLibraryAddOnly, Api28));
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.Resolve could report a never-asked permission as Denied, so the game sends first-time players to Settings.
		// RCR: AndroidPermissionPolicy.cs Resolve — `denied` forced to Denied → RED (Expected: NotDetermined But was: Denied). 2026-10-21
		public void Resolve_NotGrantedAndNeverRequested_IsNotDetermined()
		{
			Assert.AreEqual(PermissionStatus.NotDetermined, Resolve(AppPermission.Camera, Api33));
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.Resolve could never return Denied, hiding a refusal from the game.
		// RCR: AndroidPermissionPolicy.cs Resolve — `denied` forced to NotDetermined → RED (Expected: Denied But was: NotDetermined). 2026-10-21
		public void Resolve_NotGrantedAfterRequest_IsDenied()
		{
			Assert.AreEqual(PermissionStatus.Denied, Resolve(AppPermission.Camera, Api33, wasRequested: true));
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.Resolve could ignore the OS rationale flag, reporting a refusal as NotDetermined after the app's own record was cleared.
		// RCR: AndroidPermissionPolicy.cs Resolve — `wasRequested || showRationale` → `wasRequested` → RED (Expected: Denied But was: NotDetermined). 2026-10-21
		public void Resolve_RationaleFlagSet_IsDeniedWithoutOurRecord()
		{
			Assert.AreEqual(PermissionStatus.Denied, Resolve(AppPermission.Microphone, Api33, wasRequested: false, showRationale: true));
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.Resolve could report approximate-only location as fully Granted.
		// RCR: AndroidPermissionPolicy.cs Resolve — approximate-only branch returns Granted → RED (Expected: Limited But was: Granted). 2026-10-21
		public void Resolve_LocationWithOnlyApproximateGrant_IsLimited()
		{
			var status = Resolve(AppPermission.LocationWhenInUse, Api33, true, false, AndroidPermissionPolicy.CoarseLocation);

			Assert.AreEqual(PermissionStatus.Limited, status);
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.Resolve could report foreground-only location as Granted when background access was asked for.
		// RCR: AndroidPermissionPolicy.cs Resolve — background grant check removed → RED (Expected: Limited But was: Granted). 2026-10-21
		public void Resolve_LocationAlwaysWithoutBackgroundGrant_IsLimited()
		{
			var status = Resolve(
				AppPermission.LocationAlways,
				Api30,
				true,
				false,
				AndroidPermissionPolicy.FineLocation,
				AndroidPermissionPolicy.CoarseLocation);

			Assert.AreEqual(PermissionStatus.Limited, status);
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.Resolve could demand a background grant below API 29, where the permission does not exist.
		// RCR: AndroidPermissionPolicy.cs Resolve — API 29 gate forced true → RED (Expected: Granted But was: Limited). 2026-10-21
		public void Resolve_LocationAlwaysBelowApi29_ForegroundGrantIsGranted()
		{
			var status = Resolve(
				AppPermission.LocationAlways,
				Api28,
				true,
				false,
				AndroidPermissionPolicy.FineLocation,
				AndroidPermissionPolicy.CoarseLocation);

			Assert.AreEqual(PermissionStatus.Granted, status);
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.Resolve could treat Android 14 partial photo access as a refusal.
		// RCR: AndroidPermissionPolicy.cs Resolve — ReadMediaVisualUserSelected check removed → RED (Expected: Limited But was: Denied). 2026-10-21
		public void Resolve_PhotoLibraryWithOnlySelectedPhotos_IsLimited()
		{
			var status = Resolve(AppPermission.PhotoLibrary, Api34, true, false, AndroidPermissionPolicy.ReadMediaVisualUserSelected);

			Assert.AreEqual(PermissionStatus.Limited, status);
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.Resolve could report gallery saving as ungranted on API 29+, blocking a flow that needs no permission.
		// RCR: AndroidPermissionPolicy.cs Resolve — API 29 Granted return removed → RED (Expected: Granted But was: NotDetermined). 2026-10-21
		public void Resolve_PhotoAddOnlyFromApi29_IsAlwaysGranted()
		{
			Assert.AreEqual(PermissionStatus.Granted, Resolve(AppPermission.PhotoLibraryAddOnly, Api30));
			Assert.AreEqual(PermissionStatus.NotDetermined, Resolve(AppPermission.PhotoLibraryAddOnly, Api28));
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.Resolve could read POST_NOTIFICATIONS on a device where the permission is undefined and always answer NotDetermined.
		// RCR: AndroidPermissionPolicy.cs ResolveLegacyNotifications — enabled/denied branches swapped → RED (Expected: Granted But was: Denied). 2026-10-21
		public void ResolveLegacyNotifications_FollowsTheEnabledSwitch()
		{
			Assert.AreEqual(PermissionStatus.Granted, AndroidPermissionPolicy.ResolveLegacyNotifications(true));
			Assert.AreEqual(PermissionStatus.Denied, AndroidPermissionPolicy.ResolveLegacyNotifications(false));
			Assert.AreEqual(
				PermissionStatus.Granted,
				Resolve(AppPermission.Notifications, Api33, true, false, AndroidPermissionPolicy.PostNotifications));
		}

		[Test]
		// ADMIT: AndroidPermissionPolicy.GetForegroundRequest could miss a permission for an AppPermission, leaving it unrequestable on Android.
		// RCR: AndroidPermissionPolicy.cs GetForegroundRequest — Camera case returns an empty request → RED (Expected is <System.String[1]>, actual is <System.String[0]> Values differ at index [0]...). 2026-10-21
		public void GetForegroundRequest_CameraAndMicrophone_MapToTheirManifestPermission()
		{
			CollectionAssert.AreEqual(new[] { AndroidPermissionPolicy.Camera }, AndroidPermissionPolicy.GetForegroundRequest(AppPermission.Camera, Api33));
			CollectionAssert.AreEqual(new[] { AndroidPermissionPolicy.RecordAudio }, AndroidPermissionPolicy.GetForegroundRequest(AppPermission.Microphone, Api33).ToArray());
		}
	}
}

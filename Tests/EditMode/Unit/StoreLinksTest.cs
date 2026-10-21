using GameLovers.MobileServices.Device.Internal;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class StoreLinksTest
	{
		[Test]
		// ADMIT: StoreLinks.Build could keep the `id` prefix twice, producing a store link that opens a missing app.
		// RCR: StoreLinks.cs Build — `id` prefix stripping removed → RED (Expected: "https://apps.apple.com/app/id123456789" But was: null). 2026-10-21
		[TestCase("123456789", "https://apps.apple.com/app/id123456789")]
		[TestCase("id123456789", "https://apps.apple.com/app/id123456789")]
		[TestCase("  ID123456789  ", "https://apps.apple.com/app/id123456789")]
		public void Build_IosAppId_NormalizesThePrefix(string appId, string expected)
		{
			Assert.AreEqual(expected, StoreLinks.Build(RuntimePlatform.IPhonePlayer, appId, null, false));
		}

		[Test]
		// ADMIT: StoreLinks.Build could build an App Store link from a non-numeric id, opening a broken page.
		// RCR: StoreLinks.cs Build — digits check removed → RED (Expected: null But was: "https://apps.apple.com/app/idcom.example.game"). 2026-10-21
		[TestCase(null)]
		[TestCase("")]
		[TestCase("id")]
		[TestCase("com.example.game")]
		public void Build_IosAppIdNotNumeric_ReturnsNull(string appId)
		{
			Assert.IsNull(StoreLinks.Build(RuntimePlatform.IPhonePlayer, appId, "com.example.game", false));
		}

		[Test]
		// ADMIT: StoreLinks.Build could ignore the write-review request on iOS.
		// RCR: StoreLinks.cs Build — review action dropped → RED (Expected string length 51 but was 31. Strings differ at index 31. Expected: "https://ap...). 2026-10-21
		public void Build_IosWriteReview_AddsTheReviewAction()
		{
			Assert.AreEqual(
				"https://apps.apple.com/app/id42?action=write-review",
				StoreLinks.Build(RuntimePlatform.IPhonePlayer, "42", null, true));
		}

		[Test]
		// ADMIT: StoreLinks.Build could build an Android link that appends the iOS review action Google Play does not understand.
		// RCR: StoreLinks.cs Build — review action appended on Android → RED (Expected string length 62 but was 82. Strings differ at index 62. Expected: "...om/stor...). 2026-10-21
		public void Build_Android_UsesThePackageAndIgnoresWriteReview()
		{
			Assert.AreEqual(
				"https://play.google.com/store/apps/details?id=com.example.game",
				StoreLinks.Build(RuntimePlatform.Android, "42", "com.example.game", true));
		}

		[Test]
		// ADMIT: StoreLinks.Build could leave a package name unescaped, corrupting the query string.
		// RCR: StoreLinks.cs Build — package left unescaped → RED (Expected string length 51 but was 49. Strings differ at index 47. Expected: "https://pl...). 2026-10-21
		public void Build_AndroidPackage_IsEscaped()
		{
			Assert.AreEqual(
				"https://play.google.com/store/apps/details?id=a%26b",
				StoreLinks.Build(RuntimePlatform.Android, null, "a&b", false));
		}

		[Test]
		// ADMIT: StoreLinks.Build could return a link for a platform with no store, so the Editor or desktop opens a wrong page.
		// RCR: StoreLinks.cs Build — OSXEditor routed to the Android link → RED (Expected: null But was: "https://play.google.com/store/apps/details?id=com.example.game"). 2026-10-21
		[TestCase(RuntimePlatform.OSXEditor)]
		[TestCase(RuntimePlatform.WindowsPlayer)]
		[TestCase(RuntimePlatform.WebGLPlayer)]
		public void Build_PlatformWithoutStore_ReturnsNull(RuntimePlatform platform)
		{
			Assert.IsNull(StoreLinks.Build(platform, "42", "com.example.game", false));
		}

		[Test]
		// ADMIT: StoreLinks.Build could build an Android link from an empty package name.
		// RCR: StoreLinks.cs Build — empty-package guard removed → RED (Expected: null But was: "https://play.google.com/store/apps/details?id="). 2026-10-21
		public void Build_AndroidWithoutPackage_ReturnsNull()
		{
			Assert.IsNull(StoreLinks.Build(RuntimePlatform.Android, "42", "  ", false));
		}
	}
}

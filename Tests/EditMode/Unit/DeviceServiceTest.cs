using System;
using System.Linq;
using System.Text.RegularExpressions;
using GameLovers.MobileServices.Device;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class DeviceServiceTest
	{
		// Marker interfaces co-implemented with IDisposable so NSubstitute can produce a proxy
		// that satisfies both the umbrella's interface contract AND the IDisposable check inside
		// DeviceService.Dispose. The DeviceService ctor accepts the parent interface; the more-
		// derived disposable variant is implicitly convertible to it.
		public interface IBatteryServiceDisposable  : IBatteryService,  IDisposable { }
		public interface IDeepLinkServiceDisposable : IDeepLinkService, IDisposable { }

		private IBatteryServiceDisposable _battery;
		private IIosAudioSessionService _audioSession;
		private IPermissionsService _permissions;
		private IAttService _att;
		private IDeepLinkServiceDisposable _deepLink;
		private IBackgroundTaskService _backgroundTasks;
		private int _originalSleepTimeout;

		[SetUp]
		public void Init()
		{
			_originalSleepTimeout = Screen.sleepTimeout;
			_battery = Substitute.For<IBatteryServiceDisposable>();
			_audioSession = Substitute.For<IIosAudioSessionService>();
			_permissions = Substitute.For<IPermissionsService>();
			_att = Substitute.For<IAttService>();
			_deepLink = Substitute.For<IDeepLinkServiceDisposable>();
			_backgroundTasks = Substitute.For<IBackgroundTaskService>();
		}

		[TearDown]
		public void Cleanup()
		{
			Screen.sleepTimeout = _originalSleepTimeout;
		}

		[Test]
		// ADMIT: DeviceService's injection ctor could store something other than the supplied instance on a child property.
		// RCR: DeviceService.cs DeviceService(6-arg) — `Att = att` → `Att = new AttService()` → RED (AreSame fails on Att).
		public void InjectionCtor_StoresEachChildOnMatchingProperty()
		{
			var service = new DeviceService(
				_battery,
				_audioSession,
				_permissions,
				_att,
				_deepLink,
				_backgroundTasks);

			Assert.AreSame(_battery, service.Battery);
			Assert.AreSame(_audioSession, service.AudioSession);
			Assert.AreSame(_permissions, service.Permissions);
			Assert.AreSame(_att, service.Att);
			Assert.AreSame(_deepLink, service.DeepLink);
			Assert.AreSame(_backgroundTasks, service.BackgroundTasks);
		}

		[Test]
		// ADMIT: DeviceService.Dispose could skip a disposable child, leaking that child's host registrations.
		// RCR: DeviceService.cs Dispose — drop `(Battery as IDisposable)?.Dispose()` → RED (Battery expected 1 call to Dispose, got 0).
		public void Dispose_DisposesDisposableChildren_OnlyOnce()
		{
			var service = new DeviceService(
				_battery,
				_audioSession,
				_permissions,
				_att,
				_deepLink,
				_backgroundTasks);

			service.Dispose();

			_battery.Received(1).Dispose();
			_deepLink.Received(1).Dispose();
		}

		[Test]
		// ADMIT: DeviceService.Dispose could hard-cast children to IDisposable and throw for a non-disposable implementation.
		// RCR: DeviceService.cs Dispose — `(Battery as IDisposable)?.Dispose()` → `((IDisposable) Battery).Dispose()` → RED (InvalidCastException where none expected).
		public void Dispose_NonDisposableChildren_NoThrow()
		{
			var nonDisposableBattery = Substitute.For<IBatteryService>();
			var nonDisposableDeepLink = Substitute.For<IDeepLinkService>();

			var service = new DeviceService(
				nonDisposableBattery,
				_audioSession,
				_permissions,
				_att,
				nonDisposableDeepLink,
				_backgroundTasks);

			Assert.DoesNotThrow(service.Dispose);
		}

		[Test]
		// ADMIT: DeviceService.KeepAwake = true could fail to prevent the screen from dimming.
		// RCR: DeviceService.cs KeepAwake.set — true branch `NeverSleep` → `SystemSetting` → RED (expected NeverSleep). 2026-08-13
		public void KeepAwake_True_SetsScreenSleepTimeoutNeverSleep()
		{
			DeviceService.KeepAwake = true;

			Assert.AreEqual(SleepTimeout.NeverSleep, Screen.sleepTimeout);
		}

		[Test]
		// ADMIT: DeviceService.KeepAwake = false could leave the screen awake after the application releases the override.
		// RCR: DeviceService.cs KeepAwake.set — false branch `SystemSetting` → `NeverSleep` → RED (expected SystemSetting). 2026-08-13
		public void KeepAwake_False_RestoresSystemSetting()
		{
			DeviceService.KeepAwake = true;
			DeviceService.KeepAwake = false;

			Assert.AreEqual(SleepTimeout.SystemSetting, Screen.sleepTimeout);
		}

		[Test]
		// ADMIT: DeviceService.KeepAwake could report the inverse of the active screen timeout.
		// RCR: DeviceService.cs KeepAwake.get — `==` → `!=` → RED (expected true for NeverSleep). 2026-08-13
		public void KeepAwake_Get_ReflectsScreenSleepTimeout()
		{
			Screen.sleepTimeout = SleepTimeout.NeverSleep;
			Assert.IsTrue(DeviceService.KeepAwake);

			Screen.sleepTimeout = SleepTimeout.SystemSetting;
			Assert.IsFalse(DeviceService.KeepAwake);
		}

		[Test]
		// ADMIT: DeviceService.BuildStoreUrl could keep the `id` prefix twice, producing a store link that opens a missing app.
		// RCR: DeviceService.cs BuildStoreUrl — `id` prefix stripping removed → RED (Expected: "https://apps.apple.com/app/id123456789" But was: null). 2026-10-22
		[TestCase("123456789", "https://apps.apple.com/app/id123456789")]
		[TestCase("id123456789", "https://apps.apple.com/app/id123456789")]
		[TestCase("  ID123456789  ", "https://apps.apple.com/app/id123456789")]
		public void BuildStoreUrl_IosAppId_NormalizesThePrefix(string appId, string expected)
		{
			Assert.AreEqual(expected, DeviceService.BuildStoreUrl(RuntimePlatform.IPhonePlayer, appId, null, false));
		}

		[Test]
		// ADMIT: DeviceService.BuildStoreUrl could build an App Store link from a non-numeric id, opening a broken page.
		// RCR: DeviceService.cs BuildStoreUrl — digits check removed → RED (Expected: null But was: "https://apps.apple.com/app/idcom.example.game"). 2026-10-22
		[TestCase(null)]
		[TestCase("")]
		[TestCase("id")]
		[TestCase("com.example.game")]
		public void BuildStoreUrl_IosAppIdNotNumeric_ReturnsNull(string appId)
		{
			Assert.IsNull(DeviceService.BuildStoreUrl(RuntimePlatform.IPhonePlayer, appId, "com.example.game", false));
		}

		[Test]
		// ADMIT: DeviceService.BuildStoreUrl could ignore the write-review request on iOS.
		// RCR: DeviceService.cs BuildStoreUrl — review action dropped → RED (Expected string length 51 but was 31. Strings differ at index 31.). 2026-10-22
		public void BuildStoreUrl_IosWriteReview_AddsTheReviewAction()
		{
			Assert.AreEqual(
				"https://apps.apple.com/app/id42?action=write-review",
				DeviceService.BuildStoreUrl(RuntimePlatform.IPhonePlayer, "42", null, true));
		}

		[Test]
		// ADMIT: DeviceService.BuildStoreUrl could append the iOS review action to an Android link Google Play does not understand.
		// RCR: DeviceService.cs BuildStoreUrl — review action appended on Android → RED (Expected string length 62 but was 82. Strings differ at index 62.). 2026-10-22
		public void BuildStoreUrl_Android_UsesThePackageAndIgnoresWriteReview()
		{
			Assert.AreEqual(
				"https://play.google.com/store/apps/details?id=com.example.game",
				DeviceService.BuildStoreUrl(RuntimePlatform.Android, "42", "com.example.game", true));
		}

		[Test]
		// ADMIT: DeviceService.BuildStoreUrl could leave a package name unescaped, corrupting the query string.
		// RCR: DeviceService.cs BuildStoreUrl — package left unescaped → RED (Expected string length 51 but was 49. Strings differ at index 47.). 2026-10-22
		public void BuildStoreUrl_AndroidPackage_IsEscaped()
		{
			Assert.AreEqual(
				"https://play.google.com/store/apps/details?id=a%26b",
				DeviceService.BuildStoreUrl(RuntimePlatform.Android, null, "a&b", false));
		}

		[Test]
		// ADMIT: DeviceService.BuildStoreUrl could return a link for a platform with no store, so the Editor or desktop opens a wrong page.
		// RCR: DeviceService.cs BuildStoreUrl — OSXEditor routed to the Android link → RED (Expected: null But was: "https://play.google.com/store/apps/details?id=com.example.game"). 2026-10-22
		[TestCase(RuntimePlatform.OSXEditor)]
		[TestCase(RuntimePlatform.WindowsPlayer)]
		[TestCase(RuntimePlatform.WebGLPlayer)]
		public void BuildStoreUrl_PlatformWithoutStore_ReturnsNull(RuntimePlatform platform)
		{
			Assert.IsNull(DeviceService.BuildStoreUrl(platform, "42", "com.example.game", false));
		}

		[Test]
		// ADMIT: DeviceService.BuildStoreUrl could build an Android link from an empty package name.
		// RCR: DeviceService.cs BuildStoreUrl — empty-package guard removed → RED (Expected: null But was: "https://play.google.com/store/apps/details?id="). 2026-10-22
		public void BuildStoreUrl_AndroidWithoutPackage_ReturnsNull()
		{
			Assert.IsNull(DeviceService.BuildStoreUrl(RuntimePlatform.Android, "42", "  ", false));
		}

		[Test]
		// ADMIT: DeviceService.NormalizeShortcuts could reorder valid shortcuts, so the OS menu does not match the order the game set.
		// RCR: DeviceService.cs NormalizeShortcuts — shortcuts inserted at the front → RED (Expected and actual are both <System.String[3]> Values differ at index [0]). 2026-10-22
		public void NormalizeShortcuts_ValidShortcuts_KeepTheirOrder()
		{
			var result = DeviceService.NormalizeShortcuts(new[] { Shortcut("b"), Shortcut("a"), Shortcut("c") });

			CollectionAssert.AreEqual(new[] { "b", "a", "c" }, result.Select(shortcut => shortcut.Id).ToArray());
		}

		[Test]
		// ADMIT: DeviceService.NormalizeShortcuts could pass a shortcut with no id, title or absolute URI to the OS, which rejects or crashes on it.
		// RCR: DeviceService.cs NormalizeShortcuts — absolute-URI check removed → RED (Expected is <System.String[1]>, actual is <System.String[2]> Values differ at index [0]). 2026-10-22
		public void NormalizeShortcuts_IncompleteShortcuts_AreSkipped()
		{
			LogAssert.Expect(LogType.Warning, new Regex("Skipped shortcut ''"));
			LogAssert.Expect(LogType.Warning, new Regex("Skipped shortcut 'no-title'"));
			LogAssert.Expect(LogType.Warning, new Regex("Skipped shortcut 'relative'"));
			LogAssert.Expect(LogType.Warning, new Regex("Skipped shortcut ''"));

			var result = DeviceService.NormalizeShortcuts(new[]
			{
				new AppShortcut(string.Empty, "Title", "mygame://x"),
				new AppShortcut("no-title", " ", "mygame://x"),
				new AppShortcut("relative", "Title", "daily/reward"),
				null,
				Shortcut("ok"),
			});

			CollectionAssert.AreEqual(new[] { "ok" }, result.Select(shortcut => shortcut.Id).ToArray());
		}

		[Test]
		// ADMIT: DeviceService.NormalizeShortcuts could keep a repeated id, which the OS collapses unpredictably.
		// RCR: DeviceService.cs NormalizeShortcuts — repeated-id check disabled → RED (Expected: 1 But was: 2). 2026-10-22
		public void NormalizeShortcuts_RepeatedId_KeepsTheFirst()
		{
			LogAssert.Expect(LogType.Warning, new Regex("the id is already used"));

			var result = DeviceService.NormalizeShortcuts(
				new[] { new AppShortcut("a", "First", "mygame://1"), new AppShortcut("a", "Second", "mygame://2") });

			Assert.AreEqual(1, result.Count);
			Assert.AreEqual("First", result[0].Title);
		}

		[Test]
		// ADMIT: DeviceService.NormalizeShortcuts could pass more shortcuts than the platforms show, so the extras are dropped by the OS in an unknown order.
		// RCR: DeviceService.cs NormalizeShortcuts — limit check disabled → RED (Expected is <System.String[4]>, actual is <System.String[5]> Values differ at index [4]). 2026-10-22
		public void NormalizeShortcuts_MoreThanTheLimit_KeepsTheFirstOnes()
		{
			LogAssert.Expect(LogType.Warning, new Regex("Skipped shortcut 'e'"));

			var result = DeviceService.NormalizeShortcuts(
				new[] { Shortcut("a"), Shortcut("b"), Shortcut("c"), Shortcut("d"), Shortcut("e") });

			CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, result.Select(shortcut => shortcut.Id).ToArray());
		}

		[Test]
		// ADMIT: DeviceService.NormalizeShortcuts could throw on a null list instead of clearing to nothing.
		// RCR: DeviceService.cs NormalizeShortcuts — null guard removed → RED (System.NullReferenceException : Object reference not set to an instance of an object). 2026-10-22
		public void NormalizeShortcuts_Null_ReturnsEmpty()
		{
			Assert.AreEqual(0, DeviceService.NormalizeShortcuts(null).Count);
		}

		private static AppShortcut Shortcut(string id) => new AppShortcut(id, "Title " + id, "mygame://" + id);
	}
}

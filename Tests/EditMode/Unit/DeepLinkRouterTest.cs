using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class DeepLinkRouterTest
	{
		private DeepLinkService _deepLink;
		private DeepLinkRouter _router;

		[SetUp]
		public void Init()
		{
			_deepLink = new DeepLinkService();
			_router = new DeepLinkRouter(_deepLink);
		}

		[TearDown]
		public void Cleanup()
		{
			_router.Dispose();
			_deepLink.Dispose();
		}

		[Test]
		public void Ctor_NullDeepLink_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new DeepLinkRouter(null));
		}

		[Test]
		public void MapRoute_NullPattern_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => _router.MapRoute(null, (_, __) => { }));
		}

		[Test]
		public void MapRoute_NullHandler_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => _router.MapRoute("/promo/:id", null));
		}

		[Test]
		public void TryDispatch_LiteralRoute_Matches()
		{
			var fired = 0;
			_router.MapRoute("/settings", (_, __) => fired++);
			var ok = _router.TryDispatch(new Uri("myapp://settings"));
			Assert.IsTrue(ok);
			Assert.AreEqual(1, fired);
		}

		[Test]
		public void TryDispatch_CapturedSegment_PopulatesParams()
		{
			IReadOnlyDictionary<string, string> captured = null;
			_router.MapRoute("/promo/:id", (_, p) => captured = p);
			var ok = _router.TryDispatch(new Uri("myapp://promo/spring2026"));
			Assert.IsTrue(ok);
			Assert.IsNotNull(captured);
			Assert.AreEqual("spring2026", captured["id"]);
		}

		[Test]
		public void TryDispatch_MultiCaptured_PopulatesAllParams()
		{
			IReadOnlyDictionary<string, string> captured = null;
			_router.MapRoute("/profile/:userId/post/:postId", (_, p) => captured = p);
			var ok = _router.TryDispatch(new Uri("myapp://profile/abc/post/42"));
			Assert.IsTrue(ok);
			Assert.AreEqual("abc", captured["userId"]);
			Assert.AreEqual("42", captured["postId"]);
		}

		[Test]
		public void TryDispatch_NoMatch_ReturnsFalse()
		{
			_router.MapRoute("/promo/:id", (_, __) => { });
			var ok = _router.TryDispatch(new Uri("myapp://settings"));
			Assert.IsFalse(ok);
		}

		[Test]
		public void TryDispatch_FirstMatchWins()
		{
			var firedFirst = 0;
			var firedSecond = 0;
			_router.MapRoute("/promo/:id", (_, __) => firedFirst++);
			_router.MapRoute("/promo/:id", (_, __) => firedSecond++);
			_router.TryDispatch(new Uri("myapp://promo/x"));
			Assert.AreEqual(1, firedFirst);
			Assert.AreEqual(0, firedSecond);
		}

		[Test]
		public void RemoveRoute_RemovesAllMatchingRegistrations()
		{
			var fired = 0;
			_router.MapRoute("/x", (_, __) => fired++);
			_router.MapRoute("/x", (_, __) => fired++);
			_router.RemoveRoute("/x");
			_router.TryDispatch(new Uri("myapp://x"));
			Assert.AreEqual(0, fired);
		}

		[Test]
		public void TryDispatch_NullUri_ReturnsFalse()
		{
			_router.MapRoute("/x", (_, __) => { });
			Assert.IsFalse(_router.TryDispatch(null));
		}
	}
}

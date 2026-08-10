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
			_router = new DeepLinkRouter(_deepLink, _ => { });
		}

		[TearDown]
		public void Cleanup()
		{
			_router.Dispose();
			_deepLink.Dispose();
		}

		[Test]
		// ADMIT: DeepLinkRouter's constructor could stop rejecting a null IDeepLinkService with ArgumentNullException.
		// RCR: DeepLinkRouter.cs DeepLinkRouter(IDeepLinkService, configure) — throw ArgumentException instead of ArgumentNullException → RED (wrong exception type).
		public void Ctor_NullDeepLink_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new DeepLinkRouter(null, _ => { }));
		}

		[Test]
		// ADMIT: DeepLinkRouter's constructor could accept a null configuration callback and leave a
		// router with no deterministic route setup.
		// RCR: DeepLinkRouter.cs DeepLinkRouter — remove the configure null guard → RED (expected ArgumentNullException).
		public void Ctor_NullConfigure_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new DeepLinkRouter(_deepLink, null));
		}

		[Test]
		// ADMIT: DeepLinkRouter could subscribe before invoking configure, causing a cold-start link to
		// be dispatched before routes exist.
		// RCR: DeepLinkRouter.cs constructor — move `_deepLink.OnLinkActivated += OnLinkActivated` above
		// `configure(this)` → RED (handler never fires for the synchronously replayed link).
		public void Ctor_ConfiguresBeforeSubscription()
		{
			var received = 0;
			var replaying = new ReplayingDeepLinkService(new Uri("myapp://settings"));
			var router = new DeepLinkRouter(replaying, configured =>
				configured.MapRoute("/settings", (_, __) => received++));
			Assert.AreEqual(1, received);
			router.Dispose();
		}

		[Test]
		// ADMIT: DeepLinkRouter could subscribe before invoking a configuration callback that throws,
		// leaving a dangling event handler on the caller-owned service.
		// RCR: DeepLinkRouter.cs constructor — move subscription before configure → RED (add count expected 0, was 1).
		public void Ctor_ConfigurationFailure_DoesNotSubscribe()
		{
			var service = new TrackingDeepLinkService();

			Assert.Throws<InvalidOperationException>(() => new DeepLinkRouter(service, _ =>
				throw new InvalidOperationException("configuration failed")));
			Assert.AreEqual(0, service.AddCount);
		}

		[Test]
		// ADMIT: DeepLinkRouter.MapRoute could stop rejecting a null/empty pattern with ArgumentNullException.
		// RCR: DeepLinkRouter.cs MapRoute — pattern guard throws ArgumentException instead of ArgumentNullException → RED (wrong exception type).
		public void MapRoute_NullPattern_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => _router.MapRoute(null, (_, __) => { }));
		}

		[Test]
		// ADMIT: DeepLinkRouter.MapRoute could stop rejecting a null handler with ArgumentNullException.
		// RCR: DeepLinkRouter.cs MapRoute — handler guard throws ArgumentException instead of ArgumentNullException → RED (wrong exception type).
		public void MapRoute_NullHandler_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => _router.MapRoute("/promo/:id", null));
		}

		[Test]
		// ADMIT: DeepLinkRouter.Route.TryMatch could fail to report a match for an all-literal pattern that captures nothing.
		// RCR: DeepLinkRouter.cs Route.TryMatch — final `return true` → `return dict.Count > 0` → RED (TryDispatch false, handler never fired).
		public void TryDispatch_LiteralRoute_Matches()
		{
			var fired = 0;
			_router.MapRoute("/settings", (_, __) => fired++);
			var ok = _router.TryDispatch(new Uri("myapp://settings"));
			Assert.IsTrue(ok);
			Assert.AreEqual(1, fired);
		}

		[Test]
		// ADMIT: DeepLinkRouter.Route.TryMatch could store the wrong text for a `:name` segment, so handlers receive an empty parameter.
		// RCR: DeepLinkRouter.cs Route.TryMatch — `dict[pat.Substring(1)] = segments[i]` → `= string.Empty` → RED (id expected 'spring2026' was ''). Also reddens TryDispatch_MultiCaptured_PopulatesAllParams.
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
		// ADMIT: DeepLinkRouter.Route.SplitUri could mis-order the path parts it appends after the host, cross-wiring multi-capture routes.
		// RCR: DeepLinkRouter.cs Route.SplitUri — `combined[i + 1] = parts[i]` → `parts[parts.Length - 1 - i]` → RED (userId expected 'abc' was '42').
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
		// ADMIT: DeepLinkRouter.TryDispatch could report success for a URI that matched no registered route.
		// RCR: DeepLinkRouter.cs TryDispatch — trailing `return false` → `return true` → RED (expected False was True).
		public void TryDispatch_NoMatch_ReturnsFalse()
		{
			_router.MapRoute("/promo/:id", (_, __) => { });
			var ok = _router.TryDispatch(new Uri("myapp://settings"));
			Assert.IsFalse(ok);
		}

		[Test]
		// ADMIT: DeepLinkRouter.TryDispatch could walk routes out of registration order, so a later duplicate pattern wins.
		// RCR: DeepLinkRouter.cs TryDispatch — iterate `Enumerable.Reverse(_routes)` → RED (firedFirst expected 1 was 0).
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
		// ADMIT: DeepLinkRouter.RemoveRoute could leave the first of several identically-patterned registrations behind.
		// RCR: DeepLinkRouter.cs RemoveRoute — loop bound `i >= 0` → `i >= 1` → RED (fired expected 0 was 1).
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
		// ADMIT: DeepLinkRouter.TryDispatch could report success for a null URI.
		// RCR: DeepLinkRouter.cs TryDispatch — `if (uri == null) return false` → `return true` → RED (expected False was True).
		public void TryDispatch_NullUri_ReturnsFalse()
		{
			_router.MapRoute("/x", (_, __) => { });
			Assert.IsFalse(_router.TryDispatch(null));
		}

		[Test]
		// ADMIT: DeepLinkRouter.Dispose could leave its public route operations callable after ownership ends.
		// RCR: DeepLinkRouter.cs ThrowIfDisposed — replace ObjectDisposedException with return → RED
		// (expected ObjectDisposedException).
		public void Dispose_IsIdempotentAndGuardsOperations()
		{
			_router.Dispose();
			Assert.DoesNotThrow(_router.Dispose);
			Assert.Throws<ObjectDisposedException>(() => _router.MapRoute("/x", (_, __) => { }));
			Assert.Throws<ObjectDisposedException>(() => _router.RemoveRoute("/x"));
			Assert.Throws<ObjectDisposedException>(() => _router.TryDispatch(new Uri("myapp://x")));
		}

		private sealed class ReplayingDeepLinkService : IDeepLinkService
		{
			private readonly Uri _coldStart;
			private bool _replayed;

			public event Action<Uri> OnLinkActivated
			{
				add
				{
					if (_replayed) return;
					_replayed = true;
					value(_coldStart);
				}
				remove { }
			}

			public Uri PendingColdStartLink => _replayed ? null : _coldStart;

			public ReplayingDeepLinkService(Uri coldStart) => _coldStart = coldStart;
		}

		private sealed class TrackingDeepLinkService : IDeepLinkService
		{
			public int AddCount { get; private set; }

			public event Action<Uri> OnLinkActivated
			{
				add
				{
					AddCount++;
				}
				remove { }
			}

			public Uri PendingColdStartLink => null;
		}
	}
}

using System;
using GameLovers.MobileServices.Device;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class DeepLinkServiceTest
	{
		private DeepLinkService _service;

		[SetUp]
		public void Init()
		{
			_service = new DeepLinkService();
		}

		[TearDown]
		public void Dispose()
		{
			_service.Dispose();
		}

		[Test]
		// ADMIT: DeepLinkService's ctor must leave PendingColdStartLink null when the process was not cold-launched with a link.
		// RCR: none exists — an empty Application.absoluteURL trips both the ctor's `!string.IsNullOrEmpty` guard and
		// TryParse's own `IsNullOrEmpty` guard (and Uri.TryCreate("") would still fail); disabling either leaves the
		// result null (verified). Double-covered, not single-line falsifiable.
		public void Ctor_NoColdStartUrl_PendingColdStartLinkIsNull()
		{
			// Application.absoluteURL is empty in the EditMode harness — see Tests/AGENTS.md §9.
			Assert.IsNull(_service.PendingColdStartLink);
		}

		[Test]
		// ADMIT: DeepLinkService's OnLinkActivated add accessor could replay a link to a new subscriber when no cold-start link is pending.
		// RCR: DeepLinkService.cs OnLinkActivated.add — invoke `value(new Uri("myapp://cold"))` inside the no-pending-link branch → RED (received expected null).
		public void Subscribe_NoColdStartLink_HandlerNotInvoked()
		{
			Uri received = null;
			Action<Uri> handler = uri => received = uri;

			_service.OnLinkActivated += handler;

			Assert.IsNull(received);

			_service.OnLinkActivated -= handler;
		}

	}
}

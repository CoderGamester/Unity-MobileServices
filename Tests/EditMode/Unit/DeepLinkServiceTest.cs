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
		public void Ctor_NoColdStartUrl_PendingColdStartLinkIsNull()
		{
			// Application.absoluteURL is empty in the EditMode harness — see Tests/AGENTS.md §9.
			Assert.IsNull(_service.PendingColdStartLink);
		}

		[Test]
		public void Subscribe_NoColdStartLink_HandlerNotInvoked()
		{
			Uri received = null;
			Action<Uri> handler = uri => received = uri;

			_service.OnLinkActivated += handler;

			Assert.IsNull(received);

			_service.OnLinkActivated -= handler;
		}

		[Test]
		public void Dispose_DoesNotThrow_AndPendingColdStartLinkIsNull()
		{
			Assert.DoesNotThrow(_service.Dispose);
			Assert.IsNull(_service.PendingColdStartLink);
		}
	}
}

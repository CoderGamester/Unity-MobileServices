using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Device.Internal;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	public class ConnectivityServiceTest
	{
		private ConnectivityService _service;

		[SetUp]
		public void Init()
		{
			_service = new ConnectivityService(DeviceServicesHost.Instance);
		}

		[TearDown]
		public void Cleanup()
		{
			_service.Dispose();
			DeviceServicesHost.ResetForTests();
		}

		[Test]
		public void Ctor_CapturesInitialReachability()
		{
			Assert.AreEqual(Application.internetReachability, _service.Status);
		}

		[Test]
		public void Dispose_UnregistersFromHost()
		{
			Assert.DoesNotThrow(_service.Dispose);
			Assert.DoesNotThrow(_service.Dispose);
		}
	}
}

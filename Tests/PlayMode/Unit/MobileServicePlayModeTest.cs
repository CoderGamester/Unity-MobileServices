using GameLovers.MobileServices;
using GameLovers.MobileServices.Device.Internal;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	public class MobileServicePlayModeTest
	{
		private MobileService _mobile;

		[SetUp]
		public void Init()
		{
			_mobile = new MobileService();
		}

		[TearDown]
		public void Cleanup()
		{
			_mobile.Dispose();

			var go = GameObject.Find("NotificationService");
			if (go != null)
			{
				Object.Destroy(go);
			}

			DeviceServicesHost.ResetForTests();
		}

		[Test]
		public void DefaultCtor_WiresAllChildren_NonNull()
		{
			Assert.IsNotNull(_mobile.NativeUi);
			Assert.IsNotNull(_mobile.Notifications);
			Assert.IsNotNull(_mobile.Haptics);
			Assert.IsNotNull(_mobile.Device);
		}
	}
}

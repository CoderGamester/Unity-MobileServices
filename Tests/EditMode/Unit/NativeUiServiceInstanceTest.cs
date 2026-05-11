using GameLovers.MobileServices.NativeUi;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class NativeUiServiceInstanceTest
	{
		private NativeUiServiceInstance _instance;

		[SetUp]
		public void Init()
		{
			_instance = new NativeUiServiceInstance();
		}

		[Test]
		public void ImplementsInterface()
		{
			Assert.IsInstanceOf<INativeUiService>(_instance);
		}

		[Test]
		public void ShowAlertPopUp_DoesNotThrowInEditor()
		{
			Assert.DoesNotThrow(() => _instance.ShowAlertPopUp(
				false, "T", "M",
				new AlertButton { Text = "OK", Style = AlertButtonStyle.Default }));
		}

		[Test]
		public void ShowToastMessage_DoesNotThrowInEditor()
		{
			Assert.DoesNotThrow(() => _instance.ShowToastMessage("hi", false));
		}

		[Test]
		public void RequestReview_DoesNotThrowInEditor()
		{
			Assert.DoesNotThrow(() => _instance.RequestReview());
		}

		[Test]
		public void Share_DoesNotThrowInEditor()
		{
			Assert.DoesNotThrow(() => _instance.Share("text", "url"));
		}
	}
}

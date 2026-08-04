using GameLovers.MobileServices.NativeUi;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class NativeUiServiceInstanceTest
	{
		private NativeUiServiceInstance _instance;

		[SetUp]
		public void Init()
		{
			// Process-wide static the Device Simulator installs and never clears on its own; without
			// this, RequestReview would route to the simulator mock instead of the editor no-op log.
			NativeUiService.EditorRequestReviewOverride = null;
			_instance = new NativeUiServiceInstance();
		}

		[Test]
		// ADMIT: NativeUiServiceInstance.ShowAlertPopUp must forward to NativeUiService; an empty body is
		// invisible to Assert.DoesNotThrow, so the editor diagnostic is the only observable forwarding proof.
		// RCR: INativeUiService.cs NativeUiServiceInstance.ShowAlertPopUp - replace the expression body with
		// `{ }` -> RED (expected log message was never received). 2026-08-04
		public void ShowAlertPopUp_ForwardsToStaticService()
		{
			LogAssert.Expect(LogType.Log,
				"Show Alert Pop Up is not available in the editor and was triggered with: T - M");

			_instance.ShowAlertPopUp(false, "T", "M",
				new AlertButton { Text = "OK", Style = AlertButtonStyle.Default });
		}

		[Test]
		// ADMIT: NativeUiServiceInstance.ShowToastMessage must forward to NativeUiService; an empty body is
		// invisible to Assert.DoesNotThrow.
		// RCR: INativeUiService.cs NativeUiServiceInstance.ShowToastMessage - replace the expression body
		// with `{ }` -> RED (expected log message was never received). 2026-08-04
		public void ShowToastMessage_ForwardsToStaticService()
		{
			LogAssert.Expect(LogType.Log,
				"Show Toast message is not available in the editor and was triggered with: hi");

			_instance.ShowToastMessage("hi", false);
		}

		[Test]
		// ADMIT: NativeUiServiceInstance.RequestReview must forward to NativeUiService; an empty body is
		// invisible to Assert.DoesNotThrow.
		// RCR: INativeUiService.cs NativeUiServiceInstance.RequestReview - replace the expression body with
		// `{ }` -> RED (expected log message was never received). SetUp nulls the process-wide
		// NativeUiService.EditorRequestReviewOverride, which would otherwise reroute the call. 2026-08-04
		public void RequestReview_ForwardsToStaticService()
		{
			LogAssert.Expect(LogType.Log,
				"[GameLovers.MobileServices] RequestReview() is a no-op in the editor unless the Mobile Services Device Simulator is enabled.");

			_instance.RequestReview();
		}

		[Test]
		// ADMIT: NativeUiServiceInstance.Share must forward to NativeUiService with its optional args intact;
		// an empty body is invisible to Assert.DoesNotThrow.
		// RCR: INativeUiService.cs NativeUiServiceInstance.Share - replace the expression body with `{ }` ->
		// RED (expected log message was never received). 2026-08-04
		public void Share_ForwardsToStaticService()
		{
			LogAssert.Expect(LogType.Log,
				"Share is not available in the editor (text='text', url='url', imagePath='').");

			_instance.Share("text", "url");
		}
	}
}

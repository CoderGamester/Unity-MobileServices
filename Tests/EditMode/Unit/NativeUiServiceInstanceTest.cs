using System;
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
		private Action<bool, bool, string, string, AlertButton[]> _showAlertOverride;
		private Action _dismissAlertOverride;

		[SetUp]
		public void Init()
		{
			_showAlertOverride = NativeUiService.EditorShowAlertOverride;
			_dismissAlertOverride = NativeUiService.EditorDismissAlertOverride;
			NativeUiService.EditorShowAlertOverride = null;
			NativeUiService.EditorDismissAlertOverride = null;
			// Process-wide static the Device Simulator installs and never clears on its own; without
			// this, RequestReview would route to the simulator mock instead of the editor no-op log.
			NativeUiService.EditorRequestReviewOverride = null;
			_instance = new NativeUiServiceInstance();
		}

		[TearDown]
		public void Cleanup()
		{
			NativeUiService.DismissAlertPopUp();
			NativeUiService.EditorShowAlertOverride = _showAlertOverride;
			NativeUiService.EditorDismissAlertOverride = _dismissAlertOverride;
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
		// ADMIT: NativeUiServiceInstance.ShowAlertPopUpAsync could fail to forward the selected button result.
		// RCR: INativeUiService.cs NativeUiServiceInstance.ShowAlertPopUpAsync — return a never-completed Awaitable → RED (awaiter incomplete). 2026-08-14
		public void ShowAlertPopUpAsync_ForwardsSelectedIndex()
		{
			NativeUiService.EditorShowAlertOverride = (_, _, _, _, buttons) => buttons[0].Callback();

			Awaitable<int> operation = _instance.ShowAlertPopUpAsync(
				false,
				true,
				"T",
				"M",
				new AlertButton { Text = "OK", Style = AlertButtonStyle.Default });
			var awaiter = operation.GetAwaiter();

			Assert.IsTrue(awaiter.IsCompleted);
			Assert.AreEqual(0, awaiter.GetResult());
		}

		[Test]
		// ADMIT: NativeUiServiceInstance.DismissAlertPopUp must forward to the static service.
		// RCR: INativeUiService.cs NativeUiServiceInstance.DismissAlertPopUp — replace the expression body with `{ }` → RED (callback count remains zero). 2026-08-13
		public void DismissAlertPopUp_ForwardsToStaticService()
		{
			var callbackCount = 0;
			NativeUiService.EditorDismissAlertOverride = () => callbackCount++;

			_instance.DismissAlertPopUp();

			Assert.AreEqual(1, callbackCount);
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

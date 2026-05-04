using System;
using GameLovers.MobileServices.NativeUi;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class NativeUiServiceTest
	{
		[Test]
		public void ShowAlertPopUp_InEditor_LogsAndDoesNotThrow()
		{
			LogAssert.Expect(LogType.Log, "Show Alert Pop Up is not available in the editor and was triggered with: T - M");

			Assert.DoesNotThrow(() =>
			{
				NativeUiService.ShowAlertPopUp(
					false,
					"T",
					"M",
					new AlertButton { Text = "OK", Style = AlertButtonStyle.Default });
			});
		}

		[Test]
		public void ShowToastMessage_InEditor_LogsAndDoesNotThrow()
		{
			LogAssert.Expect(LogType.Log, "Show Toast message is not available in the editor and was triggered with: hello");

			Assert.DoesNotThrow(() => NativeUiService.ShowToastMessage("hello", false));
		}

		[Test]
		public void RequestReview_InEditor_LogsAndDoesNotThrow()
		{
			LogAssert.Expect(LogType.Log, "Request Review is not available in the editor.");

			Assert.DoesNotThrow(NativeUiService.RequestReview);
		}

		[Test]
		public void Share_InEditor_LogsAndDoesNotThrow()
		{
			LogAssert.Expect(LogType.Log, "Share is not available in the editor (text='hello', url='https://example.com', imagePath='').");

			Assert.DoesNotThrow(() => NativeUiService.Share("hello", "https://example.com"));
		}

		[Test]
		public void Share_NullOptionalArgs_InEditor_DoesNotThrow()
		{
			LogAssert.Expect(LogType.Log, "Share is not available in the editor (text='hi', url='', imagePath='').");

			Assert.DoesNotThrow(() => NativeUiService.Share("hi"));
		}

		[Test]
		public void AlertButton_FieldRoundTrip()
		{
			Action callback = () => { };
			var button = new AlertButton
			{
				Text = "Cancel",
				Style = AlertButtonStyle.Cancel,
				Callback = callback,
			};

			Assert.AreEqual("Cancel", button.Text);
			Assert.AreEqual(AlertButtonStyle.Cancel, button.Style);
			Assert.AreSame(callback, button.Callback);
		}
	}
}

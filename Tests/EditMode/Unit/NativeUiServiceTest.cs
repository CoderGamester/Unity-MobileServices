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
		// ADMIT: NativeUiService.ShowAlertPopUp could change the Editor diagnostic that stands in for the native alert.
		// RCR: NativeUiService.cs ShowAlertPopUp — editor log separator `{title} - {message}` → `{title} / {message}` → RED (LogAssert expected message not received).
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
		// ADMIT: NativeUiService.ShowToastMessage could change the Editor diagnostic that stands in for the native toast.
		// RCR: NativeUiService.cs ShowToastMessage — editor log text `Show Toast message` → `Show Toast msg` → RED (LogAssert expected message not received).
		public void ShowToastMessage_InEditor_LogsAndDoesNotThrow()
		{
			LogAssert.Expect(LogType.Log, "Show Toast message is not available in the editor and was triggered with: hello");

			Assert.DoesNotThrow(() => NativeUiService.ShowToastMessage("hello", false));
		}

		[Test]
		// ADMIT: NativeUiService.RequestReview could change the no-override Editor diagnostic, the only signal the review prompt was requested.
		// RCR: NativeUiService.cs RequestReview — editor log text `the Mobile Services Device Simulator` → `the Device Simulator` → RED (LogAssert expected message not received).
		public void RequestReview_InEditor_LogsAndDoesNotThrow()
		{
			LogAssert.Expect(LogType.Log, "[GameLovers.MobileServices] RequestReview() is a no-op in the editor unless the Mobile Services Device Simulator is enabled.");

			Assert.DoesNotThrow(NativeUiService.RequestReview);
		}

		[Test]
		// ADMIT: NativeUiService.Share could mangle the url it echoes in the Editor diagnostic, hiding what would have been shared.
		// RCR: NativeUiService.cs Share — editor log `url='{url}'` → `url='{url?.ToUpperInvariant()}'` → RED (LogAssert expected url='https://example.com').
		public void Share_InEditor_LogsAndDoesNotThrow()
		{
			LogAssert.Expect(LogType.Log, "Share is not available in the editor (text='hello', url='https://example.com', imagePath='').");

			Assert.DoesNotThrow(() => NativeUiService.Share("hello", "https://example.com"));
		}

		[Test]
		// ADMIT: NativeUiService.Share could substitute a placeholder for an omitted url instead of echoing it as empty.
		// RCR: NativeUiService.cs Share — editor log `url='{url}'` → `url='{(url == null ? "none" : url)}'` → RED (LogAssert expected url='').
		public void Share_NullOptionalArgs_InEditor_DoesNotThrow()
		{
			LogAssert.Expect(LogType.Log, "Share is not available in the editor (text='hi', url='', imagePath='').");

			Assert.DoesNotThrow(() => NativeUiService.Share("hi"));
		}

	}
}

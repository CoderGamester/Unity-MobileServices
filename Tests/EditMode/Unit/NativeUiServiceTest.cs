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
		private Action<bool, bool, string, string, AlertButton[]> _showAlertOverride;
		private Action _dismissAlertOverride;

		[SetUp]
		public void Init()
		{
			_showAlertOverride = NativeUiService.EditorShowAlertOverride;
			_dismissAlertOverride = NativeUiService.EditorDismissAlertOverride;
			NativeUiService.EditorShowAlertOverride = null;
			NativeUiService.EditorDismissAlertOverride = null;
		}

		[TearDown]
		public void Cleanup()
		{
			NativeUiService.DismissAlertPopUp();
			NativeUiService.EditorShowAlertOverride = _showAlertOverride;
			NativeUiService.EditorDismissAlertOverride = _dismissAlertOverride;
		}

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
		// ADMIT: NativeUiService.ShowAlertPopUp must forward the dismissibility contract to the Editor renderer.
		// RCR: NativeUiService.cs ShowAlertPopUp — pass `true` instead of `isDismissible` to EditorShowAlertOverride → RED (captured dismissibility differs). 2026-08-13
		public void ShowAlertPopUp_NonDismissible_ForwardsContractToEditorOverride()
		{
			var capturedDismissible = true;
			NativeUiService.EditorShowAlertOverride = (_, isDismissible, _, _, _) =>
				capturedDismissible = isDismissible;

			NativeUiService.ShowAlertPopUp(
				false,
				false,
				"T",
				"M",
				new AlertButton { Text = "OK", Style = AlertButtonStyle.Default });

			Assert.IsFalse(capturedDismissible);
		}

		[Test]
		// ADMIT: NativeUiService.ShowAlertPopUp must reject button styles that overwrite the same Android slot.
		// RCR: NativeUiService.cs ValidateAlert — remove the duplicate-style comparison → RED (no ArgumentException). 2026-08-13
		public void ShowAlertPopUp_DuplicateButtonStyles_ThrowsArgumentException()
		{
			var buttons = new[]
			{
				new AlertButton { Text = "One", Style = AlertButtonStyle.Default },
				new AlertButton { Text = "Two", Style = AlertButtonStyle.Default },
			};

			Assert.Throws<ArgumentException>(() =>
				NativeUiService.ShowAlertPopUp(false, "T", "M", buttons));
		}

		[Test]
		// ADMIT: NativeUiService.DismissAlertPopUp must route an Editor dismissal to the installed renderer.
		// RCR: NativeUiService.cs DismissAlertPopUp — remove `EditorDismissAlertOverride?.Invoke()` → RED (callback count remains zero). 2026-08-13
		public void DismissAlertPopUp_EditorOverride_InvokesCallback()
		{
			var callbackCount = 0;
			NativeUiService.EditorDismissAlertOverride = () => callbackCount++;

			NativeUiService.DismissAlertPopUp();

			Assert.AreEqual(1, callbackCount);
		}

		[Test]
		// ADMIT: NativeUiService.DismissAlertPopUp could leave an async alert awaiting forever.
		// RCR: NativeUiService.cs DismissAlertPopUp — remove `CancelCurrentAlert()` → RED (awaiter incomplete). 2026-08-14
		public void DismissAlertPopUp_AsyncAlert_CancelsAwait()
		{
			NativeUiService.EditorShowAlertOverride = (_, _, _, _, _) => { };
			Awaitable<int> operation = NativeUiService.ShowAlertPopUpAsync(
				false,
				true,
				"T",
				"M",
				new AlertButton { Text = "OK", Style = AlertButtonStyle.Default });
			var awaiter = operation.GetAwaiter();

			NativeUiService.DismissAlertPopUp();

			Assert.IsTrue(awaiter.IsCompleted);
			Assert.Throws<OperationCanceledException>(() => awaiter.GetResult());
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

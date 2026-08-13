using System;
using System.Collections;
using System.Threading;
using GameLovers.MobileServices.NativeUi;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class NativeUiServicePlayModeTest
	{
		private Action<bool, bool, string, string, AlertButton[]> _showAlertOverride;

		[SetUp]
		public void Init()
		{
			_showAlertOverride = NativeUiService.EditorShowAlertOverride;
		}

		[TearDown]
		public void Cleanup()
		{
			NativeUiService.DismissAlertPopUp();
			NativeUiService.EditorShowAlertOverride = _showAlertOverride;
		}

		[UnityTest]
		// ADMIT: NativeUiService.ShowAlertPopUp could stop observing the shared async alert path and lose legacy callbacks.
		// RCR: NativeUiService.cs ShowAlertPopUp — replace the BeginAlert call with `CancelCurrentAlert()` → RED (native alert was not presented). 2026-08-14
		public IEnumerator ShowAlertPopUp_ForeignSelection_InvokesCallbackOnUnityMainThread()
		{
			int mainThreadId = Thread.CurrentThread.ManagedThreadId;
			int callbackThreadId = 0;
			AlertButton[] renderedButtons = null;
			NativeUiService.EditorShowAlertOverride = (_, _, _, _, buttons) => renderedButtons = buttons;

			NativeUiService.ShowAlertPopUp(
				false,
				true,
				"Title",
				"Message",
				new AlertButton
				{
					Text = "Continue",
					Style = AlertButtonStyle.Default,
					Callback = () => callbackThreadId = Thread.CurrentThread.ManagedThreadId,
				});
			Assert.IsNotNull(renderedButtons, "Native alert was not presented.");
			var thread = new Thread(renderedButtons[0].Callback.Invoke);

			thread.Start();
			thread.Join();
			yield return null;

			Assert.AreEqual(mainThreadId, callbackThreadId);
		}

		[UnityTest]
		// ADMIT: NativeUiService.ShowAlertPopUpAsync could resume consumer code on Android's OS UI thread.
		// RCR: NativeUiService.cs CompleteAlertAsync — remove `await Awaitable.MainThreadAsync()` → RED (operation completed on worker thread). 2026-08-14
		public IEnumerator ShowAlertPopUpAsync_ForeignSelection_CompletesOnUnityMainThread()
		{
			int mainThreadId = Thread.CurrentThread.ManagedThreadId;
			int callbackThreadId = 0;
			AlertButton[] renderedButtons = null;
			NativeUiService.EditorShowAlertOverride = (_, _, _, _, buttons) => renderedButtons = buttons;
			Awaitable<int> operation = NativeUiService.ShowAlertPopUpAsync(
				false,
				true,
				"Title",
				"Message",
				new AlertButton
				{
					Text = "Continue",
					Style = AlertButtonStyle.Default,
					Callback = () => callbackThreadId = Thread.CurrentThread.ManagedThreadId,
				});
			var awaiter = operation.GetAwaiter();
			var thread = new Thread(renderedButtons[0].Callback.Invoke);

			thread.Start();
			thread.Join();

			Assert.IsFalse(awaiter.IsCompleted);
			yield return null;

			Assert.IsTrue(awaiter.IsCompleted);
			Assert.AreEqual(0, awaiter.GetResult());
			Assert.AreEqual(mainThreadId, callbackThreadId);
		}
	}
}

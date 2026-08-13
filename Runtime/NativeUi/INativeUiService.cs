using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.NativeUi
{
	/// <summary>
	/// Instance-based wrapper over <see cref="NativeUiService"/> for consumers who want to mock /
	/// inject the native UI surface. The static <see cref="NativeUiService"/> remains the primary
	/// API surface and stays the recommended path; this interface only exists to unblock test
	/// substitution.
	/// </summary>
	public interface INativeUiService
	{
		/// <inheritdoc cref="NativeUiService.ShowAlertPopUp(bool,string,string,AlertButton[])"/>
		void ShowAlertPopUp(bool isAlertSheet, string title, string message, params AlertButton[] buttons);

		/// <inheritdoc cref="NativeUiService.ShowAlertPopUp(bool,bool,string,string,AlertButton[])"/>
		void ShowAlertPopUp(
			bool isAlertSheet,
			bool isDismissible,
			string title,
			string message,
			params AlertButton[] buttons);

		/// <inheritdoc cref="NativeUiService.ShowAlertPopUpAsync(bool,bool,string,string,AlertButton[])"/>
		Awaitable<int> ShowAlertPopUpAsync(
			bool isAlertSheet,
			bool isDismissible,
			string title,
			string message,
			params AlertButton[] buttons);

		/// <inheritdoc cref="NativeUiService.DismissAlertPopUp"/>
		void DismissAlertPopUp();

		/// <inheritdoc cref="NativeUiService.ShowToastMessage(string,bool)"/>
		void ShowToastMessage(string message, bool isLongDuration);

		/// <inheritdoc cref="NativeUiService.RequestReview"/>
		void RequestReview();

		/// <inheritdoc cref="NativeUiService.Share(string,string,string,string)"/>
		void Share(string text, string url = null, string imagePath = null, string title = null);
	}

	/// <summary>
	/// Default implementation that forwards every call to the existing static
	/// <see cref="NativeUiService"/>. Plain class, no fields, safe to construct any number of times.
	/// </summary>
	public sealed class NativeUiServiceInstance : INativeUiService
	{
		/// <inheritdoc />
		public void ShowAlertPopUp(bool isAlertSheet, string title, string message, params AlertButton[] buttons)
			=> NativeUiService.ShowAlertPopUp(isAlertSheet, title, message, buttons);

		/// <inheritdoc />
		public void ShowAlertPopUp(
			bool isAlertSheet,
			bool isDismissible,
			string title,
			string message,
			params AlertButton[] buttons)
			=> NativeUiService.ShowAlertPopUp(isAlertSheet, isDismissible, title, message, buttons);

		/// <inheritdoc />
		public Awaitable<int> ShowAlertPopUpAsync(
			bool isAlertSheet,
			bool isDismissible,
			string title,
			string message,
			params AlertButton[] buttons)
			=> NativeUiService.ShowAlertPopUpAsync(isAlertSheet, isDismissible, title, message, buttons);

		/// <inheritdoc />
		public void DismissAlertPopUp() => NativeUiService.DismissAlertPopUp();

		/// <inheritdoc />
		public void ShowToastMessage(string message, bool isLongDuration)
			=> NativeUiService.ShowToastMessage(message, isLongDuration);

		/// <inheritdoc />
		public void RequestReview() => NativeUiService.RequestReview();

		/// <inheritdoc />
		public void Share(string text, string url = null, string imagePath = null, string title = null)
			=> NativeUiService.Share(text, url, imagePath, title);
	}
}

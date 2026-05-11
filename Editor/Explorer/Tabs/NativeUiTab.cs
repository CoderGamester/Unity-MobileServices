using System.Collections.Generic;
using GameLovers.MobileServices.Editor.Explorer.Overlays;
using GameLovers.MobileServices.NativeUi;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Tabs
{
	/// <summary>Native UI driver tab — fires the real <see cref="NativeUiService"/> and the matching simulator mocks.</summary>
	public sealed class NativeUiTab : MobileServiceTab
	{
		public override string DisplayName => "Native UI";
		protected override int RefreshIntervalMs => 1000;

		private TextField _alertTitle;
		private TextField _alertMessage;
		private TextField _toastMessage;
		private Toggle _toastLongDuration;
		private TextField _shareText;
		private TextField _shareUrl;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			scroll.Add(MakeSectionLabel("Alerts"));

			_alertTitle = new TextField("Title") { value = "Delete Save?" };
			_alertMessage = new TextField("Message") { value = "This action cannot be undone." };
			scroll.Add(_alertTitle);
			scroll.Add(_alertMessage);

			var alertRow = new VisualElement();
			alertRow.style.flexDirection = FlexDirection.Row;
			alertRow.Add(MakePrimaryButton("Show Alert (modal)", () => PushAlert(isSheet: false)));
			alertRow.Add(MakePrimaryButton("Show Action Sheet", () => PushAlert(isSheet: true)));
			scroll.Add(alertRow);

			scroll.Add(MakeSectionLabel("Toasts"));

			_toastMessage = new TextField("Message") { value = "Item Collected!" };
			_toastLongDuration = new Toggle("Long duration") { value = false };
			scroll.Add(_toastMessage);
			scroll.Add(_toastLongDuration);

			var toastBtn = MakePrimaryButton("Show Toast", () =>
			{
				MobileSimulatorState.PushToast(new SimulatedToastSpec
				{
					Message = _toastMessage.value,
					IsLongDuration = _toastLongDuration.value,
				});
				NativeUiService.ShowToastMessage(_toastMessage.value, _toastLongDuration.value);
			});
			scroll.Add(toastBtn);

			scroll.Add(MakeSectionLabel("Review"));
			scroll.Add(MakePrimaryButton("Request Review", () =>
			{
				MobileSimulatorState.PushReview();
				NativeUiService.RequestReview();
			}));

			scroll.Add(MakeSectionLabel("Share"));

			_shareText = new TextField("Text") { value = "Check out my high score!" };
			_shareUrl = new TextField("URL") { value = "https://example.com/game" };
			scroll.Add(_shareText);
			scroll.Add(_shareUrl);

			scroll.Add(MakePrimaryButton("Share", () =>
			{
				MobileSimulatorState.PushShare(new SimulatedShareSpec
				{
					Text = _shareText.value,
					Url = _shareUrl.value,
				});
				NativeUiService.Share(_shareText.value, _shareUrl.value);
			}));

			var bar = MakeActionBar();
			bar.Add(MakePrimaryDangerButton("Dismiss All Mocks", () => MobileSimulatorState.PushDismissAll()));
			scroll.Add(bar);

			Add(scroll);
		}

		protected override void Refresh() { }

		private void PushAlert(bool isSheet)
		{
			var spec = new SimulatedAlertSpec
			{
				Title = _alertTitle.value,
				Message = _alertMessage.value,
				IsActionSheet = isSheet,
				Buttons = new List<SimulatedAlertButton>
				{
					new SimulatedAlertButton { Text = "Cancel", Style = SimulatedAlertButtonStyle.Cancel },
					new SimulatedAlertButton { Text = "Delete", Style = SimulatedAlertButtonStyle.Destructive },
				},
			};
			MobileSimulatorState.PushAlert(spec);
			NativeUiService.ShowAlertPopUp(isSheet, _alertTitle.value, _alertMessage.value,
				new AlertButton { Text = "Cancel", Style = AlertButtonStyle.Cancel },
				new AlertButton { Text = "Delete", Style = AlertButtonStyle.Destructive });
		}
	}
}

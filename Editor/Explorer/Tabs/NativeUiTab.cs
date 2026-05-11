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

		// Mock-side toast auto-dismiss timings (mirror MobileSimulatorWindow / MobileSimulatorRuntimeOverlay).
		// Match Android's Toast.LENGTH_SHORT (~2s) / LENGTH_LONG (~3.5s); on iOS the package fakes a toast since
		// there's no native equivalent, but the durations are kept consistent for editor preview purposes.
		private const float ToastShortSeconds = 2.0f;
		private const float ToastLongSeconds = 3.5f;
		private const string ToastDurationTooltip =
			"Mirrors Android Toast.LENGTH_SHORT (~2s) and LENGTH_LONG (~3.5s). " +
			"iOS has no native Toast — the package fakes one and uses the same timings for editor preview parity.";

		// Android has no native action-sheet idiom — UIAlertController(.actionSheet) is iOS-only and on
		// Android both shapes resolve to the same Material 3 dialog (see AGENTS.md §4 / MockBuilders.BuildAlert).
		// Greying the button on Android (with this tooltip) makes that contract discoverable instead of leaving
		// the user wondering why the simulator paints the same shape for both buttons.
		private const string ActionSheetAndroidDisabledTooltip =
			"Disabled on Android: the OS has no native action-sheet idiom. " +
			"On a real device, ShowAlertPopUp(isActionSheet: true) collapses to the same Material 3 dialog as the modal alert. " +
			"Switch the platform to iOS in the header to drive the distinct sheet shape.";

		private TextField _alertTitle;
		private TextField _alertMessage;
		private Button _actionSheetBtn;
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

			_actionSheetBtn = MakePrimaryButton("Show Action Sheet", () => PushAlert(isSheet: true));
			scroll.Add(MakePrimaryButtonRow(
				MakePrimaryButton("Show Alert (modal)", () => PushAlert(isSheet: false)),
				_actionSheetBtn));

			ApplyActionSheetButtonState(MobileSimulatorState.WindowPlatform);
			MobileSimulatorState.WindowPlatformChanged += OnWindowPlatformChanged;
			RegisterCallback<DetachFromPanelEvent>(_ =>
				MobileSimulatorState.WindowPlatformChanged -= OnWindowPlatformChanged);

			scroll.Add(MakeSectionLabel("Toasts"));

			_toastMessage = new TextField("Message") { value = "Item Collected!" };
			_toastLongDuration = new Toggle($"Long duration ({ToastLongSeconds:0.0}s vs {ToastShortSeconds:0.0}s)")
			{
				value = false,
				tooltip = ToastDurationTooltip,
			};
			scroll.Add(_toastMessage);
			scroll.Add(_toastLongDuration);

			scroll.Add(MakePrimaryButtonRow(MakePrimaryButton("Show Toast", () =>
			{
				MobileSimulatorState.PushToast(new SimulatedToastSpec
				{
					Message = _toastMessage.value,
					IsLongDuration = _toastLongDuration.value,
				});
				NativeUiService.ShowToastMessage(_toastMessage.value, _toastLongDuration.value);
			})));

			scroll.Add(MakeSectionLabel("Review"));
			scroll.Add(MakePrimaryButtonRow(MakePrimaryButton("Request Review", () =>
			{
				MobileSimulatorState.PushReview();
				NativeUiService.RequestReview();
			})));

			scroll.Add(MakeSectionLabel("Share"));

			_shareText = new TextField("Text") { value = "Check out my high score!" };
			_shareUrl = new TextField("URL") { value = "https://example.com/game" };
			scroll.Add(_shareText);
			scroll.Add(_shareUrl);

			scroll.Add(MakePrimaryButtonRow(MakePrimaryButton("Share", () =>
			{
				MobileSimulatorState.PushShare(new SimulatedShareSpec
				{
					Text = _shareText.value,
					Url = _shareUrl.value,
				});
				NativeUiService.Share(_shareText.value, _shareUrl.value);
			})));

			var bar = MakeActionBar();
			bar.Add(MakePrimaryDangerButton("Dismiss All Mocks", () => MobileSimulatorState.PushDismissAll()));
			scroll.Add(bar);

			Add(scroll);
		}

		protected override void Refresh() { }

		private void OnWindowPlatformChanged(SimulatedPlatform platform) =>
			ApplyActionSheetButtonState(platform);

		private void ApplyActionSheetButtonState(SimulatedPlatform platform)
		{
			if (_actionSheetBtn == null)
			{
				return;
			}
			var isAndroid = platform == SimulatedPlatform.Android;
			_actionSheetBtn.SetEnabled(!isAndroid);
			_actionSheetBtn.tooltip = isAndroid ? ActionSheetAndroidDisabledTooltip : null;
		}

		private static VisualElement MakePrimaryButtonRow(params Button[] buttons)
		{
			var row = new VisualElement();
			row.style.flexDirection = FlexDirection.Row;
			row.style.flexWrap = Wrap.Wrap;
			foreach (var btn in buttons)
			{
				row.Add(btn);
			}
			return row;
		}

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

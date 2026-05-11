using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Explorer.Overlays;
using GameLovers.MobileServices.Editor.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Tabs
{
	/// <summary>Combined App Tracking Transparency + Deep Link inspector tab.</summary>
	public sealed class AttDeepLinkTab : MobileServiceTab
	{
		public override string DisplayName => "ATT + Deep Link";
		protected override int RefreshIntervalMs => 500;

		private readonly AttService _att = new AttService();
		private DeepLinkService _deepLink;
		private Label _attStatus;
		private DropdownField _attResultDropdown;
		private Label _coldStartLabel;
		private Label _lastDeliveredLabel;
		private TextField _deepLinkInput;
		private Uri _lastDelivered;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			scroll.Add(MakeSectionLabel("App Tracking Transparency"));
			_attStatus = new Label();
			scroll.Add(_attStatus);

			var attRow = new VisualElement();
			attRow.style.flexDirection = FlexDirection.Row;
			attRow.Add(MakeRowButton("Check", () => Refresh()));
			attRow.Add(MakeRowButton("Request", () => _ = RequestAttAsync()));
			attRow.Add(MakeRowButton("Show Mock", () =>
			{
				MobileSimulatorState.PushPermissionDialog(new SimulatedPermissionDialogSpec
				{
					TypeName = "App Tracking",
					UsageDescription = "(set NSUserTrackingUsageDescription in Project Settings)",
					IsAtt = true,
					OnResolved = result => QueueAttFromMock(result),
				});
			}));
			scroll.Add(attRow);

			_attResultDropdown = new DropdownField("Simulate next", new List<string>
			{
				"(no override)",
				AttStatus.Authorized.ToString(),
				AttStatus.Denied.ToString(),
				AttStatus.Restricted.ToString(),
				AttStatus.NotDetermined.ToString(),
			}, 0);
			_attResultDropdown.RegisterValueChangedCallback(evt =>
			{
				if (evt.newValue == "(no override)")
				{
					EditorPlatformSimulator.QueueAttResult(null);
				}
				else if (Enum.TryParse<AttStatus>(evt.newValue, out var parsed))
				{
					EditorPlatformSimulator.QueueAttResult(parsed);
				}
			});
			scroll.Add(_attResultDropdown);

			scroll.Add(MakeSectionLabel("Deep Links"));
			_coldStartLabel = new Label();
			_lastDeliveredLabel = new Label();
			scroll.Add(_coldStartLabel);
			scroll.Add(_lastDeliveredLabel);

			_deepLinkInput = new TextField("URI") { value = "myapp://promo/spring2026" };
			scroll.Add(_deepLinkInput);

			var dlRow = new VisualElement();
			dlRow.style.flexDirection = FlexDirection.Row;
			dlRow.Add(MakePrimaryButton("Send test link", SendTestLink));
			dlRow.Add(MakePrimaryButton("Initialise DeepLinkService", InitialiseDeepLinkService));
			scroll.Add(dlRow);

			Add(scroll);
			Refresh();
		}

		protected override void Refresh()
		{
			_attStatus.text = $"ATT current status: {_att.CurrentStatus}";

			if (_deepLink == null)
			{
				_coldStartLabel.text = "Pending cold-start link: (DeepLinkService not initialised)";
				_lastDeliveredLabel.text = string.Empty;
				return;
			}
			_coldStartLabel.text = _deepLink.PendingColdStartLink != null
				? $"Pending cold-start link: {_deepLink.PendingColdStartLink}"
				: "Pending cold-start link: (none)";
			_lastDeliveredLabel.text = _lastDelivered != null
				? $"Last delivered: {_lastDelivered}"
				: "Last delivered: (none)";
		}

		protected override void OnExitingPlayMode()
		{
			_deepLink?.Dispose();
			_deepLink = null;
			_lastDelivered = null;
		}

		private async System.Threading.Tasks.Task RequestAttAsync()
		{
			var result = await _att.RequestAuthorizationAsync();
			_attStatus.text = $"ATT request result: {result}";
		}

		private void QueueAttFromMock(bool authorized)
		{
			EditorPlatformSimulator.QueueAttResult(authorized ? AttStatus.Authorized : AttStatus.Denied);
		}

		private void InitialiseDeepLinkService()
		{
			if (_deepLink != null) return;
			_deepLink = new DeepLinkService();
			_deepLink.OnLinkActivated += uri =>
			{
				_lastDelivered = uri;
				Refresh();
			};
			Refresh();
		}

		private void SendTestLink()
		{
			InitialiseDeepLinkService();
			if (Uri.TryCreate(_deepLinkInput.value, UriKind.Absolute, out var uri))
			{
				EditorPlatformSimulator.SimulateDeepLink(uri, _deepLink);
			}
			else
			{
				Debug.LogWarning("[MobileServicesExplorer] Invalid URI: " + _deepLinkInput.value);
			}
		}
	}
}

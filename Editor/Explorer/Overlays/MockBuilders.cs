using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Overlays
{
	/// <summary>
	/// Builds the <see cref="VisualElement"/> tree for each simulator mock; layout lives in the
	/// platform USS files in this folder.
	/// </summary>
	internal static class MockBuilders
	{
		// ---- Alerts / action sheets ----

		internal static VisualElement BuildAlert(SimulatedPlatform platform, SimulatedAlertSpec spec, Action dismissCallback)
		{
			var scrim = new VisualElement();
			scrim.AddToClassList("mock-scrim");

			var card = new VisualElement();
			card.AddToClassList("mock-card");
			if (platform == SimulatedPlatform.iOS && spec.IsActionSheet)
			{
				card.AddToClassList("mock-card-sheet");
			}
			else
			{
				card.AddToClassList("mock-card-alert");
			}

			if (!string.IsNullOrEmpty(spec.Title))
			{
				var title = new Label(spec.Title);
				title.AddToClassList("mock-card-title");
				card.Add(title);
			}
			if (!string.IsNullOrEmpty(spec.Message))
			{
				var message = new Label(spec.Message);
				message.AddToClassList("mock-card-message");
				card.Add(message);
			}

			var buttonRow = new VisualElement();
			buttonRow.AddToClassList("mock-card-button-row");
			// iOS alerts use a horizontal divider stack; action sheets and Android dialogs flow vertically.
			if (platform == SimulatedPlatform.iOS && !spec.IsActionSheet && spec.Buttons.Count == 2)
			{
				buttonRow.AddToClassList("mock-card-button-row-horizontal");
			}
			else
			{
				buttonRow.AddToClassList("mock-card-button-row-vertical");
			}

			foreach (var btnSpec in spec.Buttons)
			{
				var btn = new Button(() =>
				{
					btnSpec.OnClicked?.Invoke();
					dismissCallback?.Invoke();
				}) { text = btnSpec.Text };
				btn.AddToClassList("mock-card-button");
				switch (btnSpec.Style)
				{
					case SimulatedAlertButtonStyle.Cancel:
						btn.AddToClassList("mock-card-button-cancel");
						break;
					case SimulatedAlertButtonStyle.Destructive:
						btn.AddToClassList("mock-card-button-destructive");
						break;
				}
				buttonRow.Add(btn);
			}

			card.Add(buttonRow);
			scrim.Add(card);
			return scrim;
		}

		// ---- Toasts ----

		internal static VisualElement BuildToast(SimulatedPlatform platform, SimulatedToastSpec spec)
		{
			var wrapper = new VisualElement();
			wrapper.AddToClassList(platform == SimulatedPlatform.iOS ? "mock-toast-top" : "mock-toast-bottom");
			wrapper.pickingMode = PickingMode.Ignore;

			var pill = new VisualElement();
			pill.AddToClassList("mock-toast-pill");
			pill.Add(new Label(spec.Message ?? string.Empty));
			wrapper.Add(pill);
			return wrapper;
		}

		// ---- Share sheet ----

		internal static VisualElement BuildShareSheet(SimulatedPlatform platform, SimulatedShareSpec spec, Action dismissCallback)
		{
			var scrim = new VisualElement();
			scrim.AddToClassList("mock-scrim");

			var card = new VisualElement();
			card.AddToClassList("mock-card");
			card.AddToClassList("mock-share-card");

			if (!string.IsNullOrEmpty(spec.Title))
			{
				var title = new Label(spec.Title);
				title.AddToClassList("mock-share-title");
				card.Add(title);
			}

			var summary = new Label(BuildShareSummary(spec));
			summary.AddToClassList("mock-share-summary");
			card.Add(summary);

			var grid = new VisualElement();
			grid.AddToClassList(platform == SimulatedPlatform.iOS ? "mock-share-grid-ios" : "mock-share-list-android");

			// Stand-in "share targets" (Messages / Mail / Save) — no real wiring, just the shape.
			var targets = platform == SimulatedPlatform.iOS
				? new[] { "Messages", "Mail", "Notes", "AirDrop", "Save Image", "Copy Link" }
				: new[] { "Messages", "Gmail", "Drive", "Bluetooth", "Save image", "Copy link" };

			foreach (var target in targets)
			{
				var icon = new VisualElement();
				icon.AddToClassList("mock-share-tile");
				icon.Add(new Label(target));
				grid.Add(icon);
			}

			card.Add(grid);

			var closeBtn = new Button(() => dismissCallback?.Invoke()) { text = platform == SimulatedPlatform.iOS ? "Cancel" : "Close" };
			closeBtn.AddToClassList("mock-card-button");
			closeBtn.AddToClassList("mock-card-button-cancel");
			card.Add(closeBtn);

			scrim.Add(card);
			return scrim;
		}

		internal static VisualElement BuildReviewPrompt(SimulatedPlatform platform, Action dismissCallback)
		{
			return platform == SimulatedPlatform.iOS
				? BuildIosReviewPrompt(dismissCallback)
				: BuildAndroidReviewPrompt(dismissCallback);
		}

		internal static VisualElement BuildPermissionDialog(SimulatedPlatform platform, SimulatedPermissionDialogSpec spec, Action<bool> dismissCallback)
		{
			var scrim = new VisualElement();
			scrim.AddToClassList("mock-scrim");

			var card = new VisualElement();
			card.AddToClassList("mock-card");
			card.AddToClassList("mock-permission-card");

			var title = new Label(BuildPermissionTitle(spec));
			title.AddToClassList("mock-card-title");
			card.Add(title);

			var message = new Label(string.IsNullOrEmpty(spec.UsageDescription)
				? "(no usage description configured — set one in Project Settings > GameLovers > Mobile Services)"
				: spec.UsageDescription);
			message.AddToClassList("mock-card-message");
			if (string.IsNullOrEmpty(spec.UsageDescription))
			{
				message.AddToClassList("mock-card-message-warning");
			}
			card.Add(message);

			var buttons = new VisualElement();
			buttons.AddToClassList("mock-card-button-row");
			buttons.AddToClassList("mock-card-button-row-horizontal");

			var denyText = spec.IsAtt ? "Ask App Not to Track" : "Don't Allow";
			var allowText = spec.IsAtt ? "Allow" : (platform == SimulatedPlatform.iOS ? "OK" : "Allow");

			if (platform == SimulatedPlatform.iOS)
			{
				buttons.Add(MakeBtn(denyText, false, dismissCallback, "mock-card-button-cancel"));
				buttons.Add(MakeBtn(allowText, true, dismissCallback, null));
			}
			else
			{
				buttons.Add(MakeBtn(denyText, false, dismissCallback, "mock-card-button-cancel"));
				buttons.Add(MakeBtn(allowText, true, dismissCallback, null));
			}

			card.Add(buttons);
			scrim.Add(card);
			return scrim;
		}

		internal static VisualElement BuildNotificationBanner(SimulatedPlatform platform, SimulatedNotificationBannerSpec spec)
		{
			var wrapper = new VisualElement();
			wrapper.AddToClassList(platform == SimulatedPlatform.iOS ? "mock-notif-top-ios" : "mock-notif-top-android");
			wrapper.pickingMode = PickingMode.Ignore;

			// Real heads-up shape: [ app icon ] [ APP NAME ........ now / title / body ].
			var card = new VisualElement();
			card.AddToClassList("mock-notif-card");

			var appName = string.IsNullOrEmpty(spec.ChannelName) ? "Your App" : spec.ChannelName;

			var icon = new VisualElement();
			icon.AddToClassList("mock-notif-icon");
			icon.Add(new Label(appName.Substring(0, 1).ToUpperInvariant()));
			card.Add(icon);

			var content = new VisualElement();
			content.AddToClassList("mock-notif-content");

			var header = new VisualElement();
			header.AddToClassList("mock-notif-header");
			var name = new Label(appName.ToUpperInvariant());
			name.AddToClassList("mock-notif-appname");
			header.Add(name);
			var spacer = new VisualElement();
			spacer.style.flexGrow = 1;
			header.Add(spacer);
			var time = new Label("now");
			time.AddToClassList("mock-notif-time");
			header.Add(time);
			content.Add(header);

			var title = new Label(spec.Title ?? string.Empty);
			title.AddToClassList("mock-notif-title");
			content.Add(title);

			if (!string.IsNullOrEmpty(spec.SubTitle))
			{
				var subtitle = new Label(spec.SubTitle);
				subtitle.AddToClassList("mock-notif-subtitle");
				content.Add(subtitle);
			}

			var body = new Label(spec.Body ?? string.Empty);
			body.AddToClassList("mock-notif-body");
			content.Add(body);

			card.Add(content);
			wrapper.Add(card);
			return wrapper;
		}

		private static string BuildShareSummary(SimulatedShareSpec spec)
		{
			var parts = new System.Text.StringBuilder();
			if (!string.IsNullOrEmpty(spec.Text)) parts.Append(spec.Text);
			if (!string.IsNullOrEmpty(spec.Url))
			{
				if (parts.Length > 0) parts.Append(' ');
				parts.Append(spec.Url);
			}
			if (!string.IsNullOrEmpty(spec.ImagePath))
			{
				if (parts.Length > 0) parts.Append('\n');
				parts.Append("[image] ").Append(spec.ImagePath);
			}
			if (parts.Length == 0)
			{
				return "(empty share payload)";
			}
			return parts.ToString();
		}

		// ---- Review prompt ----
		// Faithful per-platform shapes: iOS is StoreKit's centered system star sheet (titled with the
		// app name); Android is the Play In-App Review bottom sheet. Both use Application.productName.

		private static VisualElement BuildIosReviewPrompt(Action dismissCallback)
		{
			var scrim = new VisualElement();
			scrim.AddToClassList("mock-scrim");

			var card = new VisualElement();
			card.AddToClassList("mock-card");
			card.AddToClassList("mock-review-card");
			card.AddToClassList("mock-review-card-ios");

			var title = new Label(AppName());
			title.AddToClassList("mock-card-title");
			card.Add(title);

			var subtitle = new Label("Tap a star to rate it on the App Store.");
			subtitle.AddToClassList("mock-card-message");
			card.Add(subtitle);

			var buttons = new VisualElement();
			buttons.AddToClassList("mock-card-button-row");
			buttons.AddToClassList("mock-card-button-row-horizontal");

			// Submit only appears once the user has picked a rating — matches the system sheet.
			var submit = new Button(() => dismissCallback?.Invoke()) { text = "Submit" };
			submit.AddToClassList("mock-card-button");
			submit.style.display = DisplayStyle.None;

			card.Add(BuildStarRow(_ => submit.style.display = DisplayStyle.Flex));

			var cancel = new Button(() => dismissCallback?.Invoke()) { text = "Not Now" };
			cancel.AddToClassList("mock-card-button");
			cancel.AddToClassList("mock-card-button-cancel");
			buttons.Add(cancel);
			buttons.Add(submit);

			card.Add(buttons);
			scrim.Add(card);
			return scrim;
		}

		private static VisualElement BuildAndroidReviewPrompt(Action dismissCallback)
		{
			// Play review is a bottom sheet with no explicit buttons; rating it (or tapping the scrim)
			// dismisses. Edit-mode clicks are unreliable, so the panel's Dismiss button is the fallback.
			var scrim = new VisualElement();
			scrim.AddToClassList("mock-scrim");
			scrim.AddToClassList("mock-review-scrim-android");
			scrim.RegisterCallback<ClickEvent>(evt =>
			{
				if (evt.target == scrim)
				{
					dismissCallback?.Invoke();
				}
			});

			var sheet = new VisualElement();
			sheet.AddToClassList("mock-card");
			sheet.AddToClassList("mock-review-card");
			sheet.AddToClassList("mock-review-sheet-android");

			var header = new VisualElement();
			header.AddToClassList("mock-review-header-android");

			var icon = new VisualElement();
			icon.AddToClassList("mock-review-appicon");
			icon.Add(new Label(AppName().Substring(0, 1).ToUpperInvariant()));
			header.Add(icon);

			var title = new Label("Rate this app");
			title.AddToClassList("mock-card-title");
			header.Add(title);
			sheet.Add(header);

			var subtitle = new Label("Tell others what you think");
			subtitle.AddToClassList("mock-card-message");
			sheet.Add(subtitle);

			sheet.Add(BuildStarRow(_ => dismissCallback?.Invoke()));

			scrim.Add(sheet);
			return scrim;
		}

		// Five tappable stars; clicking the i-th fills 0..i and reports the 1-based rating.
		private static VisualElement BuildStarRow(Action<int> onRated)
		{
			var row = new VisualElement();
			row.AddToClassList("mock-review-stars");

			var stars = new List<Label>();
			for (var i = 0; i < 5; i++)
			{
				var star = new Label("\u2606");
				star.AddToClassList("mock-review-star");
				var index = i;
				star.RegisterCallback<ClickEvent>(_ =>
				{
					for (var s = 0; s < stars.Count; s++)
					{
						var filled = s <= index;
						stars[s].text = filled ? "\u2605" : "\u2606";
						if (filled)
						{
							stars[s].AddToClassList("mock-review-star-filled");
						}
						else
						{
							stars[s].RemoveFromClassList("mock-review-star-filled");
						}
					}
					onRated?.Invoke(index + 1);
				});
				stars.Add(star);
				row.Add(star);
			}
			return row;
		}

		private static string AppName()
		{
			var name = Application.productName;
			return string.IsNullOrEmpty(name) ? "Your App" : name;
		}

		// ---- Permission / ATT dialog ----

		private static Button MakeBtn(string text, bool result, Action<bool> dismissCallback, string extraClass)
		{
			var btn = new Button(() => dismissCallback?.Invoke(result)) { text = text };
			btn.AddToClassList("mock-card-button");
			if (!string.IsNullOrEmpty(extraClass))
			{
				btn.AddToClassList(extraClass);
			}
			return btn;
		}

		private static string BuildPermissionTitle(SimulatedPermissionDialogSpec spec)
		{
			if (spec.IsAtt)
			{
				return "Allow this app to track your activity across other companies' apps and websites?";
			}
			return $"\"YourApp\" Would Like to Access Your {spec.TypeName}";
		}

		// ---- Heads-up notification banner ----

	}
}

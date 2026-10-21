using System;
using System.Collections.Generic;
using System.Text;
using GameLovers.MobileServices.Device;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Settings
{
	/// <summary>
	/// Inspector for <see cref="MobileServicesConfig"/>. The per-locale usage-description lists render
	/// via the default serialized-property UI (locale code + text, add / remove), augmented with a
	/// missing-key status box, a project scan, a "suggest copy" filler, and an iOS Privacy Nutrition
	/// Label draft generator. Open the asset via
	/// <c>Tools &gt; GameLovers &gt; Mobile Services &gt; Select Mobile Services Config</c>.
	/// </summary>
	/// <remarks>
	/// The enclosing namespace contains <c>.Editor.</c>, so the Unity base class MUST be qualified as
	/// <see cref="UnityEditor.Editor"/> (a bare <c>Editor</c> would resolve to the
	/// <c>GameLovers.MobileServices.Editor</c> namespace).
	/// </remarks>
	[CustomEditor(typeof(MobileServicesConfig))]
	internal sealed class MobileServicesConfigEditor : UnityEditor.Editor
	{
		private HelpBox _statusBox;

		/// <summary>
		/// Builds the config Inspector: per-locale description lists, settings, status, and helper tools.
		/// </summary>
		public override VisualElement CreateInspectorGUI()
		{
			var config = (MobileServicesConfig)target;

			var root = new VisualElement();

			var title = new Label("Mobile Services Config");
			title.style.unityFontStyleAndWeight = FontStyle.Bold;
			title.style.fontSize = 14;
			title.style.marginBottom = 4;
			root.Add(title);

			_statusBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
			root.Add(_statusBox);

			var summaries = new List<(Label Label, Func<IReadOnlyList<string>> Lines)>();

			root.Add(MakeSection("Permissions and privacy"));
			var locHint = new Label("Each permission holds one entry per locale (ISO code: en, fr, pt-BR …). " +
				"The 'en' value is written to Info.plist; other locales are emitted as <locale>.lproj/InfoPlist.strings at build. " +
				"Writing a usage description is what switches that iOS permission on.");
			locHint.style.whiteSpace = WhiteSpace.Normal;
			locHint.style.fontSize = 10;
			locHint.style.unityFontStyleAndWeight = FontStyle.Italic;
			locHint.style.marginBottom = 4;
			root.Add(locHint);
			root.Add(new PropertyField(serializedObject.FindProperty("_permissionDescriptions"), "Permission Descriptions"));
			root.Add(new PropertyField(serializedObject.FindProperty("_attUsageDescription"), "App Tracking (ATT) Description"));
			AddRelative(root, "_capabilities", "AppTracking");
			AddRelative(root, "_androidManifest", "Camera", "RecordAudio", "AccessFineLocation", "AccessCoarseLocation",
				"AccessBackgroundLocation", "ReadMediaImages");
			AddSummary(root, summaries, () => MobileServicesBuildSummary.Permissions(config));

			root.Add(MakeSection("Notifications"));
			AddRelative(root, "_capabilities", "PushNotifications");
			AddRelative(root, "_androidManifest", "PostNotifications", "Vibrate");
			AddSummary(root, summaries, () => MobileServicesBuildSummary.Notifications(config));

			root.Add(MakeSection("Deep links"));
			AddRelative(root, "_capabilities", "AssociatedDomains", "AssociatedDomainList");
			root.Add(new PropertyField(serializedObject.FindProperty("_deepLinks"), "Deep-link registrations"));
			AddSummary(root, summaries, () => MobileServicesBuildSummary.DeepLinks(config));

			root.Add(MakeSection("Sharing"));
			AddRelative(root, "_androidManifest", "IncludeShareQueriesBlock", "IncludeShareFileProvider");
			AddSummary(root, summaries, () => MobileServicesBuildSummary.Sharing(config));

			root.Add(MakeSection("Media (gallery and picker)"));
			AddRelative(root, "_androidManifest", "IncludeMediaPicker", "WriteExternalStorageLegacy");
			AddSummary(root, summaries, () => MobileServicesBuildSummary.Media(config));

			root.Add(MakeSection("Game platform (Game Center and Play Games)"));
			var gamePlatformHint = new Label("Opt-in. Game Center needs the capability on your App ID and provisioning profile. " +
				"Entering the Play Games project id adds the Play Games Services SDK to Android builds.");
			gamePlatformHint.style.whiteSpace = WhiteSpace.Normal;
			gamePlatformHint.style.fontSize = 10;
			gamePlatformHint.style.unityFontStyleAndWeight = FontStyle.Italic;
			root.Add(gamePlatformHint);
			AddRelative(root, "_gamePlatform", "GameCenter", "PlayGamesProjectId", "PlayGamesDependencyCoordinate");
			AddSummary(root, summaries, () => MobileServicesBuildSummary.GamePlatform(config));

			root.Add(MakeSection("Store review"));
			root.Add(new PropertyField(serializedObject.FindProperty("_includePlayReviewDependency"), "Include Play Review Dependency"));
			root.Add(new PropertyField(serializedObject.FindProperty("_playReviewDependencyCoordinate"), "Play Review Coordinate"));
			AddSummary(root, summaries, () => MobileServicesBuildSummary.StoreReview(config));

			root.Add(MakeSection("Build behaviour"));
			root.Add(new PropertyField(serializedObject.FindProperty("_manageNativeBuildManually"), "Manage Native Build Manually"));

			root.Add(MakeSection("Editor diagnostics"));
			root.Add(new PropertyField(serializedObject.FindProperty("_enableHapticsDebugLogs"), "Enable Haptics Debug Logs"));

			root.Add(MakeSection("Tools"));
			root.Add(BuildScanButton(config));
			root.Add(BuildSuggestCopyButton(config));
			root.Add(BuildPrivacyNutritionButton(config));

			root.Bind(serializedObject);
			root.TrackSerializedObjectValue(serializedObject, _ =>
			{
				UpdateStatus(config);
				RefreshSummaries(config, summaries);
			});
			UpdateStatus(config);
			RefreshSummaries(config, summaries);
			return root;
		}

		private void AddRelative(VisualElement root, string structName, params string[] fieldNames)
		{
			var parent = serializedObject.FindProperty(structName);
			foreach (var fieldName in fieldNames)
			{
				root.Add(new PropertyField(parent.FindPropertyRelative(fieldName)));
			}
		}

		private static void AddSummary(VisualElement root, List<(Label Label, Func<IReadOnlyList<string>> Lines)> summaries, Func<IReadOnlyList<string>> lines)
		{
			var label = new Label();
			label.style.whiteSpace = WhiteSpace.Normal;
			label.style.fontSize = 10;
			label.style.unityFontStyleAndWeight = FontStyle.Italic;
			label.style.marginTop = 2;
			root.Add(label);
			summaries.Add((label, lines));
		}

		private static void RefreshSummaries(MobileServicesConfig config, List<(Label Label, Func<IReadOnlyList<string>> Lines)> summaries)
		{
			foreach (var (label, lines) in summaries)
			{
				if (config.ManageNativeBuildManually)
				{
					label.text = "Adds to build: nothing. Manage Native Build Manually is on.";
					continue;
				}
				var entries = lines();
				label.text = entries.Count == 0
					? "Adds to build: nothing."
					: "Adds to build:\n- " + string.Join("\n- ", entries);
			}
		}

		private static Label MakeSection(string text)
		{
			var lbl = new Label(text);
			lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
			lbl.style.marginTop = 12;
			lbl.style.marginBottom = 2;
			return lbl;
		}

		private void UpdateStatus(MobileServicesConfig config)
		{
			if (_statusBox == null)
			{
				return;
			}

			var missing = CountMissingDescriptions(config);
			if (missing == 0)
			{
				_statusBox.messageType = HelpBoxMessageType.Info;
				_statusBox.text = "All required iOS usage descriptions are configured (English).";
			}
			else
			{
				_statusBox.messageType = HelpBoxMessageType.Warning;
				_statusBox.text = $"{missing} usage description(s) missing an English value — fix before an iOS build.";
			}
		}

		private static int CountMissingDescriptions(MobileServicesConfig config)
		{
			var count = 0;
			foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
			{
				if (MobileServicesConfig.GetIosUsageKey(p) == null) continue;
				if (string.IsNullOrWhiteSpace(config.GetUsageDescriptionEn(p)))
				{
					count++;
				}
			}
			if (config.IsAttUsageDescriptionMissing())
			{
				count++;
			}
			return count;
		}

		private Button BuildScanButton(MobileServicesConfig config)
		{
			var btn = new Button(() =>
			{
				var result = MobileServicesScanner.Scan();
				var warnings = result.GetConfigurationWarnings(config);
				if (warnings.Count == 0)
				{
					Debug.Log("[GameLovers.MobileServices] Project scan complete — the explicit config covers the detected service references.");
					return;
				}
				Debug.LogWarning("[GameLovers.MobileServices] Project scan found configuration warnings:\n- " + string.Join("\n- ", warnings));
			}) { text = "Scan project for configuration warnings" };
			btn.style.marginTop = 2;
			return btn;
		}

		private Button BuildSuggestCopyButton(MobileServicesConfig config)
		{
			var btn = new Button(() =>
			{
				foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
				{
					if (MobileServicesConfig.GetIosUsageKey(p) == null) continue;
					if (!string.IsNullOrWhiteSpace(config.GetUsageDescriptionEn(p))) continue;
					var copy = MobileServicesConfig.GetSuggestedCopy(p);
					if (!string.IsNullOrEmpty(copy))
					{
						config.SetUsageDescriptionEn(p, copy);
					}
				}
				if (config.Capabilities.AppTracking && string.IsNullOrWhiteSpace(config.GetAttUsageDescriptionEn()))
				{
					config.SetAttUsageDescriptionEn(MobileServicesConfig.GetSuggestedAttCopy());
				}
				serializedObject.Update();
				UpdateStatus(config);
			}) { text = "Fill missing English descriptions with suggested copy" };
			btn.style.marginTop = 2;
			return btn;
		}

		private static VisualElement BuildPrivacyNutritionButton(MobileServicesConfig config)
		{
			var output = new TextField { multiline = true };
			output.style.minHeight = 120;
			output.style.whiteSpace = WhiteSpace.Normal;

			var btn = new Button(() => output.value = BuildPrivacyNutritionMarkdown(config))
			{
				text = "Generate iOS Privacy Nutrition Label draft",
			};
			btn.style.marginTop = 2;

			var wrapper = new VisualElement();
			wrapper.Add(btn);
			wrapper.Add(output);
			return wrapper;
		}

		private static string BuildPrivacyNutritionMarkdown(MobileServicesConfig config)
		{
			var sb = new StringBuilder();
			sb.AppendLine("# Privacy Nutrition Label (draft)");
			sb.AppendLine();
			sb.AppendLine("Generated from the Mobile Services Config asset. Review and refine before App Store submission.");
			sb.AppendLine();
			sb.AppendLine("## Data Used to Track You");
			sb.AppendLine(config.Capabilities.AppTracking
				? "- Identifiers (advertising / device IDs) — App Tracking Transparency is enabled."
				: "- (none — App Tracking is disabled)");
			sb.AppendLine();
			sb.AppendLine("## Data Linked to You");
			foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
			{
				if (MobileServicesConfig.GetIosUsageKey(p) == null) continue;
				var copy = config.GetUsageDescriptionEn(p);
				if (string.IsNullOrWhiteSpace(copy)) continue;
				sb.AppendLine($"- **{p}** — {copy}");
			}
			sb.AppendLine();
			sb.AppendLine("## Data Not Collected");
			sb.AppendLine("- (review the bound services and document anything that is genuinely not collected)");
			return sb.ToString();
		}
	}
}

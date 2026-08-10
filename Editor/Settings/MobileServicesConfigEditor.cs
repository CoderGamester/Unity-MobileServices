using System;
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

			root.Add(MakeSection("Usage descriptions (Info.plist) — localized per device language"));
			var locHint = new Label("Each permission holds one entry per locale (ISO code: en, fr, pt-BR …). " +
				"The 'en' value is written to Info.plist; other locales are emitted as <locale>.lproj/InfoPlist.strings at build.");
			locHint.style.whiteSpace = WhiteSpace.Normal;
			locHint.style.fontSize = 10;
			locHint.style.unityFontStyleAndWeight = FontStyle.Italic;
			locHint.style.marginBottom = 4;
			root.Add(locHint);
			root.Add(new PropertyField(serializedObject.FindProperty("_permissionDescriptions"), "Permission Descriptions"));
			root.Add(new PropertyField(serializedObject.FindProperty("_attUsageDescription"), "App Tracking (ATT) Description"));

			root.Add(MakeSection("Capabilities"));
			root.Add(new PropertyField(serializedObject.FindProperty("_capabilities"), "Capabilities"));

			root.Add(MakeSection("Android manifest"));
			root.Add(new PropertyField(serializedObject.FindProperty("_androidManifest"), "Android Manifest"));

			root.Add(MakeSection("Native deep links"));
			root.Add(new PropertyField(serializedObject.FindProperty("_deepLinks"), "Deep-link registrations"));

			root.Add(MakeSection("Android dependencies"));
			root.Add(new PropertyField(serializedObject.FindProperty("_includePlayReviewDependency"), "Include Play Review Dependency"));
			root.Add(new PropertyField(serializedObject.FindProperty("_playReviewDependencyCoordinate"), "Play Review Coordinate"));

			root.Add(MakeSection("Build behaviour"));
			root.Add(new PropertyField(serializedObject.FindProperty("_manageNativeBuildManually"), "Manage Native Build Manually"));

			root.Add(MakeSection("Tools"));
			root.Add(BuildScanButton(config));
			root.Add(BuildSuggestCopyButton(config));
			root.Add(BuildPrivacyNutritionButton(config));

			root.Bind(serializedObject);
			root.TrackSerializedObjectValue(serializedObject, _ => UpdateStatus(config));
			UpdateStatus(config);
			return root;
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

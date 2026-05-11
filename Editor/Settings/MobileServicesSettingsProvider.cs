using System;
using System.Collections.Generic;
using System.Text;
using GameLovers.MobileServices.Device;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Settings
{
	/// <summary>
	/// UIToolkit-driven <see cref="SettingsProvider"/> at <c>Edit &gt; Project Settings &gt; GameLovers &gt;
	/// Mobile Services</c>. Surfaces every field on <see cref="MobileServicesSettings"/> plus a
	/// project-scan button that pre-fills capability toggles based on which runtime services the
	/// project actually references.
	/// </summary>
	internal static class MobileServicesSettingsProvider
	{
		private const string SettingsPath = "Project/GameLovers/Mobile Services";
		private static readonly string[] Keywords = { "Mobile", "iOS", "Android", "Info.plist", "Permission", "ATT", "Notification" };

		// Anchored row registry so the build postprocessor's clickable error can land on the offending row.
		private static readonly Dictionary<AppPermission, VisualElement> _permissionRowAnchors =
			new Dictionary<AppPermission, VisualElement>();
		private static Label _statusBadge;
		private static Label _attMissingLabel;

		[SettingsProvider]
		public static SettingsProvider Create()
		{
			return new SettingsProvider(SettingsPath, SettingsScope.Project)
			{
				label = "Mobile Services",
				keywords = Keywords,
				activateHandler = (_, rootElement) => BuildUi(rootElement),
			};
		}

		/// <summary>Opens the Project Settings panel anchored at the row for <paramref name="permission"/>.</summary>
		public static void OpenAtPermission(AppPermission permission)
		{
			SettingsService.OpenProjectSettings(SettingsPath);
		}

		private static void BuildUi(VisualElement root)
		{
			_permissionRowAnchors.Clear();
			root.Clear();

			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.style.flexGrow = 1;
			root.Add(scroll);

			BuildHeader(scroll);

			scroll.Add(MakeSectionLabel("Usage descriptions (Info.plist)"));
			foreach (AppPermission permission in Enum.GetValues(typeof(AppPermission)))
			{
				var key = MobileServicesSettings.GetIosUsageKey(permission);
				if (key == null)
				{
					continue;
				}
				scroll.Add(BuildPermissionRow(permission, key));
			}

			scroll.Add(BuildAttRow());

			scroll.Add(MakeSectionLabel("Capabilities"));
			scroll.Add(BuildCapabilityToggles());

			scroll.Add(MakeSectionLabel("Android manifest"));
			scroll.Add(BuildAndroidManifestToggles());

			scroll.Add(MakeSectionLabel("Build behaviour"));
			scroll.Add(BuildAllowPlaceholderToggle());

			scroll.Add(MakeSectionLabel("Editor tooling"));
			scroll.Add(BuildRuntimeSimulatorOverlayToggle());

			scroll.Add(MakeSectionLabel("Tools"));
			scroll.Add(BuildScanButton());
			scroll.Add(BuildPrivacyNutritionButton());

			UpdateStatusBadge();
		}

		private static VisualElement BuildHeader(VisualElement root)
		{
			var headerRow = new VisualElement();
			headerRow.style.flexDirection = FlexDirection.Row;
			headerRow.style.alignItems = Align.Center;
			headerRow.style.marginBottom = 8;
			headerRow.style.paddingTop = 6;
			headerRow.style.paddingBottom = 6;

			var title = new Label("Mobile Services Settings");
			title.style.unityFontStyleAndWeight = FontStyle.Bold;
			title.style.fontSize = 14;
			title.style.flexGrow = 1;
			headerRow.Add(title);

			_statusBadge = new Label();
			_statusBadge.style.paddingLeft = 8;
			_statusBadge.style.paddingRight = 8;
			_statusBadge.style.paddingTop = 2;
			_statusBadge.style.paddingBottom = 2;
			_statusBadge.style.borderTopLeftRadius = 8;
			_statusBadge.style.borderTopRightRadius = 8;
			_statusBadge.style.borderBottomLeftRadius = 8;
			_statusBadge.style.borderBottomRightRadius = 8;
			_statusBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
			headerRow.Add(_statusBadge);

			root.Add(headerRow);
			return headerRow;
		}

		private static VisualElement BuildPermissionRow(AppPermission permission, string iosKey)
		{
			var row = new VisualElement();
			row.style.flexDirection = FlexDirection.Column;
			row.style.paddingTop = 4;
			row.style.paddingBottom = 4;
			row.style.borderBottomWidth = 1;
			row.style.borderBottomColor = new Color(1, 1, 1, 0.07f);

			var titleRow = new VisualElement();
			titleRow.style.flexDirection = FlexDirection.Row;
			titleRow.style.alignItems = Align.Center;

			var lbl = new Label($"{permission}  ({iosKey})");
			lbl.style.flexGrow = 1;
			lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
			titleRow.Add(lbl);

			var missingPill = new Label("Missing");
			missingPill.style.paddingLeft = 6;
			missingPill.style.paddingRight = 6;
			missingPill.style.borderTopLeftRadius = 6;
			missingPill.style.borderTopRightRadius = 6;
			missingPill.style.borderBottomLeftRadius = 6;
			missingPill.style.borderBottomRightRadius = 6;
			missingPill.style.backgroundColor = new Color(0.78f, 0.2f, 0.2f, 0.3f);
			missingPill.style.color = new Color(1f, 0.8f, 0.8f);
			missingPill.style.unityFontStyleAndWeight = FontStyle.Bold;
			missingPill.style.fontSize = 10;
			titleRow.Add(missingPill);

			row.Add(titleRow);

			var textField = new TextField { multiline = true };
			textField.style.minHeight = 36;
			textField.style.whiteSpace = WhiteSpace.Normal;
			textField.value = MobileServicesSettings.instance.GetUsageDescriptionEn(permission) ?? string.Empty;
			textField.RegisterValueChangedCallback(evt =>
			{
				MobileServicesSettings.instance.SetUsageDescriptionEn(permission, evt.newValue);
				UpdateMissingPill(missingPill, evt.newValue);
				UpdateStatusBadge();
			});
			row.Add(textField);

			var suggestBtn = new Button(() =>
			{
				textField.value = MobileServicesSettings.GetSuggestedCopy(permission);
			}) { text = "Suggest copy" };
			suggestBtn.style.alignSelf = Align.FlexStart;
			row.Add(suggestBtn);

			UpdateMissingPill(missingPill, textField.value);
			_permissionRowAnchors[permission] = row;
			return row;
		}

		private static VisualElement BuildAttRow()
		{
			var row = new VisualElement();
			row.style.flexDirection = FlexDirection.Column;
			row.style.paddingTop = 4;
			row.style.paddingBottom = 4;

			var titleRow = new VisualElement();
			titleRow.style.flexDirection = FlexDirection.Row;
			titleRow.style.alignItems = Align.Center;
			var lbl = new Label("AppTracking  (NSUserTrackingUsageDescription)");
			lbl.style.flexGrow = 1;
			lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
			titleRow.Add(lbl);
			_attMissingLabel = new Label("Missing");
			_attMissingLabel.style.paddingLeft = 6;
			_attMissingLabel.style.paddingRight = 6;
			_attMissingLabel.style.backgroundColor = new Color(0.78f, 0.2f, 0.2f, 0.3f);
			_attMissingLabel.style.color = new Color(1f, 0.8f, 0.8f);
			_attMissingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
			_attMissingLabel.style.fontSize = 10;
			titleRow.Add(_attMissingLabel);
			row.Add(titleRow);

			var field = new TextField { multiline = true };
			field.style.minHeight = 36;
			field.style.whiteSpace = WhiteSpace.Normal;
			field.value = MobileServicesSettings.instance.GetAttUsageDescriptionEn() ?? string.Empty;
			field.RegisterValueChangedCallback(evt =>
			{
				MobileServicesSettings.instance.SetAttUsageDescriptionEn(evt.newValue);
				UpdateAttMissingPill(evt.newValue);
				UpdateStatusBadge();
			});
			row.Add(field);

			var suggestBtn = new Button(() => { field.value = MobileServicesSettings.GetSuggestedAttCopy(); }) { text = "Suggest copy" };
			suggestBtn.style.alignSelf = Align.FlexStart;
			row.Add(suggestBtn);

			UpdateAttMissingPill(field.value);
			return row;
		}

		private static void UpdateMissingPill(Label pill, string text)
		{
			pill.style.display = string.IsNullOrWhiteSpace(text) ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private static void UpdateAttMissingPill(string text)
		{
			if (_attMissingLabel == null) return;
			var visible = MobileServicesSettings.instance.Capabilities.AppTracking && string.IsNullOrWhiteSpace(text);
			_attMissingLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private static VisualElement BuildCapabilityToggles()
		{
			var c = MobileServicesSettings.instance.Capabilities;
			var v = new VisualElement();

			v.Add(MakeToggleBound("Push Notifications", c.PushNotifications, val => { c.PushNotifications = val; PersistAndRefresh(); }));
			v.Add(MakeToggleBound("Background Audio (UIBackgroundModes: audio)", c.BackgroundAudio, val => { c.BackgroundAudio = val; PersistAndRefresh(); }));
			v.Add(MakeToggleBound("App Tracking", c.AppTracking, val => { c.AppTracking = val; PersistAndRefresh(); }));
			v.Add(MakeToggleBound("Associated Domains (deep links)", c.AssociatedDomains, val => { c.AssociatedDomains = val; PersistAndRefresh(); }));

			var domainsLabel = new Label("  Associated domain list (one per line, e.g. applinks:example.com):");
			domainsLabel.style.fontSize = 10;
			v.Add(domainsLabel);

			var domainsField = new TextField { multiline = true };
			domainsField.style.minHeight = 60;
			domainsField.value = string.Join("\n", c.AssociatedDomainList);
			domainsField.RegisterValueChangedCallback(evt =>
			{
				c.AssociatedDomainList = new List<string>();
				foreach (var line in evt.newValue.Split('\n'))
				{
					var t = line.Trim();
					if (!string.IsNullOrEmpty(t)) c.AssociatedDomainList.Add(t);
				}
				PersistAndRefresh();
			});
			v.Add(domainsField);

			return v;
		}

		private static VisualElement BuildAndroidManifestToggles()
		{
			var a = MobileServicesSettings.instance.AndroidManifest;
			var v = new VisualElement();
			v.Add(MakeToggleBound("CAMERA",                       a.Camera,             val => { a.Camera = val; PersistAndRefresh(); }));
			v.Add(MakeToggleBound("RECORD_AUDIO",                  a.RecordAudio,        val => { a.RecordAudio = val; PersistAndRefresh(); }));
			v.Add(MakeToggleBound("ACCESS_FINE_LOCATION",          a.AccessFineLocation, val => { a.AccessFineLocation = val; PersistAndRefresh(); }));
			v.Add(MakeToggleBound("READ_MEDIA_IMAGES (API 33+)",   a.ReadMediaImages,    val => { a.ReadMediaImages = val; PersistAndRefresh(); }));
			v.Add(MakeToggleBound("POST_NOTIFICATIONS (API 33+)",  a.PostNotifications,  val => { a.PostNotifications = val; PersistAndRefresh(); }));
			v.Add(MakeToggleBound("Share-chooser <queries> block (API 30+)", a.IncludeShareQueriesBlock, val => { a.IncludeShareQueriesBlock = val; PersistAndRefresh(); }));
			return v;
		}

		private static Toggle MakeToggleBound(string text, bool initialValue, Action<bool> onChange)
		{
			var t = new Toggle(text) { value = initialValue };
			t.RegisterValueChangedCallback(evt => onChange(evt.newValue));
			return t;
		}

		private static VisualElement BuildAllowPlaceholderToggle()
		{
			var note = new Label("CI / preview-build soft mode. When ON, missing usage descriptions inject \"[GameLovers placeholder]\" instead of failing the build. Apple WILL reject submissions containing the placeholder — by design.");
			note.style.fontSize = 10;
			note.style.whiteSpace = WhiteSpace.Normal;
			note.style.unityFontStyleAndWeight = FontStyle.Italic;
			note.style.marginBottom = 4;

			var toggle = new Toggle("Allow build with placeholder usage descriptions")
			{
				value = MobileServicesSettings.instance.AllowPlaceholderUsageDescriptions,
			};
			toggle.RegisterValueChangedCallback(evt =>
			{
				MobileServicesSettings.instance.AllowPlaceholderUsageDescriptions = evt.newValue;
				UpdateStatusBadge();
			});

			var wrapper = new VisualElement();
			wrapper.Add(note);
			wrapper.Add(toggle);
			return wrapper;
		}

		private static VisualElement BuildRuntimeSimulatorOverlayToggle()
		{
			var note = new Label("When ON, entering Play mode spawns an editor-only UIDocument inside the Game / Simulator view that renders the mock native-UI surfaces at the simulated device's pixel grid. Pairs with Unity's Device Simulator (Window > General > Device Simulator). Editor-only — does NOT ship to player builds.");
			note.style.fontSize = 10;
			note.style.whiteSpace = WhiteSpace.Normal;
			note.style.unityFontStyleAndWeight = FontStyle.Italic;
			note.style.marginBottom = 4;

			var toggle = new Toggle("Enable runtime simulator overlay (play-mode)")
			{
				value = MobileServicesSettings.instance.EnableRuntimeSimulatorOverlay,
			};
			toggle.RegisterValueChangedCallback(evt =>
			{
				MobileServicesSettings.instance.EnableRuntimeSimulatorOverlay = evt.newValue;
			});

			var wrapper = new VisualElement();
			wrapper.Add(note);
			wrapper.Add(toggle);
			return wrapper;
		}

		private static VisualElement BuildScanButton()
		{
			var btn = new Button(() =>
			{
				var result = MobileServicesScanner.Scan();
				var c = MobileServicesSettings.instance.Capabilities;
				if (result.UsesNotifications) c.PushNotifications = true;
				if (result.UsesAudioSession) c.BackgroundAudio = true;
				if (result.UsesAtt) c.AppTracking = true;
				if (result.UsesDeepLinks) c.AssociatedDomains = true;

				var a = MobileServicesSettings.instance.AndroidManifest;
				foreach (var p in result.ReferencedPermissions)
				{
					switch (p)
					{
						case AppPermission.Camera:               a.Camera = true; break;
						case AppPermission.Microphone:           a.RecordAudio = true; break;
						case AppPermission.LocationWhenInUse:
						case AppPermission.LocationAlways:       a.AccessFineLocation = true; break;
						case AppPermission.PhotoLibrary:
						case AppPermission.PhotoLibraryAddOnly:  a.ReadMediaImages = true; break;
						case AppPermission.Notifications:        a.PostNotifications = true; break;
					}
				}
				if (result.UsesNativeUiShare) a.IncludeShareQueriesBlock = true;

				MobileServicesSettings.instance.ScanPopulatedCapabilities = true;
				PersistAndRefresh();
				Debug.Log("[Mobile Services] Project scan complete — capability toggles updated.");
			}) { text = "Scan project for used services" };
			btn.style.alignSelf = Align.FlexStart;
			return btn;
		}

		private static VisualElement BuildPrivacyNutritionButton()
		{
			var note = new Label("Generates a markdown summary of the configured permissions / capabilities, formatted as a starter draft for the App Store privacy nutrition label.");
			note.style.fontSize = 10;
			note.style.whiteSpace = WhiteSpace.Normal;
			note.style.unityFontStyleAndWeight = FontStyle.Italic;
			note.style.marginBottom = 4;

			var output = new TextField { multiline = true };
			output.style.minHeight = 120;
			output.value = string.Empty;

			var btn = new Button(() => { output.value = BuildPrivacyNutritionMarkdown(); }) { text = "Generate iOS Privacy Nutrition Label draft" };

			var wrapper = new VisualElement();
			wrapper.Add(note);
			wrapper.Add(btn);
			wrapper.Add(output);
			return wrapper;
		}

		private static string BuildPrivacyNutritionMarkdown()
		{
			var sb = new StringBuilder();
			sb.AppendLine("# Privacy Nutrition Label (draft)");
			sb.AppendLine();
			sb.AppendLine("Generated from `ProjectSettings/MobileServicesSettings.asset`. Review and refine before App Store submission.");
			sb.AppendLine();
			sb.AppendLine("## Data Used to Track You");
			if (MobileServicesSettings.instance.Capabilities.AppTracking)
			{
				sb.AppendLine("- Identifiers (advertising / device IDs) — App Tracking Transparency is enabled. Configure the categories per the runtime `AttService` call site.");
			}
			else
			{
				sb.AppendLine("- (none — App Tracking is disabled)");
			}
			sb.AppendLine();
			sb.AppendLine("## Data Linked to You");
			foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
			{
				if (MobileServicesSettings.GetIosUsageKey(p) == null) continue;
				var copy = MobileServicesSettings.instance.GetUsageDescriptionEn(p);
				if (string.IsNullOrWhiteSpace(copy)) continue;
				sb.AppendLine($"- **{p}** — {copy}");
			}
			sb.AppendLine();
			sb.AppendLine("## Data Not Collected");
			sb.AppendLine("- (review the bound services and document anything that is genuinely not collected)");
			return sb.ToString();
		}

		private static Label MakeSectionLabel(string text)
		{
			var lbl = new Label(text);
			lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
			lbl.style.color = new Color(0.7f, 0.85f, 1f);
			lbl.style.fontSize = 12;
			lbl.style.marginTop = 12;
			lbl.style.marginBottom = 2;
			return lbl;
		}

		private static void PersistAndRefresh()
		{
			MobileServicesSettings.instance.Persist();
			UpdateStatusBadge();
		}

		private static void UpdateStatusBadge()
		{
			if (_statusBadge == null) return;

			var missingCount = CountMissingDescriptions();
			if (missingCount == 0)
			{
				_statusBadge.text = "All required keys configured";
				_statusBadge.style.color = new Color(0.5f, 0.95f, 0.5f);
				_statusBadge.style.backgroundColor = new Color(0.2f, 0.65f, 0.2f, 0.30f);
			}
			else
			{
				_statusBadge.text = $"{missingCount} missing key(s) — fix before iOS build";
				_statusBadge.style.color = new Color(1f, 0.85f, 0.85f);
				_statusBadge.style.backgroundColor = new Color(0.78f, 0.2f, 0.2f, 0.35f);
			}
		}

		private static int CountMissingDescriptions()
		{
			var settings = MobileServicesSettings.instance;
			var count = 0;
			foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
			{
				if (MobileServicesSettings.GetIosUsageKey(p) == null) continue;
				if (string.IsNullOrWhiteSpace(settings.GetUsageDescriptionEn(p)))
				{
					count++;
				}
			}
			if (settings.IsAttUsageDescriptionMissing())
			{
				count++;
			}
			return count;
		}
	}
}

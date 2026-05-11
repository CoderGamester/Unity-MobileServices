using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Explorer.Overlays;
using GameLovers.MobileServices.Editor.Explorer.Windows;
using GameLovers.MobileServices.Editor.Settings;
using GameLovers.MobileServices.Editor.Simulation;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.DeviceSimulatorPanel
{
	/// <summary>
	/// <see cref="DeviceSimulatorPlugin"/> implementation that embeds a slim action-button control
	/// panel inside Unity's Device Simulator window (Window &gt; General &gt; Device Simulator).
	/// Drives the in-Game-view <c>MobileSimulatorRuntimeOverlay</c> via the
	/// <see cref="MobileSimulatorState"/> broker so designers can iterate on mobile UI surfaces with
	/// the mocks rendered right inside the simulated phone screen.
	/// </summary>
	/// <remarks>
	/// <para><b>Scoped to the runtime overlay.</b> Every <c>Push*</c> call from this panel passes
	/// <see cref="SimulatorTarget.RuntimeOverlay"/>, so the standalone <c>MobileSimulatorWindow</c>
	/// is never affected by the plugin — its platform skin and its mocks are independent.</para>
	/// <para>Auto-syncs <see cref="MobileSimulatorState.OverlayPlatform"/> (the runtime overlay's
	/// platform skin) from the selected device profile via <c>Application.platform</c>, which
	/// Unity's Device Simulator spoofs for iOS / Android device picks.
	/// <see cref="MobileSimulatorState.WindowPlatform"/> is intentionally left alone so the
	/// Explorer's <c>Render as: iOS | Android</c> dropdown stays interactive and authoritative
	/// for its own surface.</para>
	/// <para>Unity auto-discovers <c>DeviceSimulatorPlugin</c> subclasses across all editor
	/// assemblies — no attribute, no registration boilerplate is needed.</para>
	/// </remarks>
	internal sealed class MobileServicesDeviceSimulatorPlugin : DeviceSimulatorPlugin
	{
		private const SimulatorTarget OverlayOnly = SimulatorTarget.RuntimeOverlay;
		private const string DefaultAlertTitle = "Delete Save?";
		private const string DefaultAlertMessage = "This action cannot be undone.";
		private const string DefaultToastMessage = "Item Collected!";
		private const string DefaultShareText = "Check out my high score!";
		private const string DefaultShareUrl = "https://example.com/game";
		private const string DefaultNotificationTitle = "Reward ready!";
		private const string DefaultNotificationBody = "Your daily quest reward is waiting.";
		private const string DefaultDeepLinkUri = "myapp://promo/spring2026";

		public override string title => "Mobile Services";

		private Label _overlayHintBanner;
		private Button _overlayHintOpenSettingsBtn;

		public override void OnCreate()
		{
			SyncPlatformFromHost();
		}

		public override VisualElement OnCreateUI()
		{
			var root = new VisualElement { name = "mobile-services-plugin-root" };
			LoadStyleSheet(root);

			root.Add(BuildHeader());
			root.Add(BuildOverlayHintBanner());
			root.Add(BuildNativeUiSection());
			root.Add(BuildNotificationsSection());
			root.Add(BuildDeviceSection());
			root.Add(BuildPermissionsSection());
			root.Add(BuildAttSection());
			root.Add(BuildDeepLinkSection());

			// Re-sync the platform skin from Unity's Device Simulator on a cheap poll. Using
			// DeviceSimulator.deviceChanged would be slightly tidier but its delegate signature
			// is documented inconsistently across Unity 6 minor versions; reading Application.platform
			// (which the simulator spoofs for iOS / Android device profile picks) is identical in
			// outcome and version-agnostic. Same poll refreshes the overlay-required banner so the
			// hint disappears the moment the user enables the setting and presses Play (no extra
			// EditorApplication.playModeStateChanged subscription needed).
			root.schedule.Execute(() =>
			{
				SyncPlatformFromHost();
				RefreshOverlayHintBanner();
			}).Every(500);

			return root;
		}

		/// <summary>
		/// Reads <see cref="Application.platform"/> — Unity's Device Simulator spoofs it to match
		/// the selected device profile, so an iPhone pick yields <c>IPhonePlayer</c> and a Pixel
		/// pick yields <c>Android</c>. Writes only to <see cref="MobileSimulatorState.OverlayPlatform"/>
		/// so the Explorer's standalone-window dropdown is never clobbered.
		/// </summary>
		private static void SyncPlatformFromHost()
		{
			switch (Application.platform)
			{
				case RuntimePlatform.IPhonePlayer:
					MobileSimulatorState.OverlayPlatform = SimulatedPlatform.iOS;
					break;
				case RuntimePlatform.Android:
					MobileSimulatorState.OverlayPlatform = SimulatedPlatform.Android;
					break;
			}
		}

		private static VisualElement BuildHeader()
		{
			var header = new VisualElement { name = "msp-header" };
			header.AddToClassList("msp-header");

			var title = new Label("Mobile Services");
			title.AddToClassList("msp-title");
			header.Add(title);

			var note = new Label("Drives the in-Game-view runtime overlay. Pair with the Explorer for full-state diagnostics.");
			note.AddToClassList("msp-note");
			header.Add(note);

			var buttonRow = new VisualElement();
			buttonRow.AddToClassList("msp-button-row");

			var explorerBtn = new Button(() => MobileServicesExplorerWindow.Open()) { text = "Open full Explorer \u2192" };
			explorerBtn.AddToClassList("msp-button");
			buttonRow.Add(explorerBtn);

			// Scoped to the runtime overlay so the standalone Simulator window keeps any mocks the
			// Explorer has open. Use EditorPlatformSimulator.DismissAllOverlays() for the global form.
			var dismissBtn = new Button(() => MobileSimulatorState.PushDismissAll(OverlayOnly)) { text = "Dismiss all mocks" };
			dismissBtn.AddToClassList("msp-button");
			dismissBtn.AddToClassList("msp-button-danger");
			buttonRow.Add(dismissBtn);

			header.Add(buttonRow);
			return header;
		}

		/// <summary>
		/// Inline banner spelling out the overlay-required precondition for this panel: the runtime
		/// overlay is opt-in (Project Settings → GameLovers → Mobile Services → Editor tooling →
		/// Enable runtime simulator overlay) AND only spawns on EnteredPlayMode. Without it, every
		/// mock fired here goes nowhere visible — by design, since the panel is scoped to
		/// <see cref="SimulatorTarget.RuntimeOverlay"/>. Banner auto-hides once both preconditions
		/// are satisfied; the 500 ms poll alongside <see cref="SyncPlatformFromHost"/> drives the
		/// refresh so toggling the setting + pressing Play removes the banner without needing the
		/// user to reopen the panel.
		/// </summary>
		private VisualElement BuildOverlayHintBanner()
		{
			var banner = new VisualElement { name = "msp-overlay-hint" };
			banner.AddToClassList("msp-overlay-hint");

			_overlayHintBanner = new Label();
			_overlayHintBanner.AddToClassList("msp-overlay-hint-label");
			banner.Add(_overlayHintBanner);

			_overlayHintOpenSettingsBtn = new Button(() =>
				SettingsService.OpenProjectSettings("Project/GameLovers/Mobile Services"))
			{
				text = "Open Project Settings \u2192",
			};
			_overlayHintOpenSettingsBtn.AddToClassList("msp-button");
			banner.Add(_overlayHintOpenSettingsBtn);

			RefreshOverlayHintBanner();
			return banner;
		}

		private void RefreshOverlayHintBanner()
		{
			if (_overlayHintBanner == null)
			{
				return;
			}

			var enabled = MobileServicesSettings.instance.EnableRuntimeSimulatorOverlay;
			var inPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;
			var overlayLive = enabled && inPlayMode;

			_overlayHintBanner.parent.style.display = overlayLive ? DisplayStyle.None : DisplayStyle.Flex;
			if (overlayLive)
			{
				return;
			}

			if (!enabled)
			{
				_overlayHintBanner.text = "Runtime overlay is OFF. Enable it in Project Settings → GameLovers → Mobile Services → Editor tooling, then press Play. Until then, mocks fired from this panel render nowhere (the panel is scoped to the overlay).";
				_overlayHintOpenSettingsBtn.style.display = DisplayStyle.Flex;
			}
			else
			{
				_overlayHintBanner.text = "Runtime overlay is enabled but not alive yet — press Play. Mocks fired now will render once the overlay spawns on EnteredPlayMode.";
				_overlayHintOpenSettingsBtn.style.display = DisplayStyle.None;
			}
		}

		private static VisualElement BuildNativeUiSection()
		{
			var foldout = new Foldout { text = "Native UI", value = true };
			foldout.AddToClassList("msp-foldout");

			foldout.Add(MakeActionButton("Alert (modal)", () => PushAlert(isSheet: false)));
			foldout.Add(MakeActionButton("Action Sheet", () => PushAlert(isSheet: true)));
			foldout.Add(MakeActionButton("Toast (short)", () => MobileSimulatorState.PushToast(new SimulatedToastSpec
			{
				Message = DefaultToastMessage,
				IsLongDuration = false,
			}, OverlayOnly)));
			foldout.Add(MakeActionButton("Toast (long)", () => MobileSimulatorState.PushToast(new SimulatedToastSpec
			{
				Message = DefaultToastMessage,
				IsLongDuration = true,
			}, OverlayOnly)));
			foldout.Add(MakeActionButton("Share", () => MobileSimulatorState.PushShare(new SimulatedShareSpec
			{
				Text = DefaultShareText,
				Url = DefaultShareUrl,
			}, OverlayOnly)));
			foldout.Add(MakeActionButton("Review prompt", () => MobileSimulatorState.PushReview(OverlayOnly)));

			return foldout;
		}

		private static VisualElement BuildNotificationsSection()
		{
			var foldout = new Foldout { text = "Notifications", value = true };
			foldout.AddToClassList("msp-foldout");

			foldout.Add(MakeActionButton("Heads-up banner", () => MobileSimulatorState.PushNotificationBanner(new SimulatedNotificationBannerSpec
			{
				ChannelName = "Rewards",
				Title = DefaultNotificationTitle,
				Body = DefaultNotificationBody,
			}, OverlayOnly)));

			return foldout;
		}

		private static VisualElement BuildDeviceSection()
		{
			var foldout = new Foldout { text = "Device state", value = true };
			foldout.AddToClassList("msp-foldout");

			var lpm = new Toggle("Low-power mode") { value = false };
			lpm.RegisterValueChangedCallback(evt =>
			{
				// No per-service fan-out from the plugin — services are plain CLR objects, not
				// UnityEngine.Object subclasses, so FindObjectsByType cannot reach them. The static
				// override is still set; the next poll tick on the service surfaces the change.
				EditorPlatformSimulator.SetIosLowPowerMode(evt.newValue);
			});
			foldout.Add(lpm);

			var connectivityField = new EnumField("Connectivity", NetworkReachability.ReachableViaLocalAreaNetwork);
			connectivityField.RegisterValueChangedCallback(evt =>
			{
				EditorPlatformSimulator.SetConnectivity((NetworkReachability)evt.newValue);
			});
			foldout.Add(connectivityField);

			return foldout;
		}

		private static VisualElement BuildPermissionsSection()
		{
			var foldout = new Foldout { text = "Permissions", value = false };
			foldout.AddToClassList("msp-foldout");

			var picker = new EnumField("Permission", AppPermission.Camera);
			foldout.Add(picker);

			var showBtn = MakeActionButton("Show OS prompt", () =>
			{
				var p = (AppPermission)picker.value;
				MobileSimulatorState.PushPermissionDialog(new SimulatedPermissionDialogSpec
				{
					TypeName = p.ToString(),
					UsageDescription = MobileServicesSettings.instance.GetUsageDescriptionEn(p),
					IsAtt = false,
					OnResolved = null,
				}, OverlayOnly);
			});
			foldout.Add(showBtn);

			var queuedResult = new EnumField("Queue next request result", PermissionStatus.Granted);
			queuedResult.RegisterValueChangedCallback(evt =>
			{
				EditorPlatformSimulator.QueuePermissionResult((AppPermission)picker.value, (PermissionStatus)evt.newValue);
			});
			foldout.Add(queuedResult);

			return foldout;
		}

		private static VisualElement BuildAttSection()
		{
			var foldout = new Foldout { text = "App Tracking Transparency", value = false };
			foldout.AddToClassList("msp-foldout");

			foldout.Add(MakeActionButton("Show ATT prompt", () =>
			{
				MobileSimulatorState.PushPermissionDialog(new SimulatedPermissionDialogSpec
				{
					TypeName = "Tracking",
					UsageDescription = MobileServicesSettings.instance.GetAttUsageDescriptionEn(),
					IsAtt = true,
					OnResolved = null,
				}, OverlayOnly);
			}));

			var queuedResult = new EnumField("Queue next request result", AttStatus.Authorized);
			queuedResult.RegisterValueChangedCallback(evt =>
			{
				EditorPlatformSimulator.QueueAttResult((AttStatus)evt.newValue);
			});
			foldout.Add(queuedResult);

			return foldout;
		}

		private static VisualElement BuildDeepLinkSection()
		{
			var foldout = new Foldout { text = "Deep links", value = false };
			foldout.AddToClassList("msp-foldout");

			var uriField = new TextField("URI") { value = DefaultDeepLinkUri };
			foldout.Add(uriField);

			foldout.Add(MakeActionButton("Send test link", () =>
			{
				if (!Uri.TryCreate(uriField.value, UriKind.Absolute, out var uri))
				{
					Debug.LogWarning($"[Mobile Services] Invalid deep-link URI: {uriField.value}");
					return;
				}
				// DeepLinkService is a plain CLR class, not a UnityEngine.Object — the plugin has
				// no way to discover live instances. Logging keeps the action discoverable while
				// being honest about the limitation; consumers with a service-locator can wire in
				// their own bridge if they need this to drive the live runtime service.
				Debug.Log($"[Mobile Services] Deep link send requested: {uri} (the plugin cannot reach a live DeepLinkService instance; call EditorPlatformSimulator.SimulateDeepLink(uri, service) from your bootstrap to deliver).");
			}));

			return foldout;
		}

		private static Button MakeActionButton(string text, Action onClick)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("msp-button");
			return btn;
		}

		private static void PushAlert(bool isSheet)
		{
			MobileSimulatorState.PushAlert(new SimulatedAlertSpec
			{
				Title = DefaultAlertTitle,
				Message = DefaultAlertMessage,
				IsActionSheet = isSheet,
				Buttons = new List<SimulatedAlertButton>
				{
					new SimulatedAlertButton { Text = "Cancel", Style = SimulatedAlertButtonStyle.Cancel },
					new SimulatedAlertButton { Text = "Delete", Style = SimulatedAlertButtonStyle.Destructive },
				},
			}, OverlayOnly);
		}

		private static void LoadStyleSheet(VisualElement root)
		{
			var guids = AssetDatabase.FindAssets("MobileServicesDeviceSimulatorPanel t:StyleSheet");
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (path.EndsWith("MobileServicesDeviceSimulatorPanel.uss"))
				{
					var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
					if (sheet != null)
					{
						root.styleSheets.Add(sheet);
					}
					return;
				}
			}
		}
	}
}

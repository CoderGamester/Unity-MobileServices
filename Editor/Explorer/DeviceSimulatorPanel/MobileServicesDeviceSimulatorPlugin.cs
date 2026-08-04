using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Explorer.Overlays;
using GameLovers.MobileServices.Editor.Simulation;
using GameLovers.MobileServices.Gestures;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Haptics.Internal;
using GameLovers.MobileServices.NativeUi;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.DeviceSimulatorPanel
{
	/// <summary>
	/// The single Mobile Services editor surface: a <see cref="DeviceSimulatorPlugin"/> that embeds
	/// the controls + live diagnostics + haptic envelope graph inside Unity's Device Simulator window
	/// (Window &gt; General &gt; Device Simulator), driving the in-Game-view
	/// <c>MobileSimulatorRuntimeOverlay</c> via the <see cref="MobileSimulatorState"/> broker so the
	/// mocks render right inside the simulated phone screen (edit mode included).
	/// </summary>
	/// <remarks>
	/// Replaces the former standalone Explorer + Simulator windows. The overlay is kept alive for as
	/// long as this panel is open (<see cref="MobileSimulatorRuntimeOverlay.NotifyPluginActive"/>),
	/// so anything fired here paints immediately, in edit or play mode. The controls that drive live
	/// state (Permission / ATT / connectivity state, gestures) require Play mode; the mock previews and
	/// the haptic envelope graph render in edit mode. The platform skin auto-syncs from
	/// the selected device profile via <c>Application.platform</c> (spoofed by the Device Simulator),
	/// which is robust across Unity 6 minor versions where <c>DeviceSimulator.deviceChanged</c> varies.
	/// </remarks>
	internal sealed class MobileServicesDeviceSimulatorPlugin : DeviceSimulatorPlugin
	{
		private const string DefaultAlertTitle = "Delete Save?";
		private const string DefaultAlertMessage = "This action cannot be undone.";
		private const string DefaultToastMessage = "Item Collected!";
		private const string DefaultShareText = "Check out my high score!";
		private const string DefaultShareUrl = "https://example.com/game";
		private const string DefaultNotificationTitle = "Reward ready!";
		private const string DefaultNotificationBody = "Your daily quest reward is waiting.";
		private const string PlayModeTooltip = "Requires Play mode — this spawns a runtime host or reads/drives live device state.";
		private const string DisabledTooltip = "Disabled in edit mode — this reads/drives live device state.";

		// ---- User-facing panel copy (edit here to tweak wording) ----
		private const string HeaderNote = "Fires native-UI mocks into the simulated phone screen (edit + play). Live diagnostics below need Play mode.";
		private const string PlayModeBannerText = "Some controls are disabled until you enter Play mode, they spawn runtime hosts or read/drive live device state. Mock previews, haptic presets, and the envelope graph work in edit mode.";
		private const string HapticsNote = "Haptics fire only on a physical device. Select a preset to inspect its vibration envelope (the editor calibration cue).";
		private const string NotificationsNote = "Preview the heads-up banner (works in edit mode). For live scheduling / channels / queueing / pending, use the NotificationsScheduler sample — it drives the game's own service.";
		private const string PermissionsInfo = "The first runtime RequestAsync() on a NotDetermined permission shows the OS prompt in the overlay; afterwards it is cached.";
		private const string AttInfo = "The first runtime RequestAuthorizationAsync() on a NotDetermined status shows the ATT prompt in the overlay (iOS skin only); afterwards it is cached.";
		private const float EnvelopePlotHeight = 110f;
		private const float EnvelopeYAxisWidth = 30f;

		/// <inheritdoc />
		public override string title => "Mobile Services";

		private readonly List<VisualElement> _playModeControls = new List<VisualElement>();
		private readonly List<VisualElement> _editModeBanners = new List<VisualElement>();

		private Toggle _enabledToggle;
		private VisualElement _sectionsContainer;

		// ---- Held service instances ----
		private readonly PermissionsService _permissions = new PermissionsService();
		private readonly AttService _att = new AttService();

		// ---- Native UI authoring fields ----
		private TextField _alertTitle;
		private TextField _alertMessage;
		private Button _actionSheetBtn;
		private TextField _toastMessage;
		private Toggle _toastLongDuration;
		private TextField _shareText;
		private TextField _shareUrl;

		// ---- Haptics (envelope preview only — haptics never fire in the editor) ----
		private HapticPreset _previewPreset = HapticPreset.Selection;
		private VisualElement _envelopeCanvas;
		private Label _envelopeMaxLabel;

		// ---- Gestures diagnostics ----
		private GestureController _gestureController;
		// Auto-spawned in play mode when the scene has no GestureController, so the user needs zero
		// setup. Destroyed on play exit / panel close.
		private GameObject _spawnedGestureHost;
		private GestureController _spawnedGestureController;
		private bool _touchSimEnabled;
		private SwipeInput _lastSwipe;
		private TapInput _lastTap;
		private bool _hasSwipe;
		private bool _hasTap;
		private Label _gestureStatus;
		private Label _gestureSwipe;
		private Label _gestureTap;

		// ---- Permissions state controls ----
		private readonly Dictionary<AppPermission, EnumField> _permStateFields = new Dictionary<AppPermission, EnumField>();
		private VisualElement _permPendingRow;
		private Label _permPendingLabel;
		private AppPermission? _permPending;

		// ---- ATT state control ----
		private EnumField _attStateField;
		private VisualElement _attPendingRow;

		/// <inheritdoc />
		public override void OnCreate()
		{
			SyncPlatformFromHost();
			MobileSimulatorRuntimeOverlay.NotifyPluginActive(true);
		}

		/// <inheritdoc />
		public override void OnDestroy()
		{
			MobileSimulatorRuntimeOverlay.NotifyPluginActive(false);
			EditorPlatformSimulator.Disengage();
			DetachGestureController();
			CleanupSpawnedGestures();
		}

		/// <inheritdoc />
		public override VisualElement OnCreateUI()
		{
			var root = new VisualElement { name = "mobile-services-plugin-root" };
			LoadStyleSheet(root);

			// OnCreateUI can run again (panel re-docked); drop stale control refs from the prior tree.
			_playModeControls.Clear();
			_editModeBanners.Clear();

			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.style.flexGrow = 1;

			scroll.Add(BuildHeader());

			// Everything below the header is gated as a group by the master switch; the header itself
			// stays interactive so the simulator can always be turned back on.
			_sectionsContainer = new VisualElement { name = "msp-sections" };
			_sectionsContainer.Add(BuildPlayModeBanner());
			_sectionsContainer.Add(BuildNativeUiSection());
			_sectionsContainer.Add(BuildHapticsSection());
			_sectionsContainer.Add(BuildNotificationsSection());
			_sectionsContainer.Add(BuildGesturesSection());
			_sectionsContainer.Add(BuildPermissionsSection());
			_sectionsContainer.Add(BuildAttSection());
			scroll.Add(_sectionsContainer);

			root.Add(scroll);

			// One cheap poll re-syncs the platform skin from the selected device profile AND refreshes
			// the live-state read-outs. Reading Application.platform (which the Device Simulator spoofs
			// for iOS / Android picks) is version-agnostic vs DeviceSimulator.deviceChanged.
			root.schedule.Execute(() =>
			{
				SyncPlatformFromHost();
				RefreshDiagnostics();
				RefreshPlayModeGating();
			}).Every(500);

			RebuildEnvelope();
			// Engage before the first diagnostics refresh so Check() / CurrentStatus already read the
			// simulated store when the dropdowns sync.
			ApplyEnabledState(MobileSimulatorState.Enabled);
			RefreshDiagnostics();
			RefreshPlayModeGating();
			ApplyActionSheetButtonState(MobileSimulatorState.Platform);
			MobileSimulatorState.PlatformChanged += OnPlatformChanged;
			MobileSimulatorState.EnabledChanged += OnEnabledChanged;
			root.RegisterCallback<DetachFromPanelEvent>(_ =>
			{
				MobileSimulatorState.PlatformChanged -= OnPlatformChanged;
				MobileSimulatorState.EnabledChanged -= OnEnabledChanged;
			});

			return root;
		}

		private static void SyncPlatformFromHost()
		{
			switch (Application.platform)
			{
				case RuntimePlatform.IPhonePlayer:
					MobileSimulatorState.Platform = SimulatedPlatform.iOS;
					break;
				case RuntimePlatform.Android:
					MobileSimulatorState.Platform = SimulatedPlatform.Android;
					break;
			}
		}

		private VisualElement BuildHeader()
		{
			var header = new VisualElement { name = "msp-header" };
			header.AddToClassList("msp-header");

			var titleLabel = new Label("Mobile Services");
			titleLabel.AddToClassList("msp-title");
			header.Add(titleLabel);

			var note = new Label(HeaderNote);
			note.AddToClassList("msp-note");
			header.Add(note);

			_enabledToggle = new Toggle("Editor Simulator") { value = MobileSimulatorState.Enabled };
			_enabledToggle.AddToClassList("msp-enabled-toggle");
			_enabledToggle.RegisterValueChangedCallback(evt => MobileSimulatorState.Enabled = evt.newValue);
			header.Add(_enabledToggle);

			return header;
		}

		private void OnEnabledChanged(bool enabled) => ApplyEnabledState(enabled);

		private void ApplyEnabledState(bool enabled)
		{
			_enabledToggle?.SetValueWithoutNotify(enabled);
			_sectionsContainer?.SetEnabled(enabled);
			if (enabled)
			{
				EditorPlatformSimulator.Engage();
			}
			else
			{
				EditorPlatformSimulator.Disengage();
				MobileSimulatorState.PushDismissAll();
			}
		}

		private VisualElement BuildPlayModeBanner()
		{
			var banner = new VisualElement { name = "msp-playmode-hint" };
			banner.AddToClassList("msp-overlay-hint");

			var label = new Label(PlayModeBannerText);
			label.AddToClassList("msp-overlay-hint-label");
			banner.Add(label);

			_editModeBanners.Add(banner);
			return banner;
		}

		private T GatePlayMode<T>(T control) where T : VisualElement
		{
			_playModeControls.Add(control);
			return control;
		}

		private void RefreshPlayModeGating()
		{
			var isPlaying = Application.isPlaying;
			foreach (var control in _playModeControls)
			{
				control.SetEnabled(isPlaying);
				control.tooltip = isPlaying ? null : PlayModeTooltip;
			}
			foreach (var banner in _editModeBanners)
			{
				banner.style.display = isPlaying ? DisplayStyle.None : DisplayStyle.Flex;
			}
		}

		// ---- Native UI ----

		private VisualElement BuildNativeUiSection()
		{
			var foldout = new Foldout { text = "Native UI", value = true };
			foldout.AddToClassList("msp-foldout");

			_alertTitle = new TextField("Alert title") { value = DefaultAlertTitle };
			_alertMessage = new TextField("Alert message") { value = DefaultAlertMessage };
			foldout.Add(_alertTitle);
			foldout.Add(_alertMessage);

			foldout.Add(MakeActionButton("Alert (modal)", () => PushAlert(isSheet: false)));
			_actionSheetBtn = MakeActionButton("Action Sheet", () => PushAlert(isSheet: true));
			foldout.Add(_actionSheetBtn);

			_toastMessage = new TextField("Toast") { value = DefaultToastMessage };
			_toastLongDuration = new Toggle("Long duration") { value = false };
			foldout.Add(_toastMessage);
			foldout.Add(_toastLongDuration);
			foldout.Add(MakeActionButton("Show Toast", () =>
			{
				MobileSimulatorState.PushToast(new SimulatedToastSpec
				{
					Message = _toastMessage.value,
					IsLongDuration = _toastLongDuration.value,
				});
				NativeUiService.ShowToastMessage(_toastMessage.value, _toastLongDuration.value);
			}));

			_shareText = new TextField("Share text") { value = DefaultShareText };
			_shareUrl = new TextField("Share URL") { value = DefaultShareUrl };
			foldout.Add(_shareText);
			foldout.Add(_shareUrl);
			foldout.Add(MakeActionButton("Share", () =>
			{
				MobileSimulatorState.PushShare(new SimulatedShareSpec { Text = _shareText.value, Url = _shareUrl.value });
				NativeUiService.Share(_shareText.value, _shareUrl.value);
			}));

			var dismissBtn = MakeActionButton("Dismiss all UIs", MobileSimulatorState.PushDismissAll);
			dismissBtn.AddToClassList("msp-button-danger");
			foldout.Add(dismissBtn);

			return foldout;
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

		private void OnPlatformChanged(SimulatedPlatform platform) => ApplyActionSheetButtonState(platform);

		private void ApplyActionSheetButtonState(SimulatedPlatform platform)
		{
			if (_actionSheetBtn == null)
			{
				return;
			}
			// Android has no native action-sheet idiom — it collapses onto the same Material 3 dialog.
			var isAndroid = platform == SimulatedPlatform.Android;
			_actionSheetBtn.SetEnabled(!isAndroid);
			_actionSheetBtn.tooltip = isAndroid
				? "Disabled on Android: no native action-sheet idiom. Switch the device to an iPhone to drive the distinct sheet shape."
				: null;
		}

		// ---- Haptics (envelope preview only) ----

		private VisualElement BuildHapticsSection()
		{
			var foldout = new Foldout { text = "Haptics", value = false };
			foldout.AddToClassList("msp-foldout");

			var note = new Label(HapticsNote);
			note.AddToClassList("msp-note");
			foldout.Add(note);

			var presetGrid = new VisualElement();
			presetGrid.style.flexDirection = FlexDirection.Row;
			presetGrid.style.flexWrap = Wrap.Wrap;
			foreach (HapticPreset preset in Enum.GetValues(typeof(HapticPreset)))
			{
				if (preset == HapticPreset.None)
				{
					continue;
				}
				var captured = preset;
				var btn = new Button(() =>
				{
					_previewPreset = captured;
					RebuildEnvelope();
				}) { text = preset.ToString() };
				btn.style.marginRight = 3;
				btn.style.marginBottom = 3;
				presetGrid.Add(btn);
			}
			foldout.Add(presetGrid);

			foldout.Add(BuildEnvelopeGraph());

			return foldout;
		}

		// Step curve, not a smooth line: each HapticEnvelopes segment holds its amplitude for its
		// duration, which is exactly how VibrationEffect.createWaveform plays it.
		private VisualElement BuildEnvelopeGraph()
		{
			var graph = new VisualElement();
			graph.style.marginTop = 4;
			graph.style.marginBottom = 4;

			var title = new Label("Intensity over time");
			title.AddToClassList("tab-section-label");
			graph.Add(title);

			var plotRow = new VisualElement();
			plotRow.style.flexDirection = FlexDirection.Row;

			var yAxis = new VisualElement();
			yAxis.style.width = EnvelopeYAxisWidth;
			yAxis.style.height = EnvelopePlotHeight;
			yAxis.style.justifyContent = Justify.SpaceBetween;
			yAxis.style.alignItems = Align.FlexEnd;
			yAxis.style.paddingRight = 4;
			yAxis.Add(MakeAxisTick("1.0"));
			yAxis.Add(MakeAxisTick("0.5"));
			yAxis.Add(MakeAxisTick("0"));
			plotRow.Add(yAxis);

			_envelopeCanvas = new VisualElement();
			_envelopeCanvas.AddToClassList("haptic-envelope-canvas");
			_envelopeCanvas.style.height = EnvelopePlotHeight;
			_envelopeCanvas.style.flexGrow = 1;
			var canvas = _envelopeCanvas;
			_envelopeCanvas.generateVisualContent += mgc => PaintEnvelope(mgc, canvas);
			plotRow.Add(_envelopeCanvas);

			graph.Add(plotRow);

			var xAxis = new VisualElement();
			xAxis.style.flexDirection = FlexDirection.Row;
			xAxis.style.marginLeft = EnvelopeYAxisWidth;
			xAxis.Add(MakeAxisTick("0 ms"));
			var spacer = new VisualElement();
			spacer.style.flexGrow = 1;
			xAxis.Add(spacer);
			_envelopeMaxLabel = MakeAxisTick("");
			xAxis.Add(_envelopeMaxLabel);
			graph.Add(xAxis);

			var caption = new Label("x: time (ms)   ·   y: intensity (0–1)");
			caption.AddToClassList("msp-note");
			graph.Add(caption);

			return graph;
		}

		private static Label MakeAxisTick(string text)
		{
			var lbl = new Label(text);
			lbl.style.fontSize = 9;
			lbl.style.color = new Color(0.6f, 0.65f, 0.72f);
			return lbl;
		}

		private void RebuildEnvelope()
		{
			if (_envelopeCanvas == null)
			{
				return;
			}
			var (timesSec, _) = HapticEnvelopes.GetFloatEnvelopeFor(_previewPreset);
			var totalMs = 0f;
			if (timesSec != null)
			{
				for (var i = 0; i < timesSec.Length; i++)
				{
					totalMs += timesSec[i] * 1000f;
				}
			}
			if (_envelopeMaxLabel != null)
			{
				_envelopeMaxLabel.text = $"{totalMs:F0} ms";
			}
			_envelopeCanvas.MarkDirtyRepaint();
		}

		private void PaintEnvelope(MeshGenerationContext mgc, VisualElement canvas)
		{
			var rect = canvas.contentRect;
			var w = rect.width;
			var h = rect.height;
			if (w <= 1f || h <= 1f)
			{
				return;
			}

			var painter = mgc.painter2D;

			// Intensity grid lines (0 / 0.25 / 0.5 / 0.75 / 1.0).
			painter.lineWidth = 1f;
			painter.strokeColor = new Color(1f, 1f, 1f, 0.07f);
			for (var i = 0; i <= 4; i++)
			{
				var gy = h * i / 4f;
				painter.BeginPath();
				painter.MoveTo(new Vector2(0f, gy));
				painter.LineTo(new Vector2(w, gy));
				painter.Stroke();
			}

			var (timesSec, amps) = HapticEnvelopes.GetFloatEnvelopeFor(_previewPreset);
			if (timesSec == null || timesSec.Length == 0)
			{
				return;
			}
			var total = 0f;
			for (var i = 0; i < timesSec.Length; i++)
			{
				total += timesSec[i];
			}
			if (total <= 0f)
			{
				return;
			}

			const float pad = 2f;
			var plotH = h - pad * 2f;
			float X(float tCumSec) => tCumSec / total * w;
			float Y(float amp) => pad + (1f - Mathf.Clamp01(amp)) * plotH;

			// Build the step curve: each segment holds its amplitude for its duration, then steps to
			// the next segment's amplitude.
			var pts = new List<Vector2>();
			var cum = 0f;
			pts.Add(new Vector2(X(0f), Y(amps[0])));
			for (var i = 0; i < timesSec.Length; i++)
			{
				cum += timesSec[i];
				pts.Add(new Vector2(X(cum), Y(amps[i])));
				if (i < timesSec.Length - 1)
				{
					pts.Add(new Vector2(X(cum), Y(amps[i + 1])));
				}
			}

			// Filled area under the curve.
			painter.fillColor = new Color(0.47f, 0.82f, 1f, 0.18f);
			painter.BeginPath();
			painter.MoveTo(new Vector2(X(0f), Y(0f)));
			foreach (var p in pts)
			{
				painter.LineTo(p);
			}
			painter.LineTo(new Vector2(X(total), Y(0f)));
			painter.ClosePath();
			painter.Fill();

			// The curve stroke.
			painter.lineWidth = 2f;
			painter.strokeColor = new Color(0.47f, 0.82f, 1f, 0.95f);
			painter.BeginPath();
			painter.MoveTo(pts[0]);
			for (var i = 1; i < pts.Count; i++)
			{
				painter.LineTo(pts[i]);
			}
			painter.Stroke();
		}

		// ---- Notifications ----

		private VisualElement BuildNotificationsSection()
		{
			var foldout = new Foldout { text = "Notifications", value = false };
			foldout.AddToClassList("msp-foldout");

			// Preview-only by design: live scheduling / channels / queueing would run on a throwaway
			// MobileNotificationService disconnected from the game, duplicating the NotificationsScheduler sample.
			var note = new Label(NotificationsNote);
			note.AddToClassList("msp-note");
			foldout.Add(note);

			foldout.Add(MakeActionButton("Show heads-up banner", () => MobileSimulatorState.PushNotificationBanner(new SimulatedNotificationBannerSpec
			{
				ChannelName = "Rewards",
				Title = DefaultNotificationTitle,
				Body = DefaultNotificationBody,
			})));

			var dismissBtn = MakeActionButton("Dismiss Banner", MobileSimulatorState.PushDismissAll);
			dismissBtn.AddToClassList("msp-button-danger");
			foldout.Add(dismissBtn);

			return foldout;
		}

		// ---- Gestures ----

		private VisualElement BuildGesturesSection()
		{
			var foldout = new Foldout { text = "Gestures", value = false };
			foldout.AddToClassList("msp-foldout");

			_gestureStatus = new Label("Enter Play mode to scan for GestureController.");
			foldout.Add(_gestureStatus);

			var swipeLabel = new Label("Last swipe");
			swipeLabel.AddToClassList("tab-section-label");
			foldout.Add(swipeLabel);
			_gestureSwipe = new Label("(none)");
			foldout.Add(_gestureSwipe);

			var tapLabel = new Label("Last tap");
			tapLabel.AddToClassList("tab-section-label");
			foldout.Add(tapLabel);
			_gestureTap = new Label("(none)");
			foldout.Add(_gestureTap);

			var resetBtn = MakeActionButton("Reset", () =>
			{
				_hasSwipe = false;
				_hasTap = false;
				_gestureSwipe.text = "(none)";
				_gestureTap.text = "(none)";
			});
			resetBtn.AddToClassList("msp-button-danger");
			foldout.Add(resetBtn);

			return foldout;
		}

		private void OnSwiped(SwipeInput swipe)
		{
			_lastSwipe = swipe;
			_hasSwipe = true;
		}

		private void OnTapped(TapInput tap)
		{
			_lastTap = tap;
			_hasTap = true;
		}

		private void DetachGestureController()
		{
			if (_gestureController == null)
			{
				return;
			}
			_gestureController.Swiped -= OnSwiped;
			_gestureController.Tapped -= OnTapped;
			_gestureController = null;
		}

		private GestureController EnsureSpawnedGestureController()
		{
			if (_spawnedGestureController != null)
			{
				return _spawnedGestureController;
			}
			_spawnedGestureHost = new GameObject("[EditorOnly] MobileServicesGesturePanel")
			{
				hideFlags = HideFlags.DontSave,
			};
			_spawnedGestureController = _spawnedGestureHost.AddComponent<GestureController>();
			if (!_touchSimEnabled)
			{
				UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Enable();
				_touchSimEnabled = true;
			}
			return _spawnedGestureController;
		}

		private void CleanupSpawnedGestures()
		{
			if (_spawnedGestureHost != null)
			{
				UnityEngine.Object.DestroyImmediate(_spawnedGestureHost);
			}
			_spawnedGestureHost = null;
			_spawnedGestureController = null;
			if (_touchSimEnabled)
			{
				UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Disable();
				_touchSimEnabled = false;
			}
		}

		// ---- Permissions ----

		private VisualElement BuildPermissionsSection()
		{
			var foldout = new Foldout { text = "Permissions", value = false };
			foldout.AddToClassList("msp-foldout");

			foldout.Add(MakeBanner(PermissionsInfo));

			_permStateFields.Clear();
			foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
			{
				var captured = p;
				var row = new VisualElement();
				row.AddToClassList("row");
				var name = new Label(p.ToString());
				name.AddToClassList("row-label");
				row.Add(name);

				var stateField = new EnumField(_permissions.Check(p));
				stateField.style.minWidth = 130;
				stateField.RegisterValueChangedCallback(evt =>
					EditorPlatformSimulator.SetPermissionState(captured, (PermissionStatus)evt.newValue));
				_permStateFields[p] = stateField;
				// Writes EditorPrefs, so it is meaningful in edit mode too (survives the Play domain
				// reload) — no play-mode gate.
				row.Add(stateField);
				foldout.Add(row);
			}

			// Fallback for resolving a pending runtime prompt from the panel, since overlay clicks are
			// unreliable in the edit-mode Game view. Visible only while a prompt is pending (the poll
			// toggles it); play-mode gated because prompts only pend at runtime.
			_permPendingRow = new VisualElement();
			_permPendingRow.style.flexDirection = FlexDirection.Row;
			_permPendingRow.style.alignItems = Align.Center;
			_permPendingRow.style.display = DisplayStyle.None;
			_permPendingLabel = new Label();
			_permPendingLabel.style.flexGrow = 1;
			_permPendingRow.Add(_permPendingLabel);
			_permPendingRow.Add(GatePlayMode(MakeActionButton("Allow", () =>
			{
				if (_permPending.HasValue)
				{
					EditorPlatformSimulator.ResolvePendingPermissionPrompt(_permPending.Value, true);
				}
			})));
			var denyBtn = MakeActionButton("Don't Allow", () =>
			{
				if (_permPending.HasValue)
				{
					EditorPlatformSimulator.ResolvePendingPermissionPrompt(_permPending.Value, false);
				}
			});
			denyBtn.AddToClassList("msp-button-danger");
			_permPendingRow.Add(GatePlayMode(denyBtn));
			foldout.Add(_permPendingRow);

			foldout.Add(MakeActionButton("Reset all to NotDetermined (reinstall / Reset Privacy)",
				EditorPlatformSimulator.ResetAllPermissions));

			return foldout;
		}

		// ---- ATT ----

		private VisualElement BuildAttSection()
		{
			var foldout = new Foldout { text = "App Tracking Transparency", value = false };
			foldout.AddToClassList("msp-foldout");

			foldout.Add(MakeBanner(AttInfo));

			var stateRow = new VisualElement();
			stateRow.style.flexDirection = FlexDirection.Row;
			stateRow.style.alignItems = Align.Center;
			stateRow.Add(new Label("Status"));
			_attStateField = new EnumField(_att.CurrentStatus);
			_attStateField.style.flexGrow = 1;
			_attStateField.style.marginLeft = 6;
			_attStateField.RegisterValueChangedCallback(evt => EditorPlatformSimulator.SetAttState((AttStatus)evt.newValue));
			stateRow.Add(_attStateField);
			foldout.Add(stateRow);

			// Panel fallback for resolving a pending runtime ATT prompt (overlay clicks are unreliable
			// in the edit-mode Game view). Visible only while a prompt is pending; play-mode gated.
			_attPendingRow = new VisualElement();
			_attPendingRow.style.flexDirection = FlexDirection.Row;
			_attPendingRow.style.alignItems = Align.Center;
			_attPendingRow.style.display = DisplayStyle.None;
			var attPendingLabel = new Label("ATT prompt pending");
			attPendingLabel.style.flexGrow = 1;
			_attPendingRow.Add(attPendingLabel);
			_attPendingRow.Add(GatePlayMode(MakeActionButton("Allow", () => EditorPlatformSimulator.ResolvePendingAttPrompt(true))));
			var attDeny = MakeActionButton("Ask Not to Track", () => EditorPlatformSimulator.ResolvePendingAttPrompt(false));
			attDeny.AddToClassList("msp-button-danger");
			_attPendingRow.Add(GatePlayMode(attDeny));
			foldout.Add(_attPendingRow);

			foldout.Add(MakeActionButton("Reset to NotDetermined (reinstall / Reset Privacy)",
				EditorPlatformSimulator.ResetAtt));

			return foldout;
		}

		// Deep links are intentionally NOT driven from this panel: DeepLinkService.SimulateLinkActivated
		// is instance-scoped (no static override like Permissions/ATT), so the panel could only fire
		// into a throwaway instance it owns — never your game's. Use the DeepLinkRouter sample, or
		// EditorPlatformSimulator.SimulateDeepLink(uri, yourService) from a bootstrap, to drive it live.

		// ---- Shared refresh ----

		private void RefreshDiagnostics()
		{
			RefreshGestureDiagnostics();
			RefreshPermissionDiagnostics();
			RefreshAttDiagnostics();
		}

		private void RefreshGestureDiagnostics()
		{
			if (_gestureStatus == null)
			{
				return;
			}

			GestureController controller = null;
			if (Application.isPlaying)
			{
				// Prefer a GestureController the user already has in the scene; otherwise spawn our own
				// so the read-out works with zero setup.
				controller = UnityEngine.Object.FindAnyObjectByType<GestureController>();
				if (controller == null)
				{
					controller = EnsureSpawnedGestureController();
				}
			}
			else
			{
				CleanupSpawnedGestures();
			}

			if (controller != _gestureController)
			{
				DetachGestureController();
				_gestureController = controller;
				if (_gestureController != null)
				{
					_gestureController.Swiped += OnSwiped;
					_gestureController.Tapped += OnTapped;
				}
			}

			if (!Application.isPlaying)
			{
				_gestureStatus.text = "Enter Play mode — a GestureController is auto-attached, no scene setup needed.";
			}
			else if (_gestureController == _spawnedGestureController)
			{
				_gestureStatus.text = "Auto-attached a GestureController (no scene object needed) — swipe / tap anywhere.";
			}
			else
			{
				_gestureStatus.text = $"Attached to scene '{_gestureController.gameObject.name}' — swipe / tap anywhere.";
			}

			if (_hasSwipe)
			{
				_gestureSwipe.text = $"dir={_lastSwipe.SwipeDirection}, vel={_lastSwipe.SwipeVelocity:F1}, sameness={_lastSwipe.SwipeSameness:F2}, start={_lastSwipe.StartPosition}, end={_lastSwipe.EndPosition}";
			}
			if (_hasTap)
			{
				_gestureTap.text = $"press={_lastTap.PressPosition}, release={_lastTap.ReleasePosition}, duration={_lastTap.TapDuration:F3}s";
			}
		}

		private void RefreshPermissionDiagnostics()
		{
			// SetValueWithoutNotify so syncing the dropdown to Check() doesn't re-fire SetPermissionState.
			foreach (var kv in _permStateFields)
			{
				kv.Value.SetValueWithoutNotify(_permissions.Check(kv.Key));
			}

			AppPermission? pending = null;
			foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
			{
				if (EditorPlatformSimulator.HasPendingPermissionPrompt(p))
				{
					pending = p;
					break;
				}
			}
			_permPending = pending;
			if (_permPendingRow != null)
			{
				_permPendingRow.style.display = pending.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
				if (pending.HasValue && _permPendingLabel != null)
				{
					_permPendingLabel.text = $"Prompt pending: {pending.Value}";
				}
			}
		}

		private void RefreshAttDiagnostics()
		{
			_attStateField?.SetValueWithoutNotify(_att.CurrentStatus);
			if (_attPendingRow != null)
			{
				_attPendingRow.style.display = EditorPlatformSimulator.HasPendingAttPrompt
					? DisplayStyle.Flex
					: DisplayStyle.None;
			}
		}

		private static Button MakeActionButton(string text, Action onClick)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("msp-button");
			return btn;
		}

		private static VisualElement MakeBanner(string text)
		{
			var banner = new VisualElement();
			banner.AddToClassList("msp-banner");
			var label = new Label(text);
			label.AddToClassList("msp-banner-label");
			banner.Add(label);
			return banner;
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

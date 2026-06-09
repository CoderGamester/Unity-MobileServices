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
		private const string PlayModeTooltip =
			"Requires Play mode — this spawns a runtime host or reads/drives live device state.";
		private const float EnvelopePlotHeight = 110f;
		private const float EnvelopeYAxisWidth = 30f;

		public override string title => "Mobile Services";

		// Controls that need Play mode (spawn a DontDestroyOnLoad host or read/drive live device
		// state). Greyed out + tooltip'd in edit mode; re-enabled on entering Play. Edit-mode-safe
		// controls (mock pushes, haptic presets + envelope graph, permission/ATT short-circuits) are
		// never added here.
		private readonly List<VisualElement> _playModeControls = new List<VisualElement>();
		// Amber banners shown only in edit mode (the top global one + one inside each gated foldout).
		private readonly List<VisualElement> _editModeBanners = new List<VisualElement>();

		// Master switch (mirrors MobileSimulatorState.Enabled): the header toggle stays interactive
		// while every section below it is enabled/disabled as a group.
		private Toggle _enabledToggle;
		private VisualElement _sectionsContainer;

		// ---- Held service instances (created on demand; disposed in OnDestroy) ----
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

		// ---- ATT state control ----
		private EnumField _attStateField;

		public override void OnCreate()
		{
			SyncPlatformFromHost();
			MobileSimulatorRuntimeOverlay.NotifyPluginActive(true);
		}

		public override void OnDestroy()
		{
			MobileSimulatorRuntimeOverlay.NotifyPluginActive(false);
			DetachGestureController();
			CleanupSpawnedGestures();
		}

		public override VisualElement OnCreateUI()
		{
			var root = new VisualElement { name = "mobile-services-plugin-root" };
			LoadStyleSheet(root);

			// Rebuilt fresh each time the panel UI is created — drop any stale control refs.
			_playModeControls.Clear();
			_editModeBanners.Clear();

			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.style.flexGrow = 1;

			scroll.Add(BuildHeader());

			// Everything below the header is gated as a group by the master switch. The header (with
			// the enable toggle) stays interactive so the user can always turn the simulator back on.
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
			RefreshDiagnostics();
			RefreshPlayModeGating();
			ApplyEnabledState(MobileSimulatorState.Enabled);
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

		/// <summary>
		/// Reads <see cref="Application.platform"/> (spoofed by the Device Simulator to match the
		/// selected device) and writes the single <see cref="MobileSimulatorState.Platform"/> skin.
		/// </summary>
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

			var note = new Label("Fires native-UI mocks into the simulated phone screen (edit + play). Live diagnostics below need Play mode.");
			note.AddToClassList("msp-note");
			header.Add(note);

			// Master switch: shows/hides the in-Game-view "[EDITOR SIMULATOR]" banner and enables /
			// disables every section below. Stays interactive even when the sections are disabled.
			_enabledToggle = new Toggle("Editor Simulator") { value = MobileSimulatorState.Enabled };
			_enabledToggle.AddToClassList("msp-enabled-toggle");
			_enabledToggle.RegisterValueChangedCallback(evt => MobileSimulatorState.Enabled = evt.newValue);
			header.Add(_enabledToggle);

			return header;
		}

		private void OnEnabledChanged(bool enabled) => ApplyEnabledState(enabled);

		/// <summary>
		/// Applies the master switch: greys out every section as a group and, when turning the
		/// simulator off, clears any mock currently painted in the in-Game-view overlay.
		/// </summary>
		private void ApplyEnabledState(bool enabled)
		{
			_enabledToggle?.SetValueWithoutNotify(enabled);
			_sectionsContainer?.SetEnabled(enabled);
			if (!enabled)
			{
				MobileSimulatorState.PushDismissAll();
			}
		}

		/// <summary>
		/// One amber banner explaining why a subset of controls is greyed out in edit mode. Auto-hides
		/// in Play mode via <see cref="RefreshPlayModeGating"/>. Reuses the existing overlay-hint style.
		/// </summary>
		private VisualElement BuildPlayModeBanner()
		{
			var banner = new VisualElement { name = "msp-playmode-hint" };
			banner.AddToClassList("msp-overlay-hint");

			var label = new Label("Some controls are disabled until you enter Play mode — they spawn runtime hosts or read/drive live device state. Mock previews, haptic presets, and the envelope graph work in edit mode.");
			label.AddToClassList("msp-overlay-hint-label");
			banner.Add(label);

			_editModeBanners.Add(banner);
			return banner;
		}

		/// <summary>
		/// Inline edit-mode banner placed inside a gated foldout, so when the user expands a section
		/// whose buttons are greyed they see why. Auto-hidden in Play mode via
		/// <see cref="RefreshPlayModeGating"/>.
		/// </summary>
		private VisualElement MakeSectionPlayModeBanner(string message = "Enter Play mode to enable these controls.")
		{
			var banner = new VisualElement();
			banner.AddToClassList("msp-overlay-hint");
			var label = new Label(message);
			label.AddToClassList("msp-overlay-hint-label");
			banner.Add(label);
			_editModeBanners.Add(banner);
			return banner;
		}

		/// <summary>
		/// Registers <paramref name="control"/> as Play-mode-only and returns it (for inline use at
		/// the add site). <see cref="RefreshPlayModeGating"/> greys these out + tooltips them in edit
		/// mode and re-enables them in Play.
		/// </summary>
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

			foldout.Add(MakeActionButton("Review prompt", () =>
			{
				MobileSimulatorState.PushReview();
				NativeUiService.RequestReview();
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

			var note = new Label("Haptics fire only on a physical device. Select a preset to inspect its vibration envelope (the editor calibration cue).");
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

		/// <summary>
		/// Builds the intensity-over-time graph: a Y-axis tick column (intensity 0..1) + a vector plot
		/// canvas (drawn via <c>Painter2D</c>) + an X-axis row (time in ms). The waveform is a step
		/// curve — each <c>HapticEnvelopes</c> segment holds its amplitude for its duration (that's how
		/// <c>VibrationEffect.createWaveform</c> plays it), with the area under it filled.
		/// </summary>
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

			// Y axis: intensity ticks (1 at top → 0 at bottom).
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

			// X axis: 0 ms … total ms.
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

			// Preview only — pushes the heads-up mock to the overlay like the Native UI mocks (works in
			// edit mode). Live scheduling / channels / queueing / pending were removed: they ran on a
			// throwaway MobileNotificationService disconnected from the game and duplicated the
			// NotificationsScheduler sample (which drives the game's own service). The mock is the
			// panel-unique value here.
			var note = new Label("Preview the heads-up banner (works in edit mode). For live scheduling / channels / queueing / pending, use the NotificationsScheduler sample — it drives the game's own service.");
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

		/// <summary>
		/// Spawns a hidden, editor-only <see cref="GestureController"/> (and enables Input System
		/// Touch Simulation so editor mouse drags count as touches) so the Gestures read-out works
		/// with zero scene setup. Used only when the scene has no GestureController of its own.
		/// </summary>
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
			foldout.Add(MakeSectionPlayModeBanner());

			// Set each permission's simulated state directly. The dropdown drives BOTH the editor
			// Check override and the next-Request override, so the running game / sample reads exactly
			// this value through Check() and RequestAsync(). The OS prompt mock isn't shown here — it
			// only makes sense at runtime, when the game actually requests (drive it from your code or
			// the Mobile Services Playground sample).
			var note = new Label("Set each permission's simulated state — the running game / sample reads it via Check() / RequestAsync().");
			note.AddToClassList("msp-note");
			foldout.Add(note);

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
				{
					var status = (PermissionStatus)evt.newValue;
					EditorPlatformSimulator.SetPermissionCheckResult(captured, status);
					EditorPlatformSimulator.QueuePermissionResult(captured, status);
				});
				_permStateFields[p] = stateField;
				// Play-mode only: the override is read by a running service, and a domain reload on
				// entering Play would wipe anything set in edit mode anyway.
				row.Add(GatePlayMode(stateField));
				foldout.Add(row);
			}

			foldout.Add(GatePlayMode(MakeActionButton("Reset all to default (Granted)", () =>
			{
				foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
				{
					EditorPlatformSimulator.SetPermissionCheckResult(p, null);
					EditorPlatformSimulator.QueuePermissionResult(p, null);
				}
			})));

			return foldout;
		}

		// ---- ATT ----

		private VisualElement BuildAttSection()
		{
			var foldout = new Foldout { text = "App Tracking Transparency", value = false };
			foldout.AddToClassList("msp-foldout");
			foldout.Add(MakeSectionPlayModeBanner());

			// Set the simulated ATT status directly. QueueAttResult drives BOTH the CurrentStatus
			// override and the next-Request override, so the running game / sample reads exactly this
			// value. The ATT prompt mock isn't shown here — it only makes sense at runtime, when the
			// game actually requests authorization.
			var note = new Label("Set the simulated ATT status — the running game / sample reads it via CurrentStatus / RequestAuthorizationAsync().");
			note.AddToClassList("msp-note");
			foldout.Add(note);

			var stateRow = new VisualElement();
			stateRow.style.flexDirection = FlexDirection.Row;
			stateRow.style.alignItems = Align.Center;
			stateRow.Add(new Label("Status"));
			_attStateField = new EnumField(_att.CurrentStatus);
			_attStateField.style.flexGrow = 1;
			_attStateField.style.marginLeft = 6;
			_attStateField.RegisterValueChangedCallback(evt => EditorPlatformSimulator.QueueAttResult((AttStatus)evt.newValue));
			// Play-mode only (same reason as Permissions: read by a running service; reset on Play entry).
			stateRow.Add(GatePlayMode(_attStateField));
			foldout.Add(stateRow);

			foldout.Add(GatePlayMode(MakeActionButton("Reset to default (Authorized)", () => EditorPlatformSimulator.QueueAttResult(null))));

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
				controller = UnityEngine.Object.FindFirstObjectByType<GestureController>();
				if (controller == null)
				{
					controller = EnsureSpawnedGestureController();
				}
			}
			else
			{
				// Left play mode — drop the auto-spawned controller + touch simulation.
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
			// Keep each dropdown in sync with the effective Check() result (the source of truth), so a
			// "Reset all" or a state change driven from elsewhere is reflected without a notify loop.
			foreach (var kv in _permStateFields)
			{
				kv.Value.SetValueWithoutNotify(_permissions.Check(kv.Key));
			}
		}

		private void RefreshAttDiagnostics()
		{
			_attStateField?.SetValueWithoutNotify(_att.CurrentStatus);
		}

		private static Button MakeActionButton(string text, Action onClick)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("msp-button");
			return btn;
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

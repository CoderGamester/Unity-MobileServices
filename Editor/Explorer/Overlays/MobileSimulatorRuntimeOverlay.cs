using System.Collections.Generic;
using GameLovers.MobileServices.NativeUi;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Overlays
{
	/// <summary>
	/// Editor-only bootstrap that spawns an in-Game-view <see cref="UIDocument"/> overlay - the
	/// single simulator canvas - painting the truth-mirror mocks pixel-aligned with the simulated
	/// device's <c>Screen.*</c> values.
	/// </summary>
	/// <remarks>
	/// <para>The overlay is alive whenever the Device Simulator plugin panel is open or a runtime
	/// alert is visible, including a plain Game view with no simulator window. A single idempotent
	/// <see cref="RefreshLifecycle"/> drives spawn / teardown.</para>
	/// <para>The overlay renders inside Unity's runtime UIToolkit panel - <see cref="UIDocument"/>
	/// is <c>[ExecuteAlways]</c>, so its panel paints into the Game / Device Simulator view in edit
	/// mode too. Runtime play-mode alerts are interactive; edit-mode panel previews retain their
	/// panel-owned dismissal controls.</para>
	/// <para>The <see cref="PanelSettings"/> instance is constructed programmatically (rather than
	/// shipped as a <c>.asset</c>) to keep the setup editor-only by construction.</para>
	/// </remarks>
	[InitializeOnLoad]
	internal static class MobileSimulatorRuntimeOverlay
	{
		private const string HostObjectName = "[EditorOnly] MobileSimulatorOverlay";
		private const string CommonStyleName = "MobileSimulator.Common";
		private const string IosStyleName = "MobileSimulator.iOS";
		private const string AndroidStyleName = "MobileSimulator.Android";

		private static GameObject _hostObject;
		private static OverlayController _controller;
		private static PanelSettings _panelSettings;
		private static bool _pluginActive;
		private static bool _standaloneAlertActive;

		private static bool ShouldBeAlive => _pluginActive || _standaloneAlertActive;

		static MobileSimulatorRuntimeOverlay()
		{
			// Re-evaluate across play-mode transitions so the host's DontDestroyOnLoad / teardown is
			// applied correctly when the panel is open while entering or exiting play mode.
			EditorApplication.playModeStateChanged += _ => RefreshLifecycle();
			NativeUiService.EditorShowAlertOverride = ShowAlert;
			NativeUiService.EditorDismissAlertOverride = DismissAlert;
		}

		/// <summary>
		/// Called by <c>MobileServicesDeviceSimulatorPlugin</c> when its panel UI is created or
		/// destroyed, so the overlay is alive (edit OR play mode) exactly while the Device Simulator
		/// window is open.
		/// </summary>
		internal static void NotifyPluginActive(bool active)
		{
			_pluginActive = active;
			RefreshLifecycle();
		}

		/// <summary>Paints one runtime-requested alert through the Editor simulator overlay.</summary>
		internal static void ShowAlert(
			bool isAlertSheet,
			bool isDismissible,
			string title,
			string message,
			AlertButton[] buttons)
		{
			_standaloneAlertActive = true;
			EnsureSpawned();

			var simulatedButtons = new List<SimulatedAlertButton>(buttons.Length);
			foreach (var button in buttons)
			{
				simulatedButtons.Add(new SimulatedAlertButton
				{
					Text = button.Text,
					Style = (SimulatedAlertButtonStyle)button.Style,
					OnClicked = button.Callback,
				});
			}

			MobileSimulatorState.PushAlert(new SimulatedAlertSpec
			{
				Title = title,
				Message = message,
				IsActionSheet = isAlertSheet,
				IsDismissible = isDismissible,
				Buttons = simulatedButtons,
			});
		}

		/// <summary>Dismisses the active Editor alert without invoking an action.</summary>
		internal static void DismissAlert()
		{
			if (_controller != null)
			{
				MobileSimulatorState.PushDismissAll();
				return;
			}

			_standaloneAlertActive = false;
			RefreshLifecycle();
		}

		private static void RefreshLifecycle()
		{
			if (ShouldBeAlive)
			{
				EnsureSpawned();
			}
			else
			{
				Teardown();
			}
		}

		private static void EnsureSpawned()
		{
			if (_hostObject != null)
			{
				return;
			}

			// A domain reload resets the statics but can leave the previous host GameObject behind
			// (it is HideFlags.DontSave, not destroyed by reload in edit mode). Sweep any stale host
			// so we never paint two overlays after a reload.
			DestroyStaleHosts();

			var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
			panelSettings.name = "MobileSimulator.PanelSettings";
			var themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
			themeStyleSheet.name = "MobileSimulator.ThemeStyleSheet";
			themeStyleSheet.hideFlags = HideFlags.HideAndDontSave;
			panelSettings.themeStyleSheet = themeStyleSheet;
			// An empty UIDocument panel still consumes Game-view pointer events ahead of lower panels.
			// Keep the simulator below a sample until it has a native mock to present.
			panelSettings.sortingOrder = short.MinValue;
			// Scale the mock USS (authored in logical-point units) UP to the device's native pixel
			// grid. The Device Simulator reports Screen.width/height in PHYSICAL pixels (e.g. iPhone
			// 15 Pro = 1179x2556), so ScaleWithScreenSize against a logical-phone reference resolution
			// yields a scale factor ≈ the device's native scale (~3x) — i.e. 1 USS px ≈ 1 iOS point.
			// Without this, ConstantPixelSize paints 1 USS px = 1 device px and every mock renders ~1/3
			// size on a 3x screen. The reference aspect (≈19.5:9) matches modern tall phones, so the
			// `match` blend barely matters; 0.5 keeps it sane if the device is rotated to landscape.
			panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
			panelSettings.referenceResolution = new Vector2Int(390, 844);
			panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
			panelSettings.match = 0.5f;
			panelSettings.targetTexture = null;
			panelSettings.clearColor = false;
			panelSettings.hideFlags = HideFlags.HideAndDontSave;

			_hostObject = new GameObject(HostObjectName)
			{
				hideFlags = HideFlags.DontSave,
				tag = "EditorOnly",
			};
			// DontDestroyOnLoad is only meaningful (and only legal without a warning) in play mode;
			// in edit mode the object simply lives in the active scene as a hidden, unsaved object.
			if (Application.isPlaying)
			{
				Object.DontDestroyOnLoad(_hostObject);
			}

			var document = _hostObject.AddComponent<UIDocument>();
			_panelSettings = panelSettings;
			document.panelSettings = _panelSettings;

			_controller = new OverlayController(document);
		}

		private static void Teardown()
		{
			if (_controller != null)
			{
				_controller.Dispose();
				_controller = null;
			}
			if (_hostObject != null)
			{
				DestroyHost(_hostObject);
				_hostObject = null;
			}
			DestroyStaleHosts();
			_panelSettings = null;
		}

		private static void DestroyStaleHosts()
		{
			foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
			{
				if (go != null && go != _hostObject && go.name == HostObjectName)
				{
					DestroyHost(go);
				}
			}
		}

		private static void DestroyHost(GameObject go)
		{
			if (Application.isPlaying)
			{
				Object.Destroy(go);
			}
			else
			{
				Object.DestroyImmediate(go);
			}
		}

		private static void SetOverlayPriority(bool aboveSample)
		{
			if (_panelSettings == null)
			{
				return;
			}

			_panelSettings.sortingOrder = aboveSample ? short.MaxValue : short.MinValue;
		}

		private sealed class OverlayController
		{
			private readonly UIDocument _document;
			private readonly VisualElement _root;
			private readonly VisualElement _stage;
			private readonly VisualElement _watermark;
			private readonly Label _platformLabel;
			private readonly StyleSheet _commonSheet;
			private readonly StyleSheet _iosSheet;
			private readonly StyleSheet _androidSheet;

			internal OverlayController(UIDocument document)
			{
				_document = document;
				_root = document.rootVisualElement;
				_root.style.flexGrow = 1;
				// Root must not absorb input — only the scrim of an active mock should be modal.
				_root.pickingMode = PickingMode.Ignore;
				_root.style.backgroundColor = Color.clear;

				_commonSheet = FindStyleSheet(CommonStyleName);
				_iosSheet = FindStyleSheet(IosStyleName);
				_androidSheet = FindStyleSheet(AndroidStyleName);
				if (_commonSheet != null)
				{
					_root.styleSheets.Add(_commonSheet);
				}

				var rootContainer = new VisualElement { name = "simulator-root" };
				rootContainer.AddToClassList("simulator-root");
				rootContainer.style.flexGrow = 1;
				rootContainer.style.position = Position.Absolute;
				rootContainer.style.left = 0;
				rootContainer.style.top = 0;
				rootContainer.style.right = 0;
				rootContainer.style.bottom = 0;
				// Override the dark background — the overlay must let the underlying Game / Simulator
				// viewport show through. Mock scrims (when an alert or permission dialog is active)
				// re-introduce their own dimming via the .mock-scrim USS rule.
				rootContainer.style.backgroundColor = Color.clear;
				// Empty stage must not steal clicks from the game. The scrim element inside an
				// active mock has its own (default) picking mode and re-absorbs input on its own.
				rootContainer.pickingMode = PickingMode.Ignore;
				_root.Add(rootContainer);

				_stage = new VisualElement { name = "simulator-stage" };
				_stage.AddToClassList("simulator-stage");
				// The stage fills the screen but must not become the top panel's idle hit target.
				// Active mock children retain PickingMode.Position and remain intentionally modal.
				_stage.pickingMode = PickingMode.Ignore;
				rootContainer.Add(_stage);

				_watermark = new VisualElement { name = "simulator-watermark" };
				_watermark.AddToClassList("simulator-watermark");
				_watermark.pickingMode = PickingMode.Ignore;
				_watermark.Add(new Label("[EDITOR SIMULATOR]"));
				_platformLabel = new Label();
				_platformLabel.AddToClassList("simulator-platform-label");
				_watermark.Add(_platformLabel);
				rootContainer.Add(_watermark);

				ApplyPlatformSheet(MobileSimulatorState.Platform);
				ApplyEnabledState(MobileSimulatorState.Enabled);

				MobileSimulatorState.EnabledChanged += OnEnabledChanged;
				MobileSimulatorState.PlatformChanged += OnPlatformChanged;
				MobileSimulatorState.AlertRequested += OnAlert;
				MobileSimulatorState.ToastRequested += OnToast;
				MobileSimulatorState.ShareRequested += OnShare;
				MobileSimulatorState.ReviewRequested += OnReview;
				MobileSimulatorState.NotificationBannerRequested += OnNotificationBanner;
				MobileSimulatorState.PermissionDialogRequested += OnPermissionDialog;
				MobileSimulatorState.DismissAllRequested += OnDismissAll;

				// In Play Mode an otherwise empty top-level UIDocument prevents the sample's
				// panel from receiving pointer input. Reattach only while rendering a mock.
				if (Application.isPlaying)
				{
					_document.enabled = false;
				}
			}

			internal void Dispose()
			{
				MobileSimulatorState.EnabledChanged -= OnEnabledChanged;
				MobileSimulatorState.PlatformChanged -= OnPlatformChanged;
				MobileSimulatorState.AlertRequested -= OnAlert;
				MobileSimulatorState.ToastRequested -= OnToast;
				MobileSimulatorState.ShareRequested -= OnShare;
				MobileSimulatorState.ReviewRequested -= OnReview;
				MobileSimulatorState.NotificationBannerRequested -= OnNotificationBanner;
				MobileSimulatorState.PermissionDialogRequested -= OnPermissionDialog;
				MobileSimulatorState.DismissAllRequested -= OnDismissAll;
			}

			private static StyleSheet FindStyleSheet(string fileBaseName)
			{
				var guids = AssetDatabase.FindAssets($"{fileBaseName} t:StyleSheet");
				foreach (var guid in guids)
				{
					var assetPath = AssetDatabase.GUIDToAssetPath(guid);
					if (assetPath.EndsWith($"{fileBaseName}.uss"))
					{
						return AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath);
					}
				}
				return null;
			}

			private void OnEnabledChanged(bool enabled) => ApplyEnabledState(enabled);

			private void ApplyEnabledState(bool enabled)
			{
				if (_watermark != null)
				{
					_watermark.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
				}
			}

			private void OnPlatformChanged(SimulatedPlatform platform) => ApplyPlatformSheet(platform);

			private void ApplyPlatformSheet(SimulatedPlatform platform)
			{
				if (_iosSheet != null && _root.styleSheets.Contains(_iosSheet))
				{
					_root.styleSheets.Remove(_iosSheet);
				}
				if (_androidSheet != null && _root.styleSheets.Contains(_androidSheet))
				{
					_root.styleSheets.Remove(_androidSheet);
				}

				var active = platform == SimulatedPlatform.iOS ? _iosSheet : _androidSheet;
				if (active != null)
				{
					_root.styleSheets.Add(active);
				}

				_root.RemoveFromClassList("platform-ios");
				_root.RemoveFromClassList("platform-android");
				_root.AddToClassList(platform == SimulatedPlatform.iOS ? "platform-ios" : "platform-android");

				if (_platformLabel != null)
				{
					_platformLabel.text = platform.ToString();
				}
			}

			private void OnAlert(SimulatedAlertSpec spec)
			{
				ClearStage();
				ShowStage();
				_stage.Add(MockBuilders.BuildAlert(MobileSimulatorState.Platform, spec, DismissAlert));
			}

			private void OnToast(SimulatedToastSpec spec)
			{
				ClearStage();
				ShowStage();
				var toast = MockBuilders.BuildToast(MobileSimulatorState.Platform, spec);
				_stage.Add(toast);
				var seconds = spec.IsLongDuration ? 3.5f : 2.0f;
				_root.schedule.Execute(() =>
				{
					if (_stage.Contains(toast))
					{
						ClearStage();
					}
				}).StartingIn((long)(seconds * 1000f));
			}

			private void OnShare(SimulatedShareSpec spec)
			{
				ClearStage();
				ShowStage();
				_stage.Add(MockBuilders.BuildShareSheet(MobileSimulatorState.Platform, spec, ClearStage));
			}

			private void OnReview()
			{
				ClearStage();
				ShowStage();
				_stage.Add(MockBuilders.BuildReviewPrompt(MobileSimulatorState.Platform, ClearStage));
			}

			private void OnNotificationBanner(SimulatedNotificationBannerSpec spec)
			{
				ClearStage();
				ShowStage();
				var banner = MockBuilders.BuildNotificationBanner(MobileSimulatorState.Platform, spec);
				_stage.Add(banner);
				_root.schedule.Execute(() =>
				{
					if (_stage.Contains(banner))
					{
						ClearStage();
					}
				}).StartingIn(4000);
			}

			private void OnPermissionDialog(SimulatedPermissionDialogSpec spec)
			{
				ClearStage();
				ShowStage();
				var dialog = MockBuilders.BuildPermissionDialog(MobileSimulatorState.Platform, spec, result =>
				{
					ClearStage();
					spec.OnResolved?.Invoke(result);
				});
				_stage.Add(dialog);
			}

			private void OnDismissAll()
			{
				DismissAlert();
			}

			private void DismissAlert()
			{
				ClearStage();
				_standaloneAlertActive = false;
				EditorApplication.delayCall += RefreshLifecycle;
			}

			private void ShowStage()
			{
				if (Application.isPlaying)
				{
					_document.enabled = true;
				}

				SetOverlayPriority(true);
			}

			private void ClearStage()
			{
				if (_stage == null)
				{
					return;
				}
				_stage.Clear();
				SetOverlayPriority(false);
				if (Application.isPlaying)
				{
					_document.enabled = false;
				}
			}
		}
	}
}

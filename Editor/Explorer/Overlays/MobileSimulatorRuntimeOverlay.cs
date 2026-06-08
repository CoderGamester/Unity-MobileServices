using GameLovers.MobileServices.Editor.Settings;
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
	/// <para>The overlay is alive whenever the Device Simulator plugin panel is open (edit OR play
	/// mode) so a designer can fire a mock from the panel and see it inside the simulated phone
	/// without entering play mode. It also still spawns on its own during play mode when the
	/// opt-in <c>Project Settings &gt; GameLovers &gt; Mobile Services &gt; Enable runtime simulator
	/// overlay</c> setting is on (so mocks render in a plain Game view even without the Device
	/// Simulator window open). Both conditions feed a single idempotent <see cref="RefreshLifecycle"/>.</para>
	/// <para>The overlay renders inside Unity's runtime UIToolkit panel - <see cref="UIDocument"/>
	/// is <c>[ExecuteAlways]</c>, so its panel paints into the Game / Device Simulator view in edit
	/// mode too. Interaction inside the mock is unreliable in the edit-mode Game view, so dismissal
	/// is driven from the plugin panel; the overlay is treated as display-only.</para>
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
		private static bool _pluginActive;
		private static bool _inPlayMode;

		static MobileSimulatorRuntimeOverlay()
		{
			_inPlayMode = EditorApplication.isPlaying;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		/// <summary>
		/// Called by <c>MobileServicesDeviceSimulatorPlugin</c> when its panel UI is created or
		/// destroyed, so the overlay is alive (edit OR play mode) exactly while the Device Simulator
		/// window is open - in addition to the opt-in play-mode lifecycle.
		/// </summary>
		internal static void NotifyPluginActive(bool active)
		{
			_pluginActive = active;
			RefreshLifecycle();
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange change)
		{
			switch (change)
			{
				case PlayModeStateChange.EnteredPlayMode:
					_inPlayMode = true;
					RefreshLifecycle();
					break;
				case PlayModeStateChange.ExitingPlayMode:
					_inPlayMode = false;
					RefreshLifecycle();
					break;
			}
		}

		private static bool ShouldBeAlive =>
			_pluginActive || (_inPlayMode && MobileServicesSettings.instance.EnableRuntimeSimulatorOverlay);

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
			// short.MaxValue puts the overlay above any consumer UIDocument that hasn't explicitly
			// claimed the same priority. Tie-breaks fall back to GameObject name lexicographic order;
			// "[EditorOnly] ..." sorts near the top thanks to the leading bracket.
			panelSettings.sortingOrder = short.MaxValue;
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
			document.panelSettings = panelSettings;

			_controller = new OverlayController(document.rootVisualElement);
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

		/// <summary>
		/// Owns the visual tree + the <see cref="MobileSimulatorState"/> subscriptions for the
		/// overlay. Same renderer surface the standalone window used to provide; the broker payload
		/// paints the mocks here.
		/// </summary>
		private sealed class OverlayController
		{
			private readonly VisualElement _root;
			private readonly VisualElement _stage;
			private readonly Label _platformLabel;
			private readonly StyleSheet _commonSheet;
			private readonly StyleSheet _iosSheet;
			private readonly StyleSheet _androidSheet;

			internal OverlayController(VisualElement root)
			{
				_root = root;
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
				rootContainer.Add(_stage);

				var watermark = new VisualElement { name = "simulator-watermark" };
				watermark.AddToClassList("simulator-watermark");
				watermark.pickingMode = PickingMode.Ignore;
				watermark.Add(new Label("[EDITOR SIMULATOR]"));
				_platformLabel = new Label();
				_platformLabel.AddToClassList("simulator-platform-label");
				watermark.Add(_platformLabel);
				rootContainer.Add(watermark);

				ApplyPlatformSheet(MobileSimulatorState.Platform);

				MobileSimulatorState.PlatformChanged += OnPlatformChanged;
				MobileSimulatorState.AlertRequested += OnAlert;
				MobileSimulatorState.ToastRequested += OnToast;
				MobileSimulatorState.ShareRequested += OnShare;
				MobileSimulatorState.ReviewRequested += OnReview;
				MobileSimulatorState.NotificationBannerRequested += OnNotificationBanner;
				MobileSimulatorState.PermissionDialogRequested += OnPermissionDialog;
				MobileSimulatorState.DismissAllRequested += OnDismissAll;
			}

			internal void Dispose()
			{
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
				_stage.Add(MockBuilders.BuildAlert(MobileSimulatorState.Platform, spec, ClearStage));
			}

			private void OnToast(SimulatedToastSpec spec)
			{
				ClearStage();
				var toast = MockBuilders.BuildToast(MobileSimulatorState.Platform, spec);
				_stage.Add(toast);
				var seconds = spec.IsLongDuration ? 3.5f : 2.0f;
				_root.schedule.Execute(() =>
				{
					if (_stage.Contains(toast))
					{
						_stage.Remove(toast);
					}
				}).StartingIn((long)(seconds * 1000f));
			}

			private void OnShare(SimulatedShareSpec spec)
			{
				ClearStage();
				_stage.Add(MockBuilders.BuildShareSheet(MobileSimulatorState.Platform, spec, ClearStage));
			}

			private void OnReview()
			{
				ClearStage();
				_stage.Add(MockBuilders.BuildReviewPrompt(MobileSimulatorState.Platform, ClearStage));
			}

			private void OnNotificationBanner(SimulatedNotificationBannerSpec spec)
			{
				ClearStage();
				var banner = MockBuilders.BuildNotificationBanner(MobileSimulatorState.Platform, spec);
				_stage.Add(banner);
				_root.schedule.Execute(() =>
				{
					if (_stage.Contains(banner))
					{
						_stage.Remove(banner);
					}
				}).StartingIn(4000);
			}

			private void OnPermissionDialog(SimulatedPermissionDialogSpec spec)
			{
				ClearStage();
				var dialog = MockBuilders.BuildPermissionDialog(MobileSimulatorState.Platform, spec, result =>
				{
					ClearStage();
					spec.OnResolved?.Invoke(result);
				});
				_stage.Add(dialog);
			}

			private void OnDismissAll()
			{
				ClearStage();
			}

			private void ClearStage()
			{
				if (_stage == null)
				{
					return;
				}
				_stage.Clear();
			}
		}
	}
}

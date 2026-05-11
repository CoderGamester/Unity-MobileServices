using GameLovers.MobileServices.Editor.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Overlays
{
	/// <summary>
	/// Editor-only bootstrap that spawns an in-Game-view <see cref="UIDocument"/> overlay during
	/// play mode, painting the same truth-mirror mocks as <see cref="MobileSimulatorWindow"/> but
	/// pixel-aligned with the simulated device's <c>Screen.*</c> values. Opt-in via Project
	/// Settings → GameLovers → Mobile Services → <c>Enable runtime simulator overlay</c>.
	/// </summary>
	/// <remarks>
	/// <para>The overlay renders inside Unity's runtime UIToolkit panel, so it composes natively
	/// with Unity's Device Simulator: a designer can pick "iPhone 15 Pro" in <c>Window > General >
	/// Device Simulator</c>, press Play, and the mock dialogs render at the right scale and inside
	/// the correct safe-area inset for that device.</para>
	/// <para>The <see cref="PanelSettings"/> instance is constructed programmatically (rather than
	/// shipped as a <c>.asset</c>) to keep the setup editor-only by construction — there's no asset
	/// file consumers can accidentally reference from a runtime <c>UIDocument</c>.</para>
	/// <para>Lifecycle: spawned on <see cref="PlayModeStateChange.EnteredPlayMode"/>, destroyed on
	/// <see cref="PlayModeStateChange.ExitingPlayMode"/> (clean teardown — no paused-snapshot
	/// preservation).</para>
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

		static MobileSimulatorRuntimeOverlay()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange change)
		{
			switch (change)
			{
				case PlayModeStateChange.EnteredPlayMode:
					if (MobileServicesSettings.instance.EnableRuntimeSimulatorOverlay)
					{
						Spawn();
					}
					break;
				case PlayModeStateChange.ExitingPlayMode:
					Teardown();
					break;
			}
		}

		private static void Spawn()
		{
			if (_hostObject != null)
			{
				return;
			}

			var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
			panelSettings.name = "MobileSimulator.PanelSettings";
			// short.MaxValue puts the overlay above any consumer UIDocument that hasn't explicitly
			// claimed the same priority. Tie-breaks fall back to GameObject name lexicographic order;
			// "[EditorOnly] ..." sorts near the top thanks to the leading bracket.
			panelSettings.sortingOrder = short.MaxValue;
			panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
			panelSettings.match = 0f;
			panelSettings.targetTexture = null;
			panelSettings.clearColor = false;
			panelSettings.hideFlags = HideFlags.HideAndDontSave;

			_hostObject = new GameObject(HostObjectName)
			{
				hideFlags = HideFlags.DontSave,
				tag = "EditorOnly",
			};
			Object.DontDestroyOnLoad(_hostObject);

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
				Object.Destroy(_hostObject);
				_hostObject = null;
			}
		}

		/// <summary>
		/// Owns the visual tree + the <see cref="MobileSimulatorState"/> subscriptions for the
		/// runtime overlay. Mirrors <see cref="MobileSimulatorWindow"/>'s renderer surface so the
		/// same broker payload paints identically in both targets.
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
				// Override the standalone window's opaque dark background — the overlay must let
				// the underlying Game / Simulator viewport show through. Mock scrims (when an alert
				// or permission dialog is active) re-introduce their own dimming via the
				// .mock-scrim USS rule.
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

				ApplyPlatformSheet(MobileSimulatorState.OverlayPlatform);

				MobileSimulatorState.OverlayPlatformChanged += OnPlatformChanged;
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
				MobileSimulatorState.OverlayPlatformChanged -= OnPlatformChanged;
				MobileSimulatorState.AlertRequested -= OnAlert;
				MobileSimulatorState.ToastRequested -= OnToast;
				MobileSimulatorState.ShareRequested -= OnShare;
				MobileSimulatorState.ReviewRequested -= OnReview;
				MobileSimulatorState.NotificationBannerRequested -= OnNotificationBanner;
				MobileSimulatorState.PermissionDialogRequested -= OnPermissionDialog;
				MobileSimulatorState.DismissAllRequested -= OnDismissAll;
			}

			private static bool TargetsThisSurface(SimulatorTarget targets) =>
				(targets & SimulatorTarget.RuntimeOverlay) != 0;

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

			private void OnAlert(SimulatorTarget targets, SimulatedAlertSpec spec)
			{
				if (!TargetsThisSurface(targets)) return;
				ClearStage();
				_stage.Add(MockBuilders.BuildAlert(MobileSimulatorState.OverlayPlatform, spec, ClearStage));
			}

			private void OnToast(SimulatorTarget targets, SimulatedToastSpec spec)
			{
				if (!TargetsThisSurface(targets)) return;
				ClearStage();
				var toast = MockBuilders.BuildToast(MobileSimulatorState.OverlayPlatform, spec);
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

			private void OnShare(SimulatorTarget targets, SimulatedShareSpec spec)
			{
				if (!TargetsThisSurface(targets)) return;
				ClearStage();
				_stage.Add(MockBuilders.BuildShareSheet(MobileSimulatorState.OverlayPlatform, spec, ClearStage));
			}

			private void OnReview(SimulatorTarget targets)
			{
				if (!TargetsThisSurface(targets)) return;
				ClearStage();
				_stage.Add(MockBuilders.BuildReviewPrompt(MobileSimulatorState.OverlayPlatform, ClearStage));
			}

			private void OnNotificationBanner(SimulatorTarget targets, SimulatedNotificationBannerSpec spec)
			{
				if (!TargetsThisSurface(targets)) return;
				ClearStage();
				var banner = MockBuilders.BuildNotificationBanner(MobileSimulatorState.OverlayPlatform, spec);
				_stage.Add(banner);
				_root.schedule.Execute(() =>
				{
					if (_stage.Contains(banner))
					{
						_stage.Remove(banner);
					}
				}).StartingIn(4000);
			}

			private void OnPermissionDialog(SimulatorTarget targets, SimulatedPermissionDialogSpec spec)
			{
				if (!TargetsThisSurface(targets)) return;
				ClearStage();
				var dialog = MockBuilders.BuildPermissionDialog(MobileSimulatorState.OverlayPlatform, spec, result =>
				{
					ClearStage();
					spec.OnResolved?.Invoke(result);
				});
				_stage.Add(dialog);
			}

			private void OnDismissAll(SimulatorTarget targets)
			{
				if (!TargetsThisSurface(targets)) return;
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

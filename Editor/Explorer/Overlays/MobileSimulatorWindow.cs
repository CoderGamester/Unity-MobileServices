using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Overlays
{
	/// <summary>
	/// Truth-mirror editor window that paints platform-shaped mocks of the native UI surfaces the
	/// package triggers. Paired with <see cref="Windows.MobileServicesExplorerWindow"/>; see
	/// <c>docs/explorer.md</c> for the comparison with Unity's Device Simulator.
	/// </summary>
	public class MobileSimulatorWindow : EditorWindow
	{
		private const string CommonStyleName = "MobileSimulator.Common";
		private const string IosStyleName = "MobileSimulator.iOS";
		private const string AndroidStyleName = "MobileSimulator.Android";
		private const float MinWidth = 360f;
		private const float MinHeight = 640f;

		private VisualElement _stage;
		private VisualElement _watermark;
		private Label _platformLabel;
		private StyleSheet _commonSheet;
		private StyleSheet _iosSheet;
		private StyleSheet _androidSheet;

		[MenuItem("Tools/GameLovers/Mobile Services Simulator Window")]
		public static MobileSimulatorWindow Open()
		{
			var window = GetWindow<MobileSimulatorWindow>();
			window.titleContent = new GUIContent("Mobile Simulator");
			window.minSize = new Vector2(MinWidth, MinHeight);
			window.Show();
			return window;
		}

		private void CreateGUI()
		{
			rootVisualElement.style.flexGrow = 1;

			_commonSheet = FindStyleSheet(CommonStyleName);
			_iosSheet = FindStyleSheet(IosStyleName);
			_androidSheet = FindStyleSheet(AndroidStyleName);
			if (_commonSheet != null)
			{
				rootVisualElement.styleSheets.Add(_commonSheet);
			}

			var root = new VisualElement { name = "simulator-root" };
			root.AddToClassList("simulator-root");
			rootVisualElement.Add(root);

			_stage = new VisualElement { name = "simulator-stage" };
			_stage.AddToClassList("simulator-stage");
			root.Add(_stage);

			_watermark = new VisualElement { name = "simulator-watermark" };
			_watermark.AddToClassList("simulator-watermark");
			_watermark.pickingMode = PickingMode.Ignore;
			_watermark.Add(new Label("[EDITOR SIMULATOR]"));
			_platformLabel = new Label();
			_platformLabel.AddToClassList("simulator-platform-label");
			_watermark.Add(_platformLabel);
			root.Add(_watermark);

			ApplyPlatformSheet(MobileSimulatorState.WindowPlatform);

			MobileSimulatorState.WindowPlatformChanged += OnPlatformChanged;
			MobileSimulatorState.AlertRequested += OnAlert;
			MobileSimulatorState.ToastRequested += OnToast;
			MobileSimulatorState.ShareRequested += OnShare;
			MobileSimulatorState.ReviewRequested += OnReview;
			MobileSimulatorState.NotificationBannerRequested += OnNotificationBanner;
			MobileSimulatorState.PermissionDialogRequested += OnPermissionDialog;
			MobileSimulatorState.DismissAllRequested += OnDismissAll;
		}

		private void OnDisable()
		{
			MobileSimulatorState.WindowPlatformChanged -= OnPlatformChanged;
			MobileSimulatorState.AlertRequested -= OnAlert;
			MobileSimulatorState.ToastRequested -= OnToast;
			MobileSimulatorState.ShareRequested -= OnShare;
			MobileSimulatorState.ReviewRequested -= OnReview;
			MobileSimulatorState.NotificationBannerRequested -= OnNotificationBanner;
			MobileSimulatorState.PermissionDialogRequested -= OnPermissionDialog;
			MobileSimulatorState.DismissAllRequested -= OnDismissAll;
		}

		private static bool TargetsThisSurface(SimulatorTarget targets) =>
			(targets & SimulatorTarget.StandaloneWindow) != 0;

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

		private void OnPlatformChanged(SimulatedPlatform platform)
		{
			ApplyPlatformSheet(platform);
		}

		private void ApplyPlatformSheet(SimulatedPlatform platform)
		{
			if (_iosSheet != null && rootVisualElement.styleSheets.Contains(_iosSheet))
			{
				rootVisualElement.styleSheets.Remove(_iosSheet);
			}
			if (_androidSheet != null && rootVisualElement.styleSheets.Contains(_androidSheet))
			{
				rootVisualElement.styleSheets.Remove(_androidSheet);
			}

			var active = platform == SimulatedPlatform.iOS ? _iosSheet : _androidSheet;
			if (active != null)
			{
				rootVisualElement.styleSheets.Add(active);
			}

			rootVisualElement.RemoveFromClassList("platform-ios");
			rootVisualElement.RemoveFromClassList("platform-android");
			rootVisualElement.AddToClassList(platform == SimulatedPlatform.iOS ? "platform-ios" : "platform-android");

			if (_platformLabel != null)
			{
				_platformLabel.text = platform.ToString();
			}
		}

		// ---- Payload renderers ----

		private void OnAlert(SimulatorTarget targets, SimulatedAlertSpec spec)
		{
			if (!TargetsThisSurface(targets)) return;
			ClearStage();
			var dialog = MockBuilders.BuildAlert(MobileSimulatorState.WindowPlatform, spec, dismissCallback: ClearStage);
			_stage.Add(dialog);
		}

		private void OnToast(SimulatorTarget targets, SimulatedToastSpec spec)
		{
			if (!TargetsThisSurface(targets)) return;
			ClearStage();
			var toast = MockBuilders.BuildToast(MobileSimulatorState.WindowPlatform, spec);
			_stage.Add(toast);

			// Toasts dismiss themselves on a real device — auto-clear the mock after the matching delay.
			// EditorWindow doesn't expose `schedule`; route through the rootVisualElement so the
			// scheduler is the panel's own (lives as long as the window).
			var seconds = spec.IsLongDuration ? 3.5f : 2.0f;
			rootVisualElement.schedule.Execute(() =>
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
			_stage.Add(MockBuilders.BuildShareSheet(MobileSimulatorState.WindowPlatform, spec, dismissCallback: ClearStage));
		}

		private void OnReview(SimulatorTarget targets)
		{
			if (!TargetsThisSurface(targets)) return;
			ClearStage();
			_stage.Add(MockBuilders.BuildReviewPrompt(MobileSimulatorState.WindowPlatform, dismissCallback: ClearStage));
		}

		private void OnNotificationBanner(SimulatorTarget targets, SimulatedNotificationBannerSpec spec)
		{
			if (!TargetsThisSurface(targets)) return;
			ClearStage();
			var banner = MockBuilders.BuildNotificationBanner(MobileSimulatorState.WindowPlatform, spec);
			_stage.Add(banner);
			rootVisualElement.schedule.Execute(() =>
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
			var dialog = MockBuilders.BuildPermissionDialog(MobileSimulatorState.WindowPlatform, spec, dismissCallback: result =>
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

			// Watermark cannot be cleared. It's not a child of the stage.
			_stage.Clear();
		}
	}
}

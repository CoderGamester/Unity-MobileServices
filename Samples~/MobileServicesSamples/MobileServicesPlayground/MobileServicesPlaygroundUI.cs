using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Gestures;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.NativeUi;
using GameLovers.MobileServices.Samples;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.MobileServicesPlayground
{
	/// <summary>A ready-to-run UI Toolkit tour of the mobile services package.</summary>
	public sealed class MobileServicesPlaygroundUI : MonoBehaviour
	{
		private UIDocument _document;
		private IDeviceService _device;
		private IHapticsService _haptics;
		private INativeUiService _nativeUi;
		private GestureController _gestures;
		private VisualElement _boundRoot;
		private Label _log;
		private Label _deviceStatus;
		private Label _safeAreaStatus;
		private readonly List<string> _logEntries = new List<string>();

		private void Awake()
		{
			_document = GetComponent<UIDocument>();
			EnsureRuntimeDependencies();
		}

		private void Start()
		{
			EnsureRuntimeDependencies();
			_device.Battery.OnLevelChanged += OnBatteryChanged;
			_device.Battery.OnStatusChanged += OnBatteryStatusChanged;
			_gestures.Tapped += OnTapped;
			_gestures.Swiped += OnSwiped;
			EnsureUiBound();
			Log("Playground ready. Try a button or swipe on the phone preview.");
		}

		private void Update()
		{
			EnsureUiBound();
			if (_deviceStatus != null)
			{
				var batteryLevel = _device.Battery.Level < 0f ? "Unknown" : $"{_device.Battery.Level:P0}";
				_deviceStatus.text = SampleStatusFormatter.Format(
					new SampleStatusEntry("Battery level", batteryLevel),
					new SampleStatusEntry("Battery status", _device.Battery.Status),
					new SampleStatusEntry("Low-power mode", SampleStatusFormatter.YesNo(_device.Battery.IsLowPowerMode)),
					new SampleStatusEntry("Keep awake", SampleStatusFormatter.YesNo(_device.ScreenWake.KeepAwake)),
					new SampleStatusEntry("ATT status", _device.Att.CurrentStatus));
			}
			if (_safeAreaStatus != null)
			{
				_safeAreaStatus.text = SampleStatusFormatter.Format(new SampleStatusEntry("Safe area", _device.SafeArea.SafeArea));
			}
		}

		private void OnDestroy()
		{
			_boundRoot?.UnregisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
			if (_device != null)
			{
				_device.Battery.OnLevelChanged -= OnBatteryChanged;
				_device.Battery.OnStatusChanged -= OnBatteryStatusChanged;
			}
			if (_gestures != null)
			{
				_gestures.Tapped -= OnTapped;
				_gestures.Swiped -= OnSwiped;
			}
			(_haptics as IDisposable)?.Dispose();
			(_device as IDisposable)?.Dispose();
		}

		private void EnsureUiBound()
		{
			EnsureRuntimeDependencies();
			if (_document == null) _document = GetComponent<UIDocument>();
			var root = _document == null ? null : _document.rootVisualElement;
			if (root == null) return;
			var deviceStatus = root.Q<Label>("device-status");
			if (ReferenceEquals(_deviceStatus, deviceStatus)) return;

			_boundRoot?.UnregisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
			_boundRoot = root;
			_log = root.Q<Label>("log");
			_deviceStatus = deviceStatus;
			_safeAreaStatus = root.Q<Label>("safe-area-status");
			var safeArea = root as SafeAreaContainer ?? root.Q<SafeAreaContainer>();
			safeArea?.SetSafeAreaService(_device.SafeArea);
			BindClickHaptics(root);
			BindButtons(root);
			BindPermissions(root);
			RefreshLog();
		}

		private void EnsureRuntimeDependencies()
		{
			if (_device == null) _device = new DeviceService();
			if (_haptics == null) _haptics = new HapticsService();
			if (_nativeUi == null) _nativeUi = new NativeUiServiceInstance();
			if (_gestures == null) _gestures = GetComponent<GestureController>();
			if (_gestures == null) _gestures = gameObject.AddComponent<GestureController>();
		}

		private void BindButtons(VisualElement root)
		{
			root.Q<Button>("native-alert")?.RegisterCallback<ClickEvent>(_ =>
			{
				_nativeUi.ShowAlertPopUp(
					false, "Delete save?", "This cannot be undone.",
					new AlertButton { Text = "Cancel", Style = AlertButtonStyle.Cancel, Callback = () => Log("Alert cancelled") },
					new AlertButton { Text = "Delete", Style = AlertButtonStyle.Destructive, Callback = () => Log("Alert delete") });
				Log("Requested native alert.");
			});
			root.Q<Button>("native-sheet")?.RegisterCallback<ClickEvent>(_ =>
			{
				_nativeUi.ShowAlertPopUp(
					true, "Photo options", "Choose an action", new AlertButton { Text = "Cancel", Style = AlertButtonStyle.Cancel },
					new AlertButton { Text = "Replace", Style = AlertButtonStyle.Default },
					new AlertButton { Text = "Remove", Style = AlertButtonStyle.Destructive });
				Log("Requested native action sheet.");
			});
			root.Q<Button>("native-toast")?.RegisterCallback<ClickEvent>(_ =>
			{
				_nativeUi.ShowToastMessage("Item collected!", false);
				Log("Requested native toast.");
			});
			root.Q<Button>("native-review")?.RegisterCallback<ClickEvent>(_ =>
			{
				_nativeUi.RequestReview();
				Log("Requested app review.");
			});
			root.Q<Button>("native-share")?.RegisterCallback<ClickEvent>(_ =>
			{
				_nativeUi.Share("Check out my score!", "https://example.com");
				Log("Requested share sheet.");
			});
			root.Q<Button>("att-request")?.RegisterCallback<ClickEvent>(async _ =>
			{
				var result = await _device.Att.RequestAuthorizationAsync();
				Log($"ATT request: {result}");
			});
			root.Q<Button>("keep-awake")?.RegisterCallback<ClickEvent>(_ =>
			{
				_device.ScreenWake.KeepAwake = !_device.ScreenWake.KeepAwake;
				Log($"Keep awake: {_device.ScreenWake.KeepAwake}");
			});
			root.Q<Button>("audio-session")?.RegisterCallback<ClickEvent>(_ =>
			{
				_device.AudioSession.ConfigureForPlayback();
				Log("Configured audio session for playback.");
			});
		}

		private void BindClickHaptics(VisualElement root)
		{
			root.RegisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
		}

		private void OnButtonClick(ClickEvent evt)
		{
			var target = evt.target as VisualElement;
			var button = target as Button ?? target?.GetFirstAncestorOfType<Button>();
			if (button != null && button.enabledInHierarchy)
			{
				_haptics.PlayPreset(HapticPreset.Selection);
			}
		}

		private void BindPermissions(VisualElement root)
		{
			var list = root.Q<VisualElement>("permission-list");
			if (list == null) return;
			list.Clear();
			foreach (AppPermission permission in Enum.GetValues(typeof(AppPermission)))
			{
				var captured = permission;
				var row = new VisualElement();
				row.AddToClassList("sample-row");
				var label = new Label(permission.ToString());
				label.AddToClassList("sample-row-label");
				row.Add(label);
				var check = new Button(() => Log($"{captured}: {_device.Permissions.Check(captured)}")) { text = "Check" };
				check.AddToClassList("sample-button");
				row.Add(check);
				var request = new Button(async () => Log($"{captured}: {await _device.Permissions.RequestAsync(captured)}")) { text = "Request" };
				request.AddToClassList("sample-button");
				row.Add(request);
				list.Add(row);
			}
		}

		private void OnBatteryChanged() => Log($"Battery {_device.Battery.Level:P0}");
		private void OnBatteryStatusChanged() => Log($"Battery status {_device.Battery.Status}");
		private void OnTapped(TapInput tap) => Log($"Tapped at {tap.PressPosition}");
		private void OnSwiped(SwipeInput swipe) => Log($"Swiped {swipe.SwipeDirection}");

		private void Log(string message)
		{
			_logEntries.Insert(0, message);
			if (_logEntries.Count > 12) _logEntries.RemoveAt(_logEntries.Count - 1);
			RefreshLog();
		}

		private void RefreshLog()
		{
			if (_log != null) _log.text = string.Join("\n", _logEntries);
		}

	}
}

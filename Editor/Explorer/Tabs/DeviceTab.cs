using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Tabs
{
	/// <summary>Device tab — live battery / connectivity / safe area / LPM plus simulator overrides.</summary>
	public sealed class DeviceTab : MobileServiceTab
	{
		public override string DisplayName => "Device";
		protected override int RefreshIntervalMs => 500;

		private DeviceService _device;
		private Label _battery;
		private Label _battStatus;
		private Label _lpm;
		private Label _connectivity;
		private Label _safeArea;
		private Label _screenWake;
		private Toggle _lpmToggle;
		private DropdownField _reachabilityDropdown;
		private FloatField _safeAreaInsetTop;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			scroll.Add(MakeSectionLabel("Live state"));
			_battery = new Label();      scroll.Add(_battery);
			_battStatus = new Label();   scroll.Add(_battStatus);
			_lpm = new Label();          scroll.Add(_lpm);
			_connectivity = new Label(); scroll.Add(_connectivity);
			_safeArea = new Label();     scroll.Add(_safeArea);
			_screenWake = new Label();   scroll.Add(_screenWake);

			scroll.Add(MakeSectionLabel("Simulator (Play mode)"));
			_lpmToggle = new Toggle("Low Power Mode") { value = false };
			_lpmToggle.RegisterValueChangedCallback(evt =>
			{
				if (_device == null) return;
				EditorPlatformSimulator.SetIosLowPowerMode(evt.newValue, _device.Battery as BatteryService);
			});
			scroll.Add(_lpmToggle);

			_reachabilityDropdown = new DropdownField("Connectivity",
				new System.Collections.Generic.List<string>
				{
					NetworkReachability.NotReachable.ToString(),
					NetworkReachability.ReachableViaCarrierDataNetwork.ToString(),
					NetworkReachability.ReachableViaLocalAreaNetwork.ToString(),
				}, 0);
			_reachabilityDropdown.RegisterValueChangedCallback(evt =>
			{
				if (_device == null) return;
				if (System.Enum.TryParse<NetworkReachability>(evt.newValue, out var parsed))
				{
					EditorPlatformSimulator.SetConnectivity(parsed, _device.Connectivity as ConnectivityService);
				}
			});
			scroll.Add(_reachabilityDropdown);

			var safeAreaRow = new VisualElement();
			safeAreaRow.style.flexDirection = FlexDirection.Row;
			safeAreaRow.Add(new Label("Notch inset (top px)"));
			_safeAreaInsetTop = new FloatField { value = 0f };
			_safeAreaInsetTop.style.flexGrow = 1;
			_safeAreaInsetTop.style.marginLeft = 8;
			safeAreaRow.Add(_safeAreaInsetTop);
			scroll.Add(safeAreaRow);

			var applySafeArea = new Button(() =>
			{
				if (_device == null) return;
				var inset = Mathf.Max(0f, _safeAreaInsetTop.value);
				var rect = new Rect(0f, 0f, Screen.width, Mathf.Max(1f, Screen.height - inset));
				EditorPlatformSimulator.SetSafeArea(rect, _device.SafeArea as SafeAreaService);
			}) { text = "Apply notch inset" };
			applySafeArea.AddToClassList("action-primary");
			scroll.Add(applySafeArea);

			var clearSafeArea = new Button(() =>
			{
				if (_device == null) return;
				EditorPlatformSimulator.ClearSafeAreaOverride(_device.SafeArea as SafeAreaService);
			}) { text = "Clear safe-area override" };
			scroll.Add(clearSafeArea);

			var bar = MakeActionBar();
			bar.Add(MakePrimaryButton("Initialise DeviceService", InitialiseService));
			bar.Add(MakePrimaryDangerButton("Dispose", DisposeService));
			scroll.Add(bar);

			Add(scroll);
		}

		protected override void Refresh()
		{
			if (_device == null)
			{
				_battery.text = "Battery: (Initialise to start polling)";
				_battStatus.text = string.Empty;
				_lpm.text = string.Empty;
				_connectivity.text = string.Empty;
				_safeArea.text = $"Screen.safeArea: {Screen.safeArea}";
				_screenWake.text = string.Empty;
				return;
			}

			_battery.text = $"Battery: {_device.Battery.Level:P0}";
			_battStatus.text = $"Status: {_device.Battery.Status}";
			_lpm.text = $"Low Power Mode: {_device.Battery.IsLowPowerMode}";
			_connectivity.text = $"Connectivity: {_device.Connectivity.Status}";
			_safeArea.text = $"Safe area: {_device.SafeArea.SafeArea}";
			_screenWake.text = $"KeepAwake: {_device.ScreenWake.KeepAwake}";
		}

		protected override void OnExitingPlayMode()
		{
			DisposeService();
		}

		private void InitialiseService()
		{
			if (!Application.isPlaying)
			{
				Debug.Log("[MobileServicesExplorer] DeviceService spawns a DontDestroyOnLoad host — requires Play mode.");
				return;
			}
			if (_device != null) return;
			_device = new DeviceService();
			Refresh();
		}

		private void DisposeService()
		{
			_device?.Dispose();
			_device = null;
			Refresh();
		}
	}
}

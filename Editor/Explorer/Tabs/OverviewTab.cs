using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Explorer.Windows;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Tabs
{
	/// <summary>Landing tab — card grid with an <c>Open</c> jump-link per other tab.</summary>
	public sealed class OverviewTab : MobileServiceTab
	{
		public override string DisplayName => "Overview";
		protected override int RefreshIntervalMs => 1000;

		private readonly MobileServicesExplorerWindow _window;
		private VisualElement _grid;

		public OverviewTab(MobileServicesExplorerWindow window)
		{
			_window = window;
		}

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			_grid = new VisualElement();
			_grid.AddToClassList("overview-grid");
			scroll.Add(_grid);

			Add(scroll);
		}

		protected override void Refresh()
		{
			_grid.Clear();

			_grid.Add(MakeCard<NativeUiTab>("Native UI", "alerts / toasts / share / review", true));
			_grid.Add(MakeCard<HapticsTab>("Haptics", "9 presets + custom + auto-stop", true));
			_grid.Add(MakeCard<NotificationsTab>("Notifications", "channels + scheduling + queueing", true));
			_grid.Add(MakeCard<GesturesTab>("Gestures", "EnhancedTouch swipes + taps", true));
			_grid.Add(BuildDeviceCard());
			_grid.Add(BuildPermissionsCard());
			_grid.Add(BuildAttDeepLinkCard());
		}

		private VisualElement BuildDeviceCard()
		{
			var card = MakeCardBase("Device");
			var pill = new Label($"safe-area: {UnityEngine.Screen.safeArea.size}");
			pill.AddToClassList("status-ok");
			card.Add(pill);

			var actions = new VisualElement();
			actions.AddToClassList("overview-card-actions");
			actions.Add(MakeOpenButton<DeviceTab>());
			card.Add(actions);
			return card;
		}

		private VisualElement BuildPermissionsCard()
		{
			var card = MakeCardBase("Permissions");
			var pill = new Label("editor: Granted by default");
			pill.AddToClassList("status-ok");
			card.Add(pill);
			var actions = new VisualElement();
			actions.AddToClassList("overview-card-actions");
			actions.Add(MakeOpenButton<PermissionsTab>());
			card.Add(actions);
			return card;
		}

		private VisualElement BuildAttDeepLinkCard()
		{
			var card = MakeCardBase("ATT + Deep Links");
			var pill = new Label("editor short-circuit: Authorized");
			pill.AddToClassList("status-ok");
			card.Add(pill);
			var actions = new VisualElement();
			actions.AddToClassList("overview-card-actions");
			actions.Add(MakeOpenButton<AttDeepLinkTab>());
			card.Add(actions);
			return card;
		}

		private VisualElement MakeCard<TTab>(string title, string subtitle, bool isOk) where TTab : MobileServiceTab
		{
			var card = MakeCardBase(title);
			var pill = new Label(subtitle);
			pill.AddToClassList(isOk ? "status-ok" : "status-warn");
			card.Add(pill);

			var actions = new VisualElement();
			actions.AddToClassList("overview-card-actions");
			actions.Add(MakeOpenButton<TTab>());
			card.Add(actions);
			return card;
		}

		private static VisualElement MakeCardBase(string title)
		{
			var card = new VisualElement();
			card.AddToClassList("overview-card");
			var titleLbl = new Label(title);
			titleLbl.AddToClassList("overview-card-title");
			card.Add(titleLbl);
			return card;
		}

		private Button MakeOpenButton<TTab>() where TTab : MobileServiceTab
		{
			return new Button(() => _window?.SelectTab<TTab>()) { text = "Open" };
		}
	}
}

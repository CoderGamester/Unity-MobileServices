using System.Collections.Generic;
using GameLovers.MobileServices.Editor.Explorer.Overlays;
using GameLovers.MobileServices.Editor.Explorer.Tabs;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Windows
{
	/// <summary>
	/// Main Mobile Services Explorer dockable window. Open via <c>Tools &gt; GameLovers &gt;
	/// Mobile Services Explorer</c>. Top-row <c>Render as: iOS | Android</c> toggle drives the
	/// platform skin of the truth-mirror <see cref="MobileSimulatorWindow"/> (the runtime
	/// overlay carries its own platform value driven by the Device Simulator plugin — see
	/// <see cref="MobileSimulatorState.OverlayPlatform"/>).
	/// </summary>
	public class MobileServicesExplorerWindow : EditorWindow
	{
		private const string SelectedTabPrefKey = "GameLovers.MobileServicesExplorer.SelectedTab";
		private const float MinWidth = 640f;
		private const float MinHeight = 480f;
		private const string ScopeHintText = "Controls the Mobile Simulator window only.";

		private TabView _tabView;
		private DropdownField _platformDropdown;
		private readonly List<MobileServiceTab> _tabs = new List<MobileServiceTab>();

		[MenuItem("Tools/GameLovers/Mobile Services Explorer")]
		public static MobileServicesExplorerWindow Open()
		{
			var window = GetWindow<MobileServicesExplorerWindow>();
			window.titleContent = new GUIContent("Mobile Services Explorer");
			window.minSize = new Vector2(MinWidth, MinHeight);
			window.Show();
			return window;
		}

		/// <summary>
		/// Opens the Explorer and navigates to the tab matching <typeparamref name="T"/>.
		/// </summary>
		public static MobileServicesExplorerWindow OpenOnTab<T>() where T : MobileServiceTab
		{
			var window = Open();
			window.SelectTab<T>();
			return window;
		}

		/// <summary>Navigates to the tab matching <typeparamref name="T"/>. No-ops if not registered.</summary>
		public void SelectTab<T>() where T : MobileServiceTab
		{
			for (var i = 0; i < _tabs.Count; i++)
			{
				if (_tabs[i] is T)
				{
					_tabView.activeTab = _tabView[i] as Tab;
					return;
				}
			}
		}

		/// <summary>The tabs registered in this window. Exposed for tests.</summary>
		internal IReadOnlyList<MobileServiceTab> RegisteredTabs => _tabs;

		private void CreateGUI()
		{
			rootVisualElement.style.flexGrow = 1;

			LoadSharedStyleSheet();

			BuildHeader();

			_tabView = new TabView { name = "mobile-service-tab-view" };
			_tabView.style.flexGrow = 1;
			rootVisualElement.Add(_tabView);

			RegisterTabs();
			RestoreSelectedTab();

			_tabView.activeTabChanged += OnActiveTabChanged;
		}

		private void LoadSharedStyleSheet()
		{
			var guids = AssetDatabase.FindAssets("MobileServicesExplorerWindow t:StyleSheet");
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (path.EndsWith("MobileServicesExplorerWindow.uss"))
				{
					var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
					if (sheet != null)
					{
						rootVisualElement.styleSheets.Add(sheet);
					}
					return;
				}
			}
		}

		private void BuildHeader()
		{
			var headerRow = new VisualElement { name = "explorer-header" };
			headerRow.AddToClassList("explorer-header");

			var label = new Label("Render as");
			label.AddToClassList("explorer-header-label");
			headerRow.Add(label);

			_platformDropdown = new DropdownField
			{
				choices = new List<string> { SimulatedPlatform.iOS.ToString(), SimulatedPlatform.Android.ToString() },
				value = MobileSimulatorState.WindowPlatform.ToString(),
				tooltip = ScopeHintText,
			};
			_platformDropdown.AddToClassList("explorer-platform-dropdown");
			_platformDropdown.RegisterValueChangedCallback(evt =>
			{
				if (System.Enum.TryParse<SimulatedPlatform>(evt.newValue, out var parsed))
				{
					MobileSimulatorState.WindowPlatform = parsed;
				}
			});
			headerRow.Add(_platformDropdown);

			// Scope hint sits inline next to the dropdown so the next dev who pairs the Explorer with
			// the Device Simulator plugin doesn't expect this toggle to drive the in-Game-view overlay
			// (it does not — the overlay's platform is auto-synced by the plugin from the device profile).
			var scopeHint = new Label(ScopeHintText);
			scopeHint.AddToClassList("explorer-header-hint");
			headerRow.Add(scopeHint);

			var openSimBtn = new Button(() => MobileSimulatorWindow.Open()) { text = "Open Simulator" };
			openSimBtn.AddToClassList("explorer-header-button");
			headerRow.Add(openSimBtn);

			rootVisualElement.Add(headerRow);
		}

		private void RegisterTabs()
		{
			_tabs.Clear();

			AddTab(new OverviewTab(this));
			AddTab(new NativeUiTab());
			AddTab(new HapticsTab());
			AddTab(new NotificationsTab());
			AddTab(new GesturesTab());
			AddTab(new DeviceTab());
			AddTab(new PermissionsTab());
			AddTab(new AttDeepLinkTab());
		}

		private void AddTab(MobileServiceTab serviceTab)
		{
			var tab = new Tab(serviceTab.DisplayName);
			tab.Add(serviceTab);
			_tabView.Add(tab);
			_tabs.Add(serviceTab);
		}

		private void RestoreSelectedTab()
		{
			var savedIndex = EditorPrefs.GetInt(SelectedTabPrefKey, 0);
			if (savedIndex >= 0 && savedIndex < _tabView.childCount)
			{
				_tabView.activeTab = _tabView[savedIndex] as Tab;
			}
		}

		private void OnActiveTabChanged(Tab previous, Tab current)
		{
			var index = _tabView.IndexOf(current);
			if (index >= 0)
			{
				EditorPrefs.SetInt(SelectedTabPrefKey, index);
			}
		}

		private void OnDisable()
		{
			if (_tabView != null)
			{
				_tabView.activeTabChanged -= OnActiveTabChanged;
			}
		}
	}
}

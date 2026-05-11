using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Tabs
{
	/// <summary>
	/// Abstract base for all Mobile Services Explorer tab panels. Mirrors the <c>ServiceTab</c>
	/// pattern in <c>com.gamelovers.services</c>; see <c>AGENTS.md</c> §2 for the contract.
	/// </summary>
	public abstract class MobileServiceTab : VisualElement
	{
		private const string BannerClass = "tab-banner";
		private const string RootClass = "tab-root";
		private const string EditModeBannerText = "Not in Play mode — showing last snapshot";
		private const string StoppedBannerText = "Play session ended — services unbound";

		private Label _banner;
		private IVisualElementScheduledItem _refreshTask;
		private bool _hasSeenPlay;

		private readonly HashSet<string> _collapsedFoldoutKeys = new HashSet<string>();
		private string _lastRefreshDigest;

		/// <summary>Tab header text shown in the TabView strip.</summary>
		public abstract string DisplayName { get; }

		/// <summary>Refresh interval in milliseconds during Play mode. Override for slower updates.</summary>
		protected virtual int RefreshIntervalMs => 250;

		protected MobileServiceTab()
		{
			AddToClassList(RootClass);
			style.flexGrow = 1;

			_banner = new Label(EditModeBannerText);
			_banner.AddToClassList(BannerClass);
			Add(_banner);

			BuildUi();
			UpdateBannerVisibility();

			RegisterCallback<AttachToPanelEvent>(OnAttach);
			RegisterCallback<DetachFromPanelEvent>(OnDetach);
		}

		/// <summary>Build all child VisualElements. Called once in the constructor after the banner.</summary>
		protected abstract void BuildUi();

		/// <summary>
		/// Pull latest data from services and repopulate UI. Called every <see cref="RefreshIntervalMs"/> ms
		/// during Play mode, and once manually on attach (Edit mode snapshot).
		/// </summary>
		protected abstract void Refresh();

		/// <summary>
		/// Called synchronously on <see cref="PlayModeStateChange.ExitingPlayMode"/>, BEFORE
		/// scene teardown. Subclasses with populated state widgets should override to forcibly
		/// clear them — see ServiceTab.OnExitingPlayMode rationale.
		/// </summary>
		protected virtual void OnExitingPlayMode() { }

		private void OnAttach(AttachToPanelEvent _)
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				_hasSeenPlay = true;
			}
			InvalidateRefreshDigest();
			UpdateBannerVisibility();
			Refresh();
			if (EditorApplication.isPlaying)
			{
				StartRefreshTimer();
			}
		}

		private void OnDetach(DetachFromPanelEvent _)
		{
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
			StopRefreshTimer();
		}

		private void OnPlayModeChanged(PlayModeStateChange state)
		{
			switch (state)
			{
				case PlayModeStateChange.EnteredPlayMode:
					_hasSeenPlay = true;
					UpdateBannerVisibility();
					InvalidateRefreshDigest();
					Refresh();
					StartRefreshTimer();
					break;
				case PlayModeStateChange.ExitingPlayMode:
					StopRefreshTimer();
					OnExitingPlayMode();
					UpdateBannerVisibility();
					InvalidateRefreshDigest();
					EditorApplication.delayCall += DelayedExitRefresh;
					break;
				case PlayModeStateChange.EnteredEditMode:
					UpdateBannerVisibility();
					InvalidateRefreshDigest();
					Refresh();
					break;
			}
		}

		private void DelayedExitRefresh()
		{
			if (panel == null)
			{
				return;
			}
			UpdateBannerVisibility();
			InvalidateRefreshDigest();
			Refresh();
		}

		private void StartRefreshTimer()
		{
			StopRefreshTimer();
			_refreshTask = schedule.Execute(() =>
			{
				if (panel != null)
				{
					Refresh();
				}
			}).Every(RefreshIntervalMs);
		}

		private void StopRefreshTimer()
		{
			_refreshTask?.Pause();
			_refreshTask = null;
		}

		private void UpdateBannerVisibility()
		{
			if (EditorApplication.isPlaying)
			{
				_banner.style.display = DisplayStyle.None;
				return;
			}
			_banner.text = _hasSeenPlay ? StoppedBannerText : EditModeBannerText;
			_banner.style.display = DisplayStyle.Flex;
		}

		// ---- Helpers for sub-classes ----

		protected static VisualElement MakeRow(string label, string value = null)
		{
			var row = new VisualElement();
			row.AddToClassList("row");
			var lbl = new Label(label);
			lbl.AddToClassList("row-label");
			row.Add(lbl);
			if (value != null)
			{
				var val = new Label(value);
				val.AddToClassList("row-value");
				row.Add(val);
			}
			return row;
		}

		protected static Button MakeRowButton(string text, Action onClick, bool danger = false)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("row-btn");
			if (danger)
			{
				btn.AddToClassList("row-btn-danger");
			}
			return btn;
		}

		protected static Label MakeSectionLabel(string text)
		{
			var lbl = new Label(text);
			lbl.AddToClassList("tab-section-label");
			return lbl;
		}

		protected static Label MakeEmptyLabel(string text = "— none —")
		{
			var lbl = new Label(text);
			lbl.AddToClassList("tab-empty-label");
			return lbl;
		}

		protected static VisualElement MakeActionBar()
		{
			var bar = new VisualElement();
			bar.AddToClassList("action-bar");
			return bar;
		}

		protected static Button MakePrimaryButton(string text, Action onClick)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("action-primary");
			return btn;
		}

		/// <summary>
		/// Destructive primary action button styled with <c>action-primary-danger</c>. Use for
		/// primary call-to-actions that remove or invalidate state — see workspace
		/// "Services Explorer Destructive-Action Styling" rule.
		/// </summary>
		protected static Button MakePrimaryDangerButton(string text, Action onClick)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("action-primary-danger");
			return btn;
		}

		/// <summary>
		/// Returns <c>true</c> when the supplied <paramref name="digest"/> matches the previous
		/// refresh's digest, so the tab can return early without rebuilding. Required for tabs
		/// whose <c>Refresh()</c> nukes-and-rebuilds the visual tree, otherwise rapid clicks are
		/// eaten by the periodic refresh destroying mouse-captured elements.
		/// </summary>
		protected bool TryShortCircuitRefresh(string digest)
		{
			if (digest != null && string.Equals(_lastRefreshDigest, digest, StringComparison.Ordinal))
			{
				return true;
			}
			_lastRefreshDigest = digest;
			return false;
		}

		protected void InvalidateRefreshDigest()
		{
			_lastRefreshDigest = null;
		}

		/// <summary>
		/// Creates a <see cref="Foldout"/> whose expanded/collapsed state survives the tab's
		/// periodic refresh — see workspace "UIToolkit Sticky Foldout" rule.
		/// </summary>
		protected Foldout MakeStickyFoldout(string key, string text, bool defaultExpanded = true)
		{
			var foldout = new Foldout { text = text };
			var initialValue = defaultExpanded
				? !_collapsedFoldoutKeys.Contains(key)
				: _collapsedFoldoutKeys.Contains(key);
			foldout.SetValueWithoutNotify(initialValue);
			foldout.RegisterValueChangedCallback(evt =>
			{
				// Required filter — ChangeEvent<bool> bubbles up the visual tree, so a nested
				// Toggle's value change would otherwise mark the ancestor collapsed too.
				if (evt.target != foldout)
				{
					return;
				}
				if (evt.newValue)
				{
					_collapsedFoldoutKeys.Remove(key);
				}
				else
				{
					_collapsedFoldoutKeys.Add(key);
				}
			});
			return foldout;
		}
	}
}

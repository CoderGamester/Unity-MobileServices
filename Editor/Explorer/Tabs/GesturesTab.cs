using GameLovers.MobileServices.Gestures;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Tabs
{
	/// <summary>Gestures tab — auto-subscribes to a scene <see cref="GestureController"/> and shows last swipe/tap metrics.</summary>
	public sealed class GesturesTab : MobileServiceTab
	{
		public override string DisplayName => "Gestures";
		protected override int RefreshIntervalMs => 500;

		private Label _swipeLabel;
		private Label _tapLabel;
		private Label _statusLabel;
		private GestureController _attached;
		private SwipeInput _lastSwipe;
		private TapInput _lastTap;
		private bool _hasSwipe;
		private bool _hasTap;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			_statusLabel = new Label("Searching for GestureController in scene…");
			scroll.Add(_statusLabel);

			scroll.Add(MakeSectionLabel("Last swipe"));
			_swipeLabel = new Label("(none)");
			scroll.Add(_swipeLabel);

			scroll.Add(MakeSectionLabel("Last tap"));
			_tapLabel = new Label("(none)");
			scroll.Add(_tapLabel);

			var bar = MakeActionBar();
			bar.Add(MakePrimaryDangerButton("Reset", () =>
			{
				_hasSwipe = false;
				_hasTap = false;
				_swipeLabel.text = "(none)";
				_tapLabel.text = "(none)";
			}));
			scroll.Add(bar);

			Add(scroll);
		}

		protected override void Refresh()
		{
			var controller = Application.isPlaying
				? Object.FindFirstObjectByType<GestureController>()
				: null;

			if (controller != _attached)
			{
				if (_attached != null)
				{
					_attached.Swiped -= OnSwiped;
					_attached.Tapped -= OnTapped;
				}
				_attached = controller;
				if (_attached != null)
				{
					_attached.Swiped += OnSwiped;
					_attached.Tapped += OnTapped;
				}
			}

			_statusLabel.text = _attached != null
				? $"Attached to {_attached.gameObject.name}"
				: Application.isPlaying
					? "No GestureController found in scene."
					: "Enter Play mode to scan for GestureController.";

			if (_hasSwipe)
			{
				_swipeLabel.text = $"dir={_lastSwipe.SwipeDirection}, vel={_lastSwipe.SwipeVelocity:F1}, sameness={_lastSwipe.SwipeSameness:F2}, start={_lastSwipe.StartPosition}, end={_lastSwipe.EndPosition}";
			}
			if (_hasTap)
			{
				_tapLabel.text = $"press={_lastTap.PressPosition}, release={_lastTap.ReleasePosition}, duration={_lastTap.TapDuration:F3}s";
			}
		}

		protected override void OnExitingPlayMode()
		{
			if (_attached != null)
			{
				_attached.Swiped -= OnSwiped;
				_attached.Tapped -= OnTapped;
			}
			_attached = null;
			_hasSwipe = false;
			_hasTap = false;
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
	}
}

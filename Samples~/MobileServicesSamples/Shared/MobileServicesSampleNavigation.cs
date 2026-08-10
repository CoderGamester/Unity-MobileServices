using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Gestures;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples
{
	/// <summary>Builds and drives the shared bottom navigation in every sample scene.</summary>
	[DefaultExecutionOrder(-1000)]
	public sealed class MobileServicesSampleNavigation : MonoBehaviour
	{
#if UNITY_EDITOR
		internal static Func<MobileServicesSamplePage, bool> EditorSceneLoader;
#endif

		private static bool _isLoading;

		private UIDocument _document;
		private VisualElement _boundHost;
		private VisualElement _boundRoot;
		private MobileServicesSampleScrollController _scrollController;
		private GestureController _gestureController;
		private readonly Dictionary<int, Button> _pressedButtons = new Dictionary<int, Button>();

		private void Awake()
		{
			_document = GetComponent<UIDocument>();
			_gestureController = GetComponent<GestureController>();
			if (_gestureController == null) _gestureController = gameObject.AddComponent<GestureController>();
			MobileServicesSampleSession.GetOrCreate();
			EnsureUiBound();
		}

		private void Update()
		{
			EnsureUiBound();
		}

		private void Start()
		{
			EnsureUiBound();
		}

		private void OnDestroy()
		{
			_boundRoot?.UnregisterCallback<PointerDownEvent>(OnButtonPointerDown, TrickleDown.TrickleDown);
			_boundRoot?.UnregisterCallback<PointerUpEvent>(OnButtonPointerUp, TrickleDown.TrickleDown);
			_boundRoot?.UnregisterCallback<PointerCancelEvent>(OnButtonPointerCancel, TrickleDown.TrickleDown);
			_boundRoot?.UnregisterCallback<PointerCaptureOutEvent>(OnButtonPointerCaptureOut, TrickleDown.TrickleDown);
			_boundRoot?.UnregisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
			ClearAllPressedButtons();
			_scrollController?.Dispose();
		}

		/// <summary>Loads a sample page once, using the editor bridge when an individual scene is played.</summary>
		internal static bool TryNavigate(MobileServicesSamplePage page)
		{
			if (_isLoading ||
			    (MobileServicesSamplePages.TryGetPage(SceneManager.GetActiveScene().name, out var current) && current == page))
			{
				return false;
			}

			_isLoading = true;
			try
			{
#if UNITY_EDITOR
				if (EditorSceneLoader != null && EditorSceneLoader(page)) return true;
#endif
				var sceneName = MobileServicesSamplePages.GetSceneName(page);
				if (!Application.CanStreamedLevelBeLoaded(sceneName)) return false;
				SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
				return true;
			}
			finally
			{
				_isLoading = false;
			}
		}

		internal static void ClearButtonPressedState(Button button)
		{
			button?.RemoveFromClassList("sample-button--pressed");
		}

		private void EnsureUiBound()
		{
			var root = _document == null ? null : _document.rootVisualElement;
			var host = root?.Q<VisualElement>("sample-navigation");
			if (host == null || ReferenceEquals(_boundHost, host)) return;

			_boundRoot?.UnregisterCallback<PointerDownEvent>(OnButtonPointerDown, TrickleDown.TrickleDown);
			_boundRoot?.UnregisterCallback<PointerUpEvent>(OnButtonPointerUp, TrickleDown.TrickleDown);
			_boundRoot?.UnregisterCallback<PointerCancelEvent>(OnButtonPointerCancel, TrickleDown.TrickleDown);
			_boundRoot?.UnregisterCallback<PointerCaptureOutEvent>(OnButtonPointerCaptureOut, TrickleDown.TrickleDown);
			_boundRoot?.UnregisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
			_scrollController?.Dispose();
			_boundRoot = root;
			_boundHost = host;
			_scrollController = MobileServicesSampleScrollController.Attach(root.Q<ScrollView>(), root, host, _gestureController);
			root.RegisterCallback<PointerDownEvent>(OnButtonPointerDown, TrickleDown.TrickleDown);
			root.RegisterCallback<PointerUpEvent>(OnButtonPointerUp, TrickleDown.TrickleDown);
			root.RegisterCallback<PointerCancelEvent>(OnButtonPointerCancel, TrickleDown.TrickleDown);
			root.RegisterCallback<PointerCaptureOutEvent>(OnButtonPointerCaptureOut, TrickleDown.TrickleDown);
			root.RegisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
			ClearAllPressedButtons();
			host.Clear();
			if (!MobileServicesSamplePages.TryGetPage(SceneManager.GetActiveScene().name, out var current)) return;

			foreach (var page in MobileServicesSamplePages.All)
			{
				var target = page;
				var button = new Button(() => TryNavigate(target)) { text = MobileServicesSamplePages.GetDisplayName(page) };
				button.AddToClassList("sample-navigation-button");
				if (page == current)
				{
					button.AddToClassList("sample-navigation-button--current");
					button.SetEnabled(false);
				}
				host.Add(button);
			}
		}

		private static Button GetButton(EventBase evt)
		{
			var target = evt.target as VisualElement;
			return target as Button ?? target?.GetFirstAncestorOfType<Button>();
		}

		private void OnButtonPointerDown(PointerDownEvent evt)
		{
			if (evt.button != 0) return;
			var button = GetButton(evt);
			if (button == null || !button.enabledInHierarchy) return;

			ClearPressedButton(evt.pointerId);
			_pressedButtons[evt.pointerId] = button;
			button.AddToClassList("sample-button--pressed");
		}

		private void OnButtonPointerUp(PointerUpEvent evt) => ClearPressedButton(evt.pointerId, GetButton(evt), true);

		private void OnButtonPointerCancel(PointerCancelEvent evt) => ClearPressedButton(evt.pointerId, GetButton(evt), true);

		private void OnButtonPointerCaptureOut(PointerCaptureOutEvent evt) => ClearPressedButton(evt.pointerId, GetButton(evt));

		private void OnButtonClick(ClickEvent evt)
		{
			if (_scrollController != null && _scrollController.ConsumeClick(evt))
			{
				ClearPressedButton(evt.pointerId, GetButton(evt), true);
				evt.StopImmediatePropagation();
				evt.StopPropagation();
				return;
			}

			ClearPressedButton(evt.pointerId, GetButton(evt), true);
		}

		private void ClearPressedButton(int pointerId, Button fallback = null, bool releasePointer = false)
		{
			_pressedButtons.TryGetValue(pointerId, out var pressedButton);
			pressedButton ??= fallback;
			_pressedButtons.Remove(pointerId);
			ClearButtonPressedState(pressedButton);
			if (releasePointer && pressedButton != null && pressedButton.HasPointerCapture(pointerId))
			{
				pressedButton.ReleasePointer(pointerId);
			}
		}

		private void ClearAllPressedButtons()
		{
			foreach (var button in _pressedButtons.Values) ClearButtonPressedState(button);
			_pressedButtons.Clear();
		}

	}

	/// <summary>
	/// Provides predictable touch dragging for sample ScrollViews when the Device Simulator omits
	/// pressed-button flags.
	/// </summary>
	internal sealed class MobileServicesSampleScrollController : IDisposable
	{
		private const float DragThreshold = 8f;
		private const float MinimumInertiaVelocity = 8f;

		private readonly ScrollView _scroll;
		private readonly VisualElement _eventRoot;
		private readonly VisualElement _navigationHost;
		private readonly GestureController _gestureController;
		private IVisualElementScheduledItem _inertia;
		private Button _pressedButton;
		private int _pointerId = -1;
		private Vector2 _startPosition;
		private Vector2 _lastPosition;
		private Vector2 _startOffset;
		private Vector2 _velocity;
		private double _lastMoveTime;
		private bool _dragging;
		private bool _gestureAllowed;
		private bool _usingPointerEvents;
		private double _suppressClickUntil;

		private MobileServicesSampleScrollController(ScrollView scroll, VisualElement eventRoot, VisualElement navigationHost, GestureController gestureController)
		{
			_scroll = scroll;
			_eventRoot = eventRoot;
			_navigationHost = navigationHost;
			_gestureController = gestureController;
			_scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
			_scroll.elasticity = 0f;
			_eventRoot.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
			_eventRoot.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
			_eventRoot.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
			_eventRoot.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
			_gestureController.PotentiallySwiped += OnPotentiallySwiped;
			_gestureController.Swiped += OnSwipeFinished;
			_gestureController.Pressed += OnGesturePressed;
			_gestureController.Tapped += OnTapFinished;
		}

		public void Dispose()
		{
			StopInertia();
			if (_pointerId >= 0 && _scroll.HasPointerCapture(_pointerId)) _scroll.ReleasePointer(_pointerId);
			MobileServicesSampleNavigation.ClearButtonPressedState(_pressedButton);
			_eventRoot.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
			_eventRoot.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
			_eventRoot.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
			_eventRoot.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
			_gestureController.PotentiallySwiped -= OnPotentiallySwiped;
			_gestureController.Swiped -= OnSwipeFinished;
			_gestureController.Pressed -= OnGesturePressed;
			_gestureController.Tapped -= OnTapFinished;
			ClearSuppressedClick();
			ResetPointer();
		}

		internal static MobileServicesSampleScrollController Attach(ScrollView scroll, VisualElement eventRoot, VisualElement navigationHost, GestureController gestureController) =>
			scroll == null || eventRoot == null || gestureController == null ? null : new MobileServicesSampleScrollController(scroll, eventRoot, navigationHost, gestureController);

		/// <summary>Consumes a delayed button click generated by the simulator after a content drag.</summary>
		internal bool ConsumeClick(ClickEvent evt)
		{
			if (Time.realtimeSinceStartupAsDouble > _suppressClickUntil)
			{
				ClearSuppressedClick();
				return false;
			}

			if (!(evt.target is VisualElement target)) return false;
			var button = GetButton(target);
			if (button == null || IsNavigationTarget(button.worldBound.center)) return false;

			ClearSuppressedClick();
			return true;
		}

		private void OnPointerDown(PointerDownEvent evt)
		{
			var target = evt.target as VisualElement;
			if (evt.button != 0 || IsScroller(target)) return;
			if (IsNavigationTarget(evt.position))
			{
				ClearSuppressedClick();
				_gestureAllowed = false;
				ResetPointer();
				return;
			}
			if (!_scroll.worldBound.Contains(ToVector2(evt.position)) && target != _eventRoot) return;
			if (_pointerId >= 0)
			{
				if (_scroll.HasPointerCapture(_pointerId)) _scroll.ReleasePointer(_pointerId);
				ResetPointer();
			}

			StopInertia();
			_pointerId = evt.pointerId;
			_startPosition = ToVector2(evt.position);
			_lastPosition = _startPosition;
			_startOffset = _scroll.scrollOffset;
			_lastMoveTime = Time.realtimeSinceStartupAsDouble;
			_dragging = false;
			_gestureAllowed = true;
			_usingPointerEvents = false;
			_pressedButton = (evt.target as VisualElement) as Button ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<Button>();
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			if (_pointerId != evt.pointerId) return;
			_usingPointerEvents = true;

			var currentPosition = ToVector2(evt.position);
			var delta = currentPosition - _startPosition;
			if (!_dragging)
			{
				if (Mathf.Abs(delta.y) < DragThreshold || Mathf.Abs(delta.y) < Mathf.Abs(delta.x)) return;

				BeginDragging();
			}

			ApplyPointerPosition(currentPosition);
			evt.StopPropagation();
		}

		private void OnPotentiallySwiped(SwipeInput swipe)
		{
			if (_pointerId < 0 || !_gestureAllowed || _usingPointerEvents) return;

			var screenDelta = swipe.EndPosition - swipe.PreviousPosition;
			if (Mathf.Abs(screenDelta.y) < Mathf.Abs(screenDelta.x)) return;

			if (!_dragging) BeginDragging();
			var panelScale = _eventRoot.worldBound.height / Mathf.Max(1f, Screen.height);
			// EnhancedTouch positions use a bottom-left origin, while ScrollView offsets grow
			// downward as content moves up. Convert the gesture delta to the panel's top-left
			// convention before applying it so a normal upward finger drag scrolls downward.
			ApplyPointerDelta(-screenDelta.y * panelScale);
		}

		private void OnGesturePressed(SwipeInput swipe)
		{
			if (_pointerId >= 0) return;

			var panelPosition = ToPanelPosition(swipe.StartPosition);
			if (IsNavigationTarget(panelPosition))
			{
				ClearSuppressedClick();
				_gestureAllowed = false;
				ResetPointer();
				return;
			}

			StopInertia();
			_pointerId = swipe.InputId;
			_startPosition = panelPosition;
			_lastPosition = panelPosition;
			_startOffset = _scroll.scrollOffset;
			_lastMoveTime = Time.realtimeSinceStartupAsDouble;
			_dragging = false;
			_gestureAllowed = true;
			_usingPointerEvents = false;
			_pressedButton = GetButtonAt(panelPosition);
		}

		private void OnTapFinished(TapInput _)
		{
			if (_pointerId < 0 || _dragging) return;

			ReleasePointer(_pointerId);
			ResetPointer();
		}

		private void OnSwipeFinished(SwipeInput swipe)
		{
			if (_pointerId < 0 || !_dragging) return;

			ReleasePointer(_pointerId);
			StartInertia();
		}

		private void BeginDragging()
		{
			_dragging = true;
			_suppressClickUntil = Time.realtimeSinceStartupAsDouble + 0.5d;
			MobileServicesSampleNavigation.ClearButtonPressedState(_pressedButton);
			_pressedButton?.ReleasePointer(_pointerId);
			_scroll.CapturePointer(_pointerId);
		}

		private void ApplyPointerPosition(Vector2 currentPosition)
		{
			var now = Time.realtimeSinceStartupAsDouble;
			var elapsed = Mathf.Max(0.001f, (float)(now - _lastMoveTime));
			var pointerDelta = currentPosition - _lastPosition;
			_velocity = new Vector2(0f, -pointerDelta.y / elapsed);
			_scroll.scrollOffset = ClampOffset(_startOffset - new Vector2(0f, currentPosition.y - _startPosition.y));
			_lastPosition = currentPosition;
			_lastMoveTime = now;
		}

		private void ApplyPointerDelta(float deltaY)
		{
			var now = Time.realtimeSinceStartupAsDouble;
			var elapsed = Mathf.Max(0.001f, (float)(now - _lastMoveTime));
			_velocity = new Vector2(0f, -deltaY / elapsed);
			_scroll.scrollOffset = ClampOffset(_scroll.scrollOffset - new Vector2(0f, deltaY));
			_lastMoveTime = now;
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			if (_pointerId != evt.pointerId) return;

			if (_dragging)
			{
				ReleasePointer(evt.pointerId);
				evt.StopPropagation();
				StartInertia();
			}
			else
			{
				MobileServicesSampleNavigation.ClearButtonPressedState(_pressedButton);
				ResetPointer();
			}
		}

		private void OnPointerCancel(PointerCancelEvent evt)
		{
			if (_pointerId != evt.pointerId) return;
			ReleasePointer(evt.pointerId);
			ResetPointer();
		}

		private void ReleasePointer(int pointerId)
		{
			if (_scroll.HasPointerCapture(pointerId)) _scroll.ReleasePointer(pointerId);
			MobileServicesSampleNavigation.ClearButtonPressedState(_pressedButton);
		}

		private void StartInertia()
		{
			var velocity = _velocity;
			ResetPointer();
			if (Mathf.Abs(velocity.y) < MinimumInertiaVelocity) return;

			_inertia = _scroll.schedule.Execute(() =>
			{
				var deltaTime = Mathf.Max(0.001f, Time.unscaledDeltaTime);
				_velocity = Vector2.Lerp(velocity, Vector2.zero, 1f - Mathf.Pow(0.08f, deltaTime));
				_scroll.scrollOffset = ClampOffset(_scroll.scrollOffset + _velocity * deltaTime);
				if (Mathf.Abs(_velocity.y) < MinimumInertiaVelocity || AtVerticalLimit(_scroll.scrollOffset.y, _velocity.y)) StopInertia();
				velocity = _velocity;
			}).Every(16);
		}

		private void StopInertia()
		{
			_inertia?.Pause();
			_inertia = null;
		}

		private void ResetPointer()
		{
			_pointerId = -1;
			_pressedButton = null;
			_dragging = false;
			_gestureAllowed = false;
			_usingPointerEvents = false;
			_velocity = Vector2.zero;
		}

		private void ClearSuppressedClick()
		{
			_suppressClickUntil = 0d;
		}

		private Vector2 ClampOffset(Vector2 offset)
		{
			var maxY = Mathf.Max(0f, _scroll.contentContainer.layout.height - _scroll.contentViewport.layout.height);
			offset.y = Mathf.Clamp(offset.y, 0f, maxY);
			return offset;
		}

		private bool AtVerticalLimit(float offset, float velocity) =>
			(offset <= 0f && velocity < 0f) || (offset >= _scroll.contentContainer.layout.height - _scroll.contentViewport.layout.height && velocity > 0f);

		private static bool IsScroller(VisualElement element) =>
			element is Scroller || element?.GetFirstAncestorOfType<Scroller>() != null;

		private bool IsNavigationTarget(Vector3 position) =>
			_navigationHost != null && _navigationHost.worldBound.Contains(ToVector2(position));

		private Vector2 ToPanelPosition(Vector2 screenPosition)
		{
			var rootBounds = _eventRoot.worldBound;
			var screenWidth = Mathf.Max(1f, Screen.width);
			var screenHeight = Mathf.Max(1f, Screen.height);
			return new Vector2(
				rootBounds.x + screenPosition.x * rootBounds.width / screenWidth,
				rootBounds.y + rootBounds.height - screenPosition.y * rootBounds.height / screenHeight);
		}

		private Button GetButtonAt(Vector2 panelPosition) => GetButton(_eventRoot.panel?.Pick(panelPosition));

		private static Button GetButton(VisualElement target) =>
			target as Button ?? target?.GetFirstAncestorOfType<Button>();

		private static Vector2 ToVector2(Vector3 position) => new Vector2(position.x, position.y);

	}
}

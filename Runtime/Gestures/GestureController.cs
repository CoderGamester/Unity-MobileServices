using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace GameLovers.MobileServices.Gestures
{
    /// <summary>
    /// Controller that interprets takes pointer input from <see cref="Touch"/> and detects
    /// directional swipes and detects taps.
    /// </summary>
    public class GestureController : MonoBehaviour
    {
        // Maximum duration of a press before it can no longer be considered a tap.
        [SerializeField]
        private float _maxTapDuration = 0.2f;

        // Maximum distance in screen units that a tap can drift from its original position before
        // it is no longer considered a tap.
        [SerializeField]
        private float _maxTapDrift = 5.0f;

        // Maximum duration of a swipe before it is no longer considered to be a valid swipe.
        [SerializeField]
        private float _maxSwipeDuration = 0.5f;

        // Minimum distance in screen units that a swipe must move before it is considered a swipe.
        // Note that if this is smaller or equal to maxTapDrift, then it is possible for a user action to be
        // returned as both a swipe and a tap.
        [SerializeField]
        private float _minSwipeDistance = 10.0f;

        // How much a swipe should consistently be in the same direction before it is considered a swipe.
        [SerializeField]
        private float _swipeDirectionSamenessThreshold = 0.6f;

        [FormerlySerializedAs("label")]
        [Header("Debug"), SerializeField]
        private Text _label;

        // Mapping of input IDs to their active gesture tracking objects.
        private readonly Dictionary<int, ActiveGesture> _activeGestures = new Dictionary<int, ActiveGesture>();

        /// <summary>
        /// Event fired when the user presses on the screen.
        /// </summary>
        public event Action<SwipeInput> Pressed;

        /// <summary>
        /// Event fired for every motion (possibly multiple times a frame) of a potential swipe gesture.
        /// </summary>
        public event Action<SwipeInput> PotentiallySwiped;

        /// <summary>
        /// Event fired when a user performs a swipe gesture.
        /// </summary>
        public event Action<SwipeInput> Swiped;

        /// <summary>
        /// Event fired when a user performs a tap gesture, on releasing.
        /// </summary>
        public event Action<TapInput> Tapped;

        protected virtual void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            Touch.onFingerDown += OnFingerDown;
            Touch.onFingerMove += OnFingerMove;
            Touch.onFingerUp += OnFingerUp;
        }

        protected virtual void OnDisable()
        {
            Touch.onFingerDown -= OnFingerDown;
            Touch.onFingerMove -= OnFingerMove;
            Touch.onFingerUp -= OnFingerUp;
            EnhancedTouchSupport.Disable();
        }

        private void OnFingerDown(Finger finger)
        {
            var touch = finger.currentTouch;
            OnPressed(finger.index, touch.screenPosition, touch.time);
        }

        private void OnFingerMove(Finger finger)
        {
            var touch = finger.currentTouch;
            OnDragged(finger.index, touch.screenPosition, touch.time);
        }

        private void OnFingerUp(Finger finger)
        {
            var touch = finger.currentTouch;
            OnReleased(finger.index, touch.screenPosition, touch.time);
        }

        // Checks whether a given active gesture will be a valid swipe.
        private bool IsValidSwipe(ref ActiveGesture gesture)
        {
            return gesture.TravelDistance >= _minSwipeDistance &&
                (gesture.EndTime - gesture.StartTime) <= _maxSwipeDuration &&
                gesture.SwipeDirectionSameness >= _swipeDirectionSamenessThreshold;
        }

        // Checks whether a given active gesture will be a valid tap.
        private bool IsValidTap(ref ActiveGesture gesture)
        {
            return gesture.TravelDistance <= _maxTapDrift &&
                (gesture.EndTime - gesture.StartTime) <= _maxTapDuration;
        }

        private void OnPressed(int inputId, Vector2 position, double time)
        {
            Debug.Assert(!_activeGestures.ContainsKey(inputId));

            var newGesture = new ActiveGesture(inputId, position, time);
            _activeGestures.Add(inputId, newGesture);

            DebugInfo(newGesture);

            Pressed?.Invoke(new SwipeInput(newGesture));
        }

        private void OnDragged(int inputId, Vector2 position, double time)
        {
            if (!_activeGestures.TryGetValue(inputId, out var existingGesture))
            {
                // Probably caught by UI, or the input was otherwise lost
                return;
            }

            existingGesture.SubmitPoint(position, time);

            if (IsValidSwipe(ref existingGesture))
            {
                PotentiallySwiped?.Invoke(new SwipeInput(existingGesture));
            }

            DebugInfo(existingGesture);
        }

        private void OnReleased(int inputId, Vector2 position, double time)
        {
            if (!_activeGestures.TryGetValue(inputId, out var existingGesture))
            {
                // Probably caught by UI, or the input was otherwise lost
                return;
            }

            _activeGestures.Remove(inputId);
            existingGesture.SubmitPoint(position, time);

            if (IsValidSwipe(ref existingGesture))
            {
                Swiped?.Invoke(new SwipeInput(existingGesture));
            }

            if (IsValidTap(ref existingGesture))
            {
                Tapped?.Invoke(new TapInput(existingGesture));
            }

            DebugInfo(existingGesture);
        }

        private void DebugInfo(ActiveGesture gesture)
        {
            if (_label == null) return;

            var builder = new StringBuilder();

            builder.AppendFormat("ID: {0}", gesture.InputId);
            builder.AppendLine();
            builder.AppendFormat("Start Position: {0}", gesture.StartPosition);
            builder.AppendLine();
            builder.AppendFormat("Position: {0}", gesture.EndPosition);
            builder.AppendLine();
            builder.AppendFormat("Duration: {0}", gesture.EndTime - gesture.StartTime);
            builder.AppendLine();
            builder.AppendFormat("Sameness: {0}", gesture.SwipeDirectionSameness);
            builder.AppendLine();
            builder.AppendFormat("Travel distance: {0}", gesture.TravelDistance);
            builder.AppendLine();
            builder.AppendFormat("Samples: {0}", gesture.Samples);
            builder.AppendLine();
            builder.AppendFormat("Realtime since startup: {0}", Time.realtimeSinceStartup);
            builder.AppendLine();
            builder.AppendFormat("Starting Timestamp: {0}", gesture.StartTime);
            builder.AppendLine();
            builder.AppendFormat("Ending Timestamp: {0}", gesture.EndTime);
            builder.AppendLine();

            _label.text = builder.ToString();

            if (Camera.main == null) return;

            var worldStart = Camera.main.ScreenToWorldPoint(gesture.StartPosition);
            var worldEnd = Camera.main.ScreenToWorldPoint(gesture.EndPosition);

            worldStart.z += 5;
            worldEnd.z += 5;
        }
    }
}

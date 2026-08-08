# Gestures

`GestureController` is a `MonoBehaviour` that subscribes to Unity's `EnhancedTouch` API and emits swipe / tap events.

## Setup

1. Add `GestureController` to any scene GameObject.
2. Subscribe to its events:

```csharp
_gestureController.Pressed             += swipe => { /* on finger down */ };
_gestureController.PotentiallySwiped   += swipe => { /* every move; valid-swipe candidate */ };
_gestureController.Swiped              += swipe => { /* on finger up, IsValidSwipe */ };
_gestureController.Tapped              += tap   => { /* on finger up, IsValidTap */ };
```

3. In editor, add a `TouchSimulation` component to a GameObject if you want mouse-as-touch in the editor.

`EnhancedTouchSupport` is enabled in `OnEnable` and disabled in `OnDisable` — no manual lifecycle management needed.

## Thresholds

`GestureController` exposes five `[SerializeField]` thresholds:

| Field | Default | Meaning |
|-------|---------|---------|
| `_maxTapDuration` | `0.2f` | Press > this is not a tap |
| `_maxTapDrift` | `5.0f` | Drift in screen units > this is not a tap |
| `_maxSwipeDuration` | `0.5f` | Swipes longer than this don't qualify |
| `_minSwipeDistance` | `10.0f` | Movement under this is not a swipe |
| `_swipeDirectionSamenessThreshold` | `0.6f` | Swipes must be consistently in one direction this fraction of the time |

**Important**: if `_minSwipeDistance <= _maxTapDrift`, a single interaction can qualify as BOTH a tap and a swipe — both events fire. Tune carefully (or treat the dual case intentionally).

## SwipeInput

```csharp
public readonly struct SwipeInput
{
    public SwipeDirection SwipeDirection;  // Up / Down / Left / Right
    public float SwipeVelocity;            // speed of the gesture
    public float SwipeSameness;            // direction consistency 0–1 (higher = cleaner)
    public Vector2 StartPosition;
    public Vector2 EndPosition;
}
```

## TapInput

```csharp
public struct TapInput
{
    public readonly Vector2 PressPosition;
    public readonly Vector2 ReleasePosition;
    public readonly double TapDuration;
    public readonly float TapDrift;
    public readonly double TimeStamp;
}
```

## NOT in the umbrella facade

The `IMobileService` umbrella doesn't expose Gestures — `GestureController` is a per-scene MonoBehaviour, not a service-locator-style service. Add it to scenes that need it; subscribe from your input controller.

## Editor

- Editor pointer simulation requires `TouchSimulation` on a scene GameObject (Unity Input System feature).
- The Device Simulator panel's **Gestures** foldout finds the live `GestureController` via `Object.FindFirstObjectByType` and surfaces the last-detected swipe + tap metrics.
- Deterministic end-to-end Enhanced Touch injection isn't part of the package test assembly because it requires the Input System's `InputTestFixture`. The package covers gesture math, duplicate input-index resilience, and the controller's Enhanced Touch subscription lifecycle; imported-sample pointer behavior is verified through the real-input sample workflow.

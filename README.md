# GameLovers Mobile Services

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/github/v/tag/CoderGamester/com.gamelovers.mobileservices?label=version)](CHANGELOG.md)

> **Quick Links**: [Installation](#installation) | [Quick Start](#quick-start) | [Services](#services-at-a-glance) | [Contributing](#contributing)

## Why Use This Package?

Building mobile-specific features in Unity often requires dealing with platform-specific code, native bridges, and fragmented APIs. This **Mobile Services** package consolidates essential mobile functionality into a unified, easy-to-use API:

| Problem | Solution |
|---------|----------|
| **Platform-specific UI code** | Native UI service bridges iOS/Android alerts, toasts, and review prompts with one API |
| **Notification complexity** | Notification service wraps Unity Mobile Notifications with channel management |
| **Custom gesture detection** | Gesture controller provides swipe and tap detection via Unity's EnhancedTouch |
| **Editor testing challenges** | Editor fallbacks for all features enable testing without device builds |

**Built for production:** Uses Unity's official packages (`com.unity.mobile.notifications`, `com.unity.inputsystem`). Tested in real mobile games.

---

## System Requirements

- **[Unity](https://unity.com/download)** 6000.0+ (Unity 6)
- **[Unity Mobile Notifications](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@latest)** (2.3.0) — automatically resolved
- **[Unity Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)** (1.11.0) — automatically resolved

| Platform | Status |
|---|---|
| iOS | ✅ Supported |
| Android | ✅ Supported |
| Editor | ✅ Supported (fallbacks) |
| Standalone | ⚠️ Gestures only; no native UI/notifications |
| WebGL | ❌ Not Supported |

## Installation

### Via Unity Package Manager (Recommended)

1. Open Unity Package Manager (`Window` → `Package Manager`)
2. Click `+` → `Add package from git URL`
3. Enter: `https://github.com/CoderGamester/com.gamelovers.mobileservices.git`

### Via manifest.json

```json
{
  "dependencies": {
    "com.gamelovers.mobileservices": "https://github.com/CoderGamester/com.gamelovers.mobileservices.git"
  }
}
```

---

## Key Components

| Component | Responsibility |
|-----------|----------------|
| **NativeUiService** | Static class bridging native iOS/Android UI (alerts, action sheets, toasts) |
| **MobileNotificationService** | Notification scheduling, cancellation, and channel management |
| **IGameNotification** | Platform-agnostic notification interface |
| **GestureController** | MonoBehaviour detecting swipe and tap gestures via EnhancedTouch |
| **SwipeInput** | Data structure with swipe direction, velocity, and consistency metrics |
| **TapInput** | Data structure for tap position and finger data |

---

## Quick Start

### Native UI

```csharp
using GameLovers.MobileServices.NativeUi;

NativeUiService.ShowAlertPopUp(
    darkMode: false,
    title: "Delete Save?",
    message: "This action cannot be undone.",
    new AlertButton { Text = "Cancel", Style = AlertButtonStyle.Cancel },
    new AlertButton { Text = "Delete", Style = AlertButtonStyle.Destructive, OnClick = OnDeleteConfirmed }
);

NativeUiService.ShowToastMessage("Item Collected!", isLongDuration: false); // Android only
NativeUiService.RequestReview();
```

### Notifications

```csharp
using GameLovers.MobileServices.Notifications;

var service = new MobileNotificationService(
    new GameNotificationChannel("default", "Default", "Default notifications"),
    new GameNotificationChannel("rewards", "Rewards", "Daily reward reminders")
);

var notification          = service.CreateNotification();
notification.Title        = "Daily Reward Ready!";
notification.Body         = "Your daily reward is waiting for you!";
notification.DeliveryTime = DateTime.Now.AddHours(24);
notification.Channel      = "rewards";
service.ScheduleNotification(notification);
```

### Gesture Detection

```csharp
using GameLovers.MobileServices.Gestures;

// Attach GestureController MonoBehaviour to a scene GameObject
// Note: uses Unity's EnhancedTouch API; in Editor add a TouchSimulation component for mouse input

_gestureController.Swiped += swipe =>
{
    // swipe.SwipeDirection  — Up / Down / Left / Right
    // swipe.SwipeVelocity   — speed of the swipe
    // swipe.SwipeSameness   — direction consistency 0–1 (higher = cleaner)
    if (swipe.SwipeSameness > 0.8f)
        ProcessSwipe(swipe.SwipeDirection);
};

_gestureController.Tapped += tap =>
{
    // tap.Position — screen position of the tap
    Debug.Log($"Tapped at {tap.Position}");
};
```

---

## Services at a Glance

### Native UI

All methods are **static** — no initialization needed. The service is platform-gated: no-op in the Editor (logs only), throws on unsupported platforms.

| Method | Platform |
|--------|----------|
| `ShowAlertPopUp(darkMode, title, message, buttons…)` | iOS + Android |
| `ShowToastMessage(message, isLongDuration)` | Android only |
| `RequestReview()` | iOS (`SKStoreReviewController`) + Android (Play In-App Review) |

**Alert Button Styles:** `Default`, `Cancel`, `Destructive`

### Notification Service

```csharp
service.CancelNotification(pending.Id);
service.CancelAllNotifications();
var scheduled = service.GetPendingNotifications();
```

Key points:
- Android requires at least one channel; the first passed becomes the default.
- Creates a `DontDestroyOnLoad` host GameObject — teardown explicitly in tests or game reset flows.
- `OperatingMode.Queue*` defers scheduling to the OS until the app backgrounds.

### Gesture Controller

Key points:
- Powered by Unity's `EnhancedTouch` API — `EnhancedTouchSupport` is enabled/disabled automatically in `OnEnable`/`OnDisable`.
- For mouse input in Editor: add a `TouchSimulation` component.
- If `minSwipeDistance <= maxTapDrift`, an interaction may qualify as both tap and swipe — tune thresholds carefully.

**SwipeInput fields:**

| Field | Type | Description |
|---|---|---|
| `SwipeDirection` | `SwipeDirection` | Up / Down / Left / Right |
| `SwipeVelocity` | `float` | Speed of the gesture |
| `SwipeSameness` | `float` | Direction consistency 0–1 |
| `StartPosition` | `Vector2` | Screen start position |
| `EndPosition` | `Vector2` | Screen end position |

---

## Platform-Specific Notes

**iOS:** Native UI via Objective-C bridge (`Plugins/iOS/NativeUi.m`). Alert callbacks matched by button text — keep button texts unique per alert.

**Android:** Native UI via `AndroidJavaClass` reflection. Notifications require channels (Android 8.0+).

**Editor:** Alerts and toasts log to console. Notifications are logged but not scheduled. Gestures work via `TouchSimulation`.

---

## Contributing

Contributions are welcome! Report bugs or request features via [GitHub Issues](https://github.com/CoderGamester/com.gamelovers.mobileservices/issues). Include target platform (iOS/Android) and device info. For development setup, architecture, and coding standards, see [AGENTS.md](AGENTS.md).

---

## Related docs

| Document | Purpose |
|---|---|
| [AGENTS.md](AGENTS.md) | Contributor/agent guide (architecture, gotchas, workflows) |
| [CHANGELOG.md](CHANGELOG.md) | Version history |

## Support

- **Issues**: [Report bugs or request features](https://github.com/CoderGamester/com.gamelovers.mobileservices/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/CoderGamester/com.gamelovers.mobileservices/discussions)

## License

MIT — see [LICENSE.md](LICENSE.md).

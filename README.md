# GameLovers Mobile Services

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/badge/version-1.0.0-green.svg)](CHANGELOG.md)

> **Quick Links**: [Installation](#installation) | [Quick Start](#quick-start) | [Services](#services-documentation) | [Contributing](#contributing)

## Why Use This Package?

Building mobile-specific features in Unity often requires dealing with platform-specific code, native bridges, and fragmented APIs. This **Mobile Services** package consolidates essential mobile functionality into a unified, easy-to-use API:

| Problem | Solution |
|---------|----------|
| **Platform-specific UI code** | Native UI service bridges iOS/Android alerts, toasts, and review prompts with one API |
| **Notification complexity** | Notification service wraps Unity Mobile Notifications with channel management |
| **Custom gesture detection** | Gesture controller provides swipe detection with velocity, direction, and consistency metrics |
| **Input System boilerplate** | Pointer input manager abstracts touch/mouse input across platforms |
| **Editor testing challenges** | Editor fallbacks for all features enable testing without device builds |

**Built for production:** Uses Unity's official packages (`com.unity.mobile.notifications`, `com.unity.inputsystem`). Clean platform abstractions. Tested in real mobile games.

### Key Features

- **🎭 Native UI Service** - Call native OS dialogs, action sheets, and toasts without platform-specific code
- **📨 Notification Service** - Schedule, cancel, and manage local/remote notifications with channel support
- **👆 Gesture Controller** - Robust swipe detection with velocity, direction, and consistency metrics
- **📱 Platform Optimized** - Built specifically for iOS and Android with editor fallbacks
- **🔧 Input System Integration** - Modern pointer input abstraction using Unity Input System

---

## System Requirements

- **[Unity](https://unity.com/download)** 6000.0+ (Unity 6)
- **[Unity Mobile Notifications](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@latest)** (2.3.0) - Automatically resolved
- **[Unity Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)** (1.11.0) - Automatically resolved

### Compatibility Matrix

| Unity Version | Status | Notes |
|---------------|--------|-------|
| 6000.0+ (Unity 6) | ✅ Fully Tested | Primary development target |
| 2022.3 LTS | ⚠️ Untested | May require minor adaptations |

| Platform | Status | Notes |
|----------|--------|-------|
| iOS | ✅ Supported | Full feature support |
| Android | ✅ Supported | Full feature support |
| Editor | ✅ Supported | Fallbacks for testing |
| Standalone | ⚠️ Limited | Gestures only; no native UI/notifications |
| WebGL | ❌ Not Supported | Mobile-only features |

## Installation

### Via Unity Package Manager (Recommended)

1. Open Unity Package Manager (`Window` → `Package Manager`)
2. Click the `+` button and select `Add package from git URL`
3. Enter the following URL:
   ```
   https://github.com/CoderGamester/com.gamelovers.mobileservices.git
   ```

### Via manifest.json

Add the following line to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.gamelovers.mobileservices": "https://github.com/CoderGamester/com.gamelovers.mobileservices.git"
  }
}
```

---

## Package Structure

```
Runtime/
├── NativeUi/
│   └── NativeUiService.cs        # Static native UI bridge (alerts, toasts)
├── Notifications/
│   ├── MobileNotificationService.cs  # Main notification manager
│   ├── IGameNotification.cs          # Notification abstraction
│   ├── GameNotificationChannel.cs    # Channel configuration
│   ├── PendingNotification.cs        # Scheduled notification wrapper
│   ├── Android/                      # Android-specific implementation
│   ├── iOS/                          # iOS-specific implementation
│   └── Internal/                     # Platform abstraction internals
└── Gestures/
    ├── GestureController.cs      # MonoBehaviour for gesture detection
    ├── SwipeInput.cs             # Swipe data structure
    ├── ActiveGesture.cs          # Gesture state tracking
    ├── PointerInputManager.cs    # Input System abstraction
    └── Controls/                 # Input action definitions

Plugins/
└── iOS/
    └── NativeUi.m                # Objective-C native bridge
```

### Key Components

| Component | Responsibility |
|-----------|----------------|
| **NativeUiService** | Static class bridging native iOS/Android UI (alerts, action sheets, toasts) |
| **MobileNotificationService** | Notification scheduling, cancellation, and channel management |
| **IGameNotification** | Platform-agnostic notification interface |
| **GestureController** | MonoBehaviour detecting swipe gestures with configurable thresholds |
| **SwipeInput** | Data structure with swipe direction, velocity, and consistency metrics |
| **PointerInputManager** | Input System wrapper for touch/mouse pointer abstraction |

---

## Quick Start

### 1. Native UI

```csharp
using GameLovers.MobileServices.NativeUi;

// Show a simple alert
NativeUiService.ShowAlertPopUp(
    darkMode: false, 
    title: "Welcome", 
    message: "Thank you for playing!", 
    new AlertButton { Text = "OK", Style = AlertButtonStyle.Default }
);

// Show an alert with multiple buttons
NativeUiService.ShowAlertPopUp(
    darkMode: true,
    title: "Delete Save?",
    message: "This action cannot be undone.",
    new AlertButton { Text = "Cancel", Style = AlertButtonStyle.Cancel },
    new AlertButton { Text = "Delete", Style = AlertButtonStyle.Destructive, OnClick = OnDeleteConfirmed }
);

// Show a toast message (Android only)
NativeUiService.ShowToastMessage("Item Collected!", isLongDuration: false);

// Request app store review
NativeUiService.RequestReview();
```

---

### 2. Notifications

```csharp
using GameLovers.MobileServices.Notifications;

public class NotificationManager : MonoBehaviour
{
    private MobileNotificationService _notificationService;

    void Awake()
    {
        // Initialize with notification channels
        _notificationService = new MobileNotificationService(
            new GameNotificationChannel("default", "Default", "Default notifications"),
            new GameNotificationChannel("rewards", "Rewards", "Daily reward reminders")
        );
    }

    public void ScheduleDailyReward()
    {
        // Create and configure notification
        var notification = _notificationService.CreateNotification();
        notification.Title = "Daily Reward Ready!";
        notification.Body = "Your daily reward is waiting for you!";
        notification.DeliveryTime = DateTime.Now.AddHours(24);
        notification.SmallIcon = "icon_reward";
        notification.Channel = "rewards";

        // Schedule it
        _notificationService.ScheduleNotification(notification);
    }

    public void CancelAllNotifications()
    {
        _notificationService.CancelAllNotifications();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // Schedule reminder when app is backgrounded
            ScheduleDailyReward();
        }
    }
}
```

---

### 3. Gesture Detection

```csharp
using UnityEngine;
using GameLovers.MobileServices.Gestures;

public class SwipeHandler : MonoBehaviour
{
    [SerializeField] private GestureController _gestureController;

    void OnEnable()
    {
        _gestureController.Swiped += OnSwipe;
    }

    void OnDisable()
    {
        _gestureController.Swiped -= OnSwipe;
    }

    private void OnSwipe(SwipeInput swipe)
    {
        Debug.Log($"Swiped {swipe.SwipeDirection}");
        Debug.Log($"Velocity: {swipe.SwipeVelocity}");
        Debug.Log($"Sameness: {swipe.SwipeSameness}"); // Direction consistency (0-1)

        switch (swipe.SwipeDirection)
        {
            case SwipeDirection.Left:
                // Handle left swipe
                break;
            case SwipeDirection.Right:
                // Handle right swipe
                break;
            case SwipeDirection.Up:
                // Handle up swipe
                break;
            case SwipeDirection.Down:
                // Handle down swipe
                break;
        }
    }
}
```

---

## Services Documentation

### Native UI Service

Static service bridging native iOS and Android UI components.

**Key Points:**
- All methods are **static** - no initialization required
- Uses `AndroidJavaClass` for Android, `[DllImport]` for iOS
- Editor provides fallback implementations for testing

```csharp
using GameLovers.MobileServices.NativeUi;

// Alert popup with callback
NativeUiService.ShowAlertPopUp(
    darkMode: false,
    title: "Confirm Purchase",
    message: "Buy 100 gems for $0.99?",
    new AlertButton 
    { 
        Text = "Cancel", 
        Style = AlertButtonStyle.Cancel 
    },
    new AlertButton 
    { 
        Text = "Buy", 
        Style = AlertButtonStyle.Default,
        OnClick = () => ProcessPurchase()
    }
);

// Toast (Android only, no-op on iOS)
NativeUiService.ShowToastMessage("Saved!", isLongDuration: false);

// App Store / Play Store review prompt
NativeUiService.RequestReview();
```

**Alert Button Styles:**
- `Default` - Standard button appearance
- `Cancel` - Cancel/dismiss style
- `Destructive` - Red/warning style for destructive actions

---

### Notification Service

Wrapper around Unity Mobile Notifications with simplified channel and scheduling API.

**Key Points:**
- Requires channel configuration at initialization
- Creates a `DontDestroyOnLoad` host GameObject (`GameNotificationsMonoBehaviour`)
- Supports platform-specific notification features via `IGameNotification`

```csharp
using GameLovers.MobileServices.Notifications;

// Initialize with channels
var service = new MobileNotificationService(
    new GameNotificationChannel("general", "General", "General notifications"),
    new GameNotificationChannel("promo", "Promotions", "Promotional offers")
);

// Create notification
var notification = service.CreateNotification();
notification.Title = "Special Offer!";
notification.Body = "50% off all items today only!";
notification.DeliveryTime = DateTime.Now.AddMinutes(30);
notification.Channel = "promo";
notification.BadgeNumber = 1;

// Schedule
var pending = service.ScheduleNotification(notification);

// Cancel specific notification
service.CancelNotification(pending.Id);

// Cancel all
service.CancelAllNotifications();

// Get pending notifications
var scheduled = service.GetPendingNotifications();
```

**IGameNotification Properties:**
| Property | Description |
|----------|-------------|
| `Title` | Notification title text |
| `Body` | Notification body text |
| `DeliveryTime` | When to deliver (DateTime) |
| `Channel` | Channel ID for grouping |
| `SmallIcon` | Icon resource name |
| `LargeIcon` | Large icon resource name |
| `BadgeNumber` | App badge count |

---

### Gesture Controller

MonoBehaviour for detecting swipe gestures with configurable sensitivity.

**Key Points:**
- Attach to a GameObject in your scene
- Raises `Swiped` event with detailed swipe data
- Configurable via inspector or code

```csharp
using UnityEngine;
using GameLovers.MobileServices.Gestures;

public class CardSwiper : MonoBehaviour
{
    [SerializeField] private GestureController _gestureController;

    void Start()
    {
        _gestureController.Swiped += HandleSwipe;
    }

    void OnDestroy()
    {
        _gestureController.Swiped -= HandleSwipe;
    }

    private void HandleSwipe(SwipeInput swipe)
    {
        // swipe.SwipeDirection - Up, Down, Left, Right
        // swipe.SwipeVelocity  - Speed of the swipe
        // swipe.SwipeSameness  - How consistent the direction was (0-1)
        // swipe.StartPosition  - Where the swipe started
        // swipe.EndPosition    - Where the swipe ended
        
        if (swipe.SwipeSameness > 0.8f) // Clean, intentional swipe
        {
            ProcessSwipe(swipe.SwipeDirection);
        }
    }
}
```

**SwipeInput Fields:**
| Field | Type | Description |
|-------|------|-------------|
| `SwipeDirection` | `SwipeDirection` | Detected direction (Up/Down/Left/Right) |
| `SwipeVelocity` | `float` | Speed of the swipe gesture |
| `SwipeSameness` | `float` | Direction consistency (0-1, higher = cleaner swipe) |
| `StartPosition` | `Vector2` | Screen position where swipe started |
| `EndPosition` | `Vector2` | Screen position where swipe ended |

---

## Platform-Specific Notes

### iOS

- Native UI uses Objective-C bridge (`Plugins/iOS/NativeUi.m`)
- Notifications require iOS notification permissions
- Review prompts use `SKStoreReviewController`

### Android

- Native UI uses `AndroidJavaClass` reflection
- Toast messages use native Android Toast API
- Notifications support notification channels (Android 8.0+)
- Review prompts use Play In-App Review API

### Editor

- Alert popups log to console (or use Unity dialog if available)
- Toast messages log to console
- Notifications are simulated (logged but not scheduled)
- Gestures work with mouse input

---

## Contributing

We welcome contributions! Here's how you can help:

### Reporting Issues

- Use the [GitHub Issues](https://github.com/CoderGamester/com.gamelovers.mobileservices/issues) page
- Include Unity version, package version, and reproduction steps
- Specify target platform (iOS/Android) and device info
- Attach relevant code samples, error logs, or screenshots

### Development Setup

1. Fork the repository on GitHub
2. Clone your fork: `git clone https://github.com/yourusername/com.gamelovers.mobileservices.git`
3. Create a feature branch: `git checkout -b feature/amazing-feature`
4. Make your changes with tests
5. Commit: `git commit -m 'Add amazing feature'`
6. Push: `git push origin feature/amazing-feature`
7. Create a Pull Request

### Code Guidelines

- Follow C# 9.0 syntax with explicit namespaces (no global usings)
- Add XML documentation to all public APIs
- Use platform defines: `#if UNITY_IOS`, `#if UNITY_ANDROID`, `#if UNITY_EDITOR`
- Include unit tests for new features
- Runtime code must not reference `UnityEditor`
- Update CHANGELOG.md for notable changes

---

## Support

- **Issues**: [Report bugs or request features](https://github.com/CoderGamester/com.gamelovers.mobileservices/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/CoderGamester/com.gamelovers.mobileservices/discussions)
- **Changelog**: See [CHANGELOG.md](CHANGELOG.md) for version history

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.

---

**Made with ❤️ for the Unity community**

*If this package helps your project, please consider giving it a ⭐ on GitHub!*

# Mobile Services

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/badge/version-1.0.0-green.svg)](CHANGELOG.md)

## Overview

**Mobile Services** is a consolidated package providing essential platform-specific services for Unity mobile projects. It simplifies native integration for UI, notifications, and advanced touch gestures.

This package consolidates three legacy packages into a single, cohesive foundation:
- **Native UI**: Alerts, toasts, and game review prompts.
- **Notifications**: Local and remote push notification management.
- **Gestures**: Advanced swipe and drag detection (extracted from legacy inputextensions).

---

## Key Features

- **🎭 Native UI Service** - Call native OS dialogs and toasts without writing platform-specific code.
- **📨 Notification Service** - Schedule, cancel, and manage local/remote notifications with ease.
- **👆 Gesture Controller** - Robust swipe detection with velocity, direction, and consistency metrics.
- **📱 Platform Optimized** - Built specifically for iOS and Android with editor fallbacks.
- **⚡ Async Ready** - Modern C# implementation designed for high performance.

---

## Installation

### Via Unity Package Manager (UPM)

1. Open Unity Package Manager (`Window` → `Package Manager`).
2. Click the `+` button and select `Add package from git URL`.
3. Enter the following URL:
   ```
   https://github.com/CoderGamester/com.gamelovers.mobileservices.git
   ```

---

## Quick Start

### 1. Native UI
```csharp
using GameLovers.MobileServices.NativeUi;

// Show a simple alert
NativeUiService.ShowAlertPopUp(false, "Welcome", "Thank you for playing!", 
    new AlertButton { Text = "OK", Style = AlertButtonStyle.Default });

// Show a toast message (Android only)
NativeUiService.ShowToastMessage("Item Collected", false);
```

### 2. Notifications
```csharp
using GameLovers.MobileServices.Notifications;

// Initialize service with channels
var service = new MobileNotificationService(new GameNotificationChannel("default", "Default", "Default Channel"));

// Schedule a notification
var notification = service.CreateNotification();
notification.Title = "Daily Reward";
notification.Body = "Your reward is ready!";
notification.DeliveryTime = DateTime.Now.AddHours(24);
service.ScheduleNotification(notification);
```

### 3. Swipe Gestures
```csharp
using GameLovers.MobileServices.Gestures;

// Attach GestureController to a GameObject and listen to events
gestureController.Swiped += (swipe) => {
    Debug.Log($"Swiped {swipe.SwipeDirection} with velocity {swipe.SwipeVelocity}");
};
```

---

## Migration from Legacy Packages

If you are migrating from the old separate packages, please refer to [MIGRATION.md](MIGRATION.md) for detailed namespace and API mapping changes.

---

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.

---

**Made with ❤️ for the Unity community**

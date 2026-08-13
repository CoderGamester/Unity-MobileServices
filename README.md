# GameLovers Mobile Services

Unity 6 services for local notifications, native UI, haptics, permissions, App Tracking Transparency, deep links, gestures, and mobile build tooling.

[![Unity](https://img.shields.io/badge/Unity-6000.0%20%7C%206000.3%20%7C%206000.5-blue.svg)](https://unity.com/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)
[![Version](https://img.shields.io/github/v/tag/CoderGamester/Unity-MobileServices?label=version)](CHANGELOG.md)

## Scope

Use Mobile Services to isolate platform-specific behavior behind Unity-friendly APIs. It provides **local** notifications, not remote push delivery; it also does not provide connectivity or store fallback services. The package is pipeline-neutral.

## Unity compatibility

| Item | Current policy |
| --- | --- |
| Minimum Unity version | `6000.0` |
| Reference streams | `6000.0.x`, `6000.3.x`, `6000.5.x` |
| Reference editors | `6000.0.81f1`, `6000.3.21f1`, `6000.5.7f1` (primary) |
| Render pipeline | Pipeline-neutral |
| Validation status | Compatibility target; do not treat a stream as validated until the repository matrix records it. |

| Platform | Intended behavior |
| --- | --- |
| iOS / Android | Native services and build-time configuration |
| Editor | Platform simulator and no-op/mock backends where applicable |
| Standalone | Limited fallback behavior; haptics reports unsupported |
| WebGL | Not supported |

## Install and configure native projects

```json
{
  "dependencies": {
    "com.gamelovers.mobileservices": "https://github.com/CoderGamester/Unity-MobileServices.git#1.0.1"
  }
}
```

Before using permissions, notifications, ATT, or native UI:

1. Create and commit the Mobile Services settings/config asset.
2. Fill in every required usage description and capability for the platforms you ship.
3. Decide whether the package or your project owns generated native files.
4. Validate an iOS and Android build on physical devices.

Without persisted configuration, the build postprocessor has no configuration to apply; it cannot infer missing privacy keys or capabilities.

## First success

Create owners during application startup and dispose them during teardown. Scheduling a notification transfers it to the operating system; disposing its service does not cancel already-scheduled OS notifications.

```csharp
using System;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Notifications;
using Unity.Notifications.Android;

var haptics = new HapticsService();
haptics.Play(HapticPreset.Selection);

var notifications = new MobileNotificationService(
    new GameNotificationChannel("default", "Default", "General notifications"));

// Keep this owner and call Dispose when your application service is torn down.
IDisposable ownedNotifications = notifications;
```

Use the specific subsystem namespaces—`Notifications`, `Haptics`, `NativeUi`, and `Device`—rather than assuming one umbrella import exposes every type.

## Services

| Area | Provides |
| --- | --- |
| Native UI | Dismissible or blocking alerts, action sheets, toasts, review requests, and sharing |
| Notifications | Local notification channels, scheduling, and management |
| Haptics | Presets, custom output, and bounded loops |
| Device | Permissions, ATT, deep links, safe-area and device helpers |
| Gestures | Gesture controller for explicit gesture input ownership |
| Editor tooling | Device Simulator integration and build helpers |

Runtime alert calls render an interactive platform-shaped mock in the Game view even when the Device Simulator window is closed. The editor simulator is for exercising application paths; it is not a substitute for device permission, notification-delivery, review, or native-build validation.

## Sample and support

Import **Mobile Services Samples** from Package Manager. Its four scenes—Overview, Haptics, Notifications, and Links—share one sample player; use its [README](Samples~/MobileServicesSamples/README.md) for scene prerequisites and build tooling.

See [docs](docs/README.md), [CHANGELOG.md](CHANGELOG.md), and [issues](https://github.com/CoderGamester/Unity-MobileServices/issues).

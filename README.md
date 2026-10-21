# GameLovers Mobile Services

Unity 6 services for local notifications, native UI, media, haptics, permissions, App Tracking Transparency, deep links, shortcuts, game services and cloud saves, gestures, and mobile build tooling.

[![Unity](https://img.shields.io/badge/Unity-6000.0%20%7C%206000.3%20%7C%206000.5-blue.svg)](https://unity.com/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)
[![Version](https://img.shields.io/github/v/tag/CoderGamester/Unity-MobileServices?label=version)](CHANGELOG.md)

## Scope

Use Mobile Services to isolate platform-specific behavior behind Unity-friendly APIs. It provides **local** notifications, not remote push delivery; it also does not provide in-app purchases, connectivity, a JavaScript-bridged WebView, or store fallback services. Game Center and Google Play Games (sign-in, leaderboards, achievements, cloud saves) are opt-in through the Mobile Services Config. The package is pipeline-neutral.

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
    "com.gamelovers.mobileservices": "https://github.com/CoderGamester/Unity-MobileServices.git#2.0.0"
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

For safe-area UI padding, use `com.gamelovers.uiservice` (`SafeAreaPanelView`/`SafeAreaContainer`).

## Services

| Area | Provides |
| --- | --- |
| Native UI | Dismissible or blocking alerts, action sheets, toasts, review requests, and sharing (including screenshots), gallery saving, image picking, and an in-app browser |
| Notifications | Local notification channels, scheduling, custom data, and opened-notification routing |
| Haptics | Presets, custom output, and bounded loops |
| Device | Permissions, ATT, deep links, background tasks, home-screen shortcuts, store and Settings links, and device helpers |
| Game Platform | Opt-in Game Center and Google Play Games sign-in, leaderboards, achievements and slot-based cloud saves |
| Gestures | Gesture controller for explicit gesture input ownership |
| Editor tooling | Device Simulator integration, optional haptics diagnostics, and build helpers |

Runtime alert calls render an interactive platform-shaped mock in the Game view even when the Device Simulator window is closed. Prefer `ShowAlertPopUpAsync` for a selected-index result on Unity's main thread; legacy callbacks also switch through Unity's `Awaitable` scheduler. The editor simulator is for exercising application paths; it is not a substitute for device permission, notification-delivery, review, or native-build validation.

## Documentation

| Topic | Page |
| --- | --- |
| Native UI, media, sharing, in-app browser | [native-ui.md](docs/native-ui.md) |
| Notifications | [notifications.md](docs/notifications.md) |
| Haptics | [haptics.md](docs/haptics.md) |
| Gestures | [gestures.md](docs/gestures.md) |
| Device: permissions, ATT, deep links, shortcuts, background tasks | [device.md](docs/device.md) |
| Game Platform: Game Center and Play Games | [game-platform.md](docs/game-platform.md) |
| Cloud Save | [cloud-save.md](docs/cloud-save.md) |
| Build configuration: config asset, plist, manifest and Gradle changes | [build-configuration.md](docs/build-configuration.md) |
| Device Simulator panel and in-Game-view overlay | [device-simulator.md](docs/device-simulator.md) |
| Samples | [samples.md](docs/samples.md) |
| Troubleshooting | [troubleshooting.md](docs/troubleshooting.md) |

## Common issues

| Symptom | First thing to check |
| --- | --- |
| A native call does nothing in the Editor | Editor backends are simulations or no-ops. Use the Device Simulator panel, or test on a device. |
| An iOS permission request never prompts | The usage description is missing from the Mobile Services Config, so the group is not compiled in. |
| `RequestReview()` does nothing on Android | The Play review dependency is turned off or missing from the generated Gradle project. |
| An Android notification never shows | Register at least one `GameNotificationChannel` in the `MobileNotificationService` constructor. |

The full symptom-to-fix list is in [troubleshooting.md](docs/troubleshooting.md).

## Samples and support

Import **Mobile Services Samples** from Package Manager. Its four scenes—Overview, Haptics, Notifications, and Links—share one sample player; use its [README](Samples~/MobileServicesSamples/README.md) for scene prerequisites and build tooling.

See [CHANGELOG.md](CHANGELOG.md) and [issues](https://github.com/CoderGamester/Unity-MobileServices/issues).

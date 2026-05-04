# GameLovers Mobile Services

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/github/v/tag/CoderGamester/com.gamelovers.mobileservices?label=version)](CHANGELOG.md)

> **Quick Links**: [Installation](#installation) | [Quick Start](#quick-start) | [Services](#services-at-a-glance) | [Contributing](#contributing)

## Why Use This Package?

Building mobile-specific features in Unity often requires dealing with platform-specific code, native bridges, and fragmented APIs. This **Mobile Services** package consolidates essential mobile functionality into a unified, easy-to-use API:

| Problem | Solution |
|---------|----------|
| **Platform-specific UI code** | Native UI service bridges iOS/Android alerts, toasts, review prompts, and share sheets with one API |
| **Notification complexity** | Notification service wraps Unity Mobile Notifications with channel management |
| **Custom gesture detection** | Gesture controller provides swipe and tap detection via Unity's EnhancedTouch |
| **Haptic plugin sprawl** | Zero-dependency `IHapticsService` with 9 presets, custom intensity, and time-bounded looping — built directly on iOS/Android primitives |
| **Scattered device APIs** | One `IDeviceService` umbrella over `SafeArea`, `ScreenWake`, `Battery`, `Connectivity`, `AudioSession`, `Permissions`, `Att`, `DeepLink` — each child also independently mockable |
| **iOS silent switch muting audio** | `device.AudioSession.ConfigureForPlayback()` overrides `AVAudioSession` category in one line |
| **iOS App Tracking Transparency** | `device.Att.RequestAuthorizationAsync()` — direct `ATTrackingManager` bridge, no `com.unity.ads.ios-support` dependency |
| **Cold-start deep link loss** | `device.DeepLink` queues the launch link for the first subscriber so you never miss it |
| **Editor testing challenges** | Editor fallbacks for all features enable testing without device builds |

**Built for production:** Uses Unity's official packages (`com.unity.mobile.notifications`, `com.unity.inputsystem`). Tested in real mobile games.

---

## System Requirements

- **[Unity](https://unity.com/download)** 6000.0+ (Unity 6)
- **[Unity Mobile Notifications](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@latest)** (2.3.0) — automatically resolved
- **[Unity Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)** (1.11.0) — automatically resolved

| Platform | Status |
|---|---|
| iOS | ✅ Fully supported |
| Android | ✅ Fully supported |
| Editor | ✅ Supported (no-op fallbacks for all native services) |
| Standalone | ⚠️ Gestures + Connectivity + SafeArea + Battery (level/status); Haptics returns `IsSupported = false`; iOS audio session / ATT are no-ops |
| WebGL | ❌ Not supported |

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
| **IIosAudioSessionService** | Overrides the iOS silent switch so audio keeps playing (no-op elsewhere) |
| **IHapticsService** | Cross-platform haptic feedback with 9 presets, custom intensity, time-bounded looping. Zero third-party deps. |
| **IDeviceService** | Umbrella facade exposing `SafeArea`, `ScreenWake`, `Battery`, `Connectivity`, `AudioSession`, `Permissions`, `Att`, `DeepLink` |
| **IPermissionsService** | Unified iOS+Android runtime permissions (Camera, Mic, Location, Photos, Notifications) — Task-based async |
| **IAttService** | iOS App Tracking Transparency. Built directly on `ATTrackingManager` — no `com.unity.ads.ios-support` dep |
| **IDeepLinkService** | `Application.deepLinkActivated` wrapper with cold-start link queueing |
| **ISafeAreaService** | `Screen.safeArea` with change events; pairs with `SafeAreaContainer` UI Toolkit element |
| **IBatteryService** | Battery level/status + iOS/Android low-power-mode awareness with events |
| **IConnectivityService** | `Application.internetReachability` with change events |
| **IScreenWakeService** | `KeepAwake` toggle over `Screen.sleepTimeout` |

---

## Quick Start

### Native UI

```csharp
using GameLovers.MobileServices.NativeUi;

NativeUiService.ShowAlertPopUp(
    isAlertSheet: false,
    title: "Delete Save?",
    message: "This action cannot be undone.",
    new AlertButton { Text = "Cancel", Style = AlertButtonStyle.Cancel },
    new AlertButton { Text = "Delete", Style = AlertButtonStyle.Destructive, Callback = OnDeleteConfirmed }
);

NativeUiService.ShowToastMessage("Item Collected!", isLongDuration: false);

// OS-mediated rating prompt (no-op in Editor; iOS SKStoreReviewController + Android Play In-App Review).
NativeUiService.RequestReview();

// OS share sheet. Pass any combination of text/url/imagePath; nulls are skipped.
NativeUiService.Share(text: "Check out my high score!", url: "https://example.com/game");
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

### iOS Audio Session

```csharp
using GameLovers.MobileServices.Device;

var audio = new IosAudioSessionService();
audio.ConfigureForPlayback(); // Call once at startup. No-op on Android / Editor.
```

### Device Services (umbrella)

```csharp
using GameLovers.MobileServices.Device;

IDeviceService device = new DeviceService();

// Battery + low-power mode.
device.Battery.OnLowPowerModeChanged += () =>
    Debug.Log($"LPM changed -> {device.Battery.IsLowPowerMode}");

// Connectivity events.
device.Connectivity.OnStatusChanged += status =>
    Debug.Log($"Reachability changed -> {status}");

// Safe area for UI Toolkit.
var safeAreaContainer = new SafeAreaContainer(device.SafeArea);
rootVisualElement.Add(safeAreaContainer);

// Keep the screen awake during gameplay.
device.ScreenWake.KeepAwake = true;

// Override iOS silent switch.
device.AudioSession.ConfigureForPlayback();

// Runtime permissions (Task-based; no UniTask dependency).
var camera = await device.Permissions.RequestAsync(AppPermission.Camera);
if (camera == PermissionStatus.Granted) { /* … */ }

// App Tracking Transparency (iOS 14.5+; returns Authorized on Android/Editor).
var att = await device.Att.RequestAuthorizationAsync();

// Deep links — cold-start safe; subscribe whenever, never miss a launch link.
device.DeepLink.OnLinkActivated += uri => Debug.Log($"Deep link: {uri}");
```

Each child interface is also independently registerable for tests, so you can mock `IBatteryService` directly without going through the facade.

### Haptics

```csharp
using GameLovers.MobileServices.Haptics;

IHapticsService haptics = new HapticsService();

// Natural one-shot for the preset's built-in duration.
haptics.PlayPreset(HapticPreset.Success);

// Loop indefinitely until you call StopCurrentHaptic().
haptics.PlayPresetDuration(HapticPreset.ImpactMedium, duration: -1f);
// ... later ...
haptics.StopCurrentHaptic();

// Loop and auto-stop after 0.5 seconds.
haptics.PlayPresetDuration(HapticPreset.ImpactHeavy, duration: 0.5f);

// Custom intensity (0..1) with explicit duration in milliseconds.
haptics.PlayCustom(intensity01: 0.7f, durationMs: 250f);

// Master toggle. Setting Enabled=false also stops any active haptic.
haptics.Enabled = false;
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
| `ShowAlertPopUp(isAlertSheet, title, message, buttons…)` | iOS + Android |
| `ShowToastMessage(message, isLongDuration)` | iOS + Android |
| `RequestReview()` | iOS (`SKStoreReviewController`) + Android (Play In-App Review) |
| `Share(text, url, imagePath, title)` | iOS (`UIActivityViewController`) + Android (`Intent.ACTION_SEND`) |

**Alert Button Styles:** `Default`, `Cancel`, `Destructive`

> **Android `RequestReview()`** requires the Play Core Review library. Add to `mainTemplate.gradle`:
> `implementation 'com.google.android.play:review:2.0.1'`

### Notification Service

```csharp
service.CancelNotification(pending.Id);
service.CancelAllScheduledNotifications();
var scheduled = service.PendingNotifications;
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

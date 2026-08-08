# GameLovers Mobile Services

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%20%7C%206000.3%20%7C%206000.5-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/github/v/tag/CoderGamester/com.gamelovers.mobileservices?label=version)](CHANGELOG.md)

> **Quick Links**: [Installation](#installation) | [Quick Start](#quick-start) | [Services](#services-at-a-glance) | [Samples](#samples) | [Related docs](#related-docs) | [Contributing](#contributing)

## Why Use This Package?

Building mobile-specific features in Unity often requires dealing with platform-specific code, native bridges, and fragmented APIs. This **Mobile Services** package consolidates essential mobile functionality into a unified, easy-to-use API:

| Problem | Solution |
|---------|----------|
| **Platform-specific UI code** | Native UI service bridges iOS/Android alerts, toasts, review prompts, and share sheets with one API |
| **Notification complexity** | Notification service wraps Unity Mobile Notifications with channel management + a fluent `service.Schedule().In(...).Title(...).Send()` builder |
| **Custom gesture detection** | Gesture controller provides swipe and tap detection via Unity's EnhancedTouch |
| **Haptic plugin sprawl** | Zero-dependency `IHapticsService` with 9 presets, custom intensity, and time-bounded looping — built directly on iOS/Android primitives |
| **Scattered device APIs** | One `IDeviceService` umbrella over `SafeArea`, `ScreenWake`, `Battery`, `AudioSession`, `Permissions`, `Att`, `DeepLink` — each child also independently mockable |
| **Deep-link routing boilerplate** | `IDeepLinkRouter.MapRoute("/promo/:id", handler)` over `IDeepLinkService` |
| **iOS silent switch muting audio** | `device.AudioSession.ConfigureForPlayback()` overrides `AVAudioSession` category in one line |
| **iOS App Tracking Transparency** | `device.Att.RequestAuthorizationAsync()` — direct `ATTrackingManager` bridge, no `com.unity.ads.ios-support` dependency |
| **Cold-start deep link loss** | `device.DeepLink` queues the launch link for the first subscriber so you never miss it |
| **Forgotten `Info.plist` keys → App Store rejection** | Mobile Services Config asset + build postprocessor auto-inject `NS*UsageDescription` keys (localized per device language), entitlements, and Android manifest entries; fail-fast validation lists every missing key |
| **Editor testing challenges** | A Device Simulator plugin panel paints platform-shaped mocks inside the simulated phone (edit + play) with live diagnostics; `EditorPlatformSimulator` drives state for unit tests |

**Built for production:** Uses Unity's official Mobile Notifications and Input System packages. Tested in real mobile games.

---

## System Requirements

- **[Unity](https://unity.com/download)** Unity 6 only. The supported validation matrix is:

  | Stream | Exact validation editor |
  |---|---|
  | Primary 6000.5.x | 6000.5.7f1 |
  | Supported 6000.3.x | 6000.3.21f1 |
  | Supported 6000.0.x | 6000.0.81f1 |

  Other Unity versions are unsupported/untested.
- **[Unity Mobile Notifications](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@latest)** (2.3.0) — automatically resolved
- **[Unity Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)** (1.11.0) — automatically resolved; set **Active Input Handling** to **Input System Package (New)** or **Both** so Unity 6 routes the sample's UI Toolkit input through InputForUI, without a uGUI dependency

| Platform | Status |
|---|---|
| iOS | ✅ Fully supported |
| Android | ✅ Fully supported |
| Editor | ✅ Supported (no-op fallbacks + truth-mirror simulator) |
| Standalone | ⚠️ Gestures + SafeArea + Battery (level/status); Haptics returns `IsSupported = false`; iOS audio session / ATT are no-ops |
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

## Quick Start

### Native UI

```csharp
using GameLovers.MobileServices.NativeUi;

NativeUiService.ShowAlertPopUp(
    isAlertSheet: false,
    title: "Delete Save?",
    message: "This action cannot be undone.",
    new AlertButton { Text = "Cancel", Style = AlertButtonStyle.Cancel },
    new AlertButton { Text = "Delete", Style = AlertButtonStyle.Destructive, Callback = OnDeleteConfirmed });

NativeUiService.ShowToastMessage("Item Collected!", isLongDuration: false);
NativeUiService.RequestReview(); // Android Play Review dependency is auto-injected at build time
NativeUiService.Share(text: "Check out my high score!", url: "https://example.com/game");
```

### Notifications

```csharp
using GameLovers.MobileServices.Notifications;

var service = new MobileNotificationService(
    new GameNotificationChannel("default", "Default", "Default notifications"),
    new GameNotificationChannel("rewards", "Rewards", "Daily reward reminders"));

service.Schedule()
    .In(TimeSpan.FromHours(24))
    .Title("Daily Reward Ready!")
    .Body("Your daily reward is waiting for you!")
    .Channel("rewards")
    .BadgeIncrement()
    .Send();
```

### Device

```csharp
using GameLovers.MobileServices.Device;

IDeviceService device = new DeviceService();

device.Battery.OnLowPowerModeChanged += () => Debug.Log($"LPM -> {device.Battery.IsLowPowerMode}");
device.ScreenWake.KeepAwake = true;
device.AudioSession.ConfigureForPlayback();

var perms = await device.Permissions.RequestAsync(AppPermission.Camera, AppPermission.Microphone);
if (perms[AppPermission.Camera] == PermissionStatus.Granted) { /* … */ }

var att = await device.Att.RequestAuthorizationAsync();

device.DeepLink.OnLinkActivated += uri => Debug.Log($"Deep link: {uri}");

// Or with the router:
var router = new DeepLinkRouter(device.DeepLink, routes =>
{
    routes.MapRoute("/promo/:id", (uri, p) => OpenPromo(p["id"]));
});
```

### Haptics

```csharp
using GameLovers.MobileServices.Haptics;

IHapticsService haptics = new HapticsService();
haptics.PlayPreset(HapticPreset.Success);
haptics.PlayPresetDuration(HapticPreset.ImpactHeavy, duration: 0.5f);  // auto-stop after 0.5s
haptics.PlayCustom(intensity01: 0.7f, durationMs: 250f);
haptics.StopCurrentHaptic();
```

### Umbrella facade

```csharp
using GameLovers.MobileServices;

IMobileService mobile = new MobileService();          // bind once
mobile.NativeUi.ShowToastMessage("hi", false);
mobile.Notifications.Schedule().In(TimeSpan.FromHours(1)).Title("x").Send();
mobile.Haptics.PlayPreset(HapticPreset.Selection);
var camera = await mobile.Device.Permissions.RequestAsync(AppPermission.Camera);
```

---

## Services at a Glance

| Service | Purpose |
|---------|---------|
| `NativeUiService` (static) + `INativeUiService` (instance) | Alerts, sheets, toasts, review, share |
| `INotificationService` / `MobileNotificationService` | Local + remote notifications with channel registration, fluent `Schedule()` builder, and 4 `OperatingMode`s |
| `GestureController` | EnhancedTouch swipe + tap detection |
| `IHapticsService` / `HapticsService` | 9 cross-platform presets + custom intensity + time-bounded looping |
| `IDeviceService` / `DeviceService` | Umbrella over `SafeArea`, `ScreenWake`, `Battery`, `AudioSession`, `Permissions`, `Att`, `DeepLink` |
| `IDeepLinkRouter` / `DeepLinkRouter` | Path-pattern routing over `IDeepLinkService` |
| `IMobileService` / `MobileService` | Package-wide umbrella facade (NativeUi / Notifications / Haptics / Device) |
| `SafeAreaContainer` | UI Toolkit `VisualElement` that pads itself to the safe area |

For full per-subsystem API reference, see [`docs/`](docs/README.md).

---

## Editor tooling

Runtime simulation and diagnostics live inside Unity's Device Simulator:

- **`Window > General > Device Simulator`** — a **Mobile Services** panel appears automatically in the Control Panel. It bundles the controls (alerts / toasts / share / haptics / notifications / gestures / permissions / ATT / app review), live-state diagnostics, and a per-preset haptic envelope graph. Firing a mock paints it **inside the simulated phone screen** at the right scale and safe area, in **edit and play mode** — no second window, no platform toggle to keep in sync (the skin auto-syncs from the selected device profile).
- **Notification scheduler connection** — when the `NotificationsScheduler` sample is active in Play Mode, the panel's **Deliver next pending** action and due-time poll drive that sample's own service and paint its exact notification payload; without an active sample, **Show heads-up banner** remains a generic editor preview.
- **`EditorPlatformSimulator`** — static API for driving device / permission / ATT / deep-link state from edit-mode tests and scripted automation.

Plus a **Mobile Services Config** asset (open via **`Tools > GameLovers > Mobile Services > Select Mobile Services Config`**) for per-permission localized usage descriptions, capability toggles, and the auto-injection build postprocessor.

See [`docs/explorer.md`](docs/explorer.md) and [`docs/build-pipeline.md`](docs/build-pipeline.md) for the full guide.

---

## Samples

Import the single **Mobile Services Samples** bundle from `Window > Package Manager > Mobile Services > Samples`. It is one sample with four ready-to-open UI Toolkit views. The imported bundle's single [sample README](Samples~/MobileServicesSamples/README.md) documents their shared setup and view-specific workflows.

| Sample | Purpose |
|--------|---------|
| [Overview](Samples~/MobileServicesSamples/README.md#overview) | Native UI, permissions, device state, ATT, gestures, and safe-area wiring. |
| [Haptics](Samples~/MobileServicesSamples/README.md#haptics) | Designer iteration tool with sequence recorder + replay. |
| [Notifications](Samples~/MobileServicesSamples/README.md#notifications) | Fixed-channel scheduling/cancellation and `OperatingMode` lifecycle demo. |
| [Links](Samples~/MobileServicesSamples/README.md#links) | Route-pattern, raw-link, and cold-start replay demo. |

Each scene opens directly from the Project window and remains independently playable. In the combined player, persistent bottom tabs navigate between **Overview**, **Haptics**, **Notifications**, and **Links**; a received OS deep link opens the Links tab automatically. Enter Play Mode before using buttons, fields, scrolling, or navigation in the Game/Simulator view; Unity 6 InputForUI routes input to UI Toolkit while the shared navigation supplies the gesture bridge, so no GameObject wiring is required. Every enabled button has visual hover/press/focus feedback and emits one `Selection` haptic when its click commits; a drag that crosses the scroll threshold cancels the button and scrolls instead. Sample ScrollViews are clamped, drag smoothly from content or controls (with a gesture fallback for simulator touch streams), and keep the bottom navigation pinned. Status cards use one `Field: Value` per line.

The only supported player output contains all four scenes, starting on Overview. Choose **Tools > Mobile Samples Examples > Build All** to install that exact scene list and open Unity's native Build Profiles window. Choose **Restore All** in the same menu to restore the prior scene configuration during the current Unity session. These sample-owned menu items and their editor bridge are never included in player builds. See [`docs/samples.md`](docs/samples.md) for setup details.

---

## Related docs

| Document | Purpose |
|---|---|
| [docs/README.md](docs/README.md) | Full API reference index |
| [docs/native-ui.md](docs/native-ui.md) | Native UI deep dive |
| [docs/notifications.md](docs/notifications.md) | Notifications deep dive (channels, modes, builder, persistence) |
| [docs/haptics.md](docs/haptics.md) | Haptics deep dive (presets, envelope, looping, backends) |
| [docs/gestures.md](docs/gestures.md) | Gesture detection deep dive |
| [docs/device.md](docs/device.md) | Device umbrella + 8 children + DeepLinkRouter |
| [docs/explorer.md](docs/explorer.md) | Device Simulator panel & in-Game-view simulator overlay |
| [docs/build-pipeline.md](docs/build-pipeline.md) | Project Settings + build postprocessor (and manual fallback) |
| [docs/samples.md](docs/samples.md) | Samples index |
| [docs/troubleshooting.md](docs/troubleshooting.md) | Symptom-to-fix table |
| [docs/superpowers/README.md](docs/superpowers/README.md) | Approved design records and implementation plans |
| [AGENTS.md](AGENTS.md) | Contributor/agent guide (architecture, gotchas, workflows) |
| [CHANGELOG.md](CHANGELOG.md) | Version history |

## Contributing

Contributions are welcome! Report bugs or request features via [GitHub Issues](https://github.com/CoderGamester/com.gamelovers.mobileservices/issues). Include target platform (iOS/Android) and device info. For development setup, architecture, and coding standards, see [AGENTS.md](AGENTS.md).

## Support

- **Issues**: [Report bugs or request features](https://github.com/CoderGamester/com.gamelovers.mobileservices/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/CoderGamester/com.gamelovers.mobileservices/discussions)

## License

MIT — see [LICENSE.md](LICENSE.md).

# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-05-05

### Added
- Initial release of consolidated **Mobile Services** package.
- **Native UI**: Alerts, sheets, and toasts for iOS/Android, Review request and Share button with different networks.
- **Notifications**: Comprehensive local and remote notification management.
- **Gestures**: Advanced swipe detection with velocity and consistency tracking.
- **iOS Audio Session**: Override the iOS silent switch so audio keeps playing.
- **Haptics**: Zero-dependency cross-platform haptic feedback with 9 presets, custom intensity, and time-bounded looping.
- **Device**: Umbrella facade over `SafeArea`, `ScreenWake`, `Battery` (with LPM awareness), `AudioSession`, `Permissions`, `Att`, and `DeepLink` sub-services.
- **Permissions**: Unified iOS+Android runtime permissions (Camera, Microphone, Location, Photo Library, Notifications) with `Task`-based async and a multi-permission `RequestAsync(params AppPermission[])` overload.
- **App Tracking Transparency**: iOS 14.5+ `ATTrackingManager` bridge with zero dependency on `com.unity.ads.ios-support`.
- **Deep Links**: `Application.deepLinkActivated` wrapper with cold-start link queueing for the first subscriber.
- **Deep Link Router**: Path-pattern routing over `IDeepLinkService` with captured params (`/promo/:id`).
- **Notification Builder**: Fluent API — `service.Schedule().In(...).Title(...).Body(...).Channel(...).Send()`.
- **`INotificationService.Mode`**: runtime-settable `OperatingMode` on the service. Previously the queueing modes (`Queue` / `ClearOnForegrounding` / `RescheduleAfterClearing`) — and the `OnLocalNotificationExpiredEvent` that only fires in queue mode — were unreachable through the public API (the host's mode field was never exposed); they are now drivable directly via `service.Mode`.
- **Mobile Service umbrella**: Single DI registration exposing `NativeUi` / `Notifications` / `Haptics` / `Device`.
- **Native UI instance interface**: `INativeUiService` + `NativeUiServiceInstance` forwarder for mockable consumer code.
- **Device Simulator Plugin**: the single Mobile Services editor surface — a `UnityEditor.DeviceSimulation.DeviceSimulatorPlugin` subclass that embeds controls + live diagnostics + a per-preset haptic envelope graph inside Unity's Device Simulator window (Window > General > Device Simulator). Drives the in-Game-view simulator overlay so mocks render right inside the simulated phone screen, and auto-syncs the platform skin from the selected device profile.
- **Runtime Simulator Overlay**: editor-only `UIDocument` overlay that paints the truth-mirror mocks (alerts, sheets, toasts, share, review, permission / ATT dialogs, heads-up banners) inside Unity's Game / Simulator view at the simulated device's pixel grid. Alive in **edit and play mode** whenever the Device Simulator panel is open, so you can preview a mock without entering play mode; also spawns on its own during play mode via the opt-in `Project Settings > GameLovers > Mobile Services > Editor tooling > Enable runtime simulator overlay`. Composes with Unity's Device Simulator for correct safe-area / scale / `Application.platform` spoofing.
- **Device Simulator master switch**: an `Editor Simulator` toggle in the panel header (state in `MobileSimulatorState.Enabled`, persisted to `EditorPrefs`) enables/disables every section as a group and shows/hides the in-Game-view `[EDITOR SIMULATOR]` banner; turning it off clears any visible mock. Per-section dismiss buttons replace the former global one — `Dismiss all UIs` in Native UI and `Dismiss Banner` in Notifications.
- **Editor Platform Simulator**: Static API for driving device / permission / ATT / deep-link state in editor tests and the Device Simulator panel.
- **Project Settings panel**: Per-permission usage descriptions, capability toggles, project scan, and an iOS Privacy Nutrition Label draft generator.
- **Build Postprocessor**: Fail-by-default validation that injects `Info.plist`, `.entitlements`, and Android `mainTemplate.xml` entries on iOS / Android builds.
- **Samples**: Four code-only samples — `MobileServicesPlayground`, `HapticsPalette`, `NotificationsScheduler`, `DeepLinkRouter`.
- **Docs**: Per-subsystem deep-dive references under `docs/` plus editor-tooling guides for the Device Simulator panel and build pipeline.

### Changed
- Refactored all namespaces to `GameLovers.MobileServices.*`.
- Updated assembly definition to `GameLovers.MobileServices`.
- Updated dependencies to target Unity 6 (6000.0+).
- Legacy tap detection (replaced by Unity Input System's `TapInteraction`).
- Gamepad input management (out of scope for mobile services), use the new input system configuration for that

### Migration
This package consolidates three previously separate packages:
- `com.gamelovers.nativeui` (v0.2.5) -> `GameLovers.MobileServices.NativeUi`
- `com.gamelovers.notificationservice` (v0.1.7) -> `GameLovers.MobileServices.Notifications`
- `com.gamelovers.inputextensions` (v0.1.0-preview.4, swipe detection only) -> `GameLovers.MobileServices.Gestures`
- `AlertButtonStyle.Positive` -> `AlertButtonStyle.Destructive`; `AlertButtonStyle.Negative` -> `AlertButtonStyle.Cancel`. Underlying iOS/Android platform mapping is unchanged; pure rename for iOS-native vocabulary.

## [0.2.5] - 2021-01-15

**Fixed**:
- Fixed crash when showing Alert buttons on the editor

## [0.2.4] - 2020-09-24

**Fixed**:
- Fixed compiler warning for not using native code

## [0.2.3] - 2020-08-12

**Fixed**:
- Fixed build errors

## [0.2.2] - 2020-08-12

**Fixed**:
- Fixed UI working on the editor

## [0.2.1] - 2020-08-03

**Fixed**:
- Fixed build error

## [0.2.0] - 2020-08-02

**Changed**:
- Removed the show rate the game pop up. From now one use the Unity direct message or Google Play package

## [0.1.4] - 2020-08-02

**Fixed**:
- Package now working properly on Android

## [0.1.3] - 2020-08-02

**Fixed**:
- Package now working properly on Android

## [0.1.2] - 2020-08-02

**Fixed**:
- Package now working properly on Android

## [0.1.1] - 2020-07-31

**Fixed**:
- Package now working properly on iOS

## [0.1.0] - 2020-07-30

- Initial submission for package distribution


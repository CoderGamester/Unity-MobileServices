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
- **Device**: Umbrella facade over `SafeArea`, `ScreenWake`, `Battery` (with LPM awareness), `Connectivity`, `AudioSession`, `Permissions`, `Att`, and `DeepLink` sub-services.
- **Permissions**: Unified iOS+Android runtime permissions (Camera, Microphone, Location, Photo Library, Notifications) with `Task`-based async and a multi-permission `RequestAsync(params AppPermission[])` overload.
- **App Tracking Transparency**: iOS 14.5+ `ATTrackingManager` bridge with zero dependency on `com.unity.ads.ios-support`.
- **Deep Links**: `Application.deepLinkActivated` wrapper with cold-start link queueing for the first subscriber.
- **Deep Link Router**: Path-pattern routing over `IDeepLinkService` with captured params (`/promo/:id`).
- **Notification Builder**: Fluent API — `service.Schedule().In(...).Title(...).Body(...).Channel(...).Send()`.
- **Mobile Service umbrella**: Single DI registration exposing `NativeUi` / `Notifications` / `Haptics` / `Device`.
- **Native UI instance interface**: `INativeUiService` + `NativeUiServiceInstance` forwarder for mockable consumer code.
- **Mobile Services Explorer**: Dockable editor window with eight tabs and a per-platform haptic envelope graph.
- **Mobile Simulator window**: Truth-mirror that paints platform-shaped mocks (iOS / Android) of every native UI surface the package can trigger.
- **Runtime Simulator Overlay**: Play-mode-only `UIDocument` overlay (opt-in via `Project Settings > GameLovers > Mobile Services > Editor tooling > Enable runtime simulator overlay`) rendering the truth-mirror mocks inside Unity's Game / Simulator view at the simulated device's pixel grid. Composes with Unity's Device Simulator for correct safe-area / scale / `Application.platform` spoofing.
- **Device Simulator Plugin**: `UnityEditor.DeviceSimulation.DeviceSimulatorPlugin` subclass that embeds a slim Mobile Services control panel inside Unity's Device Simulator window. 
- **Editor Platform Simulator**: Static API for driving device / permission / ATT / deep-link state in editor tests and the Explorer.
- **Project Settings panel**: Per-permission usage descriptions, capability toggles, project scan, and an iOS Privacy Nutrition Label draft generator.
- **Build Postprocessor**: Fail-by-default validation that injects `Info.plist`, `.entitlements`, and Android `mainTemplate.xml` entries on iOS / Android builds.
- **Samples**: Four code-only samples — `MobileServicesPlayground`, `HapticsPalette`, `NotificationsScheduler`, `DeepLinkRouter`.
- **Docs**: Per-subsystem deep-dive references under `docs/` plus editor-tooling guides for the Explorer and build pipeline.

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


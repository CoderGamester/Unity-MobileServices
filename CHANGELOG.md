# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2026-07-04

### Fixed
- `SerializableNotification` now persists nullable fields (`Id`, `BadgeNumber`, `DeliveryTime`) via explicit has-value flags so background-rescheduled notifications no longer lose their delivery time.
- `GameNotificationsMonoBehaviour` foreground reschedule reads delivery times through `GetDeliveryTime()`.

### Changed
- `SafeAreaContainer` migrates to Unity 6 `[UxmlElement]` codegen (replaces hand-rolled `UxmlFactory`).
- Device Simulator gesture diagnostics uses `FindAnyObjectByType` (Unity 6 API).

## [1.0.0] - 2026-06-21

### Added
- Initial release of consolidated **Mobile Services** package.
- **Native UI**: Alerts, action sheets, and toasts for iOS/Android, plus OS review request and a share sheet (text / URL / image).
- **Notifications**: Local notification scheduling and management (channels, queueing, fluent `Schedule().In(...).Title(...).Body(...).Channel(...).Send()` builder).
- **Gestures**: Advanced swipe detection with velocity and consistency tracking.
- **iOS Audio Session**: Override the iOS silent switch so audio keeps playing.
- **Haptics**: Zero-dependency cross-platform haptic feedback with 9 presets, custom intensity, and time-bounded looping.
- **Device**: Umbrella facade over `SafeArea`, `ScreenWake`, `Battery` (with LPM awareness), `AudioSession`, `Permissions`, `Att`, and `DeepLink` sub-services.
- **Permissions**: Unified iOS+Android runtime permissions (Camera, Microphone, Location, Photo Library, Notifications) with `Task`-based async and a multi-permission `RequestAsync(params AppPermission[])` overload.
- **App Tracking Transparency**: iOS 14.5+ `ATTrackingManager` bridge with zero dependency on `com.unity.ads.ios-support`.
- **Deep Links**: `Application.deepLinkActivated` wrapper with cold-start link queueing for the first subscriber.
- **Deep Link Router**: Path-pattern routing over `IDeepLinkService` with captured params (`/promo/:id`).
- **Mobile Service umbrella**: `IMobileService` — single DI registration aggregating `NativeUi` / `Notifications` / `Haptics` / `Device`.
- **Native UI instance interface**: `INativeUiService` + `NativeUiServiceInstance` forwarder for mockable consumer code.
- **Device Simulator Plugin**: A `DeviceSimulatorPlugin` embedded in Unity's Device Simulator window that drives platform-shaped native-UI mocks into the simulated phone screen (edit + play), with live diagnostics and a per-preset haptic envelope graph.
- **Mobile Services Config asset**: Editor `ScriptableObject` (open via `Tools > GameLovers > Mobile Services > Select Mobile Services Config`) for per-permission **localized** usage descriptions, capability toggles, Android manifest opt-ins, and the Play In-App Review Gradle auto-injection.
- **Build Postprocessor**: Auto-injects iOS `Info.plist` usage descriptions (+ per-locale `<locale>.lproj/InfoPlist.strings` for device-language localization), entitlements, Android manifest entries, and the Play In-App Review Gradle dependency; fail-fast validation lists every missing key.
- **Samples**: Four code-only samples — `MobileServicesPlayground`, `HapticsPalette`, `NotificationsScheduler`, `DeepLinkRouter`.
- **Docs**: Per-subsystem deep-dive references under `docs/` plus editor-tooling guides for the Device Simulator panel and build pipeline.

### Changed
- Refactored all namespaces to `GameLovers.MobileServices.*`.
- Updated assembly definition to `GameLovers.MobileServices`.
- Updated dependencies to target Unity 6 (6000.0+).

### Removed
- Removed legacy tap detection — use Unity Input System's `TapInteraction`.
- Removed gamepad input management — out of scope for mobile services; configure it via the Input System directly.

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


# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

**Changed**:
- Declared Unity 6000.0 as the package minimum and documented 6000.0.x, 6000.3.x, and 6000.5.x as compatibility reference streams.
- Moved the keep-awake convenience API to the static `DeviceService.KeepAwake` property and removed the redundant `IScreenWakeService` / `ScreenWakeService` child service.

## [1.0.1] - 2026-08-12

**Fixed**:
- Starting a new preset or custom haptic now stops any active output before playback begins.

## [1.0.0] - 2026-08-10

**New**:
- Initial release of the consolidated **Mobile Services** package.
- **Native UI**: Alerts, action sheets, and toasts for iOS and Android, plus OS review requests and share sheets for text, URLs, and images.
- **Notifications**: Local notification scheduling and management with channels, queueing, and the fluent `Schedule().In(...).Title(...).Body(...).Channel(...).Send()` builder.
- **Gestures**: Enhanced Touch swipe and tap detection with velocity and consistency tracking.
- **iOS Audio Session**: Override the iOS silent switch so audio can keep playing.
- **Haptics**: Zero-dependency cross-platform haptic feedback with nine presets, custom intensity, and time-bounded looping.
- **Device**: The `Device` umbrella service exposes safe area, screen wake, battery and low-power mode, iOS audio session, permissions, App Tracking Transparency, and deep links.
- **Permissions**: Unified iOS and Android runtime permissions for Camera, Microphone, Location, Photo Library, Notifications, and multi-permission async requests.
- **App Tracking Transparency**: An iOS 14.5+ `ATTrackingManager` bridge with no dependency on `com.unity.ads.ios-support`.
- **Deep Links**: An `Application.deepLinkActivated` wrapper with cold-start link queueing for the first subscriber. Added typed deep-link configuration with semantic deduplication, explicit persisted/effective-config resolution, fail-fast malformed enabled settings, warning-only scanner mismatches, and no-op behavior when no persisted config or temporary context exists.
- **Deep Link Router**: Route patterns over `IDeepLinkService` with captured parameters such as `/promo/:id`.
- **Mobile Service umbrella**: `IMobileService` provides one DI registration for Native UI, Notifications, Haptics, and Device services.
- **Native UI instance interface**: `INativeUiService` and `NativeUiServiceInstance` support mockable consumer code.
- **Device Simulator Plugin**: An embedded Unity Device Simulator panel provides platform-shaped native UI mocks, live diagnostics, and a per-preset haptic envelope graph.
- **Mobile Services Config asset**: Configure localized permission descriptions, capability toggles, Android manifest opt-ins, and Play In-App Review Gradle setup from `Tools > GameLovers > Mobile Services > Select Mobile Services Config`.
- **Build Postprocessor**: Automatically inject iOS usage descriptions and entitlements, Android manifest entries, and the Play In-App Review Gradle dependency, with validation for missing configuration.
- **Samples**: Added one importable **Mobile Services Samples** bundle containing independently playable Playground, Haptics Palette, Notifications Scheduler, and Deep Link Router scenes.
- **Documentation**: Added subsystem references and editor-tooling guides for the Device Simulator and build pipeline.

**Changed**:
- Consolidated the package under the `com.gamelovers.mobileservices` package name, `GameLovers.MobileServices.*` namespaces, and `GameLovers.MobileServices` assembly.
- Updated the package baseline to Unity 6 and documented the supported 6000.5.7f1, 6000.3.21f1, and 6000.0.81f1 validation editors.

**Fixed**:
- Fixed persisted notifications so nullable IDs, badge numbers, and delivery times survive background/foreground rescheduling.
- Fixed local notification delivered and expired events so subscribers added after service construction receive callbacks.
- Fixed editor notification scheduling so generated notifications appear in the pending collection.

**Removed**:
- Removed legacy tap detection; use the Unity Input System's `TapInteraction` instead.
- Removed gamepad input management; configure gamepad input through the Unity Input System.

**Migration**:
This package consolidates three previously separate packages:
- `com.gamelovers.nativeui` (v0.2.5) → `GameLovers.MobileServices.NativeUi`
- `com.gamelovers.notificationservice` (v0.1.7) → `GameLovers.MobileServices.Notifications`
- `com.gamelovers.inputextensions` (v0.1.0-preview.4, swipe detection only) → `GameLovers.MobileServices.Gestures`
- `AlertButtonStyle.Positive` → `AlertButtonStyle.Destructive`; `AlertButtonStyle.Negative` → `AlertButtonStyle.Cancel`.

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

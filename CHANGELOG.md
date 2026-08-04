# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-08-04

**New**:
- Added native UI services for iOS and Android alerts, action sheets, toasts, review requests, and share sheets.
- Added local notification scheduling and management with channels, queueing, and a fluent scheduling builder.
- Added Enhanced Touch swipe and tap gesture support.
- Added cross-platform haptics with presets, custom intensity, and time-bounded looping.
- Added the `Device` service facade for safe area, screen wake, battery and low-power mode, iOS audio session, permissions, App Tracking Transparency, and deep links.
- Added unified iOS and Android runtime permission requests, including multi-permission async requests and an iOS 14.5+ App Tracking Transparency bridge.
- Added deep-link activation and route-pattern handling with cold-start link replay.
- Added an embedded Unity Device Simulator plugin with native UI mocks, live diagnostics, and haptic envelope visualization.
- Added the Mobile Services Config asset and build postprocessor for localized iOS usage descriptions, entitlements, Android manifest entries, and Play In-App Review setup.
- Added `MobileServicesPlayground`, `HapticsPalette`, `NotificationsScheduler`, and `DeepLinkRouter` samples, along with subsystem and editor-tooling documentation.

**Changed**:
- Consolidated the package under the `com.gamelovers.mobileservices` package name, `GameLovers.MobileServices.*` namespaces, and `GameLovers.MobileServices` assembly.
- Updated the package baseline to Unity 6 (6000.0+) and added the Unity Mobile Notifications and Input System dependencies.

**Fixed**:
- Fixed persisted notifications so nullable IDs, badge numbers, and delivery times survive background/foreground rescheduling.
- Fixed local notification delivered and expired events so subscribers added after service construction receive callbacks.

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

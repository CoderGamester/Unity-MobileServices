# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-01-13

### Added
- Initial release of consolidated **Mobile Services** package.
- **Native UI**: Alerts, sheets, and toasts for iOS/Android.
- **Notifications**: Comprehensive local and remote notification management.
- **Gestures**: Advanced swipe detection with velocity and consistency tracking.

### Changed
- Refactored all namespaces to `GameLovers.MobileServices.*`.
- Updated assembly definition to `GameLovers.MobileServices`.
- Updated dependencies to target Unity 6 (6000.0+).

### Migration
This package consolidates three previously separate packages:
- `com.gamelovers.nativeui` (v0.2.5) -> `GameLovers.MobileServices.NativeUi`
- `com.gamelovers.notificationservice` (v0.1.7) -> `GameLovers.MobileServices.Notifications`
- `com.gamelovers.inputextensions` (v0.1.0-preview.4, swipe detection only) -> `GameLovers.MobileServices.Gestures`

### Removed
- Legacy tap detection (replaced by Unity Input System's `TapInteraction`).
- Gamepad input management (out of scope for mobile services).

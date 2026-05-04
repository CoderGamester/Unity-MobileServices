# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-05-05

### Added
- Initial release of consolidated **Mobile Services** package.
- **Native UI**: Alerts, sheets, and toasts for iOS/Android, plus `NativeUiService.RequestReview()` (iOS `SKStoreReviewController` + Android Play Core In-App Review) and `NativeUiService.Share(text, url, imagePath, title)` (iOS `UIActivityViewController` + Android `Intent.ACTION_SEND`).
- **Notifications**: Comprehensive local and remote notification management.
- **Gestures**: Advanced swipe detection with velocity and consistency tracking.
- **iOS Audio Session**: `IIosAudioSessionService.ConfigureForPlayback()` (also exposed via `device.AudioSession` on the unified `IDeviceService`) overrides the iOS silent switch so audio keeps playing.
- **Haptics**: zero-dependency haptic feedback (`IHapticsService`) with 9 preset patterns, custom intensity, time-bounded looping (`PlayPresetDuration(preset, duration)` with `-1`=loop / `0`=natural one-shot / `>0`=loop with auto-stop), and `StopCurrentHaptic()`. iOS `UI*FeedbackGenerator` + Android `VibrationEffect.createWaveform` bridges, no third-party plugin required.
- **`IDeviceService` umbrella facade** exposing `SafeArea`, `ScreenWake`, `Battery`, `Connectivity`, `AudioSession`, `Permissions`, `Att`, and `DeepLink` sub-services through one entry point. Each child is independently registerable for testing. `IBatteryService` includes low-power-mode awareness on iOS (`NSProcessInfoPowerStateDidChangeNotification`) and Android (`PowerManager.isPowerSaveMode`). `ISafeAreaService` ships with a companion `SafeAreaContainer` UI Toolkit element. All event-driven children share a single internal MonoBehaviour host for polling.
- **Permissions** (`IPermissionsService`, also at `device.Permissions`): unified iOS+Android runtime permissions covering Camera, Microphone, Location (when-in-use & always), Photo Library (read-write & add-only), and Notifications. `Task`-based async — no `UniTask` dependency.
- **App Tracking Transparency** (`IAttService`, also at `device.Att`): iOS 14.5+ `ATTrackingManager` bridge for `RequestAuthorizationAsync()` and `CurrentStatus`. **Zero dependency on the deprecation-bound `com.unity.ads.ios-support` package**. Android / Editor / unsupported platforms return `Authorized` (no equivalent restriction).
- **Deep Links** (`IDeepLinkService`, also at `device.DeepLink`): wraps `Application.deepLinkActivated` and adds **cold-start link queueing** — links delivered by the OS at app launch are not lost if the first subscriber attaches after the event has fired.

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


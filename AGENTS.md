# GameLovers.MobileServices - AI Agent Guide

## 1. Package Overview
- **Package**: `com.gamelovers.mobileservices`
- **Unity**: 6000.0+
- **Dependencies**:
  - `com.unity.mobile.notifications` (2.3.0)
  - `com.unity.inputsystem` (1.11.0)

This package consolidates mobile-specific platform services: Native UI integration, Push Notifications, and Advanced Gesture detection.

## 2. Runtime Architecture

### Native UI (`Runtime/NativeUi/`)
- **Main Class**: `NativeUiService` (static)
- **Responsibility**: Bridge to native iOS and Android UI components (Alerts, Sheets, Toasts).
- **Implementation**: Uses `AndroidJavaClass` for Android and `[DllImport("__Internal")]` for iOS (linked via `Plugins/iOS/NativeUi.m`).

### Notifications (`Runtime/Notifications/`)
- **Main Interface**: `INotificationService`
- **Concrete Class**: `MobileNotificationService`
- **Responsibility**: Wrapper around Unity's Mobile Notifications package.
- **Key Flow**: 
  - `MobileNotificationService` spawns a `GameNotificationsMonoBehaviour` host GameObject.
  - Channels must be configured during initialization.
  - Supports platform-specific notification shapes via `IGameNotification`.

### Gestures (`Runtime/Gestures/`)
- **Main Class**: `GestureController` (MonoBehaviour)
- **Responsibility**: Interprets pointer input to detect complex gestures (Swipes).
- **Key Concepts**:
  - `ActiveGesture`: Internal state tracking for a single pointer.
  - `SwipeInput`: Data structure containing swipe direction, velocity, and "sameness" (consistency).
  - Uses `PointerInputManager` to abstract Input System pointer data.

## 3. Directory Structure
- `Runtime/NativeUi/`: Native bridge code for C#.
- `Runtime/Notifications/`: Notification management logic.
- `Runtime/Gestures/`: Gesture detection algorithms and Input System integration.
- `Plugins/iOS/`: Native Objective-C code for iOS bridging.
- `Tests/`: Unit and integration tests.

## 4. Coding Standards
- **Namespaces**: Use `GameLovers.MobileServices.*` sub-namespaces.
- **Platform Defines**: Use `#if UNITY_IOS`, `#if UNITY_ANDROID`, and `#if UNITY_EDITOR` appropriately.
- **Async**: Favor async/await where possible, though many native calls are synchronous or event-based.

## 5. Migration Logic
This package replaces `com.gamelovers.nativeui`, `com.gamelovers.notificationservice`, and parts of `com.gamelovers.inputextensions`. 
- Always update old references to use the new unified assembly: `GameLovers.MobileServices`.
- Namespace mapping is documented in `MIGRATION.md`.

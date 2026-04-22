# GameLovers.MobileServices - AI Agent Guide

> **Companion files**: `CLAUDE.md` wraps this file for Claude Code — edit `AGENTS.md`, not `CLAUDE.md`. `README.md` is the user-facing entry point.

## 1. Package Overview
- **Package**: `com.gamelovers.mobileservices`
- **Unity**: 6000.0+
- **Dependencies** (see `package.json`)
  - `com.unity.mobile.notifications` (**2.3.0**)
  - `com.unity.inputsystem` (**1.11.0**)

This package consolidates mobile-specific platform services:
- **Native UI**: alerts (modal + action sheet) and toast-style messages.
- **Notifications**: platform wrapper over Unity Mobile Notifications (Android/iOS).
- **Gestures**: Input System–based pointer abstraction + swipe/tap detection.

For user-facing docs, treat `README.md` as the primary entry point. This file is for contributors/agents working on the package itself.

## 2. Runtime Architecture (high level)

### Native UI (`GameLovers.MobileServices.NativeUi`)
- **Main entry point**: `Runtime/NativeUi/NativeUiService.cs` (`NativeUiService` is `static`)
  - Android: uses `AndroidJavaClass` + `AndroidJavaObject` to build an `android.app.AlertDialog` and `android.widget.Toast`.
  - iOS: uses `[DllImport("__Internal")]` native functions implemented in `Plugins/iOS/NativeUi.m`.
- **Button model**: `AlertButton` + `AlertButtonStyle`.

### Notifications (`GameLovers.MobileServices.Notifications`)
- **Public API**: `Runtime/Notifications/MobileNotificationService.cs`
  - Interface: `INotificationService`
  - Concrete: `MobileNotificationService`
- **Host / lifecycle**: `Runtime/Notifications/GameNotificationsMonoBehaviour.cs`
  - Owns the active platform implementation (`IGameNotificationsPlatform`).
  - Handles queueing/scheduling behavior based on `OperatingMode`.
  - Persists scheduled notifications on background using `PlayerPrefs` (key: `"notifications"`).
- **Platform implementations**
  - Android: `Runtime/Notifications/Android/AndroidNotificationsPlatform.cs` + `AndroidGameNotification.cs`
  - iOS: `Runtime/Notifications/iOS/iOSNotificationsPlatform.cs` + `iOSGameNotification.cs`
  - Editor fallback: `Runtime/Notifications/Internal/EditorGameNotification.cs`
- **Notification shape**: `Runtime/Notifications/IGameNotification.cs`
  - Cross-platform surface; internally mapped to Unity Mobile Notifications types.
- **Channels**
  - Wrapper: `Runtime/Notifications/GameNotificationChannel.cs`
  - Android requires at least one channel to be registered; the first channel passed becomes the platform default (`AndroidNotificationsPlatform.DefaultChannelId`).

### Gestures (`GameLovers.MobileServices.Gestures`)
- **Input source**: Unity's `EnhancedTouch` API (`Touch.onFingerDown/Move/Up`)
- **Gesture detection**
  - `Runtime/Gestures/GestureController.cs` subscribes to EnhancedTouch finger events and emits gesture events (`Pressed`, `PotentiallySwiped`, `Swiped`, `Tapped`).
  - `Runtime/Gestures/ActiveGesture.cs` is the internal state accumulator per finger.
  - `Runtime/Gestures/SwipeInput.cs` is the public data structure for swipe output.
  - `Runtime/Gestures/TapInput.cs` is the public data structure for tap output.

## 3. Key Directories / Files
```
Runtime/
├── NativeUi/
│   └── NativeUiService.cs
├── Notifications/
│   ├── MobileNotificationService.cs
│   ├── GameNotificationsMonoBehaviour.cs
│   ├── GameNotificationChannel.cs
│   ├── IGameNotification.cs
│   ├── PendingNotification.cs
│   ├── Android/
│   │   ├── AndroidNotificationsPlatform.cs
│   │   └── AndroidGameNotification.cs
│   ├── iOS/
│   │   ├── iOSNotificationsPlatform.cs
│   │   └── iOSGameNotification.cs
│   └── Internal/
│       ├── IGameNotificationsPlatform.cs
│       ├── EditorGameNotification.cs
│       └── SerializableNotification.cs
├── Gestures/
│   ├── GestureController.cs
│   ├── ActiveGesture.cs
│   ├── SwipeInput.cs
│   └── TapInput.cs
└── GameLovers.MobileServices.asmdef

Plugins/iOS/
└── NativeUi.m
```

## 4. Important Behaviors / Gotchas
- **NativeUiService is platform-gated**
  - In `UNITY_EDITOR` it logs and does nothing.
  - In unsupported platforms it throws `SystemException`.
- **iOS alert callbacks are matched by button text**
  - `NativeUiService` stores buttons in a static array and invokes callbacks by matching `AlertButton.Text`.
  - Keep button texts unique per alert to avoid ambiguous matches.
- **Notifications host object is created at runtime**
  - `MobileNotificationService` creates a `GameObject("NotificationService")` and adds `GameNotificationsMonoBehaviour`.
  - This object is marked `DontDestroyOnLoad`, so tests or “reset game” flows may need explicit teardown.
- **Android notification channels**
  - If you pass channels, the first one is treated as the default channel id.
  - If you schedule without a channel on Android, ensure `DefaultChannelId` is set (via initialization with at least one channel).
- **Queueing vs immediate scheduling**
  - In `OperatingMode.Queue*`, notifications may be queued while foregrounded and only scheduled with the OS when the app backgrounds.
  - Foreground/background transitions are handled via `OnApplicationFocus`.
- **GestureController threshold interplay**
  - If `minSwipeDistance <= maxTapDrift`, a single interaction can qualify as both tap and swipe depending on travel distance and other thresholds.
  - `GestureController` requires `EnhancedTouchSupport` to be enabled; it handles this automatically in `OnEnable`/`OnDisable`.
  - For mouse input in Editor, add `TouchSimulation` component to convert mouse to touch.

## 5. Coding Standards (Unity 6 / C# 9.0)
- **C#**: C# 9.0 syntax; explicit namespaces; no global usings.
- **Assemblies**
  - Runtime must not reference `UnityEditor` (guard any editor-only helpers with `#if UNITY_EDITOR`).
  - Keep iOS/Android code behind platform defines (`#if UNITY_IOS`, `#if UNITY_ANDROID`).
- **Interop**
  - For iOS, keep native symbols in `Plugins/iOS/*` stable when changing `[DllImport("__Internal")]` signatures.
  - For Android JNI calls, ensure objects are disposed (`using` blocks are preferred, as in `NativeUiService`).

## 6. External Package Sources (for API lookups)
When you need third-party source/docs, prefer the locally-cached UPM packages:
- Mobile Notifications: `Library/PackageCache/com.unity.mobile.notifications@*/`
- Input System: `Library/PackageCache/com.unity.inputsystem@*/`

## 7. Dev Workflows (common changes)
- **Add a new native UI feature**
  - Add the C# surface to `Runtime/NativeUi/*` behind platform defines.
  - iOS: add/modify Objective-C in `Plugins/iOS/NativeUi.m` and keep signatures in sync with `[DllImport("__Internal")]`.
  - Android: implement via `AndroidJavaObject` or provide a Java/Kotlin plugin if it gets too complex.
- **Add a new notification capability**
  - Extend `IGameNotification` only if it can be mapped to both platforms (or clearly document platform-only fields).
  - Update the relevant platform notification wrappers (`AndroidGameNotification`, `iOSGameNotification`) and platform scheduling behavior.
  - If data must persist across background/foreground, update `SerializableNotification` + conversion helpers.
- **Add or adjust Android channels**
  - Update construction site(s) where `MobileNotificationService` is initialized.
  - Ensure at least one channel is registered; confirm default channel behavior matches expectations.
- **Change gesture detection**
  - Adjust thresholds on `GestureController` and document intended UX.

## 8. Update Policy
Update this file when:
- Public API changes (`NativeUiService`, `INotificationService`, `IGameNotification`, `GestureController` events)
- Platform integration changes (JNI calls, iOS native symbols, notification platform wrappers)
- Notification queueing/persistence behavior changes (`OperatingMode`, PlayerPrefs payload shape)
- Gesture detection logic or input source integration changes

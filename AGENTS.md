# GameLovers.MobileServices - AI Agent Guide

> **Companion files**: `CLAUDE.md` wraps this file for Claude Code — edit `AGENTS.md`, not `CLAUDE.md`. `README.md` is the user-facing entry point.

## 1. Package Overview
- **Package**: `com.gamelovers.mobileservices`
- **Unity**: 6000.0+
- **Dependencies** (see `package.json`)
  - `com.unity.mobile.notifications` (**2.3.0**)
  - `com.unity.inputsystem` (**1.11.0**)

This package consolidates mobile-specific platform services:
- **Native UI**: alerts (modal + action sheet), toast-style messages, OS rating prompt (`RequestReview`), and share sheet (`Share`).
- **Notifications**: platform wrapper over Unity Mobile Notifications (Android/iOS).
- **Gestures**: Input System–based pointer abstraction + swipe/tap detection.
- **Haptics**: zero-dependency haptic feedback with 9 presets, custom intensity, time-bounded looping. Built directly on iOS `UI*FeedbackGenerator` + Android `VibrationEffect.createWaveform` — no NiceVibrations or other third-party plugin.
- **Device**: `IDeviceService` umbrella facade over 8 sub-services — `SafeArea`, `ScreenWake`, `Battery` (with iOS / Android low-power-mode awareness), `Connectivity`, `AudioSession` (iOS silent-switch override), `Permissions` (unified iOS+Android, Task-based async), `Att` (App Tracking Transparency, no `com.unity.ads.ios-support` dep), `DeepLink` (with cold-start link queueing).

For user-facing docs, treat `README.md` as the primary entry point. This file is for contributors/agents working on the package itself.

## 2. Runtime Architecture (high level)

### Native UI (`GameLovers.MobileServices.NativeUi`)
- **Main entry point**: `Runtime/NativeUi/NativeUiService.cs` (`NativeUiService` is `static`)
  - Android: uses `AndroidJavaClass` + `AndroidJavaObject` to build an `android.app.AlertDialog` / `android.widget.Toast` / `Intent.ACTION_SEND`, and uses `com.google.android.play.core.review.ReviewManager` for in-app review.
  - iOS: uses `[DllImport("__Internal")]` native functions implemented in `Plugins/iOS/NativeUi.m` (alerts, toasts, `_GameLoversRequestReview`, `_GameLoversShare`).
- **Button model**: `AlertButton` + `AlertButtonStyle { Default, Destructive, Cancel }` (iOS-native vocabulary; renamed from `Positive/Negative` during the 1.0.0 modernization).
- **Review prompt**: `RequestReview()` — iOS `SKStoreReviewController` (modern `requestReviewInScene:` on iOS 14+, fallback to `requestReview` on iOS 10.3–13); Android Play Core `ReviewManagerFactory` + `launchReviewFlow`. The OS throttles the actual prompt frequency.
- **Share sheet**: `Share(text, url, imagePath, title)` — iOS `UIActivityViewController`; Android `Intent.ACTION_SEND` via `Intent.createChooser`. Image+text share works on both. iPad popover anchors to the view centre with no arrow.
- **Android Play Core dependency**: `RequestReview()` requires `com.google.android.play:review:2.0.1` in the consumer's `mainTemplate.gradle`. Without it the call logs an error and returns; it does not throw.

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

### Device (`GameLovers.MobileServices.Device`)
This namespace holds the umbrella facade plus every device-touching service. All sub-services live in the same namespace; sub-folders under `Runtime/Device/` (`Audio/`, `State/`, `Internal/`, `Permissions/`, `Tracking/`, `DeepLinks/`) are organizational only and do NOT add namespace nesting (same convention `Runtime/Notifications/` already uses with its `Android/`, `iOS/`, `Internal/` sub-folders).

- **Umbrella facade**: `Runtime/Device/IDeviceService.cs` + `DeviceService.cs`. Constructs each child internally for the default case; an injection constructor accepts mocks for tests. `Dispose()` propagates to children that implement `IDisposable`.
- **Shared host**: `Runtime/Device/Internal/DeviceServicesHost.cs` — internal `MonoBehaviour`, `DontDestroyOnLoad`, lazily spawned. Exposes `RegisterLateUpdate` / `RegisterSecondTick` / `RegisterFocusChanged` / `RegisterIosLowPowerModeChanged`. Means the runtime cost of the entire Device subsystem is a single GameObject.
- **Audio Session**: `Runtime/Device/Audio/IIosAudioSessionService.cs` + `IosAudioSessionService.cs`. `ConfigureForPlayback()` sets `AVAudioSessionCategoryPlayback` + `setActive:YES` via `Plugins/iOS/iOSAudioSession.m`. Android / Editor / unsupported platforms are safe no-ops. Instance (not static) so it can sit on `IDeviceService.AudioSession`.
- **Safe Area**: `Runtime/Device/State/ISafeAreaService.cs` + `SafeAreaService.cs`. Polls `Screen.safeArea` in `LateUpdate` via the host; fires `OnSafeAreaChanged` on diff. Companion `SafeAreaContainer` UI Toolkit `VisualElement` self-pads to the safe area; can be constructed with the service or wired up via `SetSafeAreaService` for UXML usage.
- **Screen Wake**: `Runtime/Device/State/IScreenWakeService.cs` + `ScreenWakeService.cs`. Trivial wrapper over `Screen.sleepTimeout`; idempotent.
- **Battery**: `Runtime/Device/State/IBatteryService.cs` + `BatteryService.cs`. Polls `SystemInfo.batteryLevel` / `batteryStatus` once per second via the host; fires `OnLevelChanged` (≥1% diff), `OnStatusChanged`, `OnLowPowerModeChanged`. iOS LPM via `Plugins/iOS/Battery.m` exposing `_GameLoversBatteryIsLowPowerModeEnabled` plus an `NSProcessInfoPowerStateDidChangeNotification` observer that calls back via `UnitySendMessage("DeviceServicesHost", "OnIosLowPowerModeChanged", "")`. Android LPM polled via JNI `PowerManager.isPowerSaveMode()` on focus change.
- **Connectivity**: `Runtime/Device/State/IConnectivityService.cs` + `ConnectivityService.cs`. Polls `Application.internetReachability` once per second + on focus regain; fires `OnStatusChanged` on transition. Documented as best-effort.
- **Permissions**: `Runtime/Device/Permissions/IPermissionsService.cs` + `PermissionsService.cs`. `Check(...)` is sync, `RequestAsync(...)` returns `Task<PermissionStatus>` (no UniTask dep). Android uses `UnityEngine.Android.Permission` with manifest mapping for Camera/Mic/FineLocation; uses `READ_MEDIA_IMAGES` (API 33+) for Photos and `POST_NOTIFICATIONS` for Notifications. iOS uses `Plugins/iOS/Permissions.m` with one bridge per permission (`AVCaptureDevice` for Camera/Mic, `CLLocationManager` for Location, `PHPhotoLibrary` for Photos, `UNUserNotificationCenter` for Notifications). Async results returned via `UnitySendMessage("PermissionsCallbackReceiver", "OnPermissionResult", "<id>:<status>")` to `Runtime/Device/Permissions/Internal/PermissionsCallbackReceiver.cs` which resolves the matching `TaskCompletionSource`.
  - **Location delegate lifetime**: iOS bridge keeps `CLLocationManager` instances alive in a static `NSMutableArray<GLLocationDelegate *>` so the delegate isn't GC'd before `locationManagerDidChangeAuthorization:` fires. The delegate clears itself from the manager after dispatch.
- **App Tracking Transparency**: `Runtime/Device/Tracking/IAttService.cs` + `AttService.cs`. iOS bridge: `Plugins/iOS/Att.m` calling `ATTrackingManager.requestTrackingAuthorizationWithCompletionHandler:` (iOS 14+ only — pre-14 returns Authorized). Same `UnitySendMessage` callback pattern as Permissions but with a separate `AttCallbackReceiver` MonoBehaviour to keep payload formats per-subsystem. **No dependency on `com.unity.ads.ios-support`** — explicit goal.
- **Deep Links**: `Runtime/Device/DeepLinks/IDeepLinkService.cs` + `DeepLinkService.cs`. Wraps `Application.deepLinkActivated`; on construction captures `Application.absoluteURL` (set by Unity before any subscriber attaches when the app is cold-launched with a link) and replays it to the first subscriber via the `OnLinkActivated` event's `add` accessor. Runtime delivery clears any pending cold-start link.

### Gestures (`GameLovers.MobileServices.Gestures`)
- **Input source**: Unity's `EnhancedTouch` API (`Touch.onFingerDown/Move/Up`)
- **Gesture detection**
  - `Runtime/Gestures/GestureController.cs` subscribes to EnhancedTouch finger events and emits gesture events (`Pressed`, `PotentiallySwiped`, `Swiped`, `Tapped`).
  - `Runtime/Gestures/ActiveGesture.cs` is the internal state accumulator per finger.
  - `Runtime/Gestures/SwipeInput.cs` is the public data structure for swipe output.
  - `Runtime/Gestures/TapInput.cs` is the public data structure for tap output.

### Haptics (`GameLovers.MobileServices.Haptics`)
- **Public API**: `Runtime/Haptics/IHapticsService.cs` + `Runtime/Haptics/HapticsService.cs`
  - `Enabled`, `IsSupported`, `IsPlaying`
  - `PlayPreset(HapticPreset)` — natural one-shot; sugar for `PlayPresetDuration(preset, 0f)`
  - `PlayPresetDuration(HapticPreset, float duration = -1f)` — `0`=natural one-shot, `<0`=loop until `StopCurrentHaptic`, `>0`=loop with real-time auto-stop
  - `PlayCustom(float intensity01, float durationMs)` — single-intensity haptic, always finite
  - `StopCurrentHaptic()` — single stop entry point; idempotent
- **Preset catalogue**: `Runtime/Haptics/HapticPreset.cs` — 9 entries (Selection, Success, Warning, Error, ImpactLight, ImpactMedium, ImpactHeavy, ImpactRigid, ImpactSoft) plus `None`.
- **Backend abstraction**: `Runtime/Haptics/Internal/IHapticsBackend.cs` selects platform impl at construction:
  - iOS: `IosHapticsBackend` → `[DllImport("__Internal")]` into `Plugins/iOS/Haptics.m` (UIKit `UIImpactFeedbackGenerator` / `UINotificationFeedbackGenerator` / `UISelectionFeedbackGenerator`); looping via `NSTimer`.
  - Android: `AndroidHapticsBackend` → pure JNI to `android.os.Vibrator.vibrate(VibrationEffect)`. `VibrationEffect.createWaveform(long[] timings, int[] amplitudes, int repeat)` for presets; `repeat=0` loops, `cancel()` stops. Requires API 26 (Android 8.0)+.
  - Editor: `EditorHapticsBackend` (logs).
  - Other: `NoOpHapticsBackend`.
- **Auto-stop**: `Runtime/Haptics/Internal/HapticsHost.cs` (internal MonoBehaviour, lazily spawned on first play, `DontDestroyOnLoad`) runs a single `WaitForSecondsRealtime` coroutine. Each new `Play*` cancels the previous coroutine — only one auto-stop is ever pending. No `ICoroutineService` dependency on `com.gamelovers.services`.
- **Lofelt/NiceVibrations**: `**zero runtime dependency**`. Lofelt code in the demons project was used as inspiration for preset envelope shapes only; every line in this package is original.

## 3. Layout convention

Section §2 names every public type and the assembly it lives in. Use that plus your IDE / `find` / `Glob` for the actual inventory — the conventions below are what's load-bearing.

- **One folder per subsystem under `Runtime/`** — `NativeUi/`, `Notifications/`, `Gestures/`, `Haptics/`, `Device/`. Each subsystem owns one C# namespace (`GameLovers.MobileServices.<Subsystem>`).
- **Sub-folders inside a subsystem are organizational only**, NOT namespace-nesting. Examples: `Runtime/Notifications/{Android,iOS,Internal}/` and `Runtime/Device/{Audio,State,Permissions,Tracking,DeepLinks,Internal}/` all use their parent subsystem's namespace. C# enforces the namespace via the `namespace` keyword in each file, not via folder paths.
- **`Internal/` sub-folders hold non-public types** (platform backends, MonoBehaviour hosts, callback receivers, serializable DTOs). Use the `internal` access modifier; tests reach in through `Runtime/AssemblyInfo.cs` which grants `InternalsVisibleTo("GameLovers.MobileServices.{Edit,Play}Mode.Tests")`.
- **Native bridges live in `Plugins/iOS/<Subsystem>.m`** — one `.m` per subsystem, paired with a backend C# class that owns the `[DllImport("__Internal")]` declarations and routes through it. iOS-side preset/permission/status enums in the `.m` file MUST mirror the C# enum integer values one-to-one; see Phase 5's `GLAppPermission` / `GLPermissionStatus` and Phase 2's `GLHapticPresetId` for the pattern.
- **`UnitySendMessage` GameObject names are contracts** — the iOS `.m` files address `DeviceServicesHost`, `PermissionsCallbackReceiver`, and `AttCallbackReceiver` by string. Renaming the C# `MonoBehaviour` requires updating the matching `.m` file.
- **Tests** live under `Tests/{EditMode,PlayMode}/` with one asmdef each. Tests do NOT mirror the runtime folder structure — group by feature, not by source path.

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
- **Haptics auto-stop coroutine cancellation**
  - Each `Play*` call cancels the previous auto-stop coroutine before scheduling its own. Looping calls (`PlayPresetDuration(preset, -1)`) leave NO auto-stop pending; the caller MUST invoke `StopCurrentHaptic()` (or set `Enabled = false`).
  - `HapticsHost` is spawned lazily on first play; subsequent calls reuse it. Resetting the game without calling `StopCurrentHaptic()` first leaves the haptic looping until the host is destroyed.
- **Device subsystem GameObjects**
  - The umbrella creates up to four `DontDestroyOnLoad` GameObjects on first use: `DeviceServicesHost` (shared poller), `PermissionsCallbackReceiver` (only on iOS, only after the first `RequestAsync`), `AttCallbackReceiver` (only on iOS, only after the first `RequestAuthorizationAsync`), and `HapticsHost` (only after the first haptic with auto-stop). Tests / "reset game" flows that destroy DDOL scenes need to recreate the umbrella afterwards.
  - `iOS Battery.m` and `Permissions.m` and `Att.m` all use `UnitySendMessage` against fixed GameObject names — the C# MonoBehaviour names MUST match (`DeviceServicesHost`, `PermissionsCallbackReceiver`, `AttCallbackReceiver`). Renaming requires updating both sides.
- **Permissions: Android API 33+ runtime requirements**
  - `READ_MEDIA_IMAGES` and `POST_NOTIFICATIONS` are runtime-required from API 33 (Android 13). Below 33 the OS auto-grants them; the `IPermissionsService` returns `Granted` immediately on those older API levels via the same code path (Unity's `Permission.HasUserAuthorizedPermission` short-circuits).
  - Manifest entries for these permissions still need to be added by the consumer's `AndroidManifest.xml` / `mainTemplate.xml`.
- **DeepLinkService cold-start link replay**
  - The cold-start link (captured from `Application.absoluteURL` at construction) is replayed to the FIRST subscriber only — subsequent subscribers do NOT receive it. This is intentional: the link represents a single user action, not a state.
  - Construct the service early in app bootstrap (before scene load) to avoid a race where Unity has already cleared `Application.absoluteURL` by the time the service is instantiated.
- **AttService never throws on Android / Editor**
  - Both methods return `AttStatus.Authorized` synchronously on non-iOS platforms. Don't read this as "the user authorized" — read it as "the platform doesn't apply ATT". Conditionalize tracking-init code on `Application.platform == RuntimePlatform.IPhonePlayer` if you care about the distinction.

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
- Public API changes (`NativeUiService`, `INotificationService`, `IGameNotification`, `GestureController` events, `IHapticsService`, `IDeviceService` and any of its 8 child interfaces)
- Platform integration changes (JNI calls, iOS native symbols in any `Plugins/iOS/*.m` file, notification platform wrappers, `UnitySendMessage` GameObject names)
- Notification queueing/persistence behavior changes (`OperatingMode`, PlayerPrefs payload shape)
- Gesture detection logic or input source integration changes
- Haptic preset envelopes (`HapticPreset` enum + per-preset time/amplitude tables in `AndroidHapticsBackend` and per-preset routing in `Plugins/iOS/Haptics.m`)
- Permissions catalogue changes (`AppPermission` enum + `AndroidManifestPermission` mapping + iOS `_GameLoversPermissionsRequest` switch)

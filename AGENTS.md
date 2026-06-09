# GameLovers.MobileServices - AI Agent Guide

> **Companion files**: `CLAUDE.md` wraps this file for Claude Code — edit `AGENTS.md`, not `CLAUDE.md`. `README.md` is the user-facing entry point.

## 1. Package Overview
- **Package**: `com.gamelovers.mobileservices`
- **Unity**: 6000.0+
- **Dependencies** (see `package.json`)
  - `com.unity.mobile.notifications` (**2.3.0**)
  - `com.unity.inputsystem` (**1.11.0**)

This package consolidates mobile-specific platform services:
- **Native UI**: alerts (modal + action sheet), toast-style messages, OS rating prompt (`RequestReview`), and share sheet (`Share`). Static `NativeUiService` plus an instance-based `INativeUiService` / `NativeUiServiceInstance` wrapper for mockable consumer code.
- **Notifications**: platform wrapper over Unity Mobile Notifications (Android/iOS) with a fluent `service.Schedule().In(...).Title(...).Send()` builder (`NotificationBuilder`).
- **Gestures**: Input System–based pointer abstraction + swipe/tap detection.
- **Haptics**: zero-dependency haptic feedback with 9 presets, custom intensity, time-bounded looping. Built directly on iOS `UI*FeedbackGenerator` + Android `VibrationEffect.createWaveform` — no NiceVibrations or other third-party plugin.
- **Device**: `IDeviceService` umbrella facade over 7 sub-services — `SafeArea`, `ScreenWake`, `Battery` (with iOS / Android low-power-mode awareness), `AudioSession` (iOS silent-switch override), `Permissions` (unified iOS+Android, Task-based async, including the multi-permission `RequestAsync(params AppPermission[])` overload), `Att` (App Tracking Transparency, no `com.unity.ads.ios-support` dep), `DeepLink` (with cold-start link queueing) — plus an `IDeepLinkRouter` layered on `IDeepLinkService` for path-pattern routing.
- **`IMobileService`** umbrella facade aggregating `NativeUi` / `Notifications` / `Haptics` / `Device` behind a single DI registration.

For user-facing docs, treat `README.md` as the primary entry point — it's the lean overview. Deeper per-subsystem API reference lives in [`docs/`](docs/) (`docs/README.md` is the index). This file is for contributors/agents working on the package itself.

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
- **Permissions**: `Runtime/Device/Permissions/IPermissionsService.cs` + `PermissionsService.cs`. `Check(...)` is sync, `RequestAsync(...)` returns `Task<PermissionStatus>` (no UniTask dep). Android uses `UnityEngine.Android.Permission` with manifest mapping for Camera/Mic/FineLocation; uses `READ_MEDIA_IMAGES` (API 33+) for Photos and `POST_NOTIFICATIONS` for Notifications. iOS uses `Plugins/iOS/Permissions.m` with one bridge per permission (`AVCaptureDevice` for Camera/Mic, `CLLocationManager` for Location, `PHPhotoLibrary` for Photos, `UNUserNotificationCenter` for Notifications). Async results returned via `UnitySendMessage("PermissionsCallbackReceiver", "OnPermissionResult", "<id>:<status>")` to `Runtime/Device/Permissions/Internal/PermissionsCallbackReceiver.cs` which resolves the matching `TaskCompletionSource`.
  - **Location delegate lifetime**: iOS bridge keeps `CLLocationManager` instances alive in a static `NSMutableArray<GLLocationDelegate *>` so the delegate isn't GC'd before `locationManagerDidChangeAuthorization:` fires. The delegate clears itself from the manager after dispatch.
- **App Tracking Transparency**: `Runtime/Device/Tracking/IAttService.cs` + `AttService.cs`. iOS bridge: `Plugins/iOS/Att.m` calling `ATTrackingManager.requestTrackingAuthorizationWithCompletionHandler:` (iOS 14+ only — pre-14 returns Authorized). Same `UnitySendMessage` callback pattern as Permissions but with a separate `AttCallbackReceiver` MonoBehaviour to keep payload formats per-subsystem. **No dependency on `com.unity.ads.ios-support`** — explicit goal.
- **Deep Links**: `Runtime/Device/DeepLinks/IDeepLinkService.cs` + `DeepLinkService.cs`. Wraps `Application.deepLinkActivated`; on construction captures `Application.absoluteURL` (set by Unity before any subscriber attaches when the app is cold-launched with a link) and replays it to the first subscriber via the `OnLinkActivated` event's `add` accessor. Runtime delivery clears any pending cold-start link.
- **Deep Link Router**: `Runtime/Device/DeepLinks/IDeepLinkRouter.cs` + `DeepLinkRouter.cs`. Layered over `IDeepLinkService`. Path-pattern routing: literal segments match exactly (case-insensitive), `:name` segments capture into a params dict (e.g. `/promo/:id` → `{ "id": "spring2026" }`). First match wins, registration order is preserved. Router subscribes once at construction; consumers hold the router for the lifetime of the app.

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

### Editor (`GameLovers.MobileServices.Editor`)
- **Assembly**: `Editor/GameLovers.MobileServices.Editor.asmdef` (`includePlatforms: ["Editor"]`). References the runtime asmdef and the Unity Input System / Notifications packages.
- **Single editor surface — the Device Simulator plugin** (`Editor/Explorer/DeviceSimulatorPanel/`): there is no standalone Explorer or Simulator window anymore (both removed during the 1.0.0 consolidation — the controller-in-one-window / canvas-in-another workflow and the split `WindowPlatform`/`OverlayPlatform` state were the UX problem). `MobileServicesDeviceSimulatorPlugin` is the one place to drive mocks, read live diagnostics, and view the haptic envelope graph; the in-Game-view overlay is the one canvas.
  - **`MobileServicesDeviceSimulatorPlugin`** — `UnityEditor.DeviceSimulation.DeviceSimulatorPlugin` subclass auto-discovered by Unity (no menu item, no registration), embedded in the Device Simulator window's Control Panel. Holds only `PermissionsService` + `AttService` instances (for reading state into the dropdowns); a single 500 ms `root.schedule.Execute(...)` poll auto-syncs the platform skin from the device profile (reads `Application.platform`, which the Device Simulator spoofs — robust across Unity 6 minor versions where `DeviceSimulator.deviceChanged` varies) and syncs the state dropdowns. Foldouts: Native UI, Haptics, Notifications (heads-up **preview only**), Gestures, Permissions, ATT. (There is no Device state foldout — see below.) **Editor-dead controls are deliberately omitted**: because the panel is editor-only, any control whose runtime service runs its `#if UNITY_EDITOR` stub with no observable effect was cut — Haptics has NO play/loop/stop/custom (the `EditorHapticsBackend` only logs; the preset buttons exist solely to plot the envelope). The envelope is an **intensity-over-time curve** rendered via `Painter2D` (`generateVisualContent` → step waveform + filled area) with X=time(ms) / Y=intensity(0–1) axis ticks — see `PaintEnvelope` / `BuildEnvelopeGraph`. **Permissions and ATT are state controls, not prompt triggers**: each permission has a `PermissionStatus` dropdown (and ATT one `AttStatus` dropdown) that sets BOTH the editor Check override and the next-Request override (`SetPermissionCheckResult` + `QueuePermissionResult`; `QueueAttResult` sets both ATT overrides), so the running game/sample reads exactly that state via `Check()` / `RequestAsync()` / `CurrentStatus`. The OS-prompt / ATT-prompt mock buttons were removed — a prompt only has meaning at runtime when the game actually requests, so push it from game/sample code (or `MobileSimulatorState.PushPermissionDialog`) rather than as an edit-mode panel button. The dropdowns are kept in sync with the effective `Check()` / `CurrentStatus` by the 500 ms poll via `SetValueWithoutNotify`, and are **play-mode-gated** (greyed in edit mode): the static override is only read by a running service, and the domain reload on entering Play would wipe an edit-mode setting anyway. Each gated foldout (Permissions, ATT) also carries its own inline "Enter Play mode" banner (`MakeSectionPlayModeBanner`, tracked in `_editModeBanners`) in addition to the global top banner. **There is no Device state foldout**: its only control had been a connectivity state dropdown, and `ConnectivityService` was removed from the package entirely (it was a thin wrapper over `Application.internetReachability` whose only value was a change event; consumers poll `Application.internetReachability` directly). Battery + LPM and notch/safe-area had already been dropped before that (desktop-junk `SystemInfo` / no in-panel visual; Unity's Device Simulator + `EditorPlatformSimulator.SetSafeArea` cover them). **Gestures** auto-spawns a hidden `[EditorOnly]` `GestureController` + enables `EnhancedTouch.TouchSimulation` in play mode when the scene has none (torn down on play-exit / panel-close), so it needs zero scene setup; it prefers a user's scene controller if present. **Notification banner mock** (`MockBuilders.BuildNotificationBanner` + the `mock-notif-*` USS) renders a realistic heads-up: app-icon + app-name/time header + bold title + body, light card on iOS, white card with a small colored icon + colored app name on Android. **There is no Deep links foldout**: `DeepLinkService.SimulateLinkActivated` is instance-scoped (no static override like Permissions/ATT use), so the panel could only fire into a throwaway instance it owns, never the game's — deep links are driven from the `DeepLinkRouter` sample or `EditorPlatformSimulator.SimulateDeepLink(uri, service)` instead. **Notifications scheduling/channels/pending/cancel were removed for the same reason** (the panel's `MobileNotificationService` was a throwaway instance disconnected from the game, duplicating the `NotificationsScheduler` sample); only the edit-mode heads-up banner **preview** mock remains. What remains earns its place via a mock render, the envelope graph, or an `EditorPlatformSimulator` override (set permission/ATT state, drives your code in play/tests). The play-mode-gated controls (Permission / ATT state dropdowns) need Play mode; the mock previews + envelope graph render in edit mode. `OnCreate`/`OnDestroy` call `MobileSimulatorRuntimeOverlay.NotifyPluginActive(true/false)` so the overlay is alive exactly while the panel is open. **Master switch**: an `Editor Simulator` toggle in the header binds to `MobileSimulatorState.Enabled`; the header stays interactive while every section below it (wrapped in `_sectionsContainer`) is enabled/disabled as a group via `SetEnabled` (composes hierarchically with the existing play-mode gating). Turning it off broadcasts `PushDismissAll` to clear any visible mock and hides the Game-view `[EDITOR SIMULATOR]` banner. **Dismissal**: there is no global header dismiss button — `Dismiss all UIs` lives in the Native UI foldout and `Dismiss Banner` in the Notifications foldout (both broadcast `PushDismissAll`, clearing the single overlay stage).
  - **`MobileSimulatorState`** (`Editor/Explorer/Overlays/`) — singleton broker / event bus. One renderer surface now, so `Push*` calls are plain broadcasts (no `SimulatorTarget`) and a single `Platform` skin (no `Window`/`Overlay` split). `MockBuilders` provides per-shape factory methods; the three USS files (`MobileSimulator.Common.uss`, `MobileSimulator.iOS.uss`, `MobileSimulator.Android.uss`) are swapped when the platform flips. An `[EDITOR SIMULATOR]` watermark is shown in the Game view while the simulator is enabled (the master switch — `MobileSimulatorState.Enabled`, persisted to `EditorPrefs` with an `EnabledChanged` event; the overlay hides the watermark when off).
  - **`MobileSimulatorRuntimeOverlay`** (`Editor/Explorer/Overlays/`) — editor-only `[InitializeOnLoad]` bootstrap. Spawns a `[EditorOnly] MobileSimulatorOverlay` GameObject with a UIDocument + programmatic `PanelSettings` (`sortingOrder = short.MaxValue`) rendering pixel-aligned with the simulated device's `Screen.*` values. A single idempotent `RefreshLifecycle()` keeps the overlay alive when `_pluginActive` (Device Simulator panel open, **edit OR play mode** — `UIDocument` is `[ExecuteAlways]`) OR (`_inPlayMode` AND `MobileServicesSettings.EnableRuntimeSimulatorOverlay`). Interaction inside the mock is unreliable in the edit-mode Game view, so dismissal is driven from the plugin's per-section dismiss buttons (`Dismiss all UIs` in Native UI, `Dismiss Banner` in Notifications — both broadcast `PushDismissAll`, which clears the single overlay stage); the overlay is display-only. `DestroyStaleHosts()` de-dups after a domain reload; `DontDestroyOnLoad` is only called in play mode (avoids an edit-mode warning).
- **`EditorPlatformSimulator`** (`Editor/Simulation/EditorPlatformSimulator.cs`, namespace `GameLovers.MobileServices.Editor.Simulation`): static editor-only façade exposing `SetIosLowPowerMode`, `SetSafeArea` / `ClearSafeAreaOverride`, `SimulateDeepLink`, `QueuePermissionResult` / `SetPermissionCheckResult`, `QueueAttResult`, `DismissAllOverlays`. Drives runtime services via the `internal` editor hooks documented under §2 below.
- **Editor-only runtime hooks** (consumed only when `UNITY_EDITOR`): `BatteryService.EditorLowPowerModeOverride` + `SimulateLowPowerModeChanged()`, `SafeAreaService.EditorSafeAreaOverride` + `SimulateSafeAreaChanged()`, `DeepLinkService.SimulateLinkActivated(Uri)`, `PermissionsService.EditorCheckOverride` / `EditorRequestOverride`, `AttService.EditorCurrentStatusOverride` / `EditorRequestResultOverride`. All gated behind `#if UNITY_EDITOR` so player builds carry none of this surface.
- **Internal introspection accessors on runtime services** (not part of the public surface; visible to the Editor asm via `InternalsVisibleTo` on `Runtime/AssemblyInfo.cs`): `HapticsService.CurrentPreset` / `CurrentDurationSeconds` / `Backend`; `MobileNotificationService.CurrentMode` / `Channels`; `PermissionsService.CheckSnapshot()`. Add similar `internal` accessors for any new service surfaced in the Device Simulator panel rather than widening public API.
- **Centralised haptic envelopes** (`Runtime/Haptics/Internal/HapticEnvelopes.cs`): the per-preset `(timings, amplitudes)` tables that previously lived only inside the `UNITY_ANDROID && !UNITY_EDITOR` block of `AndroidHapticsBackend` now live in this always-compiled internal class. The Android backend and the Device Simulator panel's envelope graph both read from it — single source of truth.
- **`MobileServicesSettings`** (`Editor/Settings/MobileServicesSettings.cs`): `ScriptableSingleton` persisted to `ProjectSettings/MobileServicesSettings.asset`. Holds per-permission iOS usage descriptions (per-locale `LocaleEntry` rows; English mandatory), ATT usage description, capability toggles, Android manifest opt-ins, the CI-mode `AllowPlaceholderUsageDescriptions` toggle, and the `EnableRuntimeSimulatorOverlay` opt-in for the in-Game-view simulator overlay. Surfaced via `MobileServicesSettingsProvider` at `Edit > Project Settings > GameLovers > Mobile Services` (UIToolkit) with live missing-keys badge, project-scan button (uses `MobileServicesScanner`), privacy-nutrition-label draft generator, and an `Editor tooling` section housing the runtime-overlay toggle. Critical: needs `using UnityEngine;` per workspace `ScriptableSingleton and [SerializeField]` rule.
- **`MobileServicesScanner`** (`Editor/Settings/MobileServicesScanner.cs`): reflection-based scan over the project's user assemblies looking for references to runtime service types (`MobileNotificationService`, `DeepLinkService`, `IosAudioSessionService`, `IPermissionsService`/`PermissionsService`, `IAttService`/`AttService`, `NativeUiService`). Returns a `ProjectScanResult` consumed by the Settings Provider and the build postprocessor.
- **`MobileServicesBuildPostprocessor`** (`Editor/Build/MobileServicesBuildPostprocessor.cs`): implements `IPostprocessBuildWithReport`. iOS path mutates the post-build Xcode project via `PlistDocument` + `ProjectCapabilityManager` (entitlements file `GameLoversMobileServices.entitlements`). Android path patches `Assets/Plugins/Android/mainTemplate.xml` with the configured `<uses-permission>` entries + share-chooser `<queries>` block. Idempotent on re-runs. Fail-by-default validation throws `BuildFailedException` listing every missing usage description; soft mode injects placeholder strings instead.

### Samples (`Samples~/`)
- Four code-only samples.
- `Samples~/MobileServicesPlayground/` — kitchen-sink runtime-built canvas covering every subsystem. Sample-only types in namespace `GameLovers.MobileServices.Samples.MobileServicesPlayground`.
- `Samples~/HapticsPalette/` — designer iteration tool. Namespace `GameLovers.MobileServices.Samples.HapticsPalette`.
- `Samples~/NotificationsScheduler/` — lifecycle demo. Namespace `GameLovers.MobileServices.Samples.NotificationsScheduler`.
- `Samples~/DeepLinkRouter/` — `IDeepLinkRouter.MapRoute` pattern demo. Namespace `GameLovers.MobileServices.Samples.DeepLinkRouter`.
- **Code-only sample policy**: divergence from peer `com.gamelovers.services` / `com.gamelovers.uiservice` which ship `.unity` + `.prefab` files with hand-authored deterministic GUIDs. Mobile samples build their UI at runtime via legacy `UnityEngine.UI` — zero asset dependencies, no `.meta` GUIDs to maintain, easy diff. The trade-off is no built-in scene hierarchy or prefab structure for the user to inspect; this is acceptable for the mobile surface (most behaviour is fired by buttons, not configured by serialised state).
- `Samples~/README.md` is the index; per-sample `README.md` documents setup + the sample-only types contract.
- `package.json` carries a `samples[]` block — adding a new sample requires updates in lockstep across `package.json`, `Samples~/README.md`, the per-sample `README.md`, and the matching `AGENTS.md` row (this list).

## 3. Layout convention

Section §2 names every public type and the assembly it lives in. Use that plus your IDE / `find` / `Glob` for the actual inventory — the conventions below are what's load-bearing.

- **One folder per subsystem under `Runtime/`** — `NativeUi/`, `Notifications/`, `Gestures/`, `Haptics/`, `Device/`. Each subsystem owns one C# namespace (`GameLovers.MobileServices.<Subsystem>`).
- **Sub-folders inside a subsystem are organizational only**, NOT namespace-nesting. Examples: `Runtime/Notifications/{Android,iOS,Internal}/` and `Runtime/Device/{Audio,State,Permissions,Tracking,DeepLinks,Internal}/` all use their parent subsystem's namespace. C# enforces the namespace via the `namespace` keyword in each file, not via folder paths.
- **`Internal/` sub-folders hold non-public types** (platform backends, MonoBehaviour hosts, callback receivers, serializable DTOs). Use the `internal` access modifier; tests reach in through `Runtime/AssemblyInfo.cs` which grants `InternalsVisibleTo("GameLovers.MobileServices.{Edit,Play}Mode.Tests")` plus `GameLovers.MobileServices.Editor` for the Device Simulator panel's introspection wedge. No `Editor.Tests` grant — editor tooling is not automated-tested (see `Tests/AGENTS.md`).
- **Editor folder layout** — `Editor/Explorer/{Overlays,DeviceSimulatorPanel}/` + `Editor/Simulation/` + `Editor/Settings/` + `Editor/Build/`. Editor asmdef name is `GameLovers.MobileServices.Editor`. The simulator broker + overlay bootstrap + mock builders use the `GameLovers.MobileServices.Editor.Explorer.Overlays` namespace, the `DeviceSimulatorPlugin` lives in `GameLovers.MobileServices.Editor.Explorer.DeviceSimulatorPanel`, the simulator façade uses `GameLovers.MobileServices.Editor.Simulation`. (The standalone Explorer window + its `Tabs/`/`Windows/` folders were removed in the 1.0.0 consolidation onto the Device Simulator plugin.) Honour the workspace `UnityEditor.Editor` namespace-collision rule for any Unity inspector base classes (qualify as `UnityEditor.Editor`).
- **Native bridges live in `Plugins/iOS/<Subsystem>.m`** — one `.m` per subsystem, paired with a backend C# class that owns the `[DllImport("__Internal")]` declarations and routes through it. iOS-side preset/permission/status enums in the `.m` file MUST mirror the C# enum integer values one-to-one; see Phase 5's `GLAppPermission` / `GLPermissionStatus` and Phase 2's `GLHapticPresetId` for the pattern.
- **`UnitySendMessage` GameObject names are contracts** — the iOS `.m` files address `DeviceServicesHost`, `PermissionsCallbackReceiver`, and `AttCallbackReceiver` by string. Renaming the C# `MonoBehaviour` requires updating the matching `.m` file.
- **Tests** live under `Tests/{EditMode,PlayMode}/` with one asmdef each. **Editor tooling is not automated-tested** — types under `Editor/` (`MobileServicesDeviceSimulatorPlugin`, `MobileSimulatorRuntimeOverlay`, `MobileSimulatorState`, `MockBuilders`, `EditorPlatformSimulator`, `MobileServicesSettings*`, `MobileServicesScanner`, `MobileServicesBuildPostprocessor`) are validated by manual editor smoke + on-device builds; see `Tests/AGENTS.md` §1 and §9 for the policy and rationale. Runtime tests do NOT mirror the runtime folder structure — group by feature, not by source path.

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
- **Runtime simulator overlay is edit+play-capable and Editor-asmdef-owned**
  - `MobileSimulatorRuntimeOverlay` lives in the Editor asmdef and spawns a `[EditorOnly]` GameObject (UIDocument is `[ExecuteAlways]`, so it paints in the edit-mode Game / Simulator view too). A single idempotent `RefreshLifecycle()` keeps it alive when the Device Simulator plugin panel is open (`NotifyPluginActive`, edit OR play) OR when in play mode with `MobileServicesSettings.EnableRuntimeSimulatorOverlay` on. `DontDestroyOnLoad` is only called in play mode; `DestroyStaleHosts()` de-dups after a domain reload. Interaction inside the mock is unreliable in the edit-mode Game view, so dismissal is driven from the plugin panel's per-section dismiss buttons (the overlay is display-only). The `[EDITOR SIMULATOR]` watermark is shown only while `MobileSimulatorState.Enabled` is on.
  - **Overlay PanelSettings scale**: the mock USS (`MobileSimulator.*.uss`) is authored in logical-point units, so the overlay's `PanelSettings` MUST use `scaleMode = ScaleWithScreenSize` with `referenceResolution = (390, 844)` (a logical-phone size). The Device Simulator reports `Screen.width/height` in PHYSICAL pixels, so this yields a scale ≈ the device's native scale (~3x) → 1 USS px ≈ 1 iOS point. Using `ConstantPixelSize` (1 USS px = 1 device px) makes every mock render ~1/3 size on a 3x screen — the symptom is "alerts/toasts are tiny." Do not switch the scale mode back.
  - Do NOT subscribe to `MobileSimulatorState` events from a non-editor assembly expecting them to fire in a player build — the broker, the events, and the overlay all live in `GameLovers.MobileServices.Editor`. The runtime `UIDocument` it spawns is a real runtime component but exists in-editor only.
  - `PanelSettings.sortingOrder = short.MaxValue` resolves ties via GameObject name lexicographic order; the host GameObject's leading `[` puts it near the top of any sort. If a consumer pins a competing UIDocument to the same sortingOrder *and* names it with a leading character that sorts after `[`, the overlay loses the tie — acceptable, documented.

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
- Public API changes (`NativeUiService`, `INativeUiService`, `INotificationService` + `NotificationBuilder`, `IGameNotification`, `GestureController` events, `IHapticsService`, `IDeviceService` and any of its child interfaces, `IDeepLinkRouter`, `IMobileService`)
- Platform integration changes (JNI calls, iOS native symbols in any `Plugins/iOS/*.m` file, notification platform wrappers, `UnitySendMessage` GameObject names)
- Notification queueing/persistence behavior changes (`OperatingMode`, PlayerPrefs payload shape)
- Gesture detection logic or input source integration changes
- Haptic preset envelopes (`HapticPreset` enum + per-preset time/amplitude tables in `HapticEnvelopes` and per-preset routing in `Plugins/iOS/Haptics.m`)
- Permissions catalogue changes (`AppPermission` enum + `AndroidManifestPermission` mapping + iOS `_GameLoversPermissionsRequest` switch)
- Editor surface changes (`MobileServicesDeviceSimulatorPlugin` panel layout / foldouts / diagnostics, `EditorPlatformSimulator` API, internal introspection accessors, `MobileSimulatorState` broker shape, simulator USS / overlay payloads, `MobileSimulatorRuntimeOverlay` lifecycle / auto-platform-sync behaviour)
- `MobileServicesSettings` schema (new `[SerializeField]` rows on the asset — current set: usage descriptions, ATT usage, capability toggles, Android manifest toggles, `AllowPlaceholderUsageDescriptions`, `ScanPopulatedCapabilities`, `EnableRuntimeSimulatorOverlay`), settings panel layout, project scanner detection rules, build postprocessor mutation logic (Info.plist keys, entitlements capabilities, Android manifest entries, queries block)
- `docs/` structure changes (new file added, file deleted, file renamed) → update `docs/README.md` index AND the matching link table row in the main `README.md` "Related docs" section
- Sample folder structure or sample-only types change → update `Samples~/README.md`, per-sample `README.md`, `package.json` `samples[]` block, AND the AGENTS.md Samples row, in lockstep

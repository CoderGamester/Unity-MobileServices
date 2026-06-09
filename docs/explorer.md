# Mobile Services Device Simulator Panel & Overlay

All Mobile Services editor tooling lives inside **Unity's Device Simulator** (`Window > General > Device Simulator`). There is no separate Explorer or Simulator window — a single **Mobile Services** panel provides the controls, the live diagnostics, and the haptic envelope graph, and an in-Game-view overlay paints the platform-shaped mocks **inside the simulated phone screen** (edit and play mode).

## The Mobile Services panel

Open `Window > General > Device Simulator`, pick a device profile, and the **Mobile Services** panel appears automatically in the Control Panel (`UnityEditor.DeviceSimulation.DeviceSimulatorPlugin` — Unity auto-discovers it; no menu item, no enable flow). The platform skin auto-syncs from the selected device profile (it reads `Application.platform`, which the Device Simulator spoofs for iOS / Android picks), so there is no platform toggle to keep in sync.

| Foldout | What it surfaces / drives |
|---------|---------------------------|
| **Native UI** | Alert (modal + sheet), toast (short/long), share, review. Title/message/text fields author the mock; buttons push the platform-shaped mock to the overlay AND call the real `NativeUiService` static methods (no-op in editor). The Action Sheet button greys out on Android (no native sheet idiom). |
| **Haptics** | 9 preset buttons that plot the preset's **intensity-over-time curve** — a `Painter2D` line/area graph with **X = time (ms)** and **Y = intensity (0–1)**, drawn as a step waveform from the canonical `HapticEnvelopes` tables (the same data the Android backend feeds to `VibrationEffect.createWaveform`), with axis ticks. Haptics fire only on a physical device, so there are deliberately no play / stop controls here; the graph is the editor calibration cue. |
| **Notifications** | A single **Show heads-up banner** preview (edit mode) that pushes the mock to the overlay, like the Native UI mocks. Live scheduling / channels / queueing / pending were removed — they ran on a throwaway `MobileNotificationService` disconnected from your game and duplicated the `NotificationsScheduler` sample (which drives the game's own service). |
| **Gestures** | Last-detected swipe (direction / velocity / sameness / start+end) and last-detected tap (position / duration). In Play mode it uses a scene `GestureController` if one exists, otherwise **auto-spawns a hidden one** (and enables Input System Touch Simulation) so it works with zero setup — swipe / tap anywhere. |
| **Permissions** | A per-`AppPermission` **state dropdown** — pick `Granted` / `Denied` / `NotDetermined` / `Restricted` and the running game / sample reads exactly that through `Check()` and `RequestAsync()` (the dropdown sets both the editor Check and next-Request overrides). Plus a `Reset all to default` button. No OS-prompt mock button — the prompt only makes sense at runtime when the game actually requests (drive it from your code or the Playground sample). |
| **App Tracking Transparency** | A single **status dropdown** — pick the `AttStatus` and the running game / sample reads it through `CurrentStatus` / `RequestAuthorizationAsync()`. Plus a `Reset to default (Authorized)` button. No prompt mock button (same rationale as Permissions). |

Deep links are intentionally **not** a panel foldout: `DeepLinkService.SimulateLinkActivated` is instance-scoped (no static override like Permissions/ATT), so the panel could only ever fire into a throwaway instance it owns — never your game's live service. Drive deep links from the `DeepLinkRouter` sample, or call `EditorPlatformSimulator.SimulateDeepLink(uri, yourService)` from your own bootstrap.

The header carries an **Editor Simulator** master-switch toggle: it enables/disables every section below as a group and shows/hides the in-Game-view `[EDITOR SIMULATOR]` banner (state persisted to `EditorPrefs`); turning it off also clears any visible mock. Dismissal lives per-section — **Dismiss all UIs** in the Native UI foldout and **Dismiss Banner** in the Notifications foldout. Controls that drive live state (permission / ATT, and Gestures — which auto-attaches a `GestureController` in Play mode) need **Play mode**; the mock previews and the envelope graph work in **edit mode**. Each gated foldout also shows its own inline "Enter Play mode to enable these controls." banner so the reason its buttons are greyed is obvious when you expand it.

**Play-mode gating**: controls that drive live device state are **greyed out in edit mode** and re-enabled on entering Play — the **Permission / ATT state dropdowns** (their static overrides are only read by a running service, and a domain reload on entering Play would wipe an edit-mode setting anyway). An amber banner at the top of the panel explains this, and auto-hides once you're in Play mode. Edit-mode-safe controls — every native-UI mock push and the haptic preset buttons + envelope graph — stay enabled (they render to the overlay / graph without a running game).

## The simulator overlay (the canvas)

`MobileSimulatorRuntimeOverlay` is an editor-only `[InitializeOnLoad]` bootstrap that spawns a `[EditorOnly] MobileSimulatorOverlay` GameObject carrying a `UIDocument` with `PanelSettings.sortingOrder = short.MaxValue`. It renders **inside the Game / Simulator view at the simulated device's pixel grid**, so an "iOS top-banner toast" sits at the top of the simulated iPhone screen — not at the top of the editor's Game window.

- **Alive while the panel is open** — the plugin calls `MobileSimulatorRuntimeOverlay.NotifyPluginActive(true/false)` on create / destroy, so the overlay exists exactly while the Device Simulator panel is open, in **edit mode and play mode**. `UIDocument` is `[ExecuteAlways]`, so it paints in the edit-mode Game view too. Fire a mock from the panel without entering play mode and it renders immediately.
- **Display-only in edit mode** — runtime-panel pointer input is unreliable in the edit-mode Game view, so the mock's own buttons are not relied upon; dismissal is driven from the panel's per-section dismiss buttons (**Dismiss all UIs** / **Dismiss Banner**).
- **Standalone play-mode spawn (opt-in)** — independently of the panel, the overlay also spawns on its own during play mode when `Project Settings > GameLovers > Mobile Services > Editor tooling > Enable runtime simulator overlay` is on (default OFF), so the mocks render in a plain Game view even without the Device Simulator window open.
- **Composes with Unity's Device Simulator** — pick "iPhone 15 Pro" in the Device Simulator, and the mocks render at the right scale and safe-area inset for that device.

The same mock payloads — alerts, action sheets, toasts, share sheets, review prompts, heads-up banners, permission / ATT dialogs — are built by `MockBuilders` and skinned by the three USS files (`MobileSimulator.Common.uss` / `.iOS.uss` / `.Android.uss`); the root element toggles `platform-ios` / `platform-android` so USS rules can scope on either.

## Editor Simulator watermark

The overlay paints an `[EDITOR SIMULATOR]` watermark in the Game view while the simulator is enabled (the header **Editor Simulator** toggle / `MobileSimulatorState.Enabled`). It exists to prevent the "looked fine in editor, broke on device" trust collapse — it is tied to the master switch, so it disappears only when the whole simulator is turned off (which also disables every panel control).

## `EditorPlatformSimulator` static API

For code-driven tests / scripted automation, `GameLovers.MobileServices.Editor.Simulation.EditorPlatformSimulator` exposes:

```csharp
EditorPlatformSimulator.SetIosLowPowerMode(true, batteryService);
EditorPlatformSimulator.SetSafeArea(new Rect(0, 100, w, h-200), safeAreaService);
EditorPlatformSimulator.ClearSafeAreaOverride(safeAreaService);
EditorPlatformSimulator.SimulateDeepLink(new Uri("myapp://promo/x"), deepLinkService);
EditorPlatformSimulator.QueuePermissionResult(AppPermission.Camera, PermissionStatus.Denied);
EditorPlatformSimulator.SetPermissionCheckResult(AppPermission.Camera, PermissionStatus.Restricted);
EditorPlatformSimulator.QueueAttResult(AttStatus.Authorized);
EditorPlatformSimulator.DismissAllOverlays();
```

Each method either sets an internal static override on the runtime service (under `#if UNITY_EDITOR`) or pushes a payload through the `MobileSimulatorState` broker.

## What's NOT mirrored

Out of scope (deliberate):

- **Audio proxy for haptics** (low-frequency oscillator burst per preset).

The envelope graph is the calibration cue for haptics; designers iterate haptic feel on a paired device through the `HapticsPalette` sample. Device-frame overlays (iPhone 15 Pro / Pixel 8 cutout outlines, safe-area inset, `Application.platform` spoofing) are handled by Unity's Device Simulator natively — this package composes with it rather than reimplementing it.

## Comparison with Unity's Device Simulator

Unity's built-in **Device Simulator** and this package's tooling are **complementary** — in fact this package's panel *lives inside* Unity's Device Simulator.

### What Unity's Device Simulator does (and does well)

- Wraps the Game view in a device frame (~30 device profiles) with the correct screen aspect, notch / dynamic island cutout, and safe-area inset.
- Overrides Unity-level APIs to match the chosen device: `Screen.safeArea`, `Screen.dpi`, `Screen.width/height`, `Screen.orientation`, `Application.platform`, `SystemInfo.deviceModel`, etc.
- Triggers `#if UNITY_IOS` / `#if UNITY_ANDROID` runtime branches by spoofing `Application.platform`.
- Mouse-as-touch input, orientation flip, foreground/background pause toggles.

### What it doesn't do (the gap this package fills)

Native OS surfaces — `UIAlertController`, `UIActivityViewController`, `SKStoreReviewController`, `UNUserNotificationCenter` heads-up banners, the iOS permission prompt, the ATT prompt, Android's Material dialog and Toast — **live in the OS process, not in Unity's renderer**. The Device Simulator can't paint them because they don't exist in the editor at all. When `NativeUiService.ShowAlertPopUp(...)` is called in the editor, Unity's Device Simulator has nothing to show.

| Surface | Unity Device Simulator | Mobile Services overlay |
|---------|------------------------|--------------------------|
| iOS alert / sheet (`ShowAlertPopUp`) | — | iOS-styled mock with the supplied buttons |
| Android Material dialog | — | mock card with the supplied buttons |
| Toast (iOS / Android) | — | top-pill / bottom-pill mock |
| Share sheet | — | iOS grid / Android list mock |
| Review prompt | — | `SKStoreReviewController`-style mock |
| Permission dialog | — | dialog rendered with project-configured `NSUsageDescription` |
| ATT dialog | — | dialog rendered with `NSUserTrackingUsageDescription` |
| Heads-up notification banner | — | iOS / Android heads-up card with app icon, app-name + time header, bold title + body |
| Device frame + bezels | full library | — |
| Touch input via mouse | yes | — |
| `Application.platform` spoofing | yes | — (consumes it to auto-skin) |

The configured-usage-description piece is the strongest unique value: Unity's Device Simulator can't know what text Apple's review team will read for `NSCameraUsageDescription`, but the Mobile Services panel reads it from `MobileServicesSettings` and surfaces it in the dialog mock — making the editor preview match what'll appear on the device.

### Acknowledged overlap

A few pieces of `EditorPlatformSimulator` overlap with Unity's Device Simulator. They earn their keep specifically for **test/automation paths** that Unity's interactive-only simulator doesn't expose.

| Feature | Unity Device Simulator | Why this package still ships it |
|---------|------------------------|--------------------------------|
| `EditorPlatformSimulator.SetSafeArea` | Richer (real device-accurate cutouts via device picker) | Programmatic API — drives `SafeAreaService.OnSafeAreaChanged` deterministically from unit tests |
| `EditorPlatformSimulator.SetIosLowPowerMode` | "Low Battery" toggle in newer versions | Programmatic — fires `BatteryService.OnLowPowerModeChanged` for tests |

If your iteration loop is interactive (designer-paired phone or just clicking around), Unity's Device Simulator wins for safe-area / platform / device-frame work. If your iteration loop is scripted (CI tests, automated previews), the `EditorPlatformSimulator` API is the path. Use both — they don't conflict.

## Recommended workflow

1. Open Unity's Device Simulator (`Window > General > Device Simulator`) and pick a target device. The **Mobile Services** panel appears in the Control Panel automatically.
2. Fire mocks from the panel — they render inside the simulated phone screen immediately, in edit mode. No play-mode round-trip and no second window to dock.
3. Press Play to drive the live-state controls (permission / ATT state, last gesture) that need a running consumer.
4. For scripted automation / CI: skip the UI and call `EditorPlatformSimulator.Set*` / `Queue*` directly from your test setup.

## When to use which

| Need | Use |
|------|-----|
| Preview what an iOS alert / toast / share / review will look like (no play mode) | Mobile Services panel Native UI buttons — fire the mock into the overlay |
| Render the mock inside the simulated phone screen at correct scale | Device Simulator device profile + the overlay (alive while the panel is open) |
| Watch live service state during play mode | Mobile Services panel diagnostics (Play mode) |
| Set a permission / ATT state your running game reads | Mobile Services panel — the Permissions / ATT state dropdowns |
| Drive a permission Request result for a unit test | `EditorPlatformSimulator.QueuePermissionResult` |
| Test that your code subscribes to `OnLowPowerModeChanged` correctly | `EditorPlatformSimulator.SetIosLowPowerMode(true, batteryService)` |
| Render mocks in a plain Game view during play without the Device Simulator open | `Project Settings > GameLovers > Mobile Services > Editor tooling > Enable runtime simulator overlay` |
| Demo a deep link routing flow without launching from the OS | DeepLinkRouter sample, or `EditorPlatformSimulator.SimulateDeepLink(uri, yourService)` |

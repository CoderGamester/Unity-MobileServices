# Mobile Services Explorer & Truth-Mirror Simulator

Two editor windows ship with the package, both designed to be docked next to (or behind) the Game View while iterating on mobile UI surfaces.

## Mobile Services Explorer

`Tools > GameLovers > Mobile Services Explorer`

Dockable `EditorWindow` (`MobileServicesExplorerWindow`) with eight tabs and a top-row `Render as: iOS | Android` dropdown.

| Tab | What it surfaces / drives |
|-----|---------------------------|
| **Overview** | Card grid with one card per other tab. "Open" button on each card jumps to that tab. |
| **Native UI** | Test alert (modal + sheet variants), toast, review, share. Buttons push the platform-shaped mocks to the simulator window AND call the real `NativeUiService` static methods (no-op in editor). |
| **Haptics** | 9 preset buttons + custom intensity / duration row + loop controls + stop. **Envelope graph** plots `(timings_ms, amplitudes_0_to_1)` from the canonical `HapticEnvelopes` tables (same data the Android backend feeds to `VibrationEffect.createWaveform`). |
| **Notifications** | Live `PendingNotifications` list, channel display, current `OperatingMode`. "Schedule test in 1s / 5s / 30s" buttons that surface a heads-up banner mock on the simulator at the simulated delivery moment. |
| **Gestures** | Last-detected swipe (direction / velocity / sameness / start+end) and last-detected tap (position). Finds the live `GestureController` via `Object.FindFirstObjectByType`. |
| **Device** | Live battery / connectivity / safe-area / KeepAwake / LPM read-outs plus a simulator panel: LPM toggle, connectivity dropdown, notch-inset apply / clear, DeviceService initialise / dispose. |
| **Permissions** | 7-row grid (one per `AppPermission`): live status pill, Check, Request, Simulate-next-result dropdown, Show-Mock button. |
| **ATT + Deep Link** | ATT status, Request, Show-Mock, Simulate-next-result dropdown. Deep-link inspector with `PendingColdStartLink` label, last-delivered URI label, URI input + Send-test-link button, Initialise-DeepLinkService button. |

### Implementation notes

- Each tab subclasses `MobileServiceTab` (mirrors `ServiceTab` in `com.gamelovers.services`).
- Sticky-foldout state, digest-short-circuit, play-mode-aware refresh — all the workspace UIToolkit gotcha hardening is in the base.
- The Explorer drives the simulator window via `MobileSimulatorState` (singleton broker / event bus). Tabs call `MobileSimulatorState.Push*` methods; the simulator subscribes and renders.

## Three rendering surfaces

The same mock payloads — alerts, toasts, share sheets, review prompts, heads-up banners, permission / ATT dialogs — can be painted on **three different targets**, all driven by the same `MobileSimulatorState` broker. Pick the one(s) that fit your iteration loop:

| Surface | When alive | Renders at |
|---|---|---|
| `MobileSimulatorWindow` | Edit-mode + Play-mode | Standalone dockable EditorWindow pixel grid |
| `MobileSimulatorRuntimeOverlay` (opt-in) | Play-mode only | Inside the Game / Simulator view, at the simulated device's pixel grid |
| `MobileServicesDeviceSimulatorPlugin` | Unity's Simulator view only | Control Panel **inside** Unity's Device Simulator window |

All three subscribe to the same `MobileSimulatorState` events, so pushing a mock from the Explorer fans out to whichever surfaces are currently alive — there's no risk of "drove the wrong one." The three USS files (`MobileSimulator.Common.uss` / `.iOS.uss` / `.Android.uss`) ship once in `Editor/Explorer/Overlays/` and are reused by both the window and the runtime overlay; the root element toggles `platform-ios` / `platform-android` classes so USS rules can scope on either.

### 1. `MobileSimulatorWindow` (edit-mode preview)

`Tools > GameLovers > Mobile Services Simulator Window`

Dockable EditorWindow that paints the platform-shaped mocks listed below. Alive in both edit mode and play mode. Use this when you want to preview a mock without entering play mode, or when you've docked it next to (instead of inside) the Game view.

- iOS centered alert / bottom action sheet, Android Material 3 dialog
- iOS top-banner toast / Android bottom-pill toast
- iOS share grid / Android share list
- iOS-style `SKStoreReviewController` rating prompt
- iOS heads-up notification banner / Android heads-up card
- iOS permission dialog + ATT dialog using the project's configured `NS*UsageDescription` text

### 2. `MobileSimulatorRuntimeOverlay` (in-Game-view, play-mode-only, opt-in)

Editor-only `[InitializeOnLoad]` bootstrap that — when enabled — spawns a `DontDestroyOnLoad` GameObject `[EditorOnly] MobileSimulatorOverlay` carrying a UIDocument with `PanelSettings.sortingOrder = short.MaxValue` whenever Unity enters play mode. The overlay renders **inside the Game / Simulator view at the simulated device's pixel grid**, so an "iOS top-banner toast" sits at the top of the simulated iPhone screen — not at the top of the editor's Game window.

**Opt-in**: `Project Settings > GameLovers > Mobile Services > Editor tooling > Enable runtime simulator overlay`. Default OFF. Toggling on requires a play-mode restart to pick up the change.

**Lifetime**: spawned on `EnteredPlayMode`, destroyed instantly on `ExitingPlayMode` (clean teardown — no paused-snapshot preservation).

**Composes with Unity's Device Simulator**: pick "iPhone 15 Pro" in `Window > General > Device Simulator`, enable the overlay setting, press Play — the mocks render at the right scale and safe-area inset for that device.

### 3. `MobileServicesDeviceSimulatorPlugin` (Control Panel, Simulator view only)

`UnityEditor.DeviceSimulation.DeviceSimulatorPlugin` subclass that embeds a slim action-button Control Panel inside Unity's Device Simulator window. Unity auto-discovers the plugin — no menu item, no enable flow.

The panel groups action buttons by subsystem (Native UI / Notifications / Device state / Permissions / ATT / Deep Links) and includes a top-row `Open full Explorer →` button that jumps to the heavyweight diagnostic surface when needed. The plugin **auto-syncs `MobileSimulatorState.Platform`** from the selected device profile (reads `Application.platform`, which Unity's Device Simulator spoofs); while it's alive, the Explorer's `Render as: iOS | Android` dropdown greys out to signal "platform is driven by the Simulator view now."

### Persistent watermark

All three surfaces carry a non-removable `[EDITOR SIMULATOR]` watermark. **By design** — prevents the "looked fine in editor, broke on device" trust collapse. Do not try to hide it.

## `EditorPlatformSimulator` static API

For code-driven tests / scripted automation, `GameLovers.MobileServices.Editor.Simulation.EditorPlatformSimulator` exposes:

```csharp
EditorPlatformSimulator.SetIosLowPowerMode(true, batteryService);
EditorPlatformSimulator.SetSafeArea(new Rect(0, 100, w, h-200), safeAreaService);
EditorPlatformSimulator.ClearSafeAreaOverride(safeAreaService);
EditorPlatformSimulator.SetConnectivity(NetworkReachability.NotReachable, connectivityService);
EditorPlatformSimulator.SimulateDeepLink(new Uri("myapp://promo/x"), deepLinkService);
EditorPlatformSimulator.QueuePermissionResult(AppPermission.Camera, PermissionStatus.Denied);
EditorPlatformSimulator.SetPermissionCheckResult(AppPermission.Camera, PermissionStatus.Restricted);
EditorPlatformSimulator.QueueAttResult(AttStatus.Authorized);
EditorPlatformSimulator.DismissAllOverlays();
```

Each method either sets an internal static override on the runtime service (under `#if UNITY_EDITOR`) or pushes a payload through `MobileSimulatorState`.

## What's NOT mirrored

Out of scope (deliberate):

- **Audio proxy for haptics** (low-frequency oscillator burst per preset).

The envelope graph is the calibration cue for haptics; designers iterate haptic feel on a paired device through the `HapticsPalette` sample. Device-frame overlays (iPhone 15 Pro / Pixel 8 cutout outlines, safe-area inset, `Application.platform` spoofing) are handled by Unity's Device Simulator natively — see the comparison section below; this package composes with it rather than reimplementing it.

## Comparison with Unity's Device Simulator

Unity ships a built-in **Device Simulator** (`Window > General > Device Simulator`). The two tools are **complementary, not competitive** — use both together.

### What Unity's Device Simulator does (and does well)

- Wraps the Game view in a device frame (iPhone 15 Pro, Pixel 8, etc. — ~30 device profiles built in) with the correct screen aspect, notch / dynamic island cutout, and safe-area inset.
- Overrides Unity-level APIs to match the chosen device: `Screen.safeArea`, `Screen.dpi`, `Screen.width/height`, `Screen.orientation`, `Application.platform`, `SystemInfo.deviceModel`, etc.
- Triggers `#if UNITY_IOS` / `#if UNITY_ANDROID` runtime branches by spoofing `Application.platform`.
- Mouse-as-touch input, orientation flip, foreground/background pause toggles.

### What it doesn't do (the gap this package fills)

Native OS surfaces — `UIAlertController`, `UIActivityViewController`, `SKStoreReviewController`, `UNUserNotificationCenter` heads-up banners, the iOS permission prompt, the ATT prompt, Android's Material dialog and Toast — **live in the OS process, not in Unity's renderer**. The Device Simulator can't paint them because they don't exist in the editor at all. When `NativeUiService.ShowAlertPopUp(...)` is called in the editor, Unity's Device Simulator has nothing to show.

| Surface | Unity Device Simulator | Mobile Simulator Window |
|---------|------------------------|--------------------------|
| iOS alert / sheet (`ShowAlertPopUp`) | — | iOS-styled mock with the supplied buttons |
| Android Material dialog | — | mock card with the supplied buttons |
| Toast (iOS / Android) | — | top-pill / bottom-pill mock |
| Share sheet | — | iOS grid / Android list mock |
| Review prompt | — | `SKStoreReviewController`-style mock |
| Permission dialog | — | dialog rendered with project-configured `NSUsageDescription` |
| ATT dialog | — | dialog rendered with `NSUserTrackingUsageDescription` |
| Heads-up notification banner | — | iOS pill / Android card |
| Device frame + bezels | full library | — |
| Touch input via mouse | yes | — |
| `Application.platform` spoofing | yes | — |

The configured-usage-description piece is the strongest unique value: Unity's Device Simulator can't know what text Apple's review team will read for `NSCameraUsageDescription`, but the Mobile Simulator reads it from `MobileServicesSettings` and surfaces it in the dialog mock — making the editor preview match what'll appear on the device.

### Acknowledged overlap

Three pieces of the package's editor tooling do overlap with Unity's Device Simulator. They earn their keep specifically for **test/automation paths** that Unity's interactive-only simulator doesn't expose. Note also that `MobileServicesDeviceSimulatorPlugin` is an _extension_ of Unity's Device Simulator (it registers via the official `DeviceSimulatorPlugin` API), not an overlap.

| Feature | Unity Device Simulator | Why this package still ships it |
|---------|------------------------|--------------------------------|
| `EditorPlatformSimulator.SetSafeArea` | Richer (real device-accurate cutouts via device picker) | Programmatic API — drives `SafeAreaService.OnSafeAreaChanged` deterministically from unit tests |
| `EditorPlatformSimulator.SetIosLowPowerMode` | "Low Battery" toggle in newer versions | Programmatic — fires `BatteryService.OnLowPowerModeChanged` for tests |
| Explorer's `Render as: iOS \| Android` toggle | Richer (per-device + per-platform via picker) | Only swaps the USS skin for the mock dialogs; orthogonal to platform spoofing |

If your iteration loop is interactive (designer-paired phone or just clicking around), Unity's Device Simulator wins for safe-area / platform / device-frame work. If your iteration loop is scripted (CI tests, automated previews), the `EditorPlatformSimulator` API is the path. Use both — they don't conflict.

### Recommended workflow

**For most iteration loops** (designer paired with phone, or just clicking around):

1. Open Unity's Device Simulator (`Window > General > Device Simulator`) and pick a target device. The Mobile Services plugin panel appears in the Control Panel automatically — fire mocks from there next to the simulated phone screen.
2. Enable `Project Settings > GameLovers > Mobile Services > Editor tooling > Enable runtime simulator overlay` if you want the mocks to render **inside** the simulated phone screen at the correct safe area / scale.
3. Press Play — the runtime overlay spawns and renders the mocks inside the Game view.
4. Keep the Explorer dockable handy for live-state diagnostics (haptic envelope graph, gesture last-event, pending notifications list, etc.) — open `Tools > GameLovers > Mobile Services Explorer` whenever you need it.

**For scripted automation / CI**: skip the windows entirely and call `EditorPlatformSimulator.Set*` / `Queue*` directly from your test setup.

## When to use which

| Need | Use |
|------|-----|
| Watch live service state during play mode | Explorer |
| Drive a permission Request result for a unit test | `EditorPlatformSimulator.QueuePermissionResult` |
| Preview what an iOS alert / ATT prompt will look like | Simulator window or runtime overlay |
| Render the mock inside the simulated phone screen at correct scale | Runtime overlay (`Editor tooling > Enable runtime simulator overlay`) + Unity's Device Simulator |
| Fire a mock without leaving the simulated phone view | Device Simulator plugin panel (Control Panel) |
| Test that your code subscribes to `OnLowPowerModeChanged` correctly | `EditorPlatformSimulator.SetIosLowPowerMode(true, batteryService)` |
| Demo a deep link routing flow without launching from the OS | DeepLinkRouter sample + Explorer's "Send test link" button |

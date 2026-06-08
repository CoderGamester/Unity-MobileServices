# Mobile Services Samples

Four samples ship with `com.gamelovers.mobileservices`.

| Sample | Purpose | Setup |
|--------|---------|-------|
| **MobileServicesPlayground** | Kitchen-sink wiring proof. Buttons fire every API call across every subsystem; the canvas wraps a safe-area panel so the playground also doubles as a safe-area visualizer. | Drop `MobileServicesPlaygroundUI` on a GameObject. |
| **HapticsPalette** | Designer iteration tool. 3x3 preset grid + sequence recorder for "tune the feel" loops on a paired device. | Drop `HapticsPaletteUI` on a GameObject. |
| **NotificationsScheduler** | Lifecycle demo. Channel CRUD, `OperatingMode` toggles, background-foreground round trip with reschedule semantics. | Drop `NotificationsSchedulerUI` on a GameObject and deploy to device. |
| **DeepLinkRouter** | Pattern demo for the `IDeepLinkRouter.MapRoute` API. Cold-start replay instructions for both platforms. | Drop `DeepLinkRouterUI` on a GameObject; follow the per-sample README for `xcrun simctl` / `adb shell am start` test commands. |

## Why code-only samples (no scene / prefab assets)

The samples build their UI at runtime via legacy `UnityEngine.UI` (Canvas + VerticalLayoutGroup + Image + Text + Button). This is intentional:

- **Zero asset dependencies** — no `.unity` scenes, no `.prefab` files, no deterministic-GUID `.meta` files to keep in sync. The sample tree is just `*.cs` + `README.md`.
- **Diff-friendly** — code reviewers see exactly what the sample does at the source level; YAML-serialised scenes are notoriously hard to review.
- **Easy to drop into any scene** — the user adds the component to any GameObject in any scene and presses Play. No "now open `<sample>.unity`" step.

This is a deliberate divergence from `com.gamelovers.services` and `com.gamelovers.uiservice`, both of which ship `.unity` scenes with hand-authored deterministic GUIDs. Those samples include prefab references and a structured scene hierarchy that genuinely need the scene file; the mobile samples don't.

## Sample-only types

All sample types live in `GameLovers.MobileServices.Samples.<SampleName>` namespaces. They are **NOT** part of the public package API surface. When updating any sample's README or the main package README, never describe these types as if they were package API — workspace AGENTS.md explicitly calls this out as a recurring documentation-drift trap.

## On-device testing

Most surfaces (haptics, native UI, real notifications, ATT, native deep-link delivery) only fully exercise on a physical device or simulator. In Unity Editor:

- `NativeUiService` logs to console.
- `HapticsService` runs the `EditorHapticsBackend` (logs).
- `MobileNotificationService` returns an `EditorGameNotification` — `Schedule` doesn't actually queue with the OS.
- `IPermissionsService.RequestAsync` short-circuits to `Granted` (or whatever `EditorPlatformSimulator.QueuePermissionResult` was last asked to return).

The Mobile Services panel inside Unity's Device Simulator (`Window > General > Device Simulator`) bridges the editor gap by painting platform-shaped mocks of every native UI surface inside the simulated phone screen; open the Device Simulator next to the playground sample for the best in-editor iteration loop.

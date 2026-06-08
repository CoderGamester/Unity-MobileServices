# Mobile Services Playground

Kitchen-sink runtime sample that wires every Mobile Services subsystem into one screen of buttons and live status labels.

## Setup

1. Import the sample via `Window > Package Manager > GameLovers.MobileServices > Samples > Mobile Services Playground`.
2. Create an empty scene (or use any existing one).
3. Create an empty GameObject and add the `MobileServicesPlaygroundUI` MonoBehaviour to it.
4. Enter Play mode. The runtime builds a Canvas + Vertical scrolling layout at runtime — no scene asset is shipped with this sample on purpose (zero asset dependencies, easy diff in PRs).

This is the **breadth** sample — one representative call per subsystem so you can confirm everything is wired. For **depth** in a single subsystem, see the focused samples: `HapticsPalette` (full haptics surface — custom intensity, looping, sequence record/replay, Stop), `NotificationsScheduler` (operating modes, multi-channel, reschedule, delivered/expired events), and `DeepLinkRouter` (the `IDeepLinkRouter` pattern-routing layer).

## Panels

- **Native UI** — modal alert, **action sheet**, short + long toast, review prompt, share sheet.
- **Haptics** — fires every preset in the catalogue (a representative subset of the haptics surface; see `HapticsPalette` for custom intensity, looping, and sequence record/replay).
- **Notifications** — schedule-in-5s + cancel-all, against the `default` channel that the script registers in `Awake` (see `NotificationsScheduler` for modes / channels / reschedule).
- **Permissions** — a sync `Check all` button + an async `Request` button per `AppPermission`.
- **ATT** — Request authorization (no-op on Android / Editor).
- **Gestures** — a live `GestureController` is attached to the host GameObject; swipe / tap anywhere and the events print to the log. In the editor, Input System Touch Simulation is enabled so mouse drags/clicks register as touches.
- **Other** — KeepAwake toggle + iOS audio session configure.
- **Deep links** — subscribes to the raw `IDeepLinkService.OnLinkActivated` and logs the URI (see `DeepLinkRouter` for pattern routing).
- **Log** — last 8 entries printed by the buttons.

The panel itself is wrapped in a colored `Image` that tracks the device safe area every frame so the playground also demonstrates `ISafeAreaService` without a separate sample.

## Notes

- Real native UI / haptics / notifications only fire on device. In Editor they log to console (per the package's documented no-op behaviour).
- The Mobile Services panel in Unity's Device Simulator (`Window > General > Device Simulator`) surfaces this sample's live state when both the playground scene is in Play mode and the Device Simulator is open.
- The same panel paints platform-shaped mocks of every native UI surface the buttons trigger, rendered inside the simulated phone screen.

## Types

All sample types live in `GameLovers.MobileServices.Samples.MobileServicesPlayground` — they are NOT part of the public package API. Do not document them as such anywhere.

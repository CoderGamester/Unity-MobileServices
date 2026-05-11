# Mobile Services Playground

Kitchen-sink runtime sample that wires every Mobile Services subsystem into one screen of buttons and live status labels.

## Setup

1. Import the sample via `Window > Package Manager > GameLovers.MobileServices > Samples > Mobile Services Playground`.
2. Create an empty scene (or use any existing one).
3. Create an empty GameObject and add the `MobileServicesPlaygroundUI` MonoBehaviour to it.
4. Enter Play mode. The runtime builds a Canvas + Vertical scrolling layout at runtime — no scene asset is shipped with this sample on purpose (zero asset dependencies, easy diff in PRs).

## Panels

- **Native UI** — alert dialog, toast, review prompt, share sheet.
- **Haptics** — every preset in the catalogue plus a custom intensity row in the Mobile Services Explorer Haptics tab (in-editor).
- **Notifications** — schedule-in-5s + cancel-all, against the `default` channel that the script registers in `Awake`.
- **Permissions** — Request button per `AppPermission`.
- **ATT** — Request authorization (no-op on Android / Editor).
- **Other** — KeepAwake toggle + iOS audio session configure.
- **Log** — last 8 entries printed by the buttons.

The panel itself is wrapped in a colored `Image` that tracks the device safe area every frame so the playground also demonstrates `ISafeAreaService` without a separate sample.

## Notes

- Real native UI / haptics / notifications only fire on device. In Editor they log to console (per the package's documented no-op behaviour).
- The Mobile Services Explorer (`Tools > GameLovers > Mobile Services Explorer`) surfaces this sample's state live when both the playground scene and the Explorer window are open.
- Open the truth-mirror simulator (`Tools > GameLovers > Mobile Services Simulator Window`) alongside the playground to see platform-shaped mocks of every native UI surface the buttons trigger.

## Types

All sample types live in `GameLovers.MobileServices.Samples.MobileServicesPlayground` — they are NOT part of the public package API. Do not document them as such anywhere.

# Mobile Services Samples

Import the single **Mobile Services Samples** entry from Package Manager. It is one sample player with four views connected by persistent bottom tabs: **Overview**, **Haptics**, **Notifications**, and **Links**.

Set **Player Settings > Active Input Handling** to **Input System Package (New)** or **Both**, then open any of the four `.unity` scenes and enter Play Mode. Each scene remains independently playable, and the shared editor bridge makes tab navigation work without preparing Build Settings first. No GameObject wiring is required. Unity 6 InputForUI routes input directly to UI Toolkit, so runtime buttons, fields, scrolling, and tabs in the Game or Device Simulator view become interactive in Play Mode without a uGUI EventSystem.

Every enabled control provides hover, press, focus, and disabled feedback. Ordinary committed clicks emit one `Selection` haptic; controls that demonstrate haptics do not add a redundant selection pulse. A press that crosses the scroll threshold cancels the button and gives the clamped content drag priority, including simulator touch streams that omit continuous UI Toolkit move events. Bottom navigation remains pinned outside the scroll region. Status cards use one sentence-case `Field: Value` per line, with `Yes` and `No` for booleans.

| View | Open this scene | Focus |
|---|---|---|
| [Overview](#overview) | `MobileServicesPlayground/MobileServicesPlayground.unity` | Native UI, permissions, ATT, device and safe-area state, gestures, and activity logging. |
| [Haptics](#haptics) | `HapticsPalette/HapticsPalette.unity` | All nine presets, playback modes, custom intensity and duration, and sequence record/replay. |
| [Notifications](#notifications) | `NotificationsScheduler/NotificationsScheduler.unity` | Fixed channels, permission state, operating modes, scheduling, cancellation, pending rows, and simulated delivery. |
| [Links](#links) | `DeepLinkRouter/DeepLinkRouter.unity` | Route patterns, captured parameters, raw and unmatched links, and warm/cold launch behavior. |

## Overview

The Overview view covers package areas that are not duplicated by the focused views:

- native alert, action sheet, toast, review, and share flows;
- synchronous checks and asynchronous requests for every `AppPermission`;
- ATT authorization, keep-awake, and iOS audio-session configuration;
- safe-area and device status;
- `GestureController` tap and swipe logs.

The live device card reports battery, low-power, keep-awake, ATT, and safe-area state on separate lines. The Editor and Device Simulator use safe no-op or mock implementations where an operating-system feature is unavailable.

## Haptics

The Haptics view exposes every preset—`Selection`, `Success`, `Warning`, `Error`, and the five impact presets—with three duration modes:

- **Natural** — one native one-shot.
- **Finite** — loop for a real-time duration and stop automatically.
- **Indefinite** — loop until **Stop** is pressed.

Custom controls demonstrate clamped intensity and duration. Every preset action records an immutable preset and delay snapshot. **Replay** cannot overlap itself, and **Clear** does not mutate a replay already in progress. Haptics supported, enabled, playing, and last-action state appear on separate `Field: Value` lines.

The activity area reports every action. Native haptic output requires a paired iOS or Android device.

## Notifications

The Notifications view starts with the fixed `default` and `rewards` channels; channels are intentionally not created or deleted at runtime. Its five scheduling actions share one responsive row, stay together when space permits, and allow two-line labels on narrow device profiles.

The flow covers notification permission state and requests, `OperatingMode`, schedules at five and thirty seconds, `Reschedule`, pending rows, individual and bulk cancellation, dismiss-all, and delivered or expired event logs. Put a device app in the background to observe operating-system delivery and foreground queue behavior.

For Editor simulation:

1. Open **Window > General > Device Simulator**.
2. Select the **Mobile Services** panel and enable **Editor Simulator**.
3. Enter Play Mode in the Notifications view and schedule a notification.
4. Use **Deliver next pending** to deliver the earliest row immediately, or wait for its delivery time to elapse.

The sample log records `Delivered`, the pending row is removed, and the overlay paints the exact title, body, and channel. When the Notifications view is not active, **Show heads-up banner** remains a generic preview and does not modify a game service. Editor scheduling never touches an operating-system notification backend; the connection is editor-only and transient.

## Links

The Links view configures three routes synchronously:

| Pattern | Example | Captured parameter |
|---|---|---|
| `/promo/:id` | `<sample-scheme>://promo/spring2026` | `id = spring2026` |
| `/profile/:user` | `<sample-scheme>://profile/abc123` | `user = abc123` |
| `/settings` | `<sample-scheme>://settings` | none |

**Router-only** actions call `TryDispatch` directly. **Raw link** logs the underlying `IDeepLinkService` event without routing it, so the service and router layers can be compared. Unmatched links are logged explicitly.

`<sample-scheme>` is derived from `Application.identifier`: it is lowercased; ASCII letters, digits, `+`, `.`, and `-` are retained; invalid runs collapse to `-`; and `gl-` is prefixed when necessary. The view displays the generated scheme for use with device tools.

On a warm launch, the OS sends a link while the app is running. On a cold launch, the combined player creates `DeepLinkService` before scene transitions, buffers the startup URL, and opens the Links view automatically. Android builds receive a `VIEW` / `DEFAULT` / `BROWSABLE` filter and vibration permission. iOS builds receive an additive URL-scheme entry. The sample adds neither push notifications nor an Associated Domains entitlement.

Example device commands:

```bash
xcrun simctl openurl booted "<sample-scheme>://promo/spring2026"
adb shell am start -W -a android.intent.action.VIEW -d "<sample-scheme>://promo/spring2026" your.bundle.id
```

## Supported player build

Use **Tools > Mobile Samples Examples > Build All** from any sample view, or with no sample scene open. It snapshots the current effective global Build Settings or active overriding Build Profile, installs the four scenes in Overview-first order, and opens Unity's native Build Profiles window. Unity retains ownership of the target, output path, and final Build command.

**Restore All** restores the captured scene list and enabled flags. The snapshot is session-only: it survives a domain reload but is unavailable after Unity closes. Restore before restarting Unity or deleting the imported sample. Build All never changes the persisted Mobile Services Config asset or `EditorPrefs`; its sample-scoped build preprocessor adds the combined native requirements only while Unity builds the canonical four-scene player. The menu commands and editor bridge disappear when the imported sample is deleted.

## On-device testing

Native UI, haptics, OS notifications, ATT, permission prompts, and OS deep-link delivery require an iOS or Android device or simulator. Editor controls and synthetic route or notification actions still exercise the package API, and the Notifications view can deliver its in-memory pending items through the Device Simulator overlay without an OS backend.

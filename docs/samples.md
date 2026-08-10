# Samples Index

Import the single **Mobile Services Samples** entry from `Window > Package Manager > Mobile Services > Samples`. The bundle is one sample with four scene-backed views and persistent bottom tabs: **Overview**, **Haptics**, **Notifications**, and **Links**. Set **Player Settings > Active Input Handling** to **Input System Package (New)** or **Both**, then open any scene from the Project window and enter Play Mode; the Game/Simulator view can render the UI outside Play Mode, but runtime buttons, fields, scrolling, and tabs become interactive only after Play Mode starts. The imported bundle contains one canonical [`README.md`](../Samples~/MobileServicesSamples/README.md) covering all four views.

Each view's scene includes its own camera, `UIDocument`, `PanelSettings`, default runtime UI theme, safe-area container, responsive layout, and teardown-safe controller. Unity 6 InputForUI routes input directly to UI Toolkit, while shared navigation supplies the gesture bridge, so no uGUI EventSystem or extra scene wiring is required. Enabled buttons provide palette-specific hover, press, focus, and disabled states plus one committed `Selection` haptic. A short press stays a button; a drag that crosses the threshold cancels the button and uses the clamped content scroll, with the bottom navigation pinned outside that region. The gesture bridge also consumes package-level potential swipes when a simulator omits continuous UI Toolkit move events. Haptics demonstration controls do not add a redundant selection pulse.

| View | Scene folder | What to try |
|---|---|---|
| Overview | [`MobileServicesPlayground`](../Samples~/MobileServicesSamples/README.md#overview) | Native UI, device/safe area, permissions, ATT, gestures, and activity logging. |
| Haptics | [`HapticsPalette`](../Samples~/MobileServicesSamples/README.md#haptics) | All nine presets, natural/finite/indefinite playback, custom intensity/duration, live state, stop, and record/replay. |
| Notifications | [`NotificationsScheduler`](../Samples~/MobileServicesSamples/README.md#notifications) | Fixed channels, permission status, operating modes, 5/30-second scheduling, reschedule, pending rows, cancellation, event logs, and the Device Simulator notification connection. Actions share a responsive wrapping row and may use two-line labels on narrow profiles. |
| Links | [`DeepLinkRouter`](../Samples~/MobileServicesSamples/README.md#links) | `/promo/:id`, `/profile/:user`, and `/settings`, with raw/routed/unmatched events, parameters, and warm/cold-link guidance. |

## Status text

Every status card follows the Notifications Scheduler convention: one sentence-case `Field: Value` per line. Boolean state uses `Yes` or `No`, missing state uses `None` or `Unknown`, and chronological events stay in the activity log. Notification pending rows place title, delivery, channel, and reschedule state on separate lines.

## Combined player and build preparation

The tabs work when the scenes are included in a build. When an individual scene is opened in the Editor, the sample's editor bridge loads another imported scene directly in Play Mode without changing Build Settings. A received OS deep link automatically opens the Links tab.

- **Tools > Mobile Samples Examples > Build All** validates the four authored scenes, snapshots the current effective global Build Settings or active overriding Build Profile, installs the exact Overview-first sample sequence, then opens Unity's native Build Profiles window.
- **Tools > Mobile Samples Examples > Restore All** restores that exact prior scene configuration. It is visible at all times and enabled only while the session snapshot exists.

Build All is preparation, not a replacement player-build dialog: Unity still owns platform selection, output path, and the final Build action. The sample-scoped preprocessor supplies all four scenes' additive native requirements through a temporary in-memory `MobileServicesConfig` clone; it never changes the persisted config asset or `EditorPrefs`. The clone is released by the sample cleanup callback at order `2000`, after the package postprocessor at order `1000` has completed.

Overview contributes the permissions and Android features it demonstrates. Haptics contributes Android vibration. Notifications contributes Android notification permission and vibration without enabling Push Notifications. Links contributes vibration and a deterministic deep-link registration through the shared config pipeline. Existing persisted project configuration remains unchanged.

Restore state is stored in Unity `SessionState`: it survives a domain reload but not closing Unity. Restore before restarting Unity, changing to another prepared Build Profile, or deleting the imported sample. Re-running Build All after a restart snapshots the then-current scene configuration.

## Build and device testing

Editor surfaces are useful for trying the wiring, but haptics, OS permission prompts, ATT, real notification delivery, and warm/cold deep links still need an Android/iOS device or simulator. The Notifications Scheduler editor adapter can deliver its own pending items through the Device Simulator overlay without touching an OS backend. The imported `MobileServicesSampleBuildCatalog.asset` is the only scene source of truth; use **Tools > Mobile Samples Examples > Verify Scene Catalog** to emit page/path/derived-GUID identities for a clean-host check. See [`build-pipeline.md`](build-pipeline.md) and the single [sample README](../Samples~/MobileServicesSamples/README.md) for platform details.

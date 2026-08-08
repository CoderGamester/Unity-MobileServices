# Troubleshooting

Symptom-to-fix mapping for the documented behaviours and gotchas. For architecture details see [AGENTS.md](../AGENTS.md) §4.

## Native UI

| Symptom | Fix |
|---------|-----|
| `ShowAlertPopUp` does nothing in editor | Editor short-circuit logs to console. Open `Window > General > Device Simulator` and use the [Mobile Services panel](explorer.md) to preview platform-shaped mocks inside the simulated phone. |
| `ShowAlertPopUp` throws `SystemException` | Running on an unsupported platform (Standalone, WebGL). Mobile-only API. |
| iOS alert button callback fires the wrong handler | Two buttons in the same alert share their `Text`. iOS matches by text — keep button texts unique within a single alert. |
| `RequestReview()` does nothing on Android | The generated Gradle project does not contain `com.google.android.play:review` (or the config opted out). The call logs an error and returns; it does NOT throw. |
| `RequestReview()` doesn't show the prompt every time | Working as intended — both iOS `SKStoreReviewController` and Play In-App Review throttle the prompt frequency. |

## Notifications

| Symptom | Fix |
|---------|-----|
| Android notification never displays | At least one `GameNotificationChannel` must be registered in the `MobileNotificationService` constructor. First channel passed becomes the default. |
| `ScheduleNotification` returns a PendingNotification but the OS never delivers | Editor short-circuit — `EditorGameNotification` is in-memory only. Build to a device. |
| Queued notification doesn't fire when app backgrounds | `OperatingMode` must include `Queue` for queueing behaviour. Default is `NoQueue` (schedule with OS immediately). |
| `RescheduleAfterClearing` doesn't re-queue | Set `pending.Reschedule = true` on the returned `PendingNotification`. Also requires `ClearOnForegrounding` in the mode. |
| Notification host survives "reset game" flow | The `NotificationService` GameObject is `DontDestroyOnLoad`. Tests / reset flows must destroy it manually. |

## Gestures

| Symptom | Fix |
|---------|-----|
| `Swiped` event never fires | `EnhancedTouchSupport` not enabled — but `GestureController` enables/disables it in `OnEnable`/`OnDisable`. Check the controller component is active. |
| Mouse clicks don't trigger swipes/taps in editor | Add a `TouchSimulation` component to any GameObject in the scene. |
| Same gesture fires BOTH `Tapped` and `Swiped` | `_minSwipeDistance <= _maxTapDrift` — tune the thresholds. |

## Haptics

| Symptom | Fix |
|---------|-----|
| `PlayPreset` is silent on a paired phone via Unity Remote | Unity Remote relays input/display only — haptics don't transmit. Build a debug player to the device. |
| `PlayPresetDuration(preset, -1f)` keeps vibrating forever | By design — `-1` is "loop until `StopCurrentHaptic`". Set `Enabled = false` or call `StopCurrentHaptic`. |
| `PlayCustom` throws "DontDestroyOnLoad can only be used in play mode" in EditMode tests | `PlayCustom` always schedules an auto-stop, which spawns the `HapticsHost` GameObject. Move the test to PlayMode. |
| Haptic continues after "reset game" | Reset flow destroys the `HapticsHost` GameObject without stopping the haptic first. Call `StopCurrentHaptic()` before reset. |

## Device subsystem

| Symptom | Fix |
|---------|-----|
| `IsLowPowerMode` is always false in editor | Editor short-circuits LPM detection. Use `EditorPlatformSimulator.SetIosLowPowerMode(true, battery)` to drive the change. |
| `device.Att.CurrentStatus` returns `Authorized` on Android | Android has no ATT equivalent — the service returns `Authorized` unconditionally. Don't read this as "the user authorized"; gate tracking-init on `Application.platform == RuntimePlatform.IPhonePlayer`. |
| Cold-start deep link is lost | Construct `DeepLinkService` early — before scene load. `Application.absoluteURL` is cleared by Unity once consumed. |
| Second subscriber to `OnLinkActivated` doesn't receive the cold-start link | By design — the link represents a single user action, replayed to the FIRST subscriber only. |

## Permissions

| Symptom | Fix |
|---------|-----|
| `RequestAsync` resolves to `Granted` in editor without prompting | Editor short-circuits to `Granted`. Use `EditorPlatformSimulator.QueuePermissionResult(...)` to override. |
| `Permission.READ_MEDIA_IMAGES` denied on Android < 13 | The permission is auto-granted below API 33. The service returns `Granted` immediately via Unity's `Permission.HasUserAuthorizedPermission` short-circuit. Add the legacy `READ_EXTERNAL_STORAGE` for that path. |
| iOS Camera prompt shows blank text | Missing `NSCameraUsageDescription`. Open `Tools > GameLovers > Mobile Services > Select Mobile Services Config` or rely on the build postprocessor to fail the build (default). |

## Editor / Build

| Symptom | Fix |
|---------|-----|
| iOS build fails with `[GameLovers.MobileServices] iOS build failed because…` | A referenced permission has an empty usage description. Open `Tools > GameLovers > Mobile Services > Select Mobile Services Config` and fill the missing field (the `Fill missing English descriptions with suggested copy` button is a quick start), or enable `Manage Native Build Manually` if you manage `Info.plist` yourself. |
| Android build doesn't pick up `<uses-permission>` entries | Inspect the generated application manifest, not `mainTemplate.xml`. The postprocessor fails when it cannot uniquely identify Unity's application activity; enable `Manage Native Build Manually` only when another build tool owns the manifest. |
| The Mobile Samples build menu is missing | Import the single **Mobile Services Samples** entry from Package Manager and wait for its editor assembly to compile. The package alone intentionally does not install sample menus. |
| Build All reports a missing scene | Remove the incomplete imported bundle and import **Mobile Services Samples** again. Build All resolves all four scenes by stable asset GUID and does not depend on the currently open scene. |
| **Build All** is disabled | Exit Play Mode and wait for compilation or another player build to finish. The command then installs the canonical four-scene list and opens Unity's native Build Profiles window. |
| **Restore All** is disabled | There is no current-session Build All snapshot. Restore state intentionally survives domain reloads but not closing Unity; re-run Build All to capture the current scene list. |
| The Mobile Services panel is missing from the Device Simulator | Open `Window > General > Device Simulator` and look in the Control Panel column. The panel is auto-discovered; if it's absent, ensure the `GameLovers.MobileServices.Editor` assembly compiled (check the Console for errors). |
| Mocks fired from the panel don't render | The overlay paints into the Game / Simulator view — keep the Device Simulator window open (the overlay is alive only while the Device Simulator panel is open). |
| Simulator overlay shows generic Unity dialogs | The simulator USS files didn't load. Verify `Editor/Explorer/Overlays/MobileSimulator.Common.uss` is present in the package and re-import. |

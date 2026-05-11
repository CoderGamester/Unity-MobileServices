# Troubleshooting

Symptom-to-fix mapping for the documented behaviours and gotchas. For architecture details see [AGENTS.md](../AGENTS.md) §4.

## Native UI

| Symptom | Fix |
|---------|-----|
| `ShowAlertPopUp` does nothing in editor | Editor short-circuit logs to console. Use the [Mobile Simulator window](explorer.md) to preview platform-shaped mocks. |
| `ShowAlertPopUp` throws `SystemException` | Running on an unsupported platform (Standalone, WebGL). Mobile-only API. |
| iOS alert button callback fires the wrong handler | Two buttons in the same alert share their `Text`. iOS matches by text — keep button texts unique within a single alert. |
| `RequestReview()` does nothing on Android | Missing `com.google.android.play:review:2.0.1` in `mainTemplate.gradle`. The call logs an error and returns; it does NOT throw. |
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
| `Connectivity.OnStatusChanged` never fires | Polls once per second + on focus regain. For editor preview, use `EditorPlatformSimulator.SetConnectivity(...)`. |
| `device.Att.CurrentStatus` returns `Authorized` on Android | Android has no ATT equivalent — the service returns `Authorized` unconditionally. Don't read this as "the user authorized"; gate tracking-init on `Application.platform == RuntimePlatform.IPhonePlayer`. |
| Cold-start deep link is lost | Construct `DeepLinkService` early — before scene load. `Application.absoluteURL` is cleared by Unity once consumed. |
| Second subscriber to `OnLinkActivated` doesn't receive the cold-start link | By design — the link represents a single user action, replayed to the FIRST subscriber only. |

## Permissions

| Symptom | Fix |
|---------|-----|
| `RequestAsync` resolves to `Granted` in editor without prompting | Editor short-circuits to `Granted`. Use `EditorPlatformSimulator.QueuePermissionResult(...)` to override. |
| `Permission.READ_MEDIA_IMAGES` denied on Android < 13 | The permission is auto-granted below API 33. The service returns `Granted` immediately via Unity's `Permission.HasUserAuthorizedPermission` short-circuit. Add the legacy `READ_EXTERNAL_STORAGE` for that path. |
| iOS Camera prompt shows blank text | Missing `NSCameraUsageDescription`. Open Project Settings > GameLovers > Mobile Services or rely on the build postprocessor to fail the build (default). |

## Editor / Build

| Symptom | Fix |
|---------|-----|
| iOS build fails with `[GameLovers.MobileServices] iOS build failed because…` | A referenced permission has an empty usage description. Open Project Settings > GameLovers > Mobile Services and fill the missing field, or enable `Allow build with placeholder usage descriptions` for CI builds (Apple will reject those placeholders on submission). |
| iOS build succeeds but App Store rejects with "placeholder text in NSCameraUsageDescription" | You shipped a build with the soft-mode placeholder. Fill in real usage descriptions in the settings panel and rebuild. |
| Android build doesn't pick up `<uses-permission>` entries | Your project lacks `Assets/Plugins/Android/mainTemplate.xml`. Enable `Player Settings > Publishing Settings > Custom Main Manifest` and rebuild. |
| Mobile Services Explorer "Open" buttons do nothing | The Explorer window is closed. Open via `Tools > GameLovers > Mobile Services Explorer`. |
| Truth-mirror simulator shows generic Unity dialogs | The simulator USS files didn't load. Verify `Editor/Explorer/Overlays/MobileSimulator.Common.uss` is present in the package and re-import. |

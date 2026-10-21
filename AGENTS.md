# GameLovers MobileServices — Agent Guide

This guide adds package-specific rules to the host repository guide. Consumer usage belongs in `README.md` and `docs/`.

## Scope

- Package: `com.gamelovers.mobileservices`; minimum Unity version and dependencies are authoritative in `package.json`.
- Runtime subsystems: Native UI, notifications, gestures, haptics, device services (battery, audio session, permissions, ATT, deep links, and background tasks), and the opt-in game platform (Game Center and Play Games sign-in, leaderboards, achievements, and cloud saves).
- Consumers must enable the Input System or Both. The gesture contract uses EnhancedTouch.
- This package is render-pipeline-neutral and has no dependency on GameLovers Services.

## Layout and native boundaries

- Each `Runtime/<Subsystem>/` owns namespace `GameLovers.MobileServices.<Subsystem>`. Its deeper folders are organizational and do not add namespace segments.
- Non-public platform backends and hosts belong under `Internal/` and remain `internal`; use existing `InternalsVisibleTo` grants for Editor/test access.
- Editor simulator, settings, and native build code stay under `Editor/`. Sample build tooling stays under `Samples~/MobileServicesSamples/Editor/`.
- iOS bridges live in `Plugins/iOS/`. `_GameLovers*` exports, C# `DllImport` signatures, enum integer values, and native implementations must change together.
- `UnitySendMessage` names are native contracts: `DeviceServicesHost`, `PermissionsCallbackReceiver`, and `AttCallbackReceiver`. Update C# and Objective-C together if one changes.

## Runtime invariants

- Native alerts accept one to three buttons with unique text and unique `AlertButtonStyle` values. A non-dismissible alert cannot be an action sheet because iOS retains outside-tap dismissal for that presentation style. `ShowAlertPopUpAsync` is the preferred API; it returns the selected index through one pooled `Awaitable`, while legacy callbacks switch through `Awaitable.MainThreadAsync`. Dismissing or replacing an async alert cancels its await.
- `RequestReview()` is fire-and-forget. The OS may suppress the prompt and provides no “shown” result; do not invent success callbacks or a store-URL fallback. Android Play review depends on the configured Play Core review artifact, which the native-build postprocessor injects unless explicitly disabled.
- `MobileNotificationService` owns its `NotificationService` GameObject. Disposal is idempotent, releases only owned resources, and public operations depending on the host throw `ObjectDisposedException` afterward. Shared validation and lifecycle behavior stay outside platform conditionals.
- Android notification scheduling requires a registered default channel. Queue modes hand pending work to the OS on background transitions.
- Every `GestureController` balances only its own `EnhancedTouchSupport.Enable()` acquisition. Multiple live controllers and external release must remain safe.
- `IGameNotification.Data` maps to `AndroidNotification.IntentData` and `iOSNotification.Data` and must survive the queue-and-reschedule persistence. Both platforms keep returning the last opened notification, so `GameNotificationsMonoBehaviour` captures the cold-start one before the first foregrounding and de-duplicates later opens by id (title and body when unset). Opened-notification behavior is exercised through the internal `AttachPlatform` seam with a fake platform.
- Android image sharing goes through the package FileProvider: the Java class `GameLoversFileProvider` (`Plugins/Android/`), the authority suffix `NativeUiService.AndroidShareProviderAuthoritySuffix`, and the postprocessor's provider and `res/xml` paths file change together. Without the toggle an image share degrades to text with a warning.
- `NativeMediaService` needs no permission. iOS saves through `PHPhotoLibrary` only when `NSPhotoLibraryAddUsageDescription` is declared (`GL_PERMISSION_PHOTOS_ADD`) and picks through `PHPickerViewController`. Android saves through `MediaStore` (legacy storage below API 29) and picks through the transparent `GameLoversMediaPickerActivity`, which the postprocessor declares behind `IncludeMediaPicker`.
- Picked images are always a private copy the caller may delete. The `_GameLoversMedia*` exports, their callback signature, and the Java `ResultCallback` and `PathCallback` interfaces change together with their C# callers.
- `GamePlatformServices` is opt-in and holds one `IGamePlatformBackend` per platform. Its guards (signed-in player, non-empty id, positive steps, one shared sign-in) live in the service, not the backends. The Editor backend and `EditorGamePlatformState` are process-wide, so the simulator panel drives the consumer's own instance.
- The Android game backend talks to Play Games v2 through JNI and `OnCompleteListener` proxies so the package compiles without the SDK; the postprocessor adds the SDK only when `GamePlatform.PlayGamesProjectId` is set.
- Cloud saves share the game platform opt-in. `CloudSaveService` owns validation, availability and conflict resolution (policy, resolver, newest-first ordering) so both platforms behave alike.
- Cloud save backends only list, load every stored version, resolve to a chosen version, save, and delete, and report failures as `CloudSaveException`. The Android backend uses JNI like the sign-in backend.
- The iOS bridge (`CloudSave.m`) replies with tagged strings decoded by `CloudSaveProtocol` and holds its own `UIBackgroundTask` during saves, because Unity's main loop is paused while backgrounded.
- `Plugins/iOS/GamePlatform.m` links GameKit only when `GL_GAME_CENTER` is defined (Game Center capability on). The `_GameLoversGamePlatform*` exports and their callback signature change together with the C# callers.
- `IBackgroundTaskService` is iOS-only in effect. `Plugins/iOS/BackgroundTask.m` must end the system task before its expiration handler returns, on the main thread; handle members are readable from any thread; the `_GameLoversBackgroundTask*` exports, C# `DllImport` signatures, and the expiration callback signature change together. Android and Editor return a pass-through handle.
- One haptic play replaces the previous play and cancels its pending auto-stop. Negative preset duration loops until explicit stop; `StopCurrentHaptic()` is idempotent.
- `DeviceServicesHost` is the shared polling host. Do not create per-service update GameObjects when the host can own the callback.
- The first `DeepLinkService` subscriber receives a pending cold-start link once. Construct the service early; event names are not persistent state.
- ATT returning `Authorized` outside iOS means “not applicable,” not an observed user decision.
- Permission, ATT, battery, native-alert, and review Editor overrides are process-wide statics. Simulator code must install and remove them symmetrically; tests must reset overrides they touch.
- The iOS location permission bridge retains each `CLLocationManager` delegate until authorization changes and removes it only after callback dispatch. Do not simplify the static delegate-retention collection into a local lifetime.
- `AndroidPermissionPolicy` owns which manifest permissions each `AppPermission` needs per API level and how grants fold into `PermissionStatus`; it stays platform-neutral so it is unit-tested. Keep it, the runtime request code, and `AndroidManifestToggles.GetPermissions` aligned.
- Android never reports a refusal directly: `Denied` is the persisted "requested" flag or the OS rationale flag, and requests are serialized through one gate because Android shows one dialog at a time. Background location is always a separate second request. Notifications below API 33 have no runtime permission and follow the enabled switch.
- `PermissionStatus` integer values are a native contract; `Limited` is 4. Limited covers partial photos, provisional notifications, approximate location, and foreground-only location when Always was asked.
- `Plugins/iOS/Permissions.m` is built in groups (camera, microphone, location, photos) gated by `GL_PERMISSION_<GROUP>`. Without `GL_PERMISSIONS_DECLARED_ONLY` every group compiles; the build postprocessor sets it and defines only the groups whose purpose string is in the final `Info.plist`. `IosPermissionGroups`, the plugin, and the postprocessor change together. Notification status is a native cache, so `Check` never blocks after the first read.

## Editor and native-build invariants

- The Device Simulator plugin is the single simulator control surface; the runtime overlay is Editor-assembly-owned. UI that claims to affect the game must target the consumer's actual service instance, not a parallel service created by the panel.
- `MobileServicesConfig` is an Editor-only asset. Native build callbacks use the persisted config boundary, never an implicit transient default.
- The package build postprocessor is the sole owner of generated iOS/Android mutation. Sample tooling contributes temporary declarative requirements and a later cleanup callback; it must not implement a competing native mutator.
- Native-build configuration resolves the persisted asset before scanning or touching generated files. Missing config and manual-management mode are no-ops; malformed enabled settings fail before mutation. Package mutation runs at callback order 1000 and sample cleanup at 2000.
- Validate all persisted config and sample build preconditions before changing build scenes, profiles, manifests, plists, entitlements, or generated Gradle files. Repeated mutation must be idempotent and preserve non-empty consumer values.
- New inspectors use UI Toolkit and qualify `UnityEditor.Editor` inside namespaces containing `.Editor`.

## Samples and tests

- `package.json` exposes one four-scene sample bundle under `Samples~/MobileServicesSamples/` with one runtime asmdef and one editor asmdef.
- Sample scenes remain independently playable, require no hand-wired scene bootstrap, and share the sample-owned navigation/session. Do not add a uGUI `EventSystem`; Unity InputForUI owns UI Toolkit input.
- Sample build tools derive scene identity from serialized `SceneAsset` references, never hand-authored paths or GUID lookup code.
- Imported sample acceptance requires actual pointer/click/drag/navigation verification after a fresh compile; direct callback invocation is not click evidence.
- The Editor overlay's USS uses logical phone points. Its `PanelSettings` stays `ScaleWithScreenSize` with reference resolution 390×844; `ConstantPixelSize` renders mocks roughly one-third size on high-density simulated devices.
- Before changing anything under `Tests/`, read `Tests/AGENTS.md`.

## Verification and documentation

- Platform bridge changes require relevant Editor simulation plus real iOS/Android build or device evidence; unavailable platform evidence is `NOT VALIDATED`.
- Android App Link `AutoVerify` needs an http or https scheme and a host; validation rejects anything else before a build file is touched.
- Update `README.md`, the relevant `docs/` page, and the canonical sample README when public behavior or sample setup changes.
- Update this guide only for durable subsystem, native-boundary, build-ownership, or test conventions.

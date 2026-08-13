# GameLovers MobileServices — Agent Guide

This guide adds package-specific rules to the host repository guide. Consumer usage belongs in `README.md` and `docs/`.

## Scope

- Package: `com.gamelovers.mobileservices`; minimum Unity version and dependencies are authoritative in `package.json`.
- Runtime subsystems: Native UI, notifications, gestures, haptics, and device services (safe area, battery, audio session, permissions, ATT, and deep links).
- Consumers must enable the Input System or Both. The gesture contract uses EnhancedTouch.
- This package is render-pipeline-neutral and has no dependency on GameLovers Services.

## Layout and native boundaries

- Each `Runtime/<Subsystem>/` owns namespace `GameLovers.MobileServices.<Subsystem>`. Its deeper folders are organizational and do not add namespace segments.
- Non-public platform backends and hosts belong under `Internal/` and remain `internal`; use existing `InternalsVisibleTo` grants for Editor/test access.
- Editor simulator, settings, and native build code stay under `Editor/`. Sample build tooling stays under `Samples~/MobileServicesSamples/Editor/`.
- iOS bridges live in `Plugins/iOS/`. `_GameLovers*` exports, C# `DllImport` signatures, enum integer values, and native implementations must change together.
- `UnitySendMessage` names are native contracts: `DeviceServicesHost`, `PermissionsCallbackReceiver`, and `AttCallbackReceiver`. Update C# and Objective-C together if one changes.

## Runtime invariants

- Native alerts accept one to three buttons with unique text and unique `AlertButtonStyle` values. A non-dismissible alert cannot be an action sheet because iOS retains outside-tap dismissal for that presentation style.
- `RequestReview()` is fire-and-forget. The OS may suppress the prompt and provides no “shown” result; do not invent success callbacks or a store-URL fallback. Android Play review depends on the configured Play Core review artifact, which the native-build postprocessor injects unless explicitly disabled.
- `MobileNotificationService` owns its `NotificationService` GameObject. Disposal is idempotent, releases only owned resources, and public operations depending on the host throw `ObjectDisposedException` afterward. Shared validation and lifecycle behavior stay outside platform conditionals.
- Android notification scheduling requires a registered default channel. Queue modes hand pending work to the OS on background transitions.
- Every `GestureController` balances only its own `EnhancedTouchSupport.Enable()` acquisition. Multiple live controllers and external release must remain safe.
- One haptic play replaces the previous play and cancels its pending auto-stop. Negative preset duration loops until explicit stop; `StopCurrentHaptic()` is idempotent.
- `DeviceServicesHost` is the shared polling host. Do not create per-service update GameObjects when the host can own the callback.
- The first `DeepLinkService` subscriber receives a pending cold-start link once. Construct the service early; event names are not persistent state.
- ATT returning `Authorized` outside iOS means “not applicable,” not an observed user decision.
- Permission, ATT, battery, safe-area, native-alert, and review Editor overrides are process-wide statics. Simulator code must install and remove them symmetrically; tests must reset overrides they touch.
- The iOS location permission bridge retains each `CLLocationManager` delegate until authorization changes and removes it only after callback dispatch. Do not simplify the static delegate-retention collection into a local lifetime.
- Android Photos and Notifications permissions use API-33 runtime permissions (`READ_MEDIA_IMAGES` and `POST_NOTIFICATIONS`) and short-circuit as granted on older APIs. Keep runtime checks and generated manifest entries aligned.

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
- Update `README.md`, the relevant `docs/` page, and the canonical sample README when public behavior or sample setup changes.
- Update this guide only for durable subsystem, native-boundary, build-ownership, or test conventions.

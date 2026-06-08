# GameLovers.MobileServices Tests - AI Agent Guide

This file contains testing conventions for the `com.gamelovers.mobileservices` package. It is the source of truth when reading, editing, or creating test files under `Tests/`.

For runtime architecture, gotchas, and package-level context, see the parent [`AGENTS.md`](../AGENTS.md).

## 1. Placement Rules (EditMode vs PlayMode)
- **EditMode / Unit** (`EditMode/Unit/`): Pure-logic types and services whose Editor-platform code path is a simple log / safe no-op. Includes:
  - `NativeUiService` (Editor logs only)
  - `HapticsService` exercised through its internal `IHapticsBackend` injection ctor (no `HapticsHost` spawn)
  - `EditorHapticsBackend`, `NoOpHapticsBackend`
  - `ActiveGesture`, `SwipeInput`, `TapInput` (gesture math)
  - `PendingNotification`, `EditorGameNotification`, `GameNotificationChannel`, `OperatingMode`
  - `ScreenWakeService`, `IosAudioSessionService`, `PermissionsService` (Editor short-circuits to `Granted`), `AttService` (Editor short-circuits to `Authorized`)
  - `DeviceService` injection ctor (with NSubstitute mocks)
  - `DeepLinkService` cold-start-absent path (Editor `Application.absoluteURL` is empty)
  - `SafeAreaContainer` (UI Toolkit `VisualElement`, no host needed)
  - Use `[Test]`. NSubstitute is referenced only on the EditMode asmdef.
- **PlayMode / Unit** (`PlayMode/Unit/`): Anything that spawns or relies on `DontDestroyOnLoad` MonoBehaviours or Unity callback frames:
  - `MobileNotificationService` (creates `GameObject("NotificationService")`)
  - `HapticsService` auto-stop paths (spawn `HapticsHost`)
  - `DeviceServicesHost` (LateUpdate / SecondTick / Focus / iOS-LPM fan-out)
  - `SafeAreaService`, `BatteryService` (depend on `DeviceServicesHost`)
  - `PermissionsCallbackReceiver`, `AttCallbackReceiver` (need a live MonoBehaviour to receive `UnitySendMessage`-style payloads)
  - Use `[UnityTest]` returning `IEnumerator`.
- **PlayMode / Smoke** (`PlayMode/Smoke/`): Lightweight "instantiate without throwing" tests. `GestureController` lives here — driving real `EnhancedTouch` events deterministically requires test-input plumbing that exceeds the smoke-test scope; we only verify lifecycle (enable/disable subscription).
- **Editor tooling (NOT tested)**: types under `Editor/` are validated by manual editor smoke + on-device builds, not by automated tests. This explicitly includes `MobileServicesDeviceSimulatorPlugin`, `MobileSimulatorRuntimeOverlay`, `MobileSimulatorState`, `MockBuilders`, `EditorPlatformSimulator`, `MobileServicesSettings` / `MobileServicesSettingsProvider`, `MobileServicesScanner`, `MobileServicesBuildPostprocessor`. The previous `GameLovers.MobileServices.Editor.Tests` assembly was removed in v1.0.0 — see §9 for rationale.

**Decision tree**: if the type spawns a `GameObject`, subscribes to a Unity callback that needs a frame to fire, or relies on an internal MonoBehaviour singleton → **PlayMode**; if it lives under `Editor/` → **not tested**; otherwise → **EditMode**.

## 2. Namespace and Suppression
All test files use `namespace GameLoversEditor.MobileServices.Tests` with the suppression comment:
```csharp
// ReSharper disable once CheckNamespace
```

This matches the convention established by sibling package `com.gamelovers.services` (namespace `GameLoversEditor.Services.Tests`). The `GameLoversEditor.*` prefix signals "these types live in test-only assemblies" and avoids shadowing runtime namespaces.

## 3. Naming
- **Test class**: `{TypeName}Test` for EditMode (e.g., `HapticsServiceTest`, `GameNotificationChannelTest`); add a `PlayMode` suffix when an EditMode test class for the same type already exists (e.g., `HapticsServicePlayModeTest`). Smoke tests use `{TypeName}SmokeTest`.
- **Test method**: `MethodOrBehavior_Condition_ExpectedResult` — e.g., `PlayPreset_None_NoBackendCall`, `Ctor_NullNotification_ThrowsArgumentNullException`, `KeepAwake_True_SetsScreenSleepTimeoutNeverSleep`.
- **SetUp method**: Named `Init()`.
- **TearDown method**: Named `Dispose()` (when calling `service.Dispose()`) or `Cleanup()` (when doing `Object.Destroy` / `DeviceServicesHost.ResetForTests()`).

## 4. Mock / Helper Types
- Define mock interfaces and classes as **nested types** inside the test class when needed.
- EditMode tests use **NSubstitute** (`Substitute.For<T>()`) for interface mocking — referenced only in the EditMode asmdef.
- For internal types whose construction shape is awkward to mock (e.g. `IHapticsBackend`), prefer hand-written `private sealed class FakeXBackend : IXBackend` nested inside the test class with explicit counters; this keeps the test reading like the production call sequence.
- PlayMode tests use concrete MonoBehaviour stubs / direct interaction with the real type. NSubstitute is **not** referenced in the PlayMode asmdef.

## 5. Black-Box Testing Policy
- **No reflection-based testing.** Tests must exercise the runtime code through its public/internal API surface only — no `BindingFlags.NonPublic` reads or writes of private fields, properties, or events.
- Internal types and members are accessible thanks to `Runtime/AssemblyInfo.cs` granting `[assembly: InternalsVisibleTo("GameLovers.MobileServices.{Edit,Play}Mode.Tests")]`. That access is intentional and is **not** considered reflection.
- If a code path is genuinely unreachable through any black-box surface (e.g. `DeepLinkService` cold-start replay needs `Application.absoluteURL`, which the Editor cannot fabricate), the test for that path is **omitted** rather than worked around. The path is documented in this file (see §9 below) and verified manually on-device.

## 6. Fields and Setup
- Fields are prefixed with `_` and use **concrete types** (not interfaces): `private HapticsService _haptics;`, `private DeviceServicesHost _host;`.
- Constants use `PascalCase`: `private const float DefaultLevel = 0.42f;`.
- `[SetUp]` creates fresh service instances. Services that create or hold references to GameObjects (`MobileNotificationService`, `HapticsService` after a Play* with auto-stop, anything depending on `DeviceServicesHost`) **must** call `Dispose()` and/or `DeviceServicesHost.ResetForTests()` in `[TearDown]`.

## 7. Assertion Style
- NUnit classic model only: `Assert.AreEqual`, `Assert.AreSame`, `Assert.IsTrue`, `Assert.Throws<T>`, `Assert.DoesNotThrow`, etc.
- No constraint-model (`Assert.That(...)`) usage.
- Async tests use `await tcs.Task` (or `await Task.WhenAny(tcs.Task, Task.Delay(timeout))` for timeout safety) inside `[UnityTest]` bodies that yield `null` between awaits — there is no `[Test, Timeout]` story for `Task<T>`-returning APIs in this package.

## 8. PlayMode Test Cleanup
- `DeviceServicesHost`, `HapticsHost`, `PermissionsCallbackReceiver`, `AttCallbackReceiver`, and the `GameObject("NotificationService")` are all `DontDestroyOnLoad` MonoBehaviours. PlayMode tests that touch them MUST tear down in this order:
  1. Call `service.Dispose()` (which unsubscribes from the host and removes its handlers from the event lists).
  2. For host singletons, call the static `ResetForTests()` accessor where one exists (`DeviceServicesHost.ResetForTests()`); otherwise `Object.Destroy(go)` on the GameObject.
- Without the reset, the next `[SetUp]` will receive the previous test's host instance and event subscriptions → flaky cross-test interference.

## 9. Coverage Gaps (intentional, do NOT regress to test workarounds)
The following code paths are **not** automated-testable from the EditMode/PlayMode runners and are validated manually on a device build instead. Documented here so future audits don't try to re-cover them:
- **`NativeUiService`** native paths (iOS `[DllImport]` / Android `AndroidJavaObject`) — the Editor short-circuits log-only; manual smoke on TestFlight / internal Play track.
- **`HapticsService` iOS/Android backends** (`IosHapticsBackend`, `AndroidHapticsBackend`) — wrap `[DllImport]` and `AndroidJavaObject`; manual smoke on real devices.
- **`MobileNotificationService` non-Editor flows** (`GameNotificationsMonoBehaviour` queueing/persisting notifications via PlayerPrefs across foreground/background) — Editor `CreateNotification`/`ScheduleNotification` returns the in-memory `EditorGameNotification`; the queue/clear/reschedule semantics are exercised at the `OperatingMode` enum level only.
- **`DeepLinkService` cold-start replay** — requires `Application.absoluteURL` to be non-empty, which only happens when the OS launches the app with a deep link. Black-box test #59 (cold-start absent) is the only automated coverage; the replay path is verified manually with `xcrun simctl openurl` (iOS) / `adb shell am start -a android.intent.action.VIEW -d <uri>` (Android).
- **`GestureController` end-to-end gesture detection** — driving `UnityEngine.InputSystem.EnhancedTouch.Touch` deterministically from a test requires the Input System's `InputTestFixture`, which adds a non-trivial setup cost. Only the lifecycle (subscribe/unsubscribe) is smoke-tested; the math is fully covered through `ActiveGesture` / `SwipeInput` / `TapInput` unit tests.
- **`BatteryService` low-power-mode change events** — driving an `OnIosLowPowerModeChanged` fan-out *through* `BatteryService` requires a `DeviceServicesHost` event invocation that's not reachable without reflection; we instead test the host's fan-out directly (`DeviceServicesHostTest.OnIosLowPowerModeChanged_PublicMethod_FanOutsToSubscribers`) and trust subscription wiring in `BatteryService` ctor.
- **Editor tooling** — all `Editor/` assembly types are validated manually (open `Window > General > Device Simulator`, drive the Mobile Services plugin panel and its in-Game-view overlay, exercise the Settings provider, perform a real iOS / Android build). Automated tests in this scope were dropped in v1.0.0 (`GameLovers.MobileServices.Editor.Tests` assembly and its five test classes — `EditorPlatformSimulatorTest`, `MobileServicesBuildPostprocessorTest`, `MobileServicesExplorerWindowTest`, `MobileServicesSettingsTest`, `MobileSimulatorWindowTest` — were removed) because they tested UIToolkit wiring rather than behaviour that affects players, and the maintenance cost (UIToolkit visual-tree diffs, EditorWindow lifecycle quirks) exceeded the regression-catch value.

## 10. Test Directory Layout

| Directory | Contents |
|-----------|----------|
| `EditMode/Unit/` | NUnit + NSubstitute; pure-logic services, math types, enum sanity, Editor-shortcircuit paths, UI Toolkit container |
| `PlayMode/Unit/` | `[UnityTest]`; `MobileNotificationService`, `DeviceServicesHost`, host-dependent services, callback receivers, `HapticsService` auto-stop |
| `PlayMode/Smoke/` | `GestureController` lifecycle smoke |

## 11. Update Policy
Update this file when:
- Test conventions change (new asmdef references, assertion style, naming patterns, new test categories)
- New test directories or categories are added
- Mock/stub patterns change (e.g., NSubstitute added to the PlayMode asmdef)
- A coverage gap from §9 becomes testable (e.g., a future Input System `InputTestFixture` adoption could promote `GestureController` from smoke to unit)
- An editor-tooling type previously listed in §1's not-tested group becomes amenable to automated coverage (e.g. behaviour split out into a runtime-facing helper that no longer depends on UIToolkit / EditorWindow plumbing)

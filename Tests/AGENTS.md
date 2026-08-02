# GameLovers.MobileServices Tests — AI Agent Guide

This file contains testing conventions for the `com.gamelovers.mobileservices` package. It is the source of truth when reading, editing, or creating test files under `Tests/`.

For runtime architecture, gotchas, and package-level context, see the parent [`AGENTS.md`](../AGENTS.md).

§1 and §2 are shared verbatim across every GameLovers package. A change to either must be applied to all six `Tests/AGENTS.md` files in the same working session, one commit per submodule.

## 1. ADMIT — Test Admission Test

A proposed test is admitted only if all five answers are YES. Record the first two
as comments on the test itself.

| | Question |
|---|---|
| **A1 DEFECT** | Can you name the defect in one sentence, referencing a production file and symbol? "It could break" is not a defect. |
| **A2 RED** | Can you name the exact production edit — one line or one branch, identified by `file` + `symbol` — that makes this test fail? If no such single edit exists, the test pins nothing. |
| **A3 PACKAGE** | Does every assertion read a value this package computed? Reject assertions on `new X() != new X()`, `!= null` on a freshly constructed object, default struct/enum values, or anything the C# spec or the Unity engine already guarantees. |
| **A4 CHEAPEST** | Is this the cheapest tier that covers the defect? EditMode beats PlayMode; a `[TestCase]` row on an existing fixture beats a new `[Test]`; a new `[Test]` beats a new fixture. Grep before writing. |
| **A5 UNIQUE** | Does no existing test already fail on the A2 edit? Grep the symbol under test across `Tests/` first. |

**A5-bis — inherited-type coverage.** Before proposing a fixture for a type that
derives from or wraps another tested type, grep `Tests/` for the derived type's
name and for paired `[SetUp]` fields. Base-and-derived pairs are tested jointly in
the base's fixture unless the derived type adds new public surface.

**Two mechanical disqualifiers** — violate one and the test is rejected:

- **D1 — tautology.** If the only assertion is `Assert.DoesNotThrow`,
  `Assert.IsNotNull`, or a disjunction of `Contains(...)` substrings, the test
  fails A2 unless you write down what *would* throw, be null, or not match. A
  substring disjunction that includes a string the input itself embeds is
  unfalsifiable by construction.
- **D2 — name/body contract.** The test name is a claim. If deleting the
  production feature the name mentions leaves the test green, the name is a lie.

**Smoke exemption, by directory.** Fixtures under `Smoke/` are exempt from A1 and
A2 and may assert construction-without-throwing only. Their defect class is "the
assembly no longer loads / bootstrap regressed", which is real and not expressible
otherwise. The exemption is by directory, not by assertion shape — a Unit test
that only asserts `IsNotNull` is still rejected.

## 2. RCR — Revert and Confirm Red

> Every new or strengthened test must be observed failing, once, against a
> one-line production revert, before it is committed.

Line coverage proves a line executed. It does not prove any test would notice if
that line were wrong. RCR is the cheap substitute for mutation testing, and it is
what makes a coverage number trustworthy.

**Procedure** (~90 seconds per test):

1. Write the test. Run it. Green.
2. Apply the A2 edit — invert the comparison, delete the guard clause, return
   early, comment out the one line. **One line only**: a broad deletion proves
   nothing, because it would also "fail" a tautological test via a compile error.
3. Run only that test. It must be **RED**, and the failure message must name the
   thing you broke. A red-by-`NullReferenceException` does not count — that is the
   test crashing, not asserting.
4. `git checkout -- <production file>`. Re-run. Green.
5. Record the mutation in the test's header comment.

**Recording format** — on the test, not in a separate ledger. A ledger rots the
moment a test is renamed; a comment travels with the test, appears in every diff
that touches it, and lets a reviewer re-run the mutation in 30 seconds.

```csharp
[Test]
// ADMIT: <one-sentence defect, naming a production file and symbol>
// RCR:   <file> <symbol> — <the one-line mutation> → RED (<what the failure says>). <YYYY-MM-DD>
public void Method_Condition_ExpectedResult()
```

**Anchor on `file` + `symbol`, never `file:line`.** Line numbers rot on the first
unrelated edit above them — a stale `:474` pointing at a method that moved to `:464`
sends the next reader to the wrong code and quietly destroys the comment's value.

**Budget: four lines is the target, six is the ceiling.** One sentence of ADMIT,
one of RCR, wrapped. This obeys the repo-wide rule in the root `AGENTS.md`
(§ Code comments): *"One sentence usually suffices. Multi-paragraph rationale is a
smell."* Anything past the ceiling belongs in the commit body or `docs/`, not on the
test. Two things in particular must NOT appear here:
- **Change narration.** *"An earlier version of this test was a tautology"* is diff
  context; the root `AGENTS.md` forbids it outright. A comment states the code's
  permanent condition, not its history. Put it in the commit message.
- **Investigation transcript.** The empirical detail that convinced *you* is not
  what the next reader needs. They need the mutation and the expected failure.

The one extension worth its lines is a **negative** result: naming a nearby edit
that looks like a valid mutation but is NOT one (because it is already guarded, or
because it reddens a sibling test instead). That stops the next reader repeating a
dead end, and it cannot be recovered from the code.

Also add one line per new test to the commit body: `RCR: <TestName> ← <file> <symbol> <mutation>`.
That makes `git log --grep=RCR` the audit surface.

**Two consequences, stated so RCR does not become theatre:**

- A test with no `// RCR:` line is not trusted coverage. In an audit it is a
  suspect by default.
- **Benchmarks are included, inverted:** a performance test must be observed
  *changing its number* when the measured operation is removed from the measured
  body. A benchmark whose measured region does not contain the workload is a
  tautology in `Measure` clothing.

## 3. Placement Rules (EditMode vs PlayMode)
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
- **Editor tooling (NOT tested)**: types under `Editor/` are validated by manual editor smoke + on-device builds, not by automated tests. This explicitly includes `MobileServicesDeviceSimulatorPlugin`, `MobileSimulatorRuntimeOverlay`, `MobileSimulatorState`, `MockBuilders`, `EditorPlatformSimulator`, `MobileServicesConfig` / `MobileServicesConfigEditor` / `MobileServicesConfigMenuItems`, `MobileServicesScanner`, `MobileServicesBuildPostprocessor`. The previous `GameLovers.MobileServices.Editor.Tests` assembly was removed in v1.0.0 — see §13 for rationale.

**Decision tree**: if the type spawns a `GameObject`, subscribes to a Unity callback that needs a frame to fire, or relies on an internal MonoBehaviour singleton → **PlayMode**; if it lives under `Editor/` → **not tested**; otherwise → **EditMode**.

## 4. Namespace and Suppression
All test files use `namespace GameLoversEditor.MobileServices.Tests` with the suppression comment:
```csharp
// ReSharper disable once CheckNamespace
```

This matches the convention established by sibling package `com.gamelovers.services` (namespace `GameLoversEditor.Services.Tests`). The `GameLoversEditor.*` prefix signals "these types live in test-only assemblies" and avoids shadowing runtime namespaces.

## 5. Naming
- **Test class**: `{TypeName}Test` for EditMode (e.g., `HapticsServiceTest`, `GameNotificationChannelTest`); add a `PlayMode` suffix when an EditMode test class for the same type already exists (e.g., `HapticsServicePlayModeTest`). Smoke tests use `{TypeName}SmokeTest`.
- **Test method**: `MethodOrBehavior_Condition_ExpectedResult` — e.g., `PlayPreset_None_NoBackendCall`, `Ctor_NullNotification_ThrowsArgumentNullException`, `KeepAwake_True_SetsScreenSleepTimeoutNeverSleep`.
- **SetUp method**: Named `Init()`.
- **TearDown method**: Named `Dispose()` (when calling `service.Dispose()`) or `Cleanup()` (when doing `Object.Destroy` / `DeviceServicesHost.ResetForTests()`).

## 6. Mock / Helper Types
- Define mock interfaces and classes as **nested types** inside the test class when needed.
- EditMode tests use **NSubstitute** (`Substitute.For<T>()`) for interface mocking — referenced only in the EditMode asmdef.
- For internal types whose construction shape is awkward to mock (e.g. `IHapticsBackend`), prefer hand-written `private sealed class FakeXBackend : IXBackend` nested inside the test class with explicit counters; this keeps the test reading like the production call sequence.
- PlayMode tests use concrete MonoBehaviour stubs / direct interaction with the real type. NSubstitute is **not** referenced in the PlayMode asmdef.

## 7. Black-Box / Reflection Policy
- **No reflection-based testing.** Tests must exercise the runtime code through its public/internal API surface only — no `BindingFlags.NonPublic` reads or writes of private fields, properties, or events.
- Internal types and members are accessible thanks to `Runtime/AssemblyInfo.cs` granting `[assembly: InternalsVisibleTo("GameLovers.MobileServices.{Edit,Play}Mode.Tests")]`. That access is intentional and is **not** considered reflection.
- If a code path is genuinely unreachable through any black-box surface (e.g. `DeepLinkService` cold-start replay needs `Application.absoluteURL`, which the Editor cannot fabricate), the test for that path is **omitted** rather than worked around. The path is documented in this file (see §13 below) and verified manually on-device.

## 8. Fields and Setup
- Fields are prefixed with `_` and use **concrete types** (not interfaces): `private HapticsService _haptics;`, `private DeviceServicesHost _host;`.
- Constants use `PascalCase`: `private const float DefaultLevel = 0.42f;`.
- `[SetUp]` creates fresh service instances. Services that create or hold references to GameObjects (`MobileNotificationService`, `HapticsService` after a Play* with auto-stop, anything depending on `DeviceServicesHost`) **must** call `Dispose()` and/or `DeviceServicesHost.ResetForTests()` in `[TearDown]`.

## 9. Assertion Style
- NUnit classic model only: `Assert.AreEqual`, `Assert.AreSame`, `Assert.IsTrue`, `Assert.Throws<T>`, `Assert.DoesNotThrow`, etc.
- No constraint-model (`Assert.That(...)`) usage.
- Async tests use `await tcs.Task` (or `await Task.WhenAny(tcs.Task, Task.Delay(timeout))` for timeout safety) inside `[UnityTest]` bodies that yield `null` between awaits — there is no `[Test, Timeout]` story for `Task<T>`-returning APIs in this package.

## 10. PlayMode Test Cleanup
- `DeviceServicesHost`, `HapticsHost`, `PermissionsCallbackReceiver`, `AttCallbackReceiver`, and the `GameObject("NotificationService")` are all `DontDestroyOnLoad` MonoBehaviours. PlayMode tests that touch them MUST tear down in this order:
  1. Call `service.Dispose()` (which unsubscribes from the host and removes its handlers from the event lists).
  2. For host singletons, call the static `ResetForTests()` accessor where one exists (`DeviceServicesHost.ResetForTests()`); otherwise `Object.Destroy(go)` on the GameObject.
- Without the reset, the next `[SetUp]` will receive the previous test's host instance and event subscriptions → flaky cross-test interference.

## 11. Performance Tests
None — this package has no performance fixtures (no `Performance/` directory and no `Unity.PerformanceTesting` reference on either test asmdef).

## 12. Test Directory Layout

| Directory | Contents |
|-----------|----------|
| `EditMode/Unit/` | NUnit + NSubstitute; pure-logic services, math types, enum sanity, Editor-shortcircuit paths, UI Toolkit container |
| `PlayMode/Unit/` | `[UnityTest]`; `MobileNotificationService`, `DeviceServicesHost`, host-dependent services, callback receivers, `HapticsService` auto-stop |
| `PlayMode/Smoke/` | `GestureController` lifecycle smoke |

## 13. Coverage Register

Every untested symbol worth naming is either ACCEPTED (justified — do not
re-report) or OPEN (a real gap, owed a test). An untested symbol in neither state
is an audit finding.

An ACCEPTED row needs one of exactly three falsifiable reasons:
- **(i) no branching** — zero conditionals, so there is no behaviour to pin.
- **(ii) engine-owned** — the assertion would target Unity/OS behaviour
  (`[DllImport]`, `AndroidJavaObject`, Addressables statics).
- **(iii) harness-impossible** — the state cannot be fabricated in EditMode or
  PlayMode, **with the specific blocker named**.

"Low value", "hard to test", and "covered by manual QA" are NOT valid reasons. If
none of the three applies, the row is OPEN.

ACCEPTED is dated and **expires on edit**: if the symbol's file changes, the
reason is re-checked in that PR. A `(i) no branching` row is void the moment
someone adds an `if`.

OPEN is the only place a deletion may park coverage. A test removed for weakness
either had a stronger sibling (named in the commit body) or leaves an OPEN row.
The count of OPEN rows is the honest coverage-debt number.

| Symbol (file:line) | State | Reason / Owed | Recorded |
|---|---|---|---|
| `NativeUiService` native paths (`Runtime/NativeUi/NativeUiService.cs:25`) | ACCEPTED | **(ii) engine-owned.** **`NativeUiService`** native paths (iOS `[DllImport]` / Android `AndroidJavaObject`) — the Editor short-circuits log-only; manual smoke on TestFlight / internal Play track. | 2026-05-04 |
| `IosHapticsBackend` (`Runtime/Haptics/Internal/IosHapticsBackend.cs:21`) | ACCEPTED | **(ii) engine-owned.** **`HapticsService` iOS/Android backends** (`IosHapticsBackend`, `AndroidHapticsBackend`) — wrap `[DllImport]` and `AndroidJavaObject`; manual smoke on real devices. | 2026-05-04 |
| `AndroidHapticsBackend` (`Runtime/Haptics/Internal/AndroidHapticsBackend.cs:15`) | ACCEPTED | **(ii) engine-owned.** **`HapticsService` iOS/Android backends** (`IosHapticsBackend`, `AndroidHapticsBackend`) — wrap `[DllImport]` and `AndroidJavaObject`; manual smoke on real devices. | 2026-05-04 |
| `MobileNotificationService` non-Editor flows (`Runtime/Notifications/MobileNotificationService.cs:76`) | ACCEPTED | **(iii) harness-impossible** — blocker: in the Editor there is no platform `IGameNotificationsPlatform`, so `CreateNotification`/`ScheduleNotification` never reach the Android/iOS queue-and-persist path. **`MobileNotificationService` non-Editor flows** (`GameNotificationsMonoBehaviour` queueing/persisting notifications via PlayerPrefs across foreground/background) — Editor `CreateNotification`/`ScheduleNotification` returns the in-memory `EditorGameNotification`; the queue/clear/reschedule semantics are exercised at the `OperatingMode` enum level only. | 2026-05-04 |
| `DeepLinkService` cold-start replay (`Runtime/Device/DeepLinks/DeepLinkService.cs:41`) | ACCEPTED | **(iii) harness-impossible** — blocker: requires non-empty `Application.absoluteURL` which only the OS can set. **`DeepLinkService` cold-start replay** — requires `Application.absoluteURL` to be non-empty, which only happens when the OS launches the app with a deep link. Black-box test #59 (cold-start absent) is the only automated coverage; the replay path is verified manually with `xcrun simctl openurl` (iOS) / `adb shell am start -a android.intent.action.VIEW -d <uri>` (Android). | 2026-05-04 |
| `GestureController` end-to-end gesture detection (`Runtime/Gestures/GestureController.cs:16`) | ACCEPTED | **(iii) harness-impossible** — blocker: needs InputSystem `InputTestFixture`. **`GestureController` end-to-end gesture detection** — driving `UnityEngine.InputSystem.EnhancedTouch.Touch` deterministically from a test requires the Input System's `InputTestFixture`, which adds a non-trivial setup cost. Only the lifecycle (subscribe/unsubscribe) is smoke-tested; the math is fully covered through `ActiveGesture` / `SwipeInput` / `TapInput` unit tests. | 2026-05-04 |
| `BatteryService` low-power-mode change events (`Runtime/Device/State/BatteryService.cs:104`) | ACCEPTED | **(iii) harness-impossible** — blocker: the fan-out needs a `DeviceServicesHost` event invocation that is not reachable from a black-box test. **`BatteryService` low-power-mode change events** — driving an `OnIosLowPowerModeChanged` fan-out *through* `BatteryService` requires a `DeviceServicesHost` event invocation that's not reachable without reflection; we instead test the host's fan-out directly (`DeviceServicesHostTest.OnIosLowPowerModeChanged_PublicMethod_FanOutsToSubscribers`) and trust subscription wiring in `BatteryService` ctor. | 2026-05-04 |
| Editor tooling — whole `Editor/` assembly (`Editor/GameLovers.MobileServices.Editor.asmdef`) | ACCEPTED | **(ii) engine-owned + (iii) harness-impossible** — blockers: UIToolkit visual-tree diffs and EditorWindow lifecycle are engine-owned, and the Device Simulator device profile / real iOS-Android build steps cannot be fabricated from a test runner. **Editor tooling** — all `Editor/` assembly types are validated manually (open `Window > General > Device Simulator`, drive the Mobile Services plugin panel and its in-Game-view overlay, edit the `MobileServicesConfig` asset via its Inspector and the `Tools > GameLovers > Mobile Services > Select Mobile Services Config` menu, perform a real iOS / Android build — including a multi-locale build to verify the `<locale>.lproj/InfoPlist.strings` emission). Automated tests in this scope were dropped in v1.0.0 (`GameLovers.MobileServices.Editor.Tests` assembly and its five test classes — `EditorPlatformSimulatorTest`, `MobileServicesBuildPostprocessorTest`, `MobileServicesExplorerWindowTest`, `MobileServicesSettingsTest`, `MobileSimulatorWindowTest` — were removed) because they tested UIToolkit wiring rather than behaviour that affects players, and the maintenance cost (UIToolkit visual-tree diffs, EditorWindow lifecycle quirks) exceeded the regression-catch value. | 2026-05-04 |
| `GameNotificationsMonoBehaviour` (`Runtime/Notifications/GameNotificationsMonoBehaviour.cs:67`) | OPEN | **Owed.** 539 lines with no fixture at all. This is the real queue/clear/reschedule state machine, the `OnApplicationFocus` handler, and the `PlayerPrefs` persistence — none of it reachable from the existing `OperatingMode` enum-level tests. The ACCEPTED `MobileNotificationService` row above covers the *platform* flows, not this host type itself, so this is a genuine gap rather than a justified omission. Owed: a PlayMode fixture that drives `Mode` / `OnApplicationFocus(false→true)` against a fake `IGameNotificationsPlatform` and asserts the queue, clear, and reschedule transitions. | 2026-07-31 |
| `SerializableNotification` / `SerializableNotificationConverter` PlayerPrefs JSON round-trip (`Runtime/Notifications/Internal/SerializableNotification.cs:11`, `:31`; consumed at `Runtime/Notifications/GameNotificationsMonoBehaviour.cs:493`) | OPEN | **Owed — suspected defect.** `JsonUtility.FromJson<List<SerializableNotification>>(PlayerPrefs.GetString("notifications"))` at `GameNotificationsMonoBehaviour.cs:493` is a known-broken Unity pattern: `JsonUtility` cannot deserialize a bare `List<T>` at the JSON root (it requires an object with a serialized field), so the persisted-notification restore may be silently returning null/empty on every foreground. Zero coverage today, so nothing would notice. Owed: a round-trip test (serialize → `PlayerPrefs` string → deserialize) that either proves the pattern works or turns this row into a bug fix (wrap in a serializable container type). | 2026-07-31 |
| `PermissionsService.EditorCheckOverride` (`Runtime/Device/Permissions/PermissionsService.cs:31`) / `AttService.EditorCurrentStatusOverride` (`Runtime/Device/Tracking/AttService.cs:27`) test-side leak | OPEN | **Owed.** Both are process-wide statics and no test resets them, so `PermissionsServiceTest` / `AttServiceTest` / `MultiPermissionRequestTest` assert the bare-editor `Granted` / `Authorized` short-circuit only while nothing has engaged the simulator — results depend on whether the Device Simulator panel was opened earlier in the same editor session (the panel `Engage()`s the overrides and only `Disengage()`s on close). The mechanism is documented in the package root `AGENTS.md` ("Editor Permission/ATT default depends on whether the simulator is engaged"); the test-side leak is documented nowhere. Owed: a `[SetUp]`/`[TearDown]` that nulls both statics (plus the `EditorRequest*Override` siblings) in the three affected fixtures. | 2026-07-31 |

## 14. Update Policy
Update this file when:
- Test conventions change (new asmdef references, assertion style, naming patterns, new test categories)
- New test directories or categories are added
- Mock/stub patterns change (e.g., NSubstitute added to the PlayMode asmdef)
- A coverage gap from §13 becomes testable (e.g., a future Input System `InputTestFixture` adoption could promote `GestureController` from smoke to unit)
- An editor-tooling type previously listed in §3's not-tested group becomes amenable to automated coverage (e.g. behaviour split out into a runtime-facing helper that no longer depends on UIToolkit / EditorWindow plumbing)

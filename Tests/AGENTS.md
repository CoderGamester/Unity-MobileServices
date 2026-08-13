# GameLovers MobileServices Tests — Agent Guide

This guide adds test-only rules to the package and host guides.

## Shared test rules

<!-- BEGIN SHARED TEST RULES -->
### Admission

A new test is admitted only when all six answers are yes:

| Check | Requirement |
|---|---|
| **A1 Defect** | Name the production file/symbol and the incorrect behavior in one sentence. “It could break” is not a defect. |
| **A2 Red** | Name a plausible production edit that should make the assertion fail. Prefer one line/branch; shared-path integration mutations are allowed when isolation is dishonest. |
| **A3 Package-owned** | Every assertion must read behavior this package computes, not C# defaults, fresh-object non-nullness, or Unity guarantees. |
| **A4 Cheapest** | Use the cheapest honest tier: a test case before a new test, EditMode before PlayMode, and an existing fixture before a new fixture. |
| **A5 Unique** | Grep the symbol, derived/wrapper types, and paired setup fields. Do not add a test already reddened by the same narrow defect. |
| **A6 Environment** | Control ambient renderer, Addressables, sample, static, and project state, or branch the expectation on the state actually observed. |

Two additional rejects apply:

- **D1 Tautology:** a lone `DoesNotThrow`, freshly-created non-null assertion, language default, or input-derived substring match pins no package behavior unless used by a named harness sentinel.
- **D2 Name/body mismatch:** deleting or bypassing the behavior promised by the test name must not leave the test green. Strengthen the assertion or rename the test to its actual claim.

Fixtures under `Smoke/` are exempt from A1/A2 and may assert construction/bootstrap viability. The exemption is directory-scoped, not permission to use smoke assertions in Unit or Integration fixtures.

### Revert and Confirm Red (RCR)

Every new or strengthened behavioral test must be observed failing once against a plausible production mutation before commit:

1. Run the new test against normal production code and observe GREEN.
2. Preserve the exact working patch or use an isolated worktree; then apply the A2 mutation. Never restore a dirty file from `HEAD`.
3. Run the smallest attributable filter. RED must come from the intended assertion with a diagnostic failure, not a compile error or unrelated `NullReferenceException`.
4. Restore the saved production state, confirm the mutation is gone without losing other edits, and observe GREEN again.
5. Record the observation on the test using `file + symbol`, never a line number.

Use this compact form, targeting four lines and never exceeding six:

```csharp
[Test]
// ADMIT: <owned defect naming production file and symbol>
// RCR: <file> <symbol> — <mutation> → RED (<assertion failure>). <YYYY-MM-DD>
public void Method_Condition_ExpectedResult()
```

Do not narrate investigation history in the test. A nearby mutation that looked valid but stayed green may be recorded when that negative result prevents repeated work.

When a test resists an isolated mutation, classify it before acting:

| Verdict | Meaning | Action |
|---|---|---|
| **A3 reject** | No package production behavior participates. | Delete the test. |
| **A5 duplicate** | The same narrow mutation already belongs to a sibling. | Delete it and name the surviving sibling in review/commit context. |
| **D2 overclaim** | The mutation implied by the name leaves the body green. | Strengthen or rename. |
| **UNFALSIFIABLE** | Real package behavior is double-guarded or cannot be broken by a safe isolated edit. | Keep only after attempted mutations are recorded with the specific reason. |
| **SHARED-PATH** | A broader mutation reddens this legitimate integration path together with siblings. | Keep, recording the observed mutation and blast radius. |

Unannotated tests have three possible histories: observed RED with lost write-back, collateral RED under another test's mutation, or never probed. Check `.test-all/rcr/` before probing and never write prepared annotation text without matching observed evidence. Mutation records stay under `.test-all/rcr/`, not `/tmp`.

Benchmarks use the inverted check: removing the workload from the measured body must materially change the result. Run the actual test assembly; a plain Unity open does not compile assemblies constrained by `UNITY_INCLUDE_TESTS`.
<!-- END SHARED TEST RULES -->

## Placement

- `EditMode/Unit/`: pure logic and Editor platform branches that use an override, log, or safe no-op. NSubstitute is available only here.
- `PlayMode/Unit/`: services that create or depend on `DontDestroyOnLoad` hosts, Unity callbacks, callback receivers, or frame timing.
- `PlayMode/Smoke/`: bootstrap/lifecycle checks only.
- Package `Editor/` tooling is not referenced by these test asmdefs. Validate Device Simulator, config inspector, overlay, scanner, and native-build tooling through focused Editor and platform verification.

## Package conventions

- Use namespace `GameLoversEditor.MobileServices.Tests` and the existing namespace-suppression comment.
- Use singular `{Subject}Test`; add `PlayMode` when the same subject also has an EditMode fixture.
- Prefer NSubstitute for simple EditMode interfaces, hand-written fakes for sequence-sensitive backends, and real MonoBehaviour stubs in PlayMode.
- No private reflection. Use public/internal surfaces exposed through `InternalsVisibleTo`; omit platform state that cannot be fabricated honestly.
- Disposable service tests separately cover owned-resource release, idempotent disposal, and post-disposal operations. Shared-global acquisitions require two-owner lifecycle coverage.

## Cleanup

- Dispose services before resetting their hosts. Reset `DeviceServicesHost`, `HapticsHost`, permission/ATT receivers, and notification hosts touched by the fixture.
- Reset every process-wide Editor override installed by the test, including permission, ATT, battery, safe-area, native-alert, and review hooks. Never rely on another fixture's cleanup.
- Simulator adapters unregister the exact running-service target they registered; a parallel throwaway instance is not acceptable test coverage.

## Verification

- Run EditMode for pure service logic and PlayMode for host/lifecycle behavior. Platform-native paths additionally require relevant simulator and device/build evidence.
- This package has no performance test assembly; do not add `Unity.PerformanceTesting` without an explicit dependency/asmdef decision.
- Update this guide only when a stable assembly, placement, cleanup, or helper convention changes.

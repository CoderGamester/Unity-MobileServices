# Mobile Services Build Ownership Design

Date: 2026-08-06
Status: Approved for planning

## Context

The Mobile Services package needs ready-to-open sample scenes that users import on demand through Unity Package Manager. Sample-only build preparation must not remain active in projects that never import a sample, and deleting an imported sample must remove all of its tooling without leaving an altered Build Profile or scene list behind.

The package-wide native build integration remains valuable to consumers. It must coexist predictably with other Android and iOS build processors, including the EDM4U, Usercentrics, AppsFlyer and Sentry integrations currently present in `/Users/miguel.cartier/Desktop/demons`.

## Goals

- Ship one UPM-importable sample bundle containing four independent, ready-to-open scenes.
- Keep all sample UI, menus, build commands and native sample hooks inside that imported sample.
- Build a selected sample without persistently changing the project's Build Profile, global scene list or `MobileServicesConfig` asset.
- Keep the package's native Android/iOS postprocessor available to normal consumers.
- Make package-native mutations explicit, additive, idempotent and diagnosable when another processor owns the same value.
- Support Unity `6000.0.80f1`, `6000.3.18f1` and `6000.5.7f1` only.
- Establish a safe UPM integration path for the demons project.

## Non-goals

- The sample bundle will not install or configure third-party SDKs.
- The package will not attempt to impose a universal callback order over processors it does not own.
- The sample build command will not replace a project's CI/CD build pipeline.
- Importing the package alone will not mutate native build output.

## UPM Sample Structure

`package.json` exposes one sample entry named **Mobile Services Samples**. Importing it copies one self-contained bundle into the consumer project's normal `Assets/Samples/...` location.

```text
Samples~/MobileServicesSamples/
├── GameLovers.MobileServices.Samples.asmdef
├── MobileServicesPlayground/
├── HapticsPalette/
├── NotificationsScheduler/
├── DeepLinkRouter/
└── Editor/
    ├── GameLovers.MobileServices.Samples.Editor.asmdef
    └── Build/
        ├── MobileServicesSampleBuildControls.cs
        ├── MobileServicesSampleBuilder.cs
        ├── MobileServicesSampleBuildConfiguration.cs
        └── DeepLinkRouterSampleBuildPostprocessor.cs
```

Each feature folder owns its scene, controller, UXML, USS, `PanelSettings`, README and related metadata. One runtime sample assembly and one editor sample assembly avoid duplicated infrastructure while keeping the four scenes independently openable and testable.

Scene discovery uses each scene asset's stable GUID through `AssetDatabase.GUIDToAssetPath`. It never hardcodes either the package's `Samples~` path or Unity's versioned `Assets/Samples/...` import path.

Deleting the imported **Mobile Services Samples** folder removes the runtime sample code, transient Game-view controls, menus and sample postprocessor together. The main package editor assembly has no compile-time or reflection reference to the sample assemblies.

## Sample Editor Experience

When one of the four sample scenes is open outside Play Mode, its sample editor assembly injects a transient UI Toolkit panel into that scene's `UIDocument`. The panel is visually distinct and labelled `EDITOR ONLY • SAMPLE BUILD`. It is not serialized and cannot enter a player build.

The panel exposes:

- **Build This Sample…** — runs the scoped build workflow.
- **Hide** — hides the panel for the current editor session through `SessionState`.

Each scene also has matching sample-local menu commands:

- `Tools/GameLovers/Mobile Services Samples/<Scene>/Build This Sample…`
- `Tools/GameLovers/Mobile Services Samples/<Scene>/Show Game View Build Controls`

Controls are removed before entering Play Mode and reappear after returning to Edit Mode unless hidden. No sample preference or build state is stored in `EditorPrefs`.

## Scoped Sample Build Workflow

`Build This Sample…` performs one synchronous build and leaves no recovery state behind:

1. Refuse to start while compiling, entering Play Mode or building another player.
2. Resolve the selected scene by GUID and verify that its imported asset and dependencies exist.
3. Ask Unity to save dirty scenes; cancellation stops without mutation.
4. Require the active build target to be Android or iOS. A non-mobile target produces a dialog directing the user to switch target first.
5. Ask for an output location using target-appropriate file/folder selection.
6. Push an in-memory `MobileServicesConfig` build override describing only that sample's native requirements.
7. Call `BuildPipeline.BuildPlayer` with `BuildPlayerOptions.scenes` containing only the selected sample scene. The active Build Profile and global `EditorBuildSettings.scenes` are never modified.
8. Dispose the in-memory override in a `finally` block and report the `BuildReport` result.

The build override is a general package-editor facility in `Editor/Settings/MobileServicesBuildContext.cs`, owned alongside `MobileServicesConfig` and independent of every sample type. It supports a single scope, rejects nesting, is never serialized and resets on domain reload. The package postprocessor reads the effective persisted configuration plus the active override during the synchronous build. This also gives project CI scripts a safe way to provide temporary native settings without rewriting a shared asset.

Because no persistent build setting is changed, the old Prepare, Restore, Forget/Discard and snapshot-recovery workflow is removed. There is no `Library/GameLovers/MobileServices/SampleBuildPreparation.json` file and no orphaned state if the sample is deleted.

## Sample-specific Native Integration

The Deep Link Router scene keeps its deterministic URL scheme support inside `DeepLinkRouterSampleBuildPostprocessor` in the imported sample editor assembly. The callback first confirms that the Deep Link Router scene GUID is in the build's effective scene list.

It then merges only the sample's URL scheme or Android intent filter, deduplicates existing entries and runs at callback order `1100`, after the core package postprocessor. No Deep Link Router scene name, path or native hook remains in the main package editor assembly.

## Package-wide Native Build Postprocessor

The core `MobileServicesBuildPostprocessor` remains in the package and moves from `Editor/Build/` to `Editor/NativeBuild/` so package-native integration is clearly separated from sample build tooling.

### Activation and ordering

- With neither a persisted `MobileServicesConfig` asset nor an active in-memory build override, the postprocessor is a complete no-op.
- Each platform mutation runs only when at least one relevant setting is enabled.
- The callback order is `1000`: late enough to merge normal/default-order plugin output, but before project-specific final repair callbacks such as demons' `int.MaxValue` Usercentrics PBX/Podfile repair.
- `ManageNativeBuildManually` remains the complete opt-out and also disables validation.

### Android behavior

- Do not inspect or rewrite generated manifests when no Android permission/query setting is enabled.
- When active, locate exactly one generated manifest containing `UnityPlayerActivity` or `UnityPlayerGameActivity`; ambiguity fails with the candidate paths.
- Merge permissions, share queries and other configured elements by namespace-aware identity checks.
- Detect `com.google.android.play:review` across generated Gradle files before injection. An existing EDM4U or hand-authored dependency wins and is not duplicated.
- Inject only into the Unity library Android module when injection is required; failure to identify the module reports a precise manual remedy.

### iOS behavior

- Preserve existing plist keys that Mobile Services does not own.
- Merge and deduplicate `CFBundleLocalizations` rather than replacing its array.
- Merge only Mobile Services-managed keys in each localized `InfoPlist.strings` file rather than overwriting the file.
- Reuse the Xcode target's existing `CODE_SIGN_ENTITLEMENTS` path when one exists; otherwise create one deterministic Mobile Services entitlements file.
- Merge entitlement arrays and capabilities without deleting third-party values.
- If an explicitly configured scalar value differs from an existing non-empty value for the same managed key, fail the build with both owners/values identified. The user must align the values, disable that Mobile Services setting or select manual native management. Silent last-writer-wins behavior is not accepted for privacy copy or signing capabilities.

### Diagnostics

Each active build logs one summary containing:

- the persistent config asset path or in-memory override identity;
- files changed;
- values already present and skipped;
- dependencies supplied by another integration;
- any ownership conflict that stops the build.

## Demons Integration Contract

The current demons project is on Unity `6000.5.7f1` and is compatible with this ownership model:

- Its custom Android manifest contains one `UnityPlayerGameActivity`, which is an unambiguous target.
- EDM4U already places `com.google.android.play:review:2.0.2` in `mainTemplate.gradle`, so Mobile Services must detect it and skip injection.
- Usercentrics plus AppsFlyer remain the owners of `NSUserTrackingUsageDescription` and its localization flow.
- The Mobile Services config for demons therefore starts with `AppTracking = false` and `IncludePlayReviewDependency = false`.
- Demons enables only native permissions, queries and capabilities it intentionally delegates to Mobile Services.

Adding a `GameLovers.MobileServices` reference to a demons assembly does not itself activate native mutation. The project must create a persisted Mobile Services config or push an explicit build override.

Integration is accepted only after fresh Android and iOS builds prove the final generated artifacts. Source inspection alone is not treated as proof against future third-party processor versions.

## Error Handling

- Cancelled save/output dialogs leave all state untouched.
- Unsupported build targets do not switch target automatically.
- Multiple persisted `MobileServicesConfig` assets are a build error, not an arbitrary first-match choice.
- A nested build override is rejected before build start.
- Build exceptions and failed `BuildReport` results still dispose the override through `finally`.
- Native document parse failures identify the exact file.
- Ownership conflicts stop the build before silently replacing privacy, localization or entitlement data.

## Verification

Verification is serialized because one Unity project permits only one editor/test lock.

1. Static package checks verify the single sample entry, expected four scenes, GUID-based discovery, sample-local editor assembly and absence of sample references in the package editor assembly.
2. Deterministic native-mutation helpers receive EditMode fixtures covering both polarities: add missing values and preserve/deduplicate existing values. Conflicting scalar fixtures must fail with attribution.
3. The existing Editor-tooling test policy is narrowed: UI Toolkit visual wiring remains manual, while pure native-file transformation and build-context state receive automated coverage.
4. Import the sample bundle into a disposable consumer project for each supported Unity editor; prove the imported runtime/editor assemblies compile and all four scenes open without missing scripts.
5. In Unity `6000.5.7f1`, visually inspect each edit-mode Game-view panel, hide/show behavior and removal in Play Mode.
6. Build representative Android and iOS samples and inspect the final manifest, Gradle dependency graph, plist, localized strings, PBX project and entitlements.
7. Install the UPM package in demons, configure the ownership contract above, then build Android and iOS. Record the final generated artifact identities and verify that AppsFlyer, Usercentrics, Sentry, EDM4U and Mobile Services contributions coexist.

## Documentation and Migration

Update `package.json`, the package README, `Samples~/README.md`, `docs/samples.md`, `docs/build-pipeline.md`, troubleshooting guidance, `AGENTS.md` and the current unpublished `CHANGELOG.md` section together.

Remove all documentation for Prepare, Restore, snapshot recovery and separately importable sample entries. Document the new single import followed by independent scene selection, the package postprocessor activation rule, callback order, conflict policy, manual opt-out and demons ownership example.

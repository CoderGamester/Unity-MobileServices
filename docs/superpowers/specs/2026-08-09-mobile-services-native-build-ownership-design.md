# Mobile Services Native Build Ownership Design

Date: 2026-08-09
Status: Ready for user review

## Context

The Mobile Services package exposes runtime systems whose native requirements must be configured in consumer Android and iOS builds. Its optional UPM sample is now one imported bundle containing four scenes and sample-only build preparation. Importing or deleting that sample must add or remove all sample tooling without changing the package's production integration contract.

Two lifetimes therefore need separate ownership:

- Package installation supplies production native-build integration for every consumer.
- Sample import supplies only the four-scene player workflow and the sample's temporary requirements.

The package must not add privacy-sensitive permissions merely because it is installed. A persisted `MobileServicesConfig` asset is the explicit production source of truth.

## Goals

- Keep one consumer-wide native integration pipeline available whenever the package is installed.
- Make production permissions, capabilities, dependencies, and deep-link registrations explicitly configuration-driven.
- Keep every sample scene reference, menu, build-preparation command, simulator adapter, and temporary requirement inside the imported sample.
- Make importing and deleting the sample add and remove all sample-only editor behavior cleanly.
- Have only one component mutate generated Android and iOS projects.
- Preserve additive, idempotent coexistence with other project and third-party build processors.
- Keep the sample ready to build without persisting its requirements into the consumer's config asset.
- Support and document only Unity `6000.0.80f1`, `6000.3.18f1`, and `6000.5.7f1`.

## Non-goals

- Installing the package will not enable every supported permission or capability.
- The scanner will not infer and silently persist native requirements.
- The sample will not own a second general-purpose native mutation pipeline.
- The package will not reference sample scenes, paths, GUIDs, assemblies, or sample-specific types.
- The build processor will not replace Unity Build Profiles, platform selection, or project CI.

## Considered Approaches

### 1. Package processor with explicit config and a temporary sample overlay — selected

The package owns the configuration schema, build context, validation, and native mutator. Production projects persist their selections in `MobileServicesConfig`. The imported sample adds its requirements to a hidden in-memory configuration clone for a canonical sample build.

This keeps one mutation pipeline, supports production consumers without requiring a sample, and gives the sample a removable lifetime.

### 2. Separate optional native-integration UPM package

A second UPM package could own all editor build integration. This is modular but creates another installation and versioning decision, makes the primary runtime package easier to misconfigure, and complicates sample dependencies. The current package is small enough that this separation does not justify the consumer friction.

### 3. Sample-owned processor only

Moving every processor into `Samples~` would remove editor automation from projects that install the runtime package without importing its sample. Those consumers would need undocumented or duplicated manual native setup, so this approach does not satisfy the production requirement.

## Ownership Model

### Main package

The main package editor assembly owns:

- `MobileServicesConfig` and its inspector/menu integration.
- `MobileServicesScanner` as a diagnostic and validation input.
- `MobileServicesBuildContext` for one temporary, non-serialized build overlay.
- `MobileServicesBuildPostprocessor` as the only component that mutates generated native projects.
- Platform-independent requirement models used by both persistent config and temporary overlays.

The package editor assembly must contain no sample knowledge and must never load or invoke the sample editor assembly. The sample editor assembly may reference the package editor assembly to push a temporary context.

### Imported sample

`Samples~/MobileServicesSamples/Editor/` owns:

- `Tools > Mobile Samples Examples > Build All` and `Restore All`.
- The stable four-scene GUID catalog and Overview-first ordering.
- Global Build Settings and Build Profile snapshot/restore handling.
- Detection of the canonical four-scene build.
- The temporary union of native requirements demonstrated by all four scenes.
- The individual-scene play-mode navigation bridge.
- The Notifications Scheduler simulator adapter.
- Any diagnostics or warnings that mention the sample bundle.

Deleting the imported sample removes all of these behaviors. No sample menu or callback remains registered by the package itself.

## Explicit Production Configuration

`MobileServicesConfig` is authoritative for normal project builds. Its persisted fields cover:

- Localized iOS permission usage descriptions.
- App Tracking Transparency usage descriptions.
- iOS capabilities and Associated Domains.
- Android permissions and share-package visibility queries.
- Play In-App Review dependency management.
- Native deep-link registrations.
- The complete `Manage Native Build Manually` opt-out.

The native processor activates only when either:

1. A persisted `MobileServicesConfig` asset exists; or
2. `MobileServicesBuildContext` has an active temporary configuration.

With neither source present, the processor is a complete no-op. Transient default instances must not accidentally activate Play Review, permissions, validation, or native-file mutation.

If multiple persisted config assets exist, the build fails with their asset paths rather than selecting one arbitrarily.

### Scanner behavior

The scanner is advisory and validating, not authoritative. It may detect that user assemblies reference a Mobile Services subsystem and report a missing corresponding configuration. It must not toggle or save config fields automatically during a build.

An explicitly enabled requirement is applied even when the scanner does not observe a reference. This permits reflection, dependency injection, addressable content, conditional code, and external assemblies that static scanning cannot prove.

## Deep-link Configuration

The independent sample `DeepLinkRouterSampleBuildPostprocessor` will be removed. Deep-link native registration becomes a generic package configuration capability used by production projects and temporary build contexts.

The configuration model provides:

- A deduplicated list of iOS custom URL schemes.
- A deduplicated list of Android browsable intent-filter registrations with a required scheme and optional host and path prefix.
- The existing iOS Associated Domains list for universal links.

The processor adds only the configured declarations. It does not infer a URL scheme from an application identifier for production projects.

The sample context computes its deterministic sample scheme from the current application identifier and adds the matching iOS URL scheme and Android intent filter. This leaves all native-document mutation in the package processor while keeping the sample value and activation rule inside the imported sample.

## Sample Build Data Flow

`Build All` changes only the effective build scene configuration and opens Unity's native build UI. At player-build time:

1. The sample preprocessor verifies that all four canonical scenes are enabled.
2. It pushes one `MobileServicesBuildContext` scope based on a clone of the persistent project config, or neutral defaults when no asset exists.
3. It additively enables the permissions, capabilities, dependencies, and deep-link registration needed by the four scenes.
4. The package processor reads the effective configuration and mutates the generated native project.
5. The sample post-build callback disposes the scope after the package processor completes.
6. An editor-update safety path disposes the scope after failed or cancelled builds.

The persistent config asset is never modified by this workflow. Build-scene restoration remains the responsibility of `Restore All` and its `SessionState` snapshot.

Repeated configuration additions must be set-like and deterministic; the same sample or production requirement cannot produce duplicate manifest, plist, entitlement, localization, or Gradle entries.

## Manual Native Management

`Manage Native Build Manually` remains a complete package-level opt-out. When enabled, the package performs no validation or generated-project mutation, including requirements contributed by the sample context.

`Build All` must warn that the imported sample's native features will require manual project configuration when this flag is enabled. It must not silently disable the consumer's opt-out or rewrite the persisted setting. The user may continue because a project-level external build system may provide the same requirements.

## Coexistence With Other Build Processors

The package processor follows these ownership rules:

- Parse and modify only the specific plist, entitlement, manifest, and Gradle elements represented by `MobileServicesConfig`.
- Preserve all unrelated values and files.
- Treat an identical existing value as satisfied and skip it.
- Merge set-like arrays and XML elements by semantic identity.
- Report conflicting non-empty scalar values with the file, key, existing value, and requested value rather than silently overwriting them.
- Detect an existing Play Review dependency before injecting another declaration.
- Use a documented stable callback order, without claiming ownership over third-party callback ordering.
- Make every transformation idempotent so reprocessing the same generated project does not change it again.

Projects whose native pipeline has a single external owner can enable `Manage Native Build Manually`. Projects with shared ownership can disable only individual Mobile Services settings they delegate elsewhere.

## Error Handling

- No persisted config and no temporary context is a valid no-op; duplicate or invalid persisted configs are reported with actionable asset paths.
- Invalid deep-link entries identify the config row and invalid field before native mutation.
- Missing or ambiguous Android Unity activity manifests stop the build rather than modifying a guessed file.
- Native XML, plist, or project parse failures identify the exact generated file.
- Temporary context creation is single-scope; nested contexts fail before mutation.
- Build failure or cancellation cannot leave a temporary sample context active.
- A missing sample scene prevents `Build All` before the scene configuration is changed.
- A missing original Build Profile preserves the Restore snapshot for later recovery.

## Verification

No test assembly or test suite ships inside the sample bundle. Sample acceptance is behavioral:

- Import the sample into clean hosts for all three supported Unity editors and confirm its runtime and editor assemblies compile without Console errors.
- Confirm package installation without a persisted config causes no native changes.
- Confirm a persisted production config produces exactly its selected Android and iOS requirements.
- Confirm Build All supplies the additive four-scene requirements without modifying the persisted config.
- Confirm deleting the imported sample removes its menus, callbacks, and assembly while production native integration remains available.
- Build twice into fresh outputs and compare the relevant native declarations to verify deduplication and idempotence.
- Exercise identical and conflicting pre-existing native values to verify preservation and actionable conflict behavior.
- Inspect final Android manifests/Gradle dependencies and iOS plist/entitlements/project files; source inspection or a successful Unity build alone is not sufficient evidence.
- Repeat representative integration builds in the Desktop/demons project with its third-party processors enabled.

Package-owned runtime behavior remains covered by the package's existing test suites. Build tooling continues to follow the package's documented manual Editor/native-build validation policy unless that policy is changed separately.

## Documentation and Compatibility

Implementation updates must keep these in sync:

- Package README and related-docs index.
- `docs/build-pipeline.md`, `docs/samples.md`, and troubleshooting guidance.
- `Samples~/README.md` and affected per-scene READMEs.
- Package and repository `AGENTS.md` ownership guidance.
- The existing unpublished changelog entry.
- `package.json` sample description without changing the package version.

`package.json` retains a Unity `6000.0` minimum because UPM cannot express the supported patch allowlist. Documentation and validation name only Unity `6000.0.80f1`, `6000.3.18f1`, and `6000.5.7f1`.

## Implementation Boundary Summary

The generic native processor and persistent configuration stay in the package because production consumers need them. All scene-aware build preparation stays in the imported sample. The sample contributes declarative temporary requirements; it never owns a competing native mutator.

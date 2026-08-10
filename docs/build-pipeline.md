# Build Pipeline & Config

The package ships a consumer-wide postprocessor (`Editor/NativeBuild/MobileServicesBuildPostprocessor.cs`) and a `MobileServicesConfig` asset that together automate the iOS Info.plist / .entitlements / Android manifest / gradle mutations the framework's permission and capability surface needs. Sample-only build tools live inside the optional **Mobile Services Samples** bundle.

## Mobile Services Config asset

`MobileServicesConfig` is an **editor-only `ScriptableObject` asset** (not a Project Settings page). Open it via **`Tools > GameLovers > Mobile Services > Select Mobile Services Config`** — this finds the single config anywhere in the project (`AssetDatabase.FindAssets`) or creates one under `Assets/Editor/` on first use, then selects it so you edit it in the Inspector. Keep it under an `Editor/` folder (the type lives in the Editor assembly, so the asset is editor-only and never ships in a player build). The asset is intended to be committed to VCS — it's team-shared state.

The Inspector (`MobileServicesConfigEditor`) shows:

- **Status box** — info when every referenced permission has an English usage description; a warning listing the count otherwise.
- **Usage descriptions (localized)** — `Permission Descriptions` is a list with one entry per `AppPermission`, and each holds a **per-locale list** of `(LocaleCode, UsageDescription)`. The `en` value is the base; add `fr`, `pt-BR`, … to localize. (Notifications has no iOS Info.plist key, so it's not emitted.)
- **App Tracking (ATT) Description** — the per-locale `NSUserTrackingUsageDescription` list. Only marked missing when the App Tracking capability is enabled.
- **Capabilities** — `Push Notifications`, `App Tracking`, `Associated Domains` (+ domain list). (Background audio is not here — use Unity's own **Player Settings > iOS > Behavior in Background = Custom > Audio**.)
- **Android manifest** — `CAMERA`, `RECORD_AUDIO`, `ACCESS_FINE_LOCATION`, `READ_MEDIA_IMAGES`, `POST_NOTIFICATIONS`, share-chooser `<queries>` block, and semantic deep-link registrations (`Scheme`, optional `Host`, optional `PathPrefix`).
- **Native deep links** — `DeepLinks.IosUrlSchemes` and `DeepLinks.AndroidIntentFilters` are deduplicated by semantic identity. Malformed rows fail before a build file is touched.
- **Android dependencies** — `Include Play Review Dependency` toggle (**ON by default**) + editable `Play Review Coordinate` (default `com.google.android.play:review:2.0.2`). When on, the Play In-App Review library is auto-injected into the generated Gradle project so `NativeUiService.RequestReview()` works with zero manual setup.
- **Build behaviour** — `Manage Native Build Manually` (**OFF by default**) — when ON, the package writes nothing to the iOS/Android build and skips the iOS usage-description validation. It's the single escape for teams that configure the native build (Xcode / Gradle) themselves. (This is *build* configuration — unrelated to render post-processing.)
- **Tools** — `Scan project for used services` (reflection-based pre-fill of capability toggles), `Fill missing English descriptions with suggested copy`, `Generate iOS Privacy Nutrition Label draft`.

Accessed in editor tooling via `MobileServicesConfig.Instance` (cached locator; it may return a transient default for convenience). Build callbacks use `TryGetPersistedConfig(out config)` and therefore perform no native work when no persisted config exists unless a sample or another explicit owner pushes a temporary context.

## Build postprocessor

`MobileServicesBuildPostprocessor : IPostprocessBuildWithReport` runs on every iOS / Android build.

### Validation step (fail-fast)

Resolves the unique persisted/effective config before scanning or file access. Missing config and `Manage Native Build Manually` are complete no-ops. Explicitly enabled malformed settings (duplicate permission/localization rows, missing English text for a configured iOS permission, incomplete enabled capabilities, malformed deep-link rows, or an invalid Maven coordinate) throw `BuildFailedException` before any native file is touched. Scanner/config mismatches are advisory warnings only; the explicit config remains authoritative. To bypass all package mutation and validation, enable `Manage Native Build Manually`.

### iOS injection step

After validation, the postprocessor mutates the post-build Xcode project:

- **Info.plist** — writes every configured **base (`en`)** usage description string. When any non-`en` locale is configured, also writes `CFBundleLocalizations` (+ `CFBundleDevelopmentRegion`).
- **Localized usage descriptions** — for every non-`en` `LocaleEntry`, writes a `<locale>.lproj/InfoPlist.strings` file (the platform-native format: `"NSCameraUsageDescription" = "…";`) and registers it on the main target so Xcode copies it into the app bundle. iOS then shows the description in the device language, falling back to the `Info.plist` base value. (The in-memory `PBXProject` is saved before `ProjectCapabilityManager` runs so the registration isn't clobbered.)
- **URL schemes** — unions configured schemes into an existing `CFBundleURLTypes` owner (or creates the package-owned entry) without replacing other SDK declarations.
- **Entitlements** — reuses an existing `CODE_SIGN_ENTITLEMENTS` path when one is configured and creates the deterministic package file only when none exists. Push Notifications and Associated Domains are merged into that file; existing domains are preserved.

Idempotent — re-running against the same Xcode project produces no diff if the configured state already matches.

### Android injection step

Android permissions and share queries are applied to the **generated Gradle project's manifest** from `IPostGenerateGradleAndroidProject`. The postprocessor parses XML with namespace-aware APIs, locates the one manifest containing Unity's actual `UnityPlayerActivity` or `UnityPlayerGameActivity`, and adds configured `<uses-permission>` entries and the optional `ACTION_SEND` `<queries>` block idempotently. If no manifest or more than one candidate activity manifest is found, the build fails with an actionable `BuildFailedException` instead of silently editing `mainTemplate.xml` or the wrong library manifest.

The package no longer mutates `Assets/Plugins/Android/mainTemplate.xml` after the build. This matters because post-build edits to that template cannot affect the already-generated player. Consumers that manage a custom manifest manually can enable `Manage Native Build Manually`.

Deep-link filters are merged by `(scheme, host, pathPrefix)` identity and are written only when the generated application manifest changes. The package requires one unambiguous Unity player activity when configured permissions, share queries, or deep links need a manifest mutation; failures name every candidate file.

### Android Gradle dependency step (Play In-App Review)

`MobileServicesBuildPostprocessor` also implements `IPostGenerateGradleAndroidProject`. When `Include Play Review dependency` is on (the default), it injects `implementation '<coordinate>'` (default `com.google.android.play:review:2.0.2`) into the generated module `build.gradle` so `NativeUiService.RequestReview()` works on Android with zero manual setup.

- **Conflict-safe**: it scans every `.gradle` file in the generated project and skips entirely if `com.google.android.play:review` is already declared by any source (hand-written gradle, EDM4U, another SDK) — it never double-declares or fights your version pin.
- **Editable**: repoint `PlayReviewDependencyCoordinate` to an internal mirror or a pinned/forced version to resolve a Gradle conflict.
- **Opt-out**: turn `Include Play Review dependency` off for non-Play targets (Amazon / Huawei / sideload) and declare it yourself.

### Deep Link Router sample requirements

The sample does not own a native build callback. For the exact ordered four-scene build prepared by **Build All**, its catalog facade adds one deterministic scheme derived from `PlayerSettings.applicationIdentifier` to the temporary `MobileServicesConfig.DeepLinks` value. The package postprocessor then performs the same additive iOS/Android merge as it does for a production config. Reordered, mixed, or partial enabled-scene builds do not activate the sample overlay.

The custom scheme is deterministic: the lower-case application identifier is filtered to ASCII letters, digits, `+`, `.`, and `-`; invalid runs become `-`, `gl-` is prefixed when the result does not start with a letter, and an empty result falls back to `gamelovers-mobile-sample`. This makes the same application identifier produce the same scheme in Android and iOS exports.

### Combined sample player build

After importing **Mobile Services Samples**, choose **Tools > Mobile Samples Examples > Build All**. The sample-owned command validates the four scenes, snapshots the effective global Build Settings or active overriding Build Profile in `SessionState`, installs the exact Overview-first scene sequence, then opens Unity's native Build Profiles window. Unity retains ownership of target selection, output location, and the final Build command.

During a canonical four-scene player build, the sample preprocessor (cleanup callback order `2000`) clones the persisted config—or a neutral all-native-disabled transient when no persisted asset exists—into a hidden in-memory object and applies the bundle's combined native requirements. The package postprocessor (callback order `1000`) reads that clone while the build is active; outside the scope it resolves only the persisted asset. Cleanup runs after the build and on a cancelled-build safety path. **Restore All** restores the captured scene list and enabled flags during the current Unity session. It never changes the persisted config asset or `EditorPrefs`; the session snapshot is unavailable after closing Unity.

The imported sample includes one `MobileServicesSampleBuildCatalog.asset` with serialized `SceneAsset` references. Type-based discovery requires exactly one catalog and validates four non-null entries, unique pages, exact `Overview → Haptics → Notifications → Links` ordering, and current paths obtained from `AssetDatabase.GetAssetPath`. The **Verify Scene Catalog** entry point emits a non-empty page/path/derived-GUID artifact for clean-host verification; no C# or shell lookup relies on hand-authored scene GUIDs or sample-relative scene path literals.

## Build mutation map & escape hatches

Everything the package writes at build time, and how to override or disable each piece. The contract is **additive, idempotent, individually opt-out-able, and always yields to consumer-owned config** — you can resolve any third-party-SDK conflict without forking the package.

| Platform | What is written | File | Override / opt-out |
|----------|-----------------|------|--------------------|
| iOS | Base (`en`) usage description strings | `Info.plist` | Per-permission text in the config asset; set the key yourself and it is not overwritten |
| iOS | Localized usage descriptions + `CFBundleLocalizations` | `<locale>.lproj/InfoPlist.strings` | Add/remove `LocaleEntry` rows per permission/ATT; emitted only for non-`en` locales |
| iOS | Push / Associated Domains capabilities | `GameLoversMobileServices.entitlements` | Per-capability toggles (see entitlements caveat below) |
| Android | `<uses-permission>` entries | generated application manifest | Per-permission Android-manifest toggles; skipped if already present |
| Android | Share `<queries>` block | generated application manifest | `IncludeShareQueriesBlock` toggle; skipped if `ACTION_SEND` already present |
| Android | `com.google.android.play:review` dependency | generated `build.gradle` | `IncludePlayReviewDependency` toggle + editable `PlayReviewDependencyCoordinate`; skipped if already declared anywhere |

Cross-cutting controls:

- **`Manage Native Build Manually`** (`ManageNativeBuildManually`, default OFF) — global escape. Makes the package perform zero native-build configuration (and skips the fail-fast iOS usage-description validation). For teams that fully configure the native build themselves or via another build tool.
- **iOS entitlements caveat** — capabilities are written to a dedicated `GameLoversMobileServices.entitlements`. Xcode allows only one entitlements file per target, so if another SDK ships its own, disable the package's capability toggles and fold the keys into the other SDK's file (or use the kill-switch and manage entitlements yourself). Plist keys are set, not blind-overwritten when a value already exists.

## Project scanner

`MobileServicesScanner.Scan()` reflects over the project's user assemblies looking for type references to:

- `MobileNotificationService` → `UsesNotifications` (drives Push Notifications capability)
- `DeepLinkService` → `UsesDeepLinks` (drives Associated Domains capability)
- `AttService` / `IAttService` → `UsesAtt` (drives App Tracking capability)
- `PermissionsService` / `IPermissionsService` → flags every `AppPermission` as potentially required
- `NativeUiService` → `UsesNativeUiShare` (drives Android share-chooser queries block)

The scan is intentionally pessimistic for permissions — when permissions are referenced but the scanner can't statically infer WHICH permissions, all of them flag. The user un-toggles the ones they don't actually call. False positives (capability enabled when not strictly needed) are preferable to false negatives (build ships missing an entitlement).

## Manual fallback

Teams that prefer to manage `Info.plist` / `.entitlements` / Android manifests by hand can opt out of the automation by leaving every capability toggle off in the config asset (or enabling `Manage Native Build Manually`). The postprocessor's validation step still runs but with nothing to inject. In that case, the configuration steps you need to follow:

### iOS Info.plist

| Permission | Key |
|------------|-----|
| Camera | `NSCameraUsageDescription` |
| Microphone | `NSMicrophoneUsageDescription` |
| Location (when in use) | `NSLocationWhenInUseUsageDescription` |
| Location (always) | `NSLocationAlwaysAndWhenInUseUsageDescription` |
| Photo Library | `NSPhotoLibraryUsageDescription` |
| Photo Library (add only) | `NSPhotoLibraryAddUsageDescription` |
| App Tracking | `NSUserTrackingUsageDescription` |

### iOS .entitlements

| Capability | Entitlement |
|------------|-------------|
| Push Notifications | `aps-environment` (set to `development` or `production`) |
| Associated Domains | `com.apple.developer.associated-domains` array |

### Android manifest (manual fallback)

```xml
<uses-permission android:name="android.permission.CAMERA" />
<uses-permission android:name="android.permission.RECORD_AUDIO" />
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
<uses-permission android:name="android.permission.READ_MEDIA_IMAGES" />
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />

<!-- For share-sheet visibility on API 30+ -->
<queries>
    <intent>
        <action android:name="android.intent.action.SEND" />
        <data android:mimeType="*/*" />
    </intent>
</queries>
```

### Android gradle (for `NativeUiService.RequestReview()`)

Auto-injected by default (see the Android Gradle dependency step above). Only needed manually if you turn `Include Play Review dependency` off:

```gradle
implementation 'com.google.android.play:review:2.0.2'
```

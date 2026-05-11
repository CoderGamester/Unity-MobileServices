# Build Pipeline & Project Settings

The package ships a build postprocessor (`MobileServicesBuildPostprocessor`) and a Project Settings panel (`Edit > Project Settings > GameLovers > Mobile Services`) that together automate the iOS Info.plist / .entitlements / Android manifest mutations the framework's permission and capability surface needs.

## Project Settings panel

Open via `Edit > Project Settings > GameLovers > Mobile Services`.

Sections:

- **Status badge** — `All required keys configured` (green) when every referenced permission has an English usage description, `N missing key(s) — fix before iOS build` (red) otherwise.
- **Usage descriptions** — one row per `AppPermission` that maps to an Info.plist key (Notifications is excluded — iOS doesn't surface it as a key). Each row has a `Missing` red pill, a multi-line text area for the English copy, and a `Suggest copy` button that drops in a starter sentence honouring Apple's review guidelines.
- **AppTracking** — `NSUserTrackingUsageDescription` text. Only marked missing when the App Tracking capability is enabled below.
- **Capabilities** — `Push Notifications`, `Background Audio`, `App Tracking`, `Associated Domains` (with a domain-list text area, one per line, e.g. `applinks:example.com`).
- **Android manifest** — `CAMERA`, `RECORD_AUDIO`, `ACCESS_FINE_LOCATION`, `READ_MEDIA_IMAGES`, `POST_NOTIFICATIONS`, share-chooser `<queries>` block.
- **Build behaviour** — `Allow build with placeholder usage descriptions` toggle. **OFF by default.** When ON, missing usage descriptions are auto-injected as `[GameLovers placeholder — replace before App Store submission]` instead of failing the build. Apple WILL reject those placeholders on submission — by design.
- **Tools** — `Scan project for used services` (reflection-based pre-fill of capability toggles), `Generate iOS Privacy Nutrition Label draft` (Markdown summary of declared data uses for App Store submission).

The settings asset is persisted to `ProjectSettings/MobileServicesSettings.asset` and is intended to be committed to VCS — it's team-shared state.

## Build postprocessor

`MobileServicesBuildPostprocessor : IPostprocessBuildWithReport` runs on every iOS / Android build.

### Validation step (fail-by-default)

Reads the settings asset + runs the project scanner. For each referenced permission that has an Info.plist key but an empty usage description, the postprocessor:

1. **Default (soft mode OFF)** — throws `BuildFailedException` with a message listing every missing key and a fix hint pointing at the Project Settings panel.
2. **Soft mode ON** — injects `[GameLovers placeholder]` and logs a warning. The build proceeds. The submission to App Store will be rejected.

Same logic applies to `NSUserTrackingUsageDescription` when the App Tracking capability is enabled.

### iOS injection step

After validation, the postprocessor mutates the post-build Xcode project:

- **Info.plist** — writes every configured usage description string. Appends `UIBackgroundModes: audio` when the Background Audio capability is on.
- **Entitlements** — opens / creates `GameLoversMobileServices.entitlements` via `ProjectCapabilityManager` and adds Push Notifications / Background Modes (audio) / Associated Domains capabilities per the settings.

Idempotent — re-running against the same Xcode project produces no diff if the configured state already matches.

### Android injection step

Patches `Assets/Plugins/Android/mainTemplate.xml`:

- Appends `<uses-permission android:name="..." />` entries for the configured toggles (Camera, Mic, Location, Photo Library, Notifications). Idempotent (skips entries already present).
- Appends a `<queries><intent><action android:name="android.intent.action.SEND" /><data android:mimeType="*/*" /></intent></queries>` block when the share-chooser opt-in is on (Android 11+ visibility requirement for share targets).

If `mainTemplate.xml` is absent, the postprocessor logs a warning pointing at `Player Settings > Publishing Settings > Custom Main Manifest` — Unity won't generate one automatically.

The postprocessor also logs a one-time hint about the `com.google.android.play:review:2.0.1` gradle dependency that `NativeUiService.RequestReview()` needs on Android.

## Project scanner

`MobileServicesScanner.Scan()` reflects over the project's user assemblies looking for type references to:

- `MobileNotificationService` → `UsesNotifications` (drives Push Notifications capability)
- `DeepLinkService` → `UsesDeepLinks` (drives Associated Domains capability)
- `IosAudioSessionService` → `UsesAudioSession` (drives Background Audio capability)
- `AttService` / `IAttService` → `UsesAtt` (drives App Tracking capability)
- `PermissionsService` / `IPermissionsService` → flags every `AppPermission` as potentially required
- `NativeUiService` → `UsesNativeUiShare` (drives Android share-chooser queries block)

The scan is intentionally pessimistic for permissions — when permissions are referenced but the scanner can't statically infer WHICH permissions, all of them flag. The user un-toggles the ones they don't actually call. False positives (capability enabled when not strictly needed) are preferable to false negatives (build ships missing an entitlement).

## Manual fallback

Teams that prefer to manage `Info.plist` / `.entitlements` / `mainTemplate.xml` by hand can opt out of the automation by leaving every capability toggle off in the settings panel. The postprocessor's validation step still runs but with nothing to inject. In that case, the configuration steps you need to follow:

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
| Background Audio | Add `audio` to `UIBackgroundModes` in Info.plist |
| Associated Domains | `com.apple.developer.associated-domains` array |

### Android manifest

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

```gradle
implementation 'com.google.android.play:review:2.0.1'
```

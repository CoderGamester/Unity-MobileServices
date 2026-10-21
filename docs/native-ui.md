# Native UI

Static `NativeUiService` + instance-based `INativeUiService` wrapper. iOS bridge in `Plugins/iOS/NativeUi.m`, Android bridge via `AndroidJavaObject`.

## Static API

```csharp
NativeUiService.ShowAlertPopUp(
    isAlertSheet: false,
    isDismissible: false,
    title: "Delete Save?",
    message: "This action cannot be undone.",
    new AlertButton { Text = "Cancel", Style = AlertButtonStyle.Cancel },
    new AlertButton { Text = "Delete", Style = AlertButtonStyle.Destructive, Callback = OnDeleteConfirmed });

NativeUiService.ShowToastMessage("Item Collected!", isLongDuration: false);

NativeUiService.DismissAlertPopUp();

NativeUiService.RequestReview();

NativeUiService.Share(text: "Check out my high score!", url: "https://example.com/game");
```

## Async alerts (preferred)

```csharp
int selectedButton = await NativeUiService.ShowAlertPopUpAsync(
    isAlertSheet: false,
    isDismissible: false,
    title: "Connection Lost",
    message: "Please check your connection and try again.",
    new AlertButton { Text = "Reconnect", Style = AlertButtonStyle.Default });
```

The returned `Awaitable<int>` completes on Unity's main thread with the selected zero-based button index. Await it exactly once because Unity pools `Awaitable` instances. Existing `AlertButton.Callback` actions still run before completion; omit them when the caller handles the returned index. Programmatic dismissal or replacement cancels the pending await.

## Instance API

For mock-friendly consumer code:

```csharp
INativeUiService ui = new NativeUiServiceInstance();
ui.ShowToastMessage("hello", false);
```

The instance class is a pure forwarder — it holds no fields and is safe to construct any number of times. In tests, substitute the interface (e.g. via NSubstitute).

## Alert buttons

```csharp
public enum AlertButtonStyle
{
    Default,        // iOS: tinted; Android: positive button slot
    Destructive,    // iOS: red text; Android: negative button slot
    Cancel,         // iOS: bold cancel; Android: neutral button slot
}
```

Alerts accept one to three buttons. Labels and styles must each be unique within an alert: iOS matches callbacks by button text, while Android maps the three styles onto its three native button slots.

Call alert APIs from Unity's main thread. Legacy `AlertButton.Callback` actions switch through `Awaitable.MainThreadAsync` before invoking consumer code, including Android callbacks originating on the OS UI thread.

The overload without `isDismissible` preserves the original dismissible behavior. Set `isDismissible: false` for blocking alerts that must remain until the user selects a button; non-dismissible action sheets are rejected because iOS action sheets can be dismissed outside the sheet.

`DismissAlertPopUp()` closes the active alert without invoking an action. Showing a new alert replaces the current one.

## Review prompt

`RequestReview()`:

- **iOS**: `SKStoreReviewController`. Modern `requestReviewInScene:` on iOS 14+, fallback to `requestReview` on iOS 10.3–13. The OS throttles the prompt frequency — calling this every launch is safe.
- **Android**: Play Core `ReviewManagerFactory` + `launchReviewFlow`. The `com.google.android.play:review` dependency is **auto-injected at build time** (default ON — see [build pipeline](build-pipeline.md)), so no manual `mainTemplate.gradle` editing is needed. It never throws.

**Fire-and-forget — no store-page fallback.** Neither platform exposes a "was actually shown" signal: the OS may silently suppress the prompt under its own throttling quota, and that is normal, not an error. Because there is no success callback, the package **logs when the prompt is requested**:

- **iOS** — a `Debug.Log` when `RequestReview()` is called (StoreKit gives no callback at all).
- **Android** — a `Debug.Log` when the Play review flow is launched. When the Play flow genuinely cannot run (Play Core missing, the request flow returns an unsuccessful task, or launch throws), it is logged as a **warning / error** instead.

The prompt never appears in TestFlight builds, and the OS quota means it will often not appear even in production — do not gate game logic on it having been shown.

## Share sheet

`Share(text, url, imagePath, title)`:

- **iOS**: `UIActivityViewController`. `title` is ignored (iOS doesn't surface a sheet title).
- **Android**: `Intent.ACTION_SEND` via `Intent.createChooser`. `title` is the chooser title.

Any combination of `text` / `url` / `imagePath` may be supplied. `imagePath` must be an absolute filesystem path. Nulls are skipped.

### Sharing images on Android

Android 7+ rejects `file://` URIs, so images travel through a package-owned `FileProvider` (`GameLoversFileProvider`). Turn on **Include Share FileProvider** in the Mobile Services Config; the build postprocessor then declares the provider and its `res/xml/gamelovers_file_paths.xml` in the generated manifest. The provider grants access to the app's files and cache directories (`Application.persistentDataPath`, `Application.temporaryCachePath`). With the toggle off, an image share logs a warning and shares only the text. iOS needs no setup.

### Sharing a screenshot

```csharp
await NativeUiService.ShareScreenshotAsync("New high score!", "https://example.com/game");
```

`ShareScreenshotAsync(text, url, title)` waits for the end of the frame, captures what the player sees, writes a PNG to `Application.temporaryCachePath`, and opens the share sheet with it. On Android it needs the same **Include Share FileProvider** toggle.

## In-app browser

```csharp
NativeUiService.OpenInAppBrowser("https://example.com/terms");
```

`OpenInAppBrowser(url)` shows a web page in a browser sheet over the game, so the player returns with one tap: `SFSafariViewController` on iOS and Chrome Custom Tabs on Android (also on `INativeUiService`). Use it for terms of service, patch notes or a survey; for anything that needs two-way messaging with the page, use a WebView package.

- Only absolute `http` and `https` URLs are accepted; anything else is logged and ignored.
- On Android the sheet needs the `androidx.browser` library. The build postprocessor injects it unless **Include Custom Tabs Dependency** is turned off (it is skipped when already declared; `CustomTabsDependencyCoordinate` pins a version or mirror). Without the library, and on other platforms and in the Editor, the page opens in the external browser with `Application.OpenURL`.

## Gallery and image picker

```csharp
bool saved = await NativeMediaService.SaveToGalleryAsync(screenshotPath);             // image
bool clip  = await NativeMediaService.SaveToGalleryAsync(videoPath, MediaType.Video);
string picked = await NativeMediaService.PickImageAsync();                            // null when cancelled
```

`NativeMediaService` (also on `INativeUiService`) saves a file to the shared gallery and picks one image with the operating system's own picker. Neither call needs the Photos permission.

- **Save, iOS** — `PHPhotoLibrary` creates the asset. iOS shows its add-only prompt the first time, so the Mobile Services Config needs a **PhotoLibraryAddOnly** usage description (`NSPhotoLibraryAddUsageDescription`); without it the save code is not compiled into the build and the call returns `false` with a log message.
- **Save, Android** — `MediaStore` writes to `Pictures/<product name>` or `Movies/<product name>`; no permission on Android 10 and newer. Android 9 and older need the **Write External Storage Legacy** toggle, which declares `WRITE_EXTERNAL_STORAGE` up to API 28.
- **Pick, iOS** — `PHPickerViewController`, which runs out of process and needs no usage description.
- **Pick, Android** — the Android 13 photo picker, or the document picker on older versions, hosted by a transparent package activity. Turn on **Include Media Picker** in the Mobile Services Config so the build postprocessor declares it.
- **The picked image is a private copy** in a temporary or cache folder the app owns, so it can be loaded at once and deleted when done. Only one pick can be open at a time; a second call returns `null`.
- **Editor** — with the Device Simulator panel engaged, saves land in `Library/GameLovers/SimulatedGallery` and the picker opens a file dialog. Without it both log and return `false` or `null`.

## Editor & unsupported platforms

In `UNITY_EDITOR`, alerts render through the in-Game-view simulator overlay and invoke their real button callbacks. The other methods log to console and return unless their Device Simulator override is engaged. On unsupported player platforms (Standalone, WebGL), `ShowAlertPopUp` and `ShowToastMessage` throw `SystemException`; `RequestReview` and `Share` are safe no-ops, and `SaveToGalleryAsync` and `PickImageAsync` return `false` and `null`.

The [Device Simulator panel](explorer.md) selects the platform skin and renders platform-shaped mocks for every native UI surface so designers / engineers can iterate in the editor without device builds. Alerts also render in a plain Game view when the panel is closed.

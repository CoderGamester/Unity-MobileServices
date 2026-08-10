# Native UI

Static `NativeUiService` + instance-based `INativeUiService` wrapper. iOS bridge in `Plugins/iOS/NativeUi.m`, Android bridge via `AndroidJavaObject`.

## Static API

```csharp
NativeUiService.ShowAlertPopUp(
    isAlertSheet: false,
    title: "Delete Save?",
    message: "This action cannot be undone.",
    new AlertButton { Text = "Cancel", Style = AlertButtonStyle.Cancel },
    new AlertButton { Text = "Delete", Style = AlertButtonStyle.Destructive, Callback = OnDeleteConfirmed });

NativeUiService.ShowToastMessage("Item Collected!", isLongDuration: false);

NativeUiService.RequestReview();

NativeUiService.Share(text: "Check out my high score!", url: "https://example.com/game");
```

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

The `AlertButton.Callback` action fires when the user taps that button. Callbacks are matched **by button text** on iOS — keep button texts unique within a single alert to avoid ambiguous matches.

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

## Editor & unsupported platforms

In `UNITY_EDITOR`, every method logs to console and returns. On unsupported platforms (Standalone, WebGL), `ShowAlertPopUp` and `ShowToastMessage` throw `SystemException`; `RequestReview` and `Share` are safe no-ops.

The [Device Simulator panel](explorer.md) renders platform-shaped mocks for every native UI surface so designers / engineers can iterate in the editor (edit or play mode) without device builds.

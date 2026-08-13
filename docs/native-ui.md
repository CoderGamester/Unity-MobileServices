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

## Editor & unsupported platforms

In `UNITY_EDITOR`, alerts render through the in-Game-view simulator overlay and invoke their real button callbacks. The other methods log to console and return unless their Device Simulator override is engaged. On unsupported player platforms (Standalone, WebGL), `ShowAlertPopUp` and `ShowToastMessage` throw `SystemException`; `RequestReview` and `Share` are safe no-ops.

The [Device Simulator panel](explorer.md) selects the platform skin and renders platform-shaped mocks for every native UI surface so designers / engineers can iterate in the editor without device builds. Alerts also render in a plain Game view when the panel is closed.

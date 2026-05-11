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
- **Android**: Play Core `ReviewManagerFactory` + `launchReviewFlow`. Requires `com.google.android.play:review:2.0.1` (or newer) on the consumer's `mainTemplate.gradle`. Without that dependency the call logs an error and returns — it does NOT throw.

## Share sheet

`Share(text, url, imagePath, title)`:

- **iOS**: `UIActivityViewController`. `title` is ignored (iOS doesn't surface a sheet title).
- **Android**: `Intent.ACTION_SEND` via `Intent.createChooser`. `title` is the chooser title.

Any combination of `text` / `url` / `imagePath` may be supplied. `imagePath` must be an absolute filesystem path. Nulls are skipped.

## Editor & unsupported platforms

In `UNITY_EDITOR`, every method logs to console and returns. On unsupported platforms (Standalone, WebGL), `ShowAlertPopUp` and `ShowToastMessage` throw `SystemException`; `RequestReview` and `Share` are safe no-ops.

The [Mobile Services Explorer](explorer.md) renders platform-shaped mocks for every native UI surface so designers / engineers can iterate in the editor without device builds.

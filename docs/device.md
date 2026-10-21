# Device

`IDeviceService` is an umbrella facade aggregating six independently mockable sub-services. Each child interface is also independently registerable for testing. Stateless conveniences (screen wake, opening OS Settings, opening the store page, home-screen shortcuts) are available directly on `DeviceService`.

```csharp
IDeviceService device = new DeviceService();

device.Battery.OnLowPowerModeChanged += () =>
    Debug.Log($"LPM -> {device.Battery.IsLowPowerMode}");

DeviceService.KeepAwake = true;
device.AudioSession.ConfigureForPlayback();

var camera = await device.Permissions.RequestAsync(AppPermission.Camera);
var att    = await device.Att.RequestAuthorizationAsync();

device.DeepLink.OnLinkActivated += uri => Debug.Log($"Deep link: {uri}");
```

## Children

| Property | Interface | What it does |
|----------|-----------|--------------|
| `Battery` | `IBatteryService` | Level + status + low-power-mode events (iOS `NSProcessInfoPowerStateDidChangeNotification`, Android `PowerManager.isPowerSaveMode`). |
| `AudioSession` | `IIosAudioSessionService` | iOS `AVAudioSession` category override (silent-switch). No-op on Android / Editor. |
| `Permissions` | `IPermissionsService` | Unified iOS+Android runtime permissions. Task-based async. Multi-permission overload. |
| `Att` | `IAttService` | iOS App Tracking Transparency. Direct `ATTrackingManager` bridge — no `com.unity.ads.ios-support` dependency. |
| `DeepLink` | `IDeepLinkService` | `Application.deepLinkActivated` wrapper with cold-start link queueing. |
| `BackgroundTasks` | `IBackgroundTaskService` | Extra execution time when the app moves to the background (iOS); pass-through on Android. |

Additionally, an `IDeepLinkRouter` / `DeepLinkRouter` layered on `IDeepLinkService` provides path-pattern routing — see below.

## Screen wake

`DeviceService.KeepAwake` is static sugar over Unity's `Screen.sleepTimeout`:

```csharp
DeviceService.KeepAwake = true;  // SleepTimeout.NeverSleep
DeviceService.KeepAwake = false; // SleepTimeout.SystemSetting
```

Because the setting is process-wide, it is not part of the injectable `IDeviceService` child graph.

## Store page and Settings

```csharp
DeviceService.OpenStorePage(iosAppId: "123456789");                 // App Store / Google Play page
DeviceService.OpenStorePage("123456789", writeReview: true);        // iOS write-review sheet
DeviceService.OpenAppSettings();                                    // this app's OS Settings page
```

`OpenStorePage` builds an `https://apps.apple.com/app/id...` or `https://play.google.com/store/apps/details?id=...` link and opens it with `Application.OpenURL`, so the store app handles it when installed. On iOS the numeric App Store id is required (with or without an `id` prefix); on Android `androidPackage` defaults to `Application.identifier`. Android has no review deep link, so `writeReview` opens the store page there. In the Editor it logs the link instead of opening it. This is a link, not the in-app review prompt: use `NativeUiService.RequestReview()` for that.

## App shortcuts

```csharp
DeviceService.SetShortcuts(new[]
{
    new AppShortcut("daily", "Daily reward", "mygame://rewards/daily"),
    new AppShortcut("versus", "Play versus", "mygame://play/versus", subtitle: "Ranked match"),
});

// Taps arrive as ordinary deep links, including as the cold-start link when the tap launched the app.
device.DeepLink.OnLinkActivated += uri => router.TryDispatch(uri);

DeviceService.ClearShortcuts();
```

`SetShortcuts` replaces the quick actions shown when the player long-presses the app icon. Each `AppShortcut` has a stable `Id`, a `Title`, an absolute `Uri` and an optional `Subtitle`. Taps are delivered through `IDeepLinkService`, so the same [router](#deep-link-router) that handles web links handles shortcuts.

- **Limits** — at most four shortcuts are kept. Entries without an id, title or absolute URI, and repeated ids, are skipped with a log message.
- **Android** — dynamic shortcuts through `ShortcutManager` (API 25+, otherwise a logged no-op). Each shortcut launches this app's activity with an `ACTION_VIEW` intent carrying the URI, which Unity reports as a deep link. Shortcuts use the app icon.
- **iOS** — `UIApplication.shortcutItems`. The package hooks `application:performActionForShortcutItem:completionHandler:` on `UnityAppController` at load time and chains to any implementation another SDK already installed. A tapped shortcut is held natively and collected when `DeepLinkService` is constructed and whenever the app returns to the foreground. The hook covers the app-delegate lifecycle; it does not see shortcuts delivered through a UIScene lifecycle.
- **Cold start** — construct `DeepLinkService` early, as for web links: a shortcut tapped before anyone subscribed is replayed to the first subscriber.
- **Editor** — setting shortcuts logs; drive taps with `EditorPlatformSimulator.SimulateDeepLink(uri, yourService)`.

## Background tasks

```csharp
using var task = device.BackgroundTasks.Begin("upload-save", onExpiring: () => cancel.Cancel());
await Task.Run(() => UploadSave(cancel.Token));   // runs off the main thread
```

`IBackgroundTaskService.Begin(name, onExpiring)` asks iOS for extra execution time (roughly 30 seconds) when the app moves to the background and returns a `BackgroundTaskHandle`. Dispose the handle as soon as the work finishes; leaving it open past the limit lets the system terminate the app.

- **Run the work off the main thread.** Unity's main loop is paused while backgrounded, so `Awaitable` and other main-thread continuations do not resume until the player returns. Use a thread-pool task or native I/O.
- `onExpiring` is raised on the main thread from the system's expiration handler, immediately before the task ends. Keep it to setting a flag or cancelling a token.
- The handle's `IsExpired`, `IsActive` and `RemainingTime` are readable from any thread, so the protected work can poll `IsExpired`. `RemainingTime` is `TimeSpan.MaxValue` in the foreground.
- **Android and Editor:** the handle is a pass-through (always active, unlimited). Android keeps the process alive for a short grace period but offers no per-task extension; use a foreground service or WorkManager outside this package for long work.

## Shared host

Every event-driven child shares a single internal `MonoBehaviour` (`DeviceServicesHost`, `DontDestroyOnLoad`, lazily spawned). One `RegisterLateUpdate` / `RegisterSecondTick` / `RegisterFocusChanged` / `RegisterIosLowPowerModeChanged` fan-out drives the entire device subsystem. **The runtime cost of the entire Device subsystem is one auto-spawned GameObject** — not one per service.

`DeviceServicesHost.ResetForTests()` is exposed for EditMode test teardown.

## Permissions

```csharp
public enum AppPermission { Camera, Microphone, LocationWhenInUse, LocationAlways, PhotoLibrary, PhotoLibraryAddOnly, Notifications }
public enum PermissionStatus { NotDetermined, Denied, Granted, Restricted, Limited }

PermissionStatus Check(AppPermission permission);              // sync, no prompt
Task<PermissionStatus> RequestAsync(AppPermission permission); // async, may prompt

// Multi-permission convenience — awaits sequentially (iOS prompts cannot stack).
Task<IReadOnlyDictionary<AppPermission, PermissionStatus>> RequestAsync(params AppPermission[] permissions);
```

iOS callbacks land at `PermissionsCallbackReceiver` (auto-spawned `DontDestroyOnLoad` GameObject) via `UnitySendMessage("PermissionsCallbackReceiver", "OnPermissionResult", "<id>:<status>")`. Android uses Unity's `UnityEngine.Android.Permission` API.

Treat `Granted` and `Limited` as usable access. `Limited` means partial access: selected photos only (iOS 14+ and Android 14+), provisional notifications (iOS), approximate location (iOS reduced accuracy, Android coarse-only), or When-In-Use location after `LocationAlways` was requested.

### Android behavior

- **Location** — `LocationWhenInUse` and `LocationAlways` ask for precise and approximate location together, because Android 12+ ignores a precise-only request. `LocationAlways` then makes a second, separate request for `ACCESS_BACKGROUND_LOCATION` once foreground access is granted (API 29+). Foreground-only access reports `Limited`.
- **Photos** — `READ_MEDIA_IMAGES` on API 33+, plus `READ_MEDIA_VISUAL_USER_SELECTED` on API 34+ so a partial selection reports `Limited`; `READ_EXTERNAL_STORAGE` below API 33. `PhotoLibraryAddOnly` needs no permission on API 29+ and reports `Granted`.
- **Notifications** — `POST_NOTIFICATIONS` on API 33+. Below API 33 there is nothing to request: `Check` reports `Granted` or `Denied` from the user's notifications-enabled switch, and `RequestAsync` returns that status without a prompt.
- **`Denied` versus `NotDetermined`** — Android never reports a refusal directly. The service remembers (in `PlayerPrefs`) that it requested a permission and also reads the OS rationale flag, so a refused permission reports `Denied` and an untouched one `NotDetermined`. A refused permission often cannot be requested again.
- Requests are serialized because Android shows one system dialog at a time.

### Sending the player to Settings

When a permission is `Denied`, explain why it is needed (for example with `ShowAlertPopUpAsync`) and call `DeviceService.OpenAppSettings()`. It opens this app's page in iOS Settings or Android App Info. The game should `Check` again when it returns to the foreground.

### iOS behavior

- Notification status is cached natively (refreshed at startup, on every foreground return, and after each request) so `Check` never blocks. Only the very first call after launch can wait up to 250 ms for the initial read.
- Only permission groups whose usage description is present in the generated `Info.plist` are compiled into the iOS binary (camera, microphone, location, photos read/write, photos add-only) (see [build-configuration.md](build-configuration.md)). Requesting an undeclared group returns `NotDetermined` and logs a message instead of calling an API Apple would flag as missing its purpose string.
- `LocationAlways` requested while When-In-Use is already granted returns `Limited` immediately; iOS shows its own upgrade prompt later, so `Check` again after the app returns to the foreground.

Manifest entries are auto-injected by the build postprocessor when the matching Android manifest toggle is on — see [build-configuration.md](build-configuration.md).

## App Tracking Transparency (`IAttService`)

```csharp
public enum AttStatus { NotDetermined, Restricted, Denied, Authorized }

AttStatus CurrentStatus { get; }
Task<AttStatus> RequestAuthorizationAsync();
```

- **iOS 14.5+** — bridges `ATTrackingManager.requestTrackingAuthorizationWithCompletionHandler:` via `Plugins/iOS/Att.m`.
- **iOS < 14.5 / Android / Editor / unsupported** — returns `Authorized`. **Don't read this as "the user authorized"** — read it as "the platform doesn't apply ATT". Conditionalize tracking-init code on `Application.platform == RuntimePlatform.IPhonePlayer` if you care about the distinction.

## Deep Links

```csharp
public interface IDeepLinkService
{
    event Action<Uri> OnLinkActivated;        // runtime + cold-start replay
    Uri PendingColdStartLink { get; }         // null after first consume / never set
}
```

The cold-start link (captured from `Application.absoluteURL` at construction) is replayed to the **first** subscriber only — subsequent subscribers do NOT receive it. Construct the service early in app bootstrap (before scene load).

### Deep Link Router

For pattern-based routing rather than a giant switch in your `OnLinkActivated` handler:

```csharp
public interface IDeepLinkRouter
{
    void MapRoute(string pathPattern, Action<Uri, IReadOnlyDictionary<string, string>> handler);
    void RemoveRoute(string pathPattern);
    bool TryDispatch(Uri uri);
}
```

```csharp
using var router = new DeepLinkRouter(device.DeepLink, routes =>
{
    routes.MapRoute("/promo/:id", (uri, p) => OpenPromo(p["id"]));
});
router.MapRoute("/profile/:userId", (uri, p) => OpenProfile(p["userId"]));
router.MapRoute("/settings", (uri, p) => OpenSettings());
```

Literal segments match exactly (case-insensitive); `:name` segments capture into the params dict. Routes are checked in registration order — first match wins. The router subscribes once to `OnLinkActivated` at construction.

Notification taps can feed the same router: attach a deep-link URI with `NotificationBuilder.Data(...)` and call `router.TryDispatch(new Uri(opened.Notification.Data))` from `OnNotificationOpened` — see [notifications.md](notifications.md).

For safe-area UI padding, use `com.gamelovers.uiservice` (`SafeAreaPanelView`/`SafeAreaContainer`).

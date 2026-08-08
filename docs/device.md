# Device

`IDeviceService` is an umbrella facade aggregating seven independently mockable sub-services. Each child interface is also independently registerable for testing.

```csharp
IDeviceService device = new DeviceService();

device.Battery.OnLowPowerModeChanged += () =>
    Debug.Log($"LPM -> {device.Battery.IsLowPowerMode}");

device.ScreenWake.KeepAwake = true;
device.AudioSession.ConfigureForPlayback();

var camera = await device.Permissions.RequestAsync(AppPermission.Camera);
var att    = await device.Att.RequestAuthorizationAsync();

device.DeepLink.OnLinkActivated += uri => Debug.Log($"Deep link: {uri}");
```

## Children

| Property | Interface | What it does |
|----------|-----------|--------------|
| `SafeArea` | `ISafeAreaService` | `Screen.safeArea` with change events. Pairs with `SafeAreaContainer` UI Toolkit element. |
| `ScreenWake` | `IScreenWakeService` | `Screen.sleepTimeout` toggle. Idempotent. |
| `Battery` | `IBatteryService` | Level + status + low-power-mode events (iOS `NSProcessInfoPowerStateDidChangeNotification`, Android `PowerManager.isPowerSaveMode`). |
| `AudioSession` | `IIosAudioSessionService` | iOS `AVAudioSession` category override (silent-switch). No-op on Android / Editor. |
| `Permissions` | `IPermissionsService` | Unified iOS+Android runtime permissions. Task-based async. Multi-permission overload. |
| `Att` | `IAttService` | iOS App Tracking Transparency. Direct `ATTrackingManager` bridge — no `com.unity.ads.ios-support` dependency. |
| `DeepLink` | `IDeepLinkService` | `Application.deepLinkActivated` wrapper with cold-start link queueing. |

Additionally, an `IDeepLinkRouter` / `DeepLinkRouter` layered on `IDeepLinkService` provides path-pattern routing — see below.

## Shared host

Every event-driven child shares a single internal `MonoBehaviour` (`DeviceServicesHost`, `DontDestroyOnLoad`, lazily spawned). One `RegisterLateUpdate` / `RegisterSecondTick` / `RegisterFocusChanged` / `RegisterIosLowPowerModeChanged` fan-out drives the entire device subsystem. **The runtime cost of the entire Device subsystem is one auto-spawned GameObject** — not one per service.

`DeviceServicesHost.ResetForTests()` is exposed for EditMode test teardown.

## Permissions

```csharp
public enum AppPermission { Camera, Microphone, LocationWhenInUse, LocationAlways, PhotoLibrary, PhotoLibraryAddOnly, Notifications }
public enum PermissionStatus { NotDetermined, Denied, Granted, Restricted }

PermissionStatus Check(AppPermission permission);              // sync, no prompt
Task<PermissionStatus> RequestAsync(AppPermission permission); // async, may prompt

// Multi-permission convenience — awaits sequentially (iOS prompts cannot stack).
Task<IReadOnlyDictionary<AppPermission, PermissionStatus>> RequestAsync(params AppPermission[] permissions);
```

iOS callbacks land at `PermissionsCallbackReceiver` (auto-spawned `DontDestroyOnLoad` GameObject) via `UnitySendMessage("PermissionsCallbackReceiver", "OnPermissionResult", "<id>:<status>")`. Android uses Unity's `UnityEngine.Android.Permission` API.

Android API 33+ requirements (`READ_MEDIA_IMAGES` for Photos, `POST_NOTIFICATIONS` for Notifications) are baked into the `AndroidManifestPermission` mapping. Manifest entries are auto-injected by the build postprocessor when the matching capability toggle is on — see [build-pipeline.md](build-pipeline.md).

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

## Safe Area

`SafeAreaContainer` is a companion UI Toolkit `VisualElement` that pads itself to the safe area:

```csharp
var container = new SafeAreaContainer(device.SafeArea);
rootVisualElement.Add(container);
```

For UXML usage, construct via the default constructor and call `SetSafeAreaService` once the service is available:

```xml
<ui:UXML xmlns:gl="GameLovers.MobileServices.Device">
    <gl:SafeAreaContainer>
        <!-- content -->
    </gl:SafeAreaContainer>
</ui:UXML>
```

```csharp
rootVisualElement.Q<SafeAreaContainer>().SetSafeAreaService(device.SafeArea);
```

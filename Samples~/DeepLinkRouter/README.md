# Deep Link Router

Routing pattern sample. Demonstrates `IDeepLinkRouter.MapRoute` with three routes (`/promo/:id`, `/profile/:userId`, `/settings`) and a fall-through "unmatched" path.

## Setup

1. Import via `Window > Package Manager > GameLovers.MobileServices > Samples > Deep Link Router`.
2. Create an empty scene and add `DeepLinkRouterUI` to a GameObject.
3. Build & install to device for the real cold-start replay path; in the editor the buttons exercise the `TryDispatch` API only (no OS-level link delivery).

## Routes registered

| Pattern | Example URI | Captured params |
|---------|-------------|-----------------|
| `/promo/:id` | `myapp://promo/spring2026` | `{ "id": "spring2026" }` |
| `/profile/:userId` | `myapp://profile/abc123` | `{ "userId": "abc123" }` |
| `/settings` | `myapp://settings` | `{}` |

## Path-pattern syntax

- Literal segments match exactly (case-insensitive).
- Segments prefixed with `:` capture into the params dictionary.
- Routes are checked in registration order; first match wins.

## Testing the OS-level launch path

### iOS (Simulator)

```bash
xcrun simctl openurl booted "myapp://promo/spring2026"
```

The simulator launches the installed app and `Application.absoluteURL` carries the URI on cold-start. The router's constructor subscribes synchronously, so `DeepLinkService` replays the launch URL to the router immediately.

### Android (`adb`)

```bash
adb shell am start -W -a android.intent.action.VIEW -d "myapp://promo/spring2026" com.your.bundle.id
```

Note the `-d "<uri>"` argument and your app's full package id at the end. You'll need an `<intent-filter>` in your `AndroidManifest.xml` declaring `myapp` as a scheme.

## Cold-start replay caveat

`DeepLinkService` replays the cold-start link to the **first** subscriber only. The router IS the first subscriber (constructed in the sample's `Awake`), so the replay is intercepted by it automatically. If your real app sets up additional subscribers, attach the router first.

## Types

`GameLovers.MobileServices.Samples.DeepLinkRouter` — sample-only types, NOT package public API.

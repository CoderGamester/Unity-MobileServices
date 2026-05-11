# Samples Index

Four code-only samples ship with the package. Each is a single C# MonoBehaviour that builds a runtime UI canvas via legacy `UnityEngine.UI` — drop it on a GameObject, press Play. No `.unity` scene file, no `.prefab`, no asset references.

| Sample | Folder | Purpose |
|--------|--------|---------|
| Mobile Services Playground | [`Samples~/MobileServicesPlayground`](../Samples~/MobileServicesPlayground/README.md) | Kitchen-sink wiring proof. Buttons for every native UI / haptic / notification / permission / ATT / deep-link call. |
| Haptics Palette | [`Samples~/HapticsPalette`](../Samples~/HapticsPalette/README.md) | Designer iteration tool. 3x3 preset grid + sequence recorder + replay. |
| Notifications Scheduler | [`Samples~/NotificationsScheduler`](../Samples~/NotificationsScheduler/README.md) | Lifecycle demo. Channel CRUD, `OperatingMode` toggles, background-foreground round trip. |
| Deep Link Router | [`Samples~/DeepLinkRouter`](../Samples~/DeepLinkRouter/README.md) | `IDeepLinkRouter.MapRoute` demo with three routes and cold-start replay instructions. |

## Importing

`Window > Package Manager > GameLovers.MobileServices > Samples > <sample-name>` — imports into `Assets/Samples/Mobile Services/<version>/<sample-name>/`.

## Sample-only types

All sample types live in `GameLovers.MobileServices.Samples.<Name>` namespaces and are **NOT** part of the public package API surface. When updating any sample's README or the main package README, never describe these types as if they were package API.

## Why code-only

Divergence from peer `com.gamelovers.services` / `com.gamelovers.uiservice` which ship `.unity` + `.prefab` files with hand-authored deterministic GUIDs.

- Zero asset dependencies — no `.unity` scenes, no `.prefab` files, no deterministic-GUID `.meta` files to keep in sync.
- Diff-friendly — code reviewers see exactly what the sample does at the source level.
- Easy to drop into any scene — no "now open `<sample>.unity`" step.

The trade-off is no built-in scene hierarchy or prefab structure for the user to inspect; acceptable for the mobile surface since most behaviour is fired by buttons, not configured by serialised state.

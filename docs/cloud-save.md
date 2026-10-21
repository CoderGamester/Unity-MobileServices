# Cloud Save

`IGamePlatformServices.CloudSave` (`ICloudSaveService`, namespace `GameLovers.MobileServices.GamePlatform`) stores slot-based saves on the platform's own cloud: **Game Center saved games** (iCloud) on iOS and **Google Play Games saved games** on Android. It needs the sign-in of [Game Platform](game-platform.md) and is opt-in like it.

```csharp
var platform = new GamePlatformServices();
await platform.AuthenticateAsync();
var cloud = platform.CloudSave;

await cloud.SaveAsync("slot1", bytes, "Level 4");

byte[] loaded = await cloud.LoadAsync("slot1");      // null when the slot was never saved
foreach (var slot in await cloud.ListAsync())
    Debug.Log($"{slot.Name} saved {slot.ModifiedUtc} on {slot.DeviceName}");

await cloud.DeleteAsync("slot1");
```

## Rules

- **Slots** — names use letters, digits, `-`, `.`, `_` and `~`, 1 to 100 characters (the common subset of both platforms). A save is at most `CloudSaveService.MaxDataBytes` (3 MiB, the Play Games limit, applied on both platforms). Invalid input throws `ArgumentException` before the platform is called.
- **Availability** — `IsAvailable` is true when a player is signed in and the platform's storage is usable (iOS also needs an iCloud account for the app). Without it every call throws a `CloudSaveException` with `NotAuthenticated` or `CloudUnavailable`.
- **Failures** — `CloudSaveException.Reason` is `NotAuthenticated`, `CloudUnavailable`, `Conflict` or `Io`. Missing slots are not errors: `LoadAsync` returns `null` and `DeleteAsync` does nothing.
- **Description** — saved with the slot on Android; Game Center saved games have none, so it is empty on iOS.
- **Backgrounding on iOS** — `SaveAsync` holds a native background task until Game Center confirms, so a save started just before the player leaves the app still completes. The result is delivered when the app returns.

## Conflicts

Two devices can save the same slot before either syncs. The next `LoadAsync` finds several versions and resolves them by `ConflictPolicy`, then writes the winner back so the conflict is gone:

```csharp
cloud.ConflictPolicy = CloudSaveConflictPolicy.Manual;
cloud.ConflictResolver = conflict =>
{
    // conflict.Versions is newest first; return one of them or a merge.
    return Merge(conflict.Versions);
};
```

- `MostRecent` (the default) keeps the most recently modified version.
- `Manual` calls `ConflictResolver` with a `CloudSaveConflict` (the slot name and every version, newest first). The returned `CloudSaveVersion`'s data is saved. With no resolver, or a resolver that returns `null`, the load throws `CloudSaveException` with `Conflict`.

## Setup

1. Set up [Game Platform](game-platform.md) for the platforms you ship (Game Center on iOS, the Play Games project id on Android).
2. Turn on **Cloud Save** in the *Game platform* section of the Mobile Services Config. The postprocessor adds the iCloud Documents capability (with the default container) to the iOS entitlements. Validation rejects Cloud Save when neither Game Center nor a Play Games project id is set.
3. iOS: enable iCloud for the App ID and provisioning profile. Android: enable **Saved Games** for the Play Games project in the Play Console.

## Editor

In the Editor the service stores saves under `Library/GameLovers/SimulatedCloudSave`. The **Cloud Save** foldout of the [Device Simulator panel](explorer.md) toggles storage availability, injects a newer conflicting version of a slot (so both conflict policies can be exercised), lists the saved slots, and deletes them. Sign the simulated player in under **Game Platform** first.

On other platforms (Standalone, WebGL) every call throws `CloudUnavailable`.

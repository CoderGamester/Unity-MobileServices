# Game Platform

`GamePlatformServices` (`IGamePlatformServices`, namespace `GameLovers.MobileServices.GamePlatform`) gives one API for the platform game services: **Game Center** on iOS and **Google Play Games Services v2** on Android. It covers sign-in, leaderboards and achievements, and `CloudSave` stores slot-based saves on the same sign-in (see [cloud-save.md](cloud-save.md)).

It is **opt-in**. Nothing is added to a build until you enable it in the Mobile Services Config, and the package needs no Apple or Google Unity plugin.

```csharp
IGamePlatformServices platform = new GamePlatformServices();

if (await platform.AuthenticateAsync())
{
    Debug.Log($"Signed in as {platform.LocalPlayer.DisplayName}");

    await platform.ReportScoreAsync(Boards.HighScore, 4200);
    await platform.UnlockAchievementAsync(Achievements.FirstWin);
    await platform.IncrementAchievementAsync(Achievements.Collector, steps: 1, totalSteps: 50);

    platform.ShowLeaderboards();   // all boards, or ShowLeaderboards(Boards.HighScore)
    platform.ShowAchievements();
}

static class Boards
{
    public static readonly PlatformId HighScore = new PlatformId(ios: "grp.highscore", android: "CgkI...");
}

static class Achievements
{
    public static readonly PlatformId FirstWin = new PlatformId(ios: "grp.firstwin", android: "CgkI...");
    public static readonly PlatformId Collector = new PlatformId(ios: "grp.collector", android: "CgkI...");
}
```

## Platform ids

Game Center and Play Games issue different ids. Keep both in a `PlatformId` and pass it anywhere an id string is expected; the id for the running platform is picked automatically. Outside iOS and Android (the Editor) it falls back to whichever id is set.

## Behavior

- **Sign-in** — `AuthenticateAsync` returns at once when already signed in, shows the platform's sign-in UI otherwise, and shares one request between concurrent callers. Returns `false` when the player cancels or the platform is unavailable. `IsAuthenticated` and `LocalPlayer` always reflect the live state.
- **Progress calls** — `ReportScoreAsync`, `UnlockAchievementAsync` and `IncrementAchievementAsync` return `true` when the platform accepted the call. Without a signed-in player, an empty id, or a non-positive step count they return `false` after a log message and never reach the platform. The platform applies its own leaderboard rules (best score, decreasing scores, and so on).
- **Incremental achievements** — Play Games reads the total from the Play Console. Game Center tracks a percentage, so iOS needs `totalSteps`: it loads the current progress and adds `steps / totalSteps`.
- **UI** — `ShowLeaderboards` and `ShowAchievements` open the platform's own screens (`GKGameCenterViewController`, the Play Games intents). Both need a signed-in player.
- **Threading** — results come back on Unity's main thread.
- **Not included** — server auth codes and identity verification for backend account linking, friends, and multiplayer.

## Setup

### iOS: Game Center

1. Enable Game Center for the App ID and provisioning profile, and create the leaderboards and achievements in App Store Connect.
2. Turn on **Game Center** in the *Game platform* section of the Mobile Services Config.

The build postprocessor adds the Game Center entitlement and the `gamekit` required device capability, and compiles `Plugins/iOS/GamePlatform.m` against GameKit. Without the toggle the bridge compiles to stubs that link no GameKit symbols, so every call fails with a log message.

### Android: Google Play Games

1. In the Play Console, create a Play Games Services project and add the leaderboards and achievements.
2. Enter the numeric **project id** in the *Game platform* section of the Mobile Services Config.

The postprocessor then injects `com.google.android.gms:play-services-games-v2` into the generated Gradle project (skipped when it is already declared), adds the `com.google.android.gms.games.APP_ID` manifest meta-data, and writes the project id to `res/values/gamelovers_play_games_project_id.xml`. Override `PlayGamesDependencyCoordinate` to pin a different version or use an internal mirror. Clearing the project id removes the integration. The package talks to the SDK through JNI, so it compiles without it.

Google may require the app's signing certificate to be registered with the Play Games project; that is a Play Console step the package cannot do.

## Editor

In the Editor `GamePlatformServices` uses a simulation. The **Game Platform** foldout of the [Device Simulator panel](device-simulator.md) signs the simulated player in and out, makes the next sign-in fail, renames the player, and lists the scores, achievements and UI requests the game reported. The simulation is process-wide, so it drives the game's own `GamePlatformServices` instance.

On other platforms (Standalone, WebGL) every call fails after a log message.

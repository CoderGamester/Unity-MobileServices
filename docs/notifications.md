# Notifications

`INotificationService` / `MobileNotificationService` wrap Unity's `com.unity.mobile.notifications` package. The service creates a `DontDestroyOnLoad` `GameObject("NotificationService")` carrying a `GameNotificationsMonoBehaviour` host on construction.

## Quick start

```csharp
var service = new MobileNotificationService(
    new GameNotificationChannel("default", "Default", "Default notifications"),
    new GameNotificationChannel("rewards", "Rewards", "Daily reward reminders"));

service.Schedule()
    .In(TimeSpan.FromHours(24))
    .Title("Daily Reward")
    .Body("Your daily reward is waiting!")
    .Channel("rewards")
    .BadgeIncrement()
    .Send();
```

## Long-form API

```csharp
var notification = service.CreateNotification();
notification.Title = "Daily Reward Ready!";
notification.Body = "Your daily reward is waiting for you!";
notification.DeliveryTime = DateTime.Now.AddHours(24);
notification.Channel = "rewards";
var pending = service.ScheduleNotification(notification);

// pending.Reschedule = true   // re-queue if foregrounded before delivery (Queue + RescheduleAfterClearing)

service.CancelNotification(pending.Notification.Id.Value);
service.CancelAllScheduledNotifications();
service.DismissAllDisplayedNotifications();

var scheduled = service.PendingNotifications;  // IReadOnlyList<PendingNotification>
```

## Channels (Android)

Android requires at least one notification channel registered with the OS before any notification will display (Android 8+). The constructor takes a `params GameNotificationChannel[]`:

```csharp
new MobileNotificationService(
    new GameNotificationChannel("default", "Default", "Default notifications"),
    new GameNotificationChannel("rewards", "Rewards", "Daily reward reminders"));
```

**The first channel passed becomes the platform default** — notifications scheduled without an explicit `Channel` field land here. On iOS, channels are stored but not exposed to the OS (iOS has no channel concept).

## Operating modes

```csharp
[Flags]
public enum OperatingMode
{
    NoQueue                   = 0x00,  // default — schedule with OS immediately
    Queue                     = 0x01,  // hold in memory, only schedule when app backgrounds
    ClearOnForegrounding      = 0x02,  // clear pending when app foregrounds
    RescheduleAfterClearing   = 0x04,  // re-queue Reschedule=true entries after a clear (requires ClearOnForegrounding)
    QueueAndClear             = Queue | ClearOnForegrounding,
    QueueClearAndReschedule   = Queue | ClearOnForegrounding | RescheduleAfterClearing,
}
```

Set the mode via `INotificationService.Mode` (defaults to `NoQueue`). It's settable at runtime and the new value takes effect on the next `ScheduleNotification` call and the next foreground/background transition:

```csharp
service.Mode = OperatingMode.QueueClearAndReschedule;
```

`OnLocalNotificationExpiredEvent` only fires while a `Queue` mode is active, so set the mode before scheduling if you rely on it. The `NotificationsScheduler` sample drives this live.

## Foreground delivery events

```csharp
service.OnLocalNotificationDeliveredEvent += pending =>
    Debug.Log($"Delivered while foreground: {pending.Notification.Title}");
service.OnLocalNotificationExpiredEvent += pending =>
    Debug.Log($"Cleared because foregrounded: {pending.Notification.Title}");
```

`OnLocalNotificationDeliveredEvent` fires when the OS hands the notification to the app while it's in the foreground (typical for iOS — Android usually displays the heads-up regardless). `OnLocalNotificationExpiredEvent` fires when `ClearOnForegrounding` cancelled a queued notification because the user was already inside the app at delivery time.

## Editor behaviour

In `UNITY_EDITOR`:

- `CreateNotification` returns an `EditorGameNotification` (in-memory POCO).
- `ScheduleNotification` assigns a hashed-`DateTime` id if `Id == null` and returns a `PendingNotification` wrapper — the OS layer is NOT touched.
- The [Device Simulator panel](explorer.md)'s **Notifications** foldout + the in-Game-view simulator overlay combine to give you a banner-mock preview at the simulated delivery time, driven by the editor's update loop.

## Teardown

The host GameObject is `DontDestroyOnLoad`. Tests / "reset game" flows that destroy the DDOL scene need to recreate the service afterwards. There is no explicit `Dispose` — destroy `GameObject.Find("NotificationService")` if you need to tear down.

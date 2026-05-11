# Notifications Scheduler

Lifecycle-focused notifications sample. Educates on the three concerns the kitchen-sink playground deliberately hides:

1. **Channel CRUD** — add channels at runtime and observe the first-channel-becomes-default-on-Android rule.
2. **`OperatingMode` toggles** — `NoQueue` / `Queue` / `QueueAndClear` / `QueueClearAndReschedule`. The buttons switch the displayed value; rewire the host MonoBehaviour to consume the change if your app needs runtime mode flips.
3. **Persistence-on-background round trip** — the `OnLocalNotificationDeliveredEvent` and `OnLocalNotificationExpiredEvent` events log what happens when you background and foreground the app while a queued notification is pending.

## Setup

1. Import via `Window > Package Manager > GameLovers.MobileServices > Samples > Notifications Scheduler`.
2. Create an empty scene and add `NotificationsSchedulerUI` to a GameObject.
3. Build & deploy to device for the full background-foreground round trip — the foreground-cancel path only fires when `OnApplicationFocus(false)` then `(true)` transitions through, which the Editor's run-without-focus model doesn't truly exercise.

## What it covers

- Schedule a notification with `Reschedule = false` (default) and one with `Reschedule = true`. Then put the app in the background, wait past the delivery time, foreground it, and observe:
  - In `QueueClearAndReschedule` mode, the `Reschedule = true` entry is re-queued, the other one fires `OnLocalNotificationExpiredEvent`.
  - In `QueueAndClear` mode, both fire `OnLocalNotificationExpiredEvent`.
  - In `NoQueue` mode (default), the OS delivered the notification at the requested time regardless of foreground/background — you see the heads-up banner on a real device.

## Types

`GameLovers.MobileServices.Samples.NotificationsScheduler` — sample-only types, NOT package public API.

# Native haptics cancellation regression

On macOS with Xcode installed:

```sh
bash Tests~/Native/run-haptics-stop-tests.sh
```

This compiles and executes the actual `Plugins/iOS/Haptics.m` bridge. UIKit feedback generators and the haptic engine/player are test doubles; Foundation, pattern construction, and main-queue dispatch use the macOS frameworks. No device haptics play.

The cases exercise active and stopped engines, a native cancellation exception, repeated cancellation, fresh envelope playback after failure, and cleanup through the exported asynchronous stop entry point. The stopped player returns the Core Haptics engine-not-running error through `NSError`; the exception case raises independently of that output to verify the native cancellation boundary.

Pass an alternate production source path to verify a regression against an earlier revision:

```sh
bash Tests~/Native/run-haptics-stop-tests.sh /absolute/path/to/Haptics.m
```

The runner prints the source path, SHA-256, named results, and executed counts. It removes its temporary binary on exit. These tests model the reported failure; they do not reproduce iOS suspension, Core Haptics service recovery, or physical-device feedback.

# Haptics

Zero-dependency haptic feedback. Built directly on iOS `UI*FeedbackGenerator` (iOS 10+) and Android `VibrationEffect.createWaveform` (API 26+) — no NiceVibrations, no Lofelt-runtime.

## API

```csharp
public interface IHapticsService
{
    bool Enabled { get; set; }     // master toggle; setting false stops any active haptic
    bool IsSupported { get; }      // device + OS can produce at least basic vibration
    bool IsPlaying { get; }        // between any Play* and the matching stop

    void PlayPreset(HapticPreset preset);                              // natural one-shot
    void PlayPresetDuration(HapticPreset preset, float duration = -1f); // 0 = natural, <0 = loop, >0 = loop+auto-stop
    void PlayCustom(float intensity01, float durationMs);               // single intensity, always finite
    void StopCurrentHaptic();                                           // single stop entry point, idempotent
}
```

## Preset catalogue

```csharp
public enum HapticPreset
{
    None = 0,
    Selection = 1,        // crisp tick (picker / discrete value change)
    Success = 2,          // two-tap ascending notification
    Warning = 3,          // single warning tap
    Error = 4,            // multi-tap error
    ImpactLight = 5,      // soft low-amplitude impact
    ImpactMedium = 6,     // default impact
    ImpactHeavy = 7,      // strong impact
    ImpactRigid = 8,      // sharp short impact (snappy)
    ImpactSoft = 9,       // gentle longer impact (cushioned)
}
```

The per-preset `(timings_ms, amplitudes_0_to_255)` envelopes live in `Runtime/Haptics/Internal/HapticEnvelopes.cs` and feed both `AndroidHapticsBackend` and the [Mobile Services Explorer](explorer.md) Haptics tab's envelope graph — single source of truth.

## Looping semantics

`PlayPresetDuration(preset, duration)`:

- `duration == 0f` — play the preset's natural one-shot duration. Same as `PlayPreset(preset)`.
- `duration < 0f` (default `-1f`) — loop indefinitely. **Caller MUST invoke `StopCurrentHaptic`** (or `Enabled = false`) to end it.
- `duration > 0f` — loop the preset, auto-stop after `duration` real-time seconds (unaffected by `Time.timeScale`).

Each new `Play*` call cancels any previously pending auto-stop coroutine — only one auto-stop is ever pending. `HapticsHost` (the internal MonoBehaviour) is spawned lazily on the first play with auto-stop.

## Custom intensity

```csharp
haptics.PlayCustom(intensity01: 0.7f, durationMs: 250f);
```

Always finite; `durationMs <= 0` is a no-op. Intensity is clamped to `[0, 1]`. On Android this routes through `VibrationEffect.createOneShot(milliseconds, amplitude0to255)`. On iOS, the bridge maps the intensity to the closest UIKit feedback generator on a discrete amplitude curve.

## Platform support

| Platform | Backend |
|----------|---------|
| iOS | `IosHapticsBackend` — UIKit `UIImpactFeedbackGenerator` / `UINotificationFeedbackGenerator` / `UISelectionFeedbackGenerator`. Looping via `NSTimer`. |
| Android | `AndroidHapticsBackend` — pure JNI `Vibrator.vibrate(VibrationEffect)`. Requires API 26 (Android 8.0)+. |
| Editor | `EditorHapticsBackend` — logs only. |
| Other (Standalone, WebGL) | `NoOpHapticsBackend` — `IsSupported = false`. |

The backend is selected at construction by `CreateDefaultBackend()`. Tests construct `HapticsService` with the internal injection constructor + a `FakeHapticsBackend`.

## Editor introspection

Internal accessors on `HapticsService` (visible to the Editor assembly via `InternalsVisibleTo`):

- `CurrentPreset` — the preset most recently passed to a `Play*`. `HapticPreset.None` when not playing or after a `PlayCustom`.
- `CurrentDurationSeconds` — scheduled duration in real-time seconds (preset's natural duration for one-shots, `-1` for indefinite loop, explicit positive for finite).
- `Backend` — the platform backend selected for this instance.

These power the Explorer's **Haptics** tab status label.

## Editor testing on a real device

In the editor, the `EditorHapticsBackend` only logs. To test haptic feel on a paired phone:

1. Use Unity Remote (haptics will NOT fire through Unity Remote — it only relays input/display).
2. Or deploy a debug build to the device for the iteration loop. The `HapticsPalette` sample is designed for this loop.

# Haptics

Zero-dependency haptic feedback. Built directly on iOS `UI*FeedbackGenerator` (iOS 10+), iOS Core Haptics (iOS 13+) for envelope presets, and Android `VibrationEffect.createWaveform` (API 26+) — no NiceVibrations, no Lofelt-runtime.

## API

```csharp
public interface IHapticsService
{
    bool Enabled { get; set; }     // master toggle; setting false stops any active haptic
    bool IsSupported { get; }      // device + OS can produce at least basic vibration
    bool IsPlaying { get; }        // between any Play* and the matching stop

    void PlayPreset(HapticPreset preset);                              // natural one-shot
    void PlayPreset(HapticPreset preset, float intensityScale);        // natural one-shot at 0-1 of its strength
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
    QuickImpactHeavy = 10, // full-strength impact at half the ImpactHeavy length
    Explosion = 11,        // full-strength blast decaying quadratically to a soft tail
}
```

The per-preset `(timings_ms, amplitudes_0_to_255)` envelopes live in `Runtime/Haptics/Internal/HapticEnvelopes.cs` and feed both `AndroidHapticsBackend` and the [Device Simulator panel](device-simulator.md)'s Haptics envelope graph — single source of truth.

`Explosion` plays six 40 ms steps (100%, 70%, 46%, 29%, 19%, 16%). iOS plays the same curve as a Core Haptics continuous event and falls back to the heavy impact on hardware without Core Haptics. `Plugins/iOS/Haptics.m` mirrors the step table, so change both together.

## Intensity scaling

```csharp
haptics.PlayPreset(HapticPreset.Explosion, intensityScale: 0.4f);
```

Plays a one-shot at a fraction of its authored strength, clamped to `[0, 1]`. A scale of `0` plays nothing and leaves an active haptic running, so a caller can attenuate by distance without cutting nearer feedback. Android multiplies every envelope step; silent steps stay silent and audible steps never round to silence. iOS scales impact styles through `impactOccurredWithIntensity:` and `Explosion` through its Core Haptics intensity; selection and notification presets play at full strength because UIKit gives them no intensity.

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
| iOS | `IosHapticsBackend` — UIKit `UIImpactFeedbackGenerator` / `UINotificationFeedbackGenerator` / `UISelectionFeedbackGenerator`, plus Core Haptics (`CoreHaptics.framework`) for `Explosion`. Looping via `NSTimer`. |
| Android | `AndroidHapticsBackend` — pure JNI `Vibrator.vibrate(VibrationEffect)`. Requires API 26 (Android 8.0)+. |
| Editor | `EditorHapticsBackend` — no native output; optional console diagnostics. |
| Other (Standalone, WebGL) | `NoOpHapticsBackend` — `IsSupported = false`. |

The backend is selected at construction by `CreateDefaultBackend()`. Tests construct `HapticsService` with the internal injection constructor + a `FakeHapticsBackend`.

## Editor introspection

Internal accessors on `HapticsService` (visible to the Editor assembly via `InternalsVisibleTo`):

- `CurrentPreset` — the preset most recently passed to a `Play*`. `HapticPreset.None` when not playing or after a `PlayCustom`.
- `CurrentDurationSeconds` — scheduled duration in real-time seconds (preset's natural duration for one-shots, `-1` for indefinite loop, explicit positive for finite).
- `Backend` — the platform backend selected for this instance.

These power the Device Simulator panel's **Haptics** status read-out.

## Editor diagnostics and real-device testing

The Editor backend is silent by default so frequent haptics do not overwhelm the Console. To inspect every
play and stop call, open **Tools > GameLovers > Mobile Services > Select Mobile Services Config** and enable
**Editor diagnostics > Enable Haptics Debug Logs**. This team-shared setting affects only the Editor and is
never included in player builds.

To test haptic feel on a paired phone:

1. Use Unity Remote (haptics will NOT fire through Unity Remote — it only relays input/display).
2. Or deploy a debug build to the device for the iteration loop. The `HapticsPalette` sample is designed for this loop.

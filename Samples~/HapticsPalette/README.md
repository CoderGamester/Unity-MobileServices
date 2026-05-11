# Haptics Palette

Designer-focused haptic exploration sample.

## Setup

1. Import via `Window > Package Manager > GameLovers.MobileServices > Samples > Haptics Palette`.
2. Create an empty scene and add `HapticsPaletteUI` to a GameObject.
3. Pair a device with the editor (haptics are silent in Editor / Standalone — the sample's flow assumes a paired phone).

## Flow

- Tap any preset button to trigger that haptic.
- The "Recorded sequence" label captures the last 16 triggers and their inter-trigger delays.
- `Replay` replays the recorded sequence at the original timings (uses `WaitForSecondsRealtime` so `Time.timeScale` doesn't distort).
- `Clear` empties the recording.
- `Stop` cancels any active haptic (matters for the looped variants exposed via the Mobile Services Explorer).

## Why it's a separate sample

The interaction model is "tune the feel until it lands" — designers iterating with a paired phone. The kitchen-sink playground is for "smoke-test the API surface" and is the wrong place to dwell on haptic timing because the surrounding UI noise distracts.

## Types

All sample types live in `GameLovers.MobileServices.Samples.HapticsPalette` — NOT package public API.

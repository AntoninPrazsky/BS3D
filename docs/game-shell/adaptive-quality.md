# Adaptive quality: the probe

Part of [The game: shell](../game-shell.md).

2× supersampling costs four times the shaded pixels, the biggest thing in a weak GPU's frame. `BS3DGame.TuneQualityToFrameRate` (`BS3DGame.Quality.cs`, #62) notices and steps the tier down. What a tier gives up: [The quality tier](quality-tier.md); what the frame costs: [What the frame costs](quality-measurements.md).

## What it does

- **It measures; it does not recognise the adapter** (a name list cannot see 4K, battery or a shared GPU). The front end is its own probe and **so is every level** (#121): `GameplayScreen.Update` calls it on real time, **never during a drop cinematic**. A menu frame is the lightest of a session, and its preview map (#249) is not the level about to be played.
- ⚠ **Exactly half the refresh is a vsync signature, not a cost** (Onion read 37.5 FPS on 75 Hz and was stepped down; retracted in #209: with the vsync gone, #270, it costs 5.96 ms at `High`).
- **Seconds, not frames** (`QUALITY_WARMUP_SECONDS`, `QUALITY_WINDOW_SECONDS`): a frame count depends on the frame rate.
- **The floor is the display's refresh** less `QUALITY_REFRESH_MARGIN`, clamped to `QUALITY_MIN_FPS_FLOOR` when the adapter reports none (`SetQualityMinFpsFromRefresh`, the *window's own* monitor, #81): 75 Hz logs `floor 68`. The margin keeps a machine on the frame limiter's cap from tripping on jitter.
- **A downward step waits for a plateau** (`QUALITY_RAMP_FRACTION`): a GPU still raising its clocks reads slow. It steps only when a window is no more than that fraction faster than the last, so **a drop takes at least two windows**; a window clearing the floor settles at once.
- **One step per verdict, never upwards, and it latches** (`_qualitySettled`): set by `quality=`/`ssaa=` (`LaunchOptions` keeps them nullable: null means "nobody said"), by the player touching Quality or Auto quality, when the machine is fast enough, or when nothing is left to lower. The step is one rung down and **settles** at `Low`/`Potato` rather than "lowering" `Low` to `Low`.
- **It steps the tier, not supersampling alone** (#63: the factor alone left the neon city at 30 FPS). A step resizes the scene target, so it **re-arms `_qualityWarmupLeft`**, or the next window measures that hitch and steps again.
- Log `[quality] 25 FPS in the menu at High (floor 68) — lowering to Medium`; the main menu explains a drop on its own dark plate (no scrim there).

## When it re-opens

Always through `BS3DGame.ReopenQualityProbe`: every level build, every fullscreen switch **at every tier** (#123), a Resolution-row click ([The resolution](resolution.md)) and turning Auto quality on. **A tier the player pinned is never re-opened** (`_qualityPinnedByPlayer`).

## ⚠ The probe's verdict is never stored

A measured tier is not written to `%LOCALAPPDATA%\BS3D\Settings.json`: the step is one-way, so a stored verdict is a ratchet, and one unlucky window (a background build, a thermal dip) would pin `Low` for good. Re-measuring costs seconds per launch (the owner's ruling). The tier the **player** picks is stored as `"quality"`.

## The Auto quality row (#390)

It reads `!_qualityPinnedByPlayer`, so it cannot misreport: picking a tier flips it to Off, as does `quality=`/`ssaa=`. **Off** stores the tier shown with `"adaptiveQuality": false` (at startup that key pins as a stored tier does; with none stored the game plays `High`). **On** clears the stored tier and re-opens one window now. A file with a tier and no key reads Off, with neither On.

To test it pass **`userdata=<dir>`** (else the run writes the real `Settings.json`), and `fpscap=40` under a 75 Hz floor to trip it; an A/B must pass `quality=` or `ssaa=`.

---
name: benchmark
description: Measure BS3D's frame rate under pinned, repeatable conditions and compare machines or settings. Use before deciding what a quality tier should turn down, or to check whether a rendering change cost anything.
---

# Benchmarking BS3D

Companion to `verify` (build/launch) and `screenshot` (framing a shot). This one is about **numbers**.

```powershell
dotnet build C:\GitHub\Game.sln
.\benchmark.ps1                                      # the 12-scene sweep at ssaa 1 and 2
.\benchmark.ps1 -Scenes neon -Ssaa 0 -Extra @('quality=low')
.\benchmark.ps1 -Scenes neon -Ssaa 2 -Seconds 20     # longer window on a noisy machine
```

It reads the game's own `[fps]` lines rather than screenshotting a counter, prints the hardware, and averages
the readings after discarding the warm-up. `-Ssaa 0` omits `ssaa=` entirely, which is how a `quality=` tier is
measured with the factor **it** chose instead of one forced over the top of it.

## The four ways to measure nothing at all

Each of these has actually happened; the first two are the expensive ones.

1. **An unfocused window.** MonoGame leaves `InactiveSleepTime` at its default, so a background window is
   capped near 50 FPS and that cap quietly becomes your measurement. The script clicks the title bar for this
   reason alone — do not pass `-NoFocus` unless the frame rate is well under 50 either way.
2. **Letting the adaptive path move under you.** With neither `quality=` nor `ssaa=` named, the game measures
   the machine and steps the tier down about three seconds in — which reads as a spectacular win for whatever
   you were testing. Always pin one.
3. **Not pinning the scene.** The backdrop is a different one of the seven every launch and they span 6.8× in
   cost, so two unpinned runs are not comparable at all. `scene=` and `sky=` both matter: the sea and the
   savanna each force a dome of their own, so pin `sky=` after.
4. **Believing the first seconds.** The opening frames are shader compiles and the first touch of each render
   target; the readings climb for several seconds. `-WarmupLines` (4 by default) drops them.

5. **Forgetting that the front end's camera orbits.** The cost swings with the view: on the neon city one run
   read 43 FPS and then climbed steadily to 74 over the following fifteen seconds, purely because the orbit
   carried the lens to an angle with fewer towers across it. So a fixed window samples one arc of the orbit,
   not the scene — which is fine for an A/B (every run starts from the same angle and samples the same arc, and
   it is the *expensive* arc) but means the absolute figure is a near-worst case rather than an average. Do not
   compare a 12-second run against a 30-second one.
6. **A stray process from an earlier run.** A harness that died between launch and cleanup leaves a BS3D
   competing for the same GPU, and it halves everything measured afterwards without any sign of why. It has
   happened: `Get-Process BS3D` before trusting a surprising number.

Also: keep both halves of an A/B in the **same build configuration**, and remember `nocap` (which the script
always passes) is what makes the number a frame cost rather than the display's refresh.

## What the game gives you

- `logfps` — one line a second to stdout: the frame rate, the scene, the dome, the supersample factor, the
  back-buffer size, whether vsync is on, and in the two city scenes `city N/M` — how many of the city's
  buildings survived the frustum cull from where the camera is standing. Everything that changes what the
  number means is on the line, so two runs — or two machines — can be compared without remembering how each
  was launched.
- `quality=<low|medium|high>` — the bundled detail tier. `ssaa=<n>` is *documented* to then override just its
  supersample entry, and **currently does not**: `LoadContent` calls `ApplyQuality(_quality)` unconditionally,
  whose last line is `SetSupersampleFactor(preset.SupersampleFactor)`, so the tier's factor overwrites the one
  the command line asked for. Check the `[fps]` line's own `ssaa Nx` before believing any A/B that varies it —
  an ssaa 1-vs-2 sweep silently measures 2× twice.
- `scene=<city|sea|savanna|desert|mountain|meadow|neon|forest|space|dream|cavern|moon>`, `sky=<1..18>` — pin the backdrop. The scene names are
  the Testbed's, so a script written against one executable drives the other.

## The Testbed measures too, and it is the one that can aim

`logfps` is the Testbed's as well now (#151), writing the same `[fps]` line, so `benchmark.ps1 -Exe …\Testbed.exe`
works unchanged. Before that the Testbed's only frame-rate line came out of `autoshoot`, which fires a ball a
second to produce it, and the overlay's own counter stops advancing while the overlay is hidden — which a
benchmark run does. Note an argument the Testbed does **not** recognise falls through to its startup map path,
so `logfps` used to make it try to load a map called "logfps" rather than fail.

What the Testbed has and the Game has not is `campos=x,y,z` / `camtarget=x,y,z` — a **fixed** camera. The
Game's front end orbits, so its absolute figures are one arc of an orbit (above); pin the camera and an A/B is
the same pixels twice. It also has `width=`/`height=`, and `ssaa=` up to 4: 1600×900 at ssaa 4 shades 23.0 Mpix,
within 7 % of fullscreen 3840×1600 at ssaa 2, so a small window can carry a 4K-class `High` load. That matters
because at 1600×900 ssaa 2 the reference desktop runs the arena view at ~550 FPS, which measures what the CPU
can submit and nothing about the frame.

- `arena=<list>` — which members of the arena are drawn: `cap`, `drum`, `pit`, `rims`, `glass`, plus `all` and
  `none`, comma-separated, a leading `-` removing. `arena=all,-cap` is the form an isolation wants. The `[fps]`
  line names the surviving members for the same reason it names everything else on it. #151's whole answer came
  out of this one argument — the stone cap is 88 % of the arena's cost at a play camera, and the translucent
  drain everyone suspected is nothing.

The measurement is wall-clock and cannot split CPU from GPU (MonoGame exposes no GPU timer queries). The cheap
discriminator is to run the same pin at two `ssaa` values: if the frame time does not scale with the pixel
count, the candidate is CPU- or draw-call-bound and turning pixel work off will not help it.

## Reference numbers

Front end, windowed 1600×900, vsync off, dome 13, **integrated Radeon (Ryzen 7 5700U)** — the project's weakest
development machine, and the class of hardware the tiers exist for:

| Scene | High (ssaa 2) | Medium (ssaa 1) | Low |
|---|---|---|---|
| neon city | 36.4 FPS / 27.5 ms | 95.6 | 103.7 |
| city | 43.4 / 23.0 ms | 108.0 | 119.2 |
| mountain | 52.3 / 19.1 ms | 80 | — |
| desert | 59 / 16.9 ms | 92.8 | — |
| savanna | 60.3 / 16.6 ms | 105.2 | — |
| meadow | 64.6 / 15.5 ms | 119 | — |
| sea | 68.5 / 14.6 ms | 125 | 124.5 |

The city rows are measured **after** the front-to-back sort (20 s runs; they read 9.9/30.0/43.4 and
13.1/38.4/55.4 before it — see "Drawing the city near to far" in `docs/rendering.md`); the terrain rows predate
it and are unaffected, there being no city in them. The five terrain scenes are within 15–19 ms of each other
and never needed a tier. The two city scenes are still the dearest, and `docs/game-shell.md`'s "The quality
tier" section has the block-by-block breakdown.

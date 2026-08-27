---
name: benchmark
description: Measure BS3D's frame rate under pinned, repeatable conditions and compare machines or settings. Use before deciding what a quality tier should turn down, or to check whether a rendering change cost anything.
---

# Benchmarking BS3D

Companion to `verify` (build/launch) and `screenshot` (framing a shot). This one is about **numbers**.

```powershell
dotnet build C:\GitHub\Game.sln
.\benchmark.ps1                                      # the 14-scene sweep at ssaa 1 and 2
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
   the machine and steps the tier down once the frame rate settles (a few seconds in, later on a machine that
   ramps slowly) — which reads as a spectacular win for whatever you were testing. Always pin one.
3. **Not pinning the scene.** The backdrop is a different one of the seven every launch and they span 6.8× in
   cost, so two unpinned runs are not comparable at all. `scene=` and `sky=` both matter: the sea and the
   savanna each force a dome of their own, so pin `sky=` after. **Pinning them is not the same as them
   holding** — a level overrides both, silently; see trap 11.
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

7. **Sweeping `ssaa` on the cavern or the dream, where it moves the pass not at all.** Since #155 those two
   backdrops shade a target the size of the **back buffer** and scale it up, so an `ssaa` 1/2/4 sweep shades
   the same pixels three times and only the resolve over them grows. Measured on the cavern: 23.9 / 25.2 /
   40.0 ms before #250's cut and 13.3 / 16.0 / 31.7 after — **the saving is the same ~9 ms at all three**,
   which is the signature to expect. To scale the *pass*, change `width=`/`height=`. (It is also a useful
   consistency check: an A/B whose delta holds constant across `ssaa` on these two scenes is measuring the
   pass; one whose delta grows with `ssaa` is measuring the resolve.)
8. **Asking for a back buffer larger than the panel — it is silently clamped.** `width=2560 height=1440` on a
   1920×1080 laptop produced a window Windows resized, and the run reported `958x484` on its own `[fps]` line
   with readings swinging 11.8–32.1 FPS while it settled. The line names the back buffer for exactly this
   reason: **read it back before believing the run**, and throw away anything whose reported size is not what
   was asked for.
9. **A MAXIMIZED window is the same trap from the other side, and it arrives unasked.** Twice in one #151
   sweep a run came back reporting `3840x1529` — the panel, not the pinned `1920x1080` — and read about 3×
   dearer, with nothing else on the line to say why. The mechanism was not pinned down: a *second* click on
   a title bar is what maximizes a window and this harness leaves the cursor sitting on one between runs, so
   that is the suspect rather than the finding. What follows from it is not "read the size when a run looks
   odd" but **compare the reported size against what you asked for on every run, and re-run the ones that
   differ** — a sweep that does it costs one line. Parking the cursor off the title bar after the focus
   click is cheap insurance beside it.
10. **Believing a VSYNC-capped reading is a frame cost. It is not, and it can be wrong by 2× in the
   direction nobody expects.** A vsync-capped run reports the refresh over an integer and nothing else, so
   every reading is a *bracket*: 75.0 means "cheaper than 13.3 ms" and 37.5 means only "dearer than that".
   Worse, #270 measured the Game presenting a frame at **37.5 while that frame cost under 5 ms** — held
   under `fpscap=200`, `150`, `100` and even `76`, which paces it exactly as a 75 Hz vsync does. Paired
   repeats on the same level: vsync **37.5 / 37.5** against `fpscap=75` **75.0 / 75.0**. So a vsync run can
   report half rate for a frame with 8 ms of headroom, and reading that number as a cost sends you hunting a
   10× shader fault that is not there — which is exactly what #270 was filed as. **The tell is
   non-monotonicity**: #270's ssaa sweep read 1 → 75, 2 → 37.5, 3 → 31.8, 4 → 75, and no cost curve is
   V-shaped with its minimum in the middle. Always re-run a surprising vsync number under `fpscap=` before
   believing it.
11. **`scene=` and `sky=` DO NOT hold once a level loads, and nothing warns you.** A `Level` file carries its
   own scene, its own sky dome and its own weather, and `GameplayScreen.Session.cs` applies all three over
   whatever the command line asked for. So `play level=Column scene=meadow` renders the **mountain** — the
   scene the level names — and the run looks perfectly healthy while doing it. This is not hypothetical: five
   of #270's runs were read as a cross-scene comparison and were the *same scene measured twice*, one of them
   the headline table of a document that then had to be corrected. The startup line `[game] scene X` prints
   what was **asked for**, which is what makes it treacherous; **the `[fps]` line prints what is actually
   being drawn, and it is the only authority.** Read the scene and the dome off `[fps]` on every run and
   throw away any that does not match what you asked for, exactly as trap 8 says to do with the back buffer.
   To compare scenes at all, either use the **front end** (no level, so `scene=` holds — see trap 5 about the
   orbit) or use levels that genuinely name the scenes you mean, and then accept that the map and the dome
   differ too and the comparison is no longer clean.
12. **Average the per-second readings with a MEDIAN, not a mean.** Trap 6 does not always take a whole run:
   twice now a run has opened at its neighbours' frame rate exactly, then collapsed to a third of it partway
   through and stayed there, which drags a mean far enough to invert an A/B. A median of the kept readings
   cannot be moved by it, and printing the lowest reading beside the median still shows that it happened.

13. **A CONTROL camera that draws nothing is not the control you think it is.** #151's "sky only" frame was
   `campos=0,0,0 camtarget=0,80,0` — straight up, i.e. along the up vector, which makes `Matrix.CreateLookAt`
   degenerate, and *nothing at all is drawn*. Two different scenes through it give byte-identical flat frames
   of the clear colour (7.8 ms), while tilting the same camera 14° off vertical draws the dome and its clouds
   and costs 30.2. So it bounded the post chain over an empty frame — legitimate for that, since the post
   chain is full-screen and scene-independent, but nothing like the "sky only" it was labelled and used as.
   It also went straight into a subtraction whose remainder was named "the arena". **Screenshot the control
   too**, not only the measurement camera, and look for a frame that is one flat colour.

14. **On the APU a spread across RUNS can be wider than the thing you are measuring, and the fix is to stop
   comparing runs.** The integrated Radeon shares one 15 W package budget with the CPU, so its sustained clock
   moves with whatever else the machine is doing: two 125-second runs of one *unchanged* variant came back
   **33.6 and 25.7 ms**. It is not a scale factor either — under contention every variant compresses towards
   the same number, so a sweep taken then reads as "nothing costs anything", which is a false negative rather
   than a noisy one. Pairing between processes does not help; the drift is *between* the runs. The Testbed's
   `alt=` (below) is the answer: cycle the variants inside one process and difference them within each cycle.
   And on a laptop, **look at what else is running before believing anything** — a stray `find` had been
   pinning a core for three and a half hours here, and killing it moved a fixed pin from 26.7 to 24.5 ms.

Also: keep both halves of an A/B in the **same build configuration**, and remember that lifting the cap is
what makes the number a frame cost rather than the display's refresh. The script always passes `nocap`;
**prefer `fpscap=N` on the desktop**, which does the same job for a measurement without leaving the card flat
out (see the section on it below, and note the Game's default cap is no longer vsync since #270 — it is a
CPU-side limiter at the refresh, so an un-pinned Game run now reads that limiter rather than a vblank).

**Attribution does not travel between the desktop and the APU.** #102 measured every individual cavern
reduction at zero on the 6900 XT (the pass being occupancy-bound there) and #250 then measured one of the same
cuts at **20.7 → 17.9 ms** on the integrated Radeon. A wide desktop part has occupancy to spare; the machine
the tiers exist for does not. So measure on the class you are trying to fix, and label which class produced
every figure that gets written down.

**A wide spread is not automatically a hot machine.** A `level=` run has real variance of its own — the
ceiling descends, the cluster swings, physics spikes — and two runs of the *same* build came 4.4 ms apart. The
cheap discriminator is to re-run a **fixed-camera** Testbed pin after the series: it read 13.3 ms both before
and 20 minutes into a continuous session, which said the spread was the level and not throttling.

## What the game gives you

- `logfps` — one line a second to stdout: the frame rate, the scene, the dome, the supersample factor, the
  back-buffer size, the **frame limit** and where it came from, and in the two city scenes `city N/M` — how many of the city's
  buildings survived the frustum cull from where the camera is standing. Everything that changes what the
  number means is on the line, so two runs — or two machines — can be compared without remembering how each
  was launched. **The Game's line reads `limit N (refresh)` or `limit N (fpscap)` or `limit off`, not
  `vsync on/off`** — since #270 the Game does not vsync at all, and a line still saying so would misreport
  the one setting that decides what the number means. The Testbed still says `vsync`, which is still true of
  the Testbed; do not assume the two lines are identical any more.
- `quality=<low|medium|high>` — the bundled detail tier. `ssaa=<n>` then overrides just its supersample entry,
  and **this now works**: `ApplyQuality` ends `SetSupersampleFactor(_supersampleOverride ?? preset.SupersampleFactor)`,
  so a command-line factor survives the tier being applied again in `LoadContent` and on every adaptive step.
  This entry said the opposite for a long time, from the era when `ApplyQuality` wrote the tier's factor over
  it unconditionally; re-checked against `[fps]` in #270, where `quality=high ssaa=1` reported `ssaa 1x` against
  the tier's own 2. Read the line's `ssaa Nx` back anyway — it is on the line so that an A/B varying it can be
  believed, and either half of a pinned pair going astray is the kind of thing that only shows there.
- `fpscap=<n>` — present immediately but never more than N frames a second (see the section on it below).
  **Both executables**, the Game's since #270. This is what makes a reading a frame cost; a vsync-capped
  number is a bracket, not a cost, and trap 10 is what that has already cost once.
- `scene=<city|sea|savanna|desert|mountain|meadow|neon|forest|space|dream|cavern|moon>`, `sky=<1..18>` — pin the backdrop. The scene names are
  the Testbed's, so a script written against one executable drives the other.
- `play` — skip the front end into the **first** level, and `level=<n|name>` into any other (its 1-based place
  in the set, as the title bar numbers it, or its name: `level=Colossus`, `level=11`). **The front end is not
  the game**: it has no cluster, no gun, no HUD and no simulation, and `docs/game-shell.md` names that blind
  spot for the adaptive probe too. A backdrop that clears 75 FPS empty says nothing about the same backdrop
  with 959 balls hanging over it, which is what #166 and #167 were each left unable to answer.
- **Measure in a WINDOW.** Four back-to-back `fullscreen` runs took the owner's desktop down hard on
  2026-08-22 — a Windows 11 machine whose card runs at a reduced power limit, and which never did this under
  Windows 10. Nothing needs fullscreen: `logfps` reports the size it measured at, so a windowed figure is
  reproducible as long as the size is quoted beside it, and `shot=` saves the game's own back buffer whatever
  that size is. Quote the resolution with every number — they are not comparable across resolutions.
- `preview=<n|name>` — pin the map the **front end** hangs, named exactly the way `level=` is. The front end
  is not empty since #249 and its camera is framed for whatever is hanging since #254 — a small map is
  watched from a different stand-off than a big one, and once a cycle the lens flies in at it — so two
  front-end runs that did not pin the same preview measured two different scenes from two different places.
- `balls=<beach|bubble>` — pin what every ball is **made of** (#258), overriding what each level file names.
  The glass bubble is a second technique with a heavier pixel shader and it puts the shell out **twice**
  (two walls, opposite cull modes), so it is a real fill-rate difference and it is the *map* that decides it:
  a run that did not pin this measured whatever style the level happened to be authored in. Measured with it,
  959 balls under the cavern at ssaa 2×: windowed 1600×900, vinyl 576.7 / 593.9 FPS against bubble 537.1 /
  536.3 (~8 %); at 3840×1600, vinyl 166.1 / 166.3 against bubble 149.5 / 149.9 (~10 %). The figure does not
  move with how opaque the film is, nor with its screening fade — same passes, same instructions.

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
  out of this one argument — the stone cap is ~80–88 % of the arena's cost at a play camera on both classes of
  machine, and the translucent drain everyone suspected is nothing. **⚠ The members are a flags enum, so the
  `[fps]` line's arena field is itself comma-separated** (`arena Drum, Pit, Rims, Glass`): a harness regex that
  stops the field at the first comma silently matches nothing on every multi-member run, and reports it as "no
  output" rather than as a parse failure. That cost a whole sweep here before the logs were re-read — and
  re-reading them was enough, because the readings were in them all along.
- `alt=<members>[/<probe>];<members>[/<probe>];…` — draw the arena a **different way on every `[fps]` window**,
  cycling the listed variants: `alt=all;none`, `alt=all/0;all/6`, `alt=all;all,-cap;none;all/3`. Each variant is
  an `arena=` list with an optional `/N` cap probe after it; the line already prints the members and the probe,
  so every reading labels its own variant, and the switch happens after the line is written so no window is a
  mixture of two. **This is the only way the APU can be measured** (trap 14): the variants share one process,
  one clock and the same neighbours, so what is left between two readings a second apart is what the two
  variants cost. Analyse it **paired**: group the readings into whole cycles, take each variant's difference
  from the baseline *within its own cycle*, then median those differences — and report **how often the sign
  held**, not only the size. Real effects come out cheaper in 92–100 % of cycles; noise sits at 42–71 %, and
  that column is what tells the two apart when the machine is drifting under both.

The measurement is wall-clock and cannot split CPU from GPU (MonoGame exposes no GPU timer queries). The cheap
discriminator is to run the same pin at two `ssaa` values: if the frame time does not scale with the pixel
count, the candidate is CPU- or draw-call-bound and turning pixel work off will not help it.


## `fpscap=N`: measuring without leaving the card flat out

**Read this before benchmarking on the desktop.** `nocap` is what turns an FPS reading into a frame cost, and
it is also what leaves the GPU rendering thousands of frames a second. That was believed to be the dangerous
part — the owner reported an uncapped run hard-resetting his desktop — and #250 found it is not:

- the machine hard-reset **twice in one afternoon under capped runs**, at 18:40 on 2026-08-19;
- the System log has `Kernel-Power 41` and `EventLog 6008` and **nothing else** — no bugcheck, no `MEMORY.DMP`
  (kernel dumps are enabled), no WHEA entry, no display-driver reset (4101). Windows never got control;
- there were **ten unexpected shutdowns in the preceding thirty days**, i.e. it predates any of this work.

So this is a machine-level fault (the signature points at power delivery), not a shader or a benchmark mode,
and no cap can be assumed to protect it. Ask the owner before running a measurement sweep on the desktop.

The instrument is `fpscap=N`, and **both executables have it** — the Testbed's `TestOptions.FpsCap` since
#250, the Game's since #270, which needed it because the Game is the only one that can load a **level** and a
vsync-capped level can only ever say "dearer than one refresh" (trap 10 above). It presents immediately, so nothing
quantizes the reading, and idles out the rest of each frame's period, so a frame **cheaper** than the cap never
runs away while a frame **dearer** than it is never delayed and still reads its true cost. Set the cap under
the frame rate being measured — at 150 anything dearer than 6.7 ms comes out exact — and read the plateau
itself as "cheaper than this", never as a cost. It implies `nocap`'s presentation, and the `[fps]` line carries
`(cap N)` so a capped run cannot be mistaken for a free one later.

The idle is a **spin**, never `Thread.Sleep`: at Windows' default 15.6 ms timer resolution `Sleep(1)` returns at
the next tick and costs about six milliseconds, which measured a 300 FPS cap down to 143 and a 400 FPS cap to
209 — the instrument reading its own idle. Spun, the plateau sits exactly on the cap (60.0, 200.0, 300.0
measured flat).

**One agent on the GPU at a time.** Two agents work this repo on the same desktop; a `BS3D.exe` belonging to
the other one was alive through the runs above, which both doubles the load and invalidates every number.
`Get-Process BS3D, Testbed` before a sweep, and say in `docs/agent-notes.md` that you are taking the card.

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

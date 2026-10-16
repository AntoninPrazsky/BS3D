# Capturing and measuring

Part of [The Testbed](../testbed.md).

## Saving a frame: `shot=`, `shotframe=`, F8

The Testbed writes PNGs out of its own back buffer through `Prazsky.Core.Render.ScreenshotWriter`, shared with the Game (#371).

- `shot=<t1,t2,…>` fires at wall-clock seconds (the Game's spelling and parse, so one script drives either). `shotframe=<n1,n2,…>` fires on frame indices counted from 1 (Testbed only). `F8` takes one by hand.
- Each prints `[shot] <path>`; the file lands in `Screenshots\` beside the exe as `testbed-<yyyyMMdd-HHmmss>-<scene>.png`.
- **Prefer it to any capture through the screen**, which can return the lock screen, the window in front or a blank frame without failing (measured 2026-08-12, recorded in `.claude/skills/screenshot`).
- **`shotframe=` exists because a schedule in seconds samples whatever moves.** With F5 freezing the simulation and `campos`/`camtarget` pinning the camera, frame *n* of two runs is the same frame; the Game can hold neither still, so it does not take `shotframe=`.
- The capture is the **last** statement of `Draw`, after `[fps]` and `CapFrameRate`, because the readback stalls the pipeline and encodes on this thread (~0.1 s at 1600×900) and must not land in a frame a benchmark counts. It runs off `_pulseSeconds`, so a scheduled shot lands at the same moment whether the simulation runs, is slowed or frozen.

## Starting the scene's clock late: `clock=<seconds>`

`clock=600` runs everything that draws the scene ten minutes into a session from the first frame (#903). The Game's scene clock is the seconds since launch and the Testbed's starts at 0, so a term that grows with the clock is invisible in a fresh Testbed capture (the sea's fish turned tail first from about a minute in, and no capture short of waiting an hour could show it).

- It is **one** shifted clock, `SceneClock` in the Testbed (the wall clock plus the argument), handed to every consumer the Game feeds from its own wall clock: the scene's shaders (`SceneFrame.Time`), the scene lights, the ground glow, the volcano's eruption glow, the aurora's tint, the rooftops, the forest's fireflies, the city's windows, the clouds, the balls' pulse and the tonemap's grain. A light and the thing it lights (the savanna's campfire light and its flame, the eruption's glow and its jets) are one flicker only if they read the same clock, so a new consumer of the scene's time takes `SceneClock`, never the bare `_pulseSeconds`. `shot=`, `shotframe=`, `at=`, the input script and the crosshair's blink run off the wall clock as before, so a script that drives a run does not move.
- Prints `[clock] scene clock starts at <n> s`. Testbed only; the Game has no spelling for it.
- A pair of frames at `clock=600` with the camera pinned is what separates "right at the start" from "right for as long as the session runs" (`scene=sea sceneseed=0 campos=24,-36,6 camtarget=0,-20,0 nopost clock=600`).
- ⚠ **Two shots in a row are not 0.5 s apart on the scene clock if they are close.** Each readback takes about 0.1 s on this thread and the clock advances by the whole frame, so `shot=6,6.1` collapses or lands 0.2 s apart; ask for gaps of 0.5 s or more, or read the gap off a probe that prints it.

## Reading the camera back: `C`

`C` (or `at=<t>:C`) prints the live camera in the spelling the command line takes:

```
[campin] campos=0.00,-4.00,30.00 camtarget=0.00,-8.00,0.00 fov=72
```

- In game mode it gives the pose `GameCameraFit` computed, a framing no argument can produce, plus the aim as `[aimpin] … -> aim=<t>:<elevation>:<traverse>` ([Driving a run without a keyboard](scripted-input.md)).
- Invariant culture, as `TryParseVec3` reads it, because a decimal comma would not parse back.
- `fov=` is printed in free mode only, because game mode's `GAME_FOV` is not a pin the argument can hold.

## Sweeping variants inside one process: `alt=`

`alt=<pins>;<pins>;…` applies the next variant on every `[fps]` window; a variant is a comma-separated list of the command line's own `<dial>=<value>` pins:

```powershell
Testbed.exe scene=meadow logfps fpscap=400 alt=ssaa=1;ssaa=2;ssaa=4
Testbed.exe logfps alt=scene=meadow;scene=savanna;scene=forest
Testbed.exe logfps alt=all;all,-cap;none            # the old arena-only spelling, still read
```

**Two runs cannot be compared on this project's machines**: on the integrated-Radeon laptop two runs of one unchanged variant read 33.6 and 25.7 ms, and under contention variants converge, so a sweep across runs says "nothing costs anything". Inside one process they share one clock and driver state.

- ⚠ The variant advances inside the `[fps]` logging, so **without `logfps` only the first variant is ever applied**.
- The `[fps]` line ends `variant <spec>` in the caller's words, written **before** the switch, so no window mixes two variants. The plan is printed on an `[alt]` line, which names every refused pin, because a dropped pin would make the sweep two readings of one build.
- A token without `=` is the old arena spelling (`all,-cap`, `all/6` = arena plus `capprobe`), kept because the benchmark skill and recorded sweeps use it.
- ⚠ The first variant is applied at the **end** of `LoadContent`, not in `BuildCity`, which runs before the pipeline and ball set exist.
- A capped reading means "cheaper than this", never a cost; set `fpscap=` above what is measured.
- **Alternate for numbers, run plainly for pictures**: a `shot=` in an alternating run lands in whichever variant is up (both captures once landed in the same one).

### The dials

**Only a dial whose switch leaves nothing behind may alternate**, or the next window measures the transition. The list is `ALTERNATION_DIALS` in `Testbed.cs`: `arena`, `capprobe`, `scene`, `sky`, `balls`, `colourcue` (#737, also a plain argument: `none`, `motif` (each style its own set, the default), `motif:1` to `motif:4`; ⚠ for its cost run one set per process, see "The colour cue" in `docs/rendering/ball-styles.md`), `ssaa`, `msaa`, `rscale`, `detail`, `finemeshes`, `shadow`, `shadowmap`, `ballshadow`, `glassshadow`, `exposure`, `nopost`, `volcano`.

- **`nooverc` is refused**, because the overcast lerp carries across a switch; pin it for the whole run. For the same reason a scene pin snaps its weather (`SetScene(…, immediately: true)`).
- **`ssaa` reaches four places** (the pipeline's target, the tonemap's filter, the space scene's stars, the balls' dither cell), so it goes through `SetSupersampleFactor`; setting some of them measures a mixture.
- **`shadow=<0..1>`** pins `SceneRenderer.ShadowScale` over the scene's `ShadowConfig.Strength`; 0 is the same code path as a `Strength` of 0 (no target, no caster pass, no taps). `detail=0` is not a substitute: it also switches scenes to a reduced program. The `[fps]` line shows `shadow <x>` only below 1.
- **`shadowmap=<size>`** (#484) pins `SceneRenderer.ShadowMapSizeOverride` (0 = the scene's `ShadowConfig.MapSize`), winning over the Game's `ShadowMapSizeCap`/`ShadowMapSizeScale`.
- **`finemeshes=<0|1>`** (#758, also a plain argument) pins `SceneRenderer.FineMeshes`: 0 draws Low and Medium's coarser meshes (the savanna acacias' tube wood, the spruces' lathe, #733) instead of High's grown fork and boughs. It is read per draw, so it alternates cleanly. On by default.
- **`ballshadow=<0|1>`** isolates the balls' casting cost; **`glassshadow=<0|1>`** (#553) the ceiling glass's shadow ("The ceiling's glass shadow" in `docs/rendering.md`). Both default on.
- ⚠ **`shadowmap=` and `volcano=` are pins only**: `TestOptions.Parse` has no plain case, so `shadowmap=2048` alone prints `[args] Ignored` and changes nothing; use `alt=shadowmap=2048`. A dial added to `ApplyVariant` needs a `TestOptions.Parse` case to work plainly. Any unrecognised argument that does not look like a file is printed as `[args] Ignored '<arg>'` (#574).

## Checking the terrain mirrors: `mirrorcheck`

Compares the current scene's terrain height on the GPU with `TerrainMirror`, the CPU copy that the scatters, the lamps and the intro lenses are placed by. On the third frame it draws the scene effect's `HeightProbe` technique into a `Vector4` grid (`TerrainMirrorCheck.SAMPLES_PER_SIDE`, ±`HALF_EXTENT`), compares, prints one line and exits:

```
[mirrorcheck] forest n=65536 max|dh|=0.000069 mean|dh|=0.0000058 worst at (-510,186) over tolerance 0 of 65536 tol=0.01 PASS
```

- **Exit code 0 pass, 1 fail, 2 no mirror in this scene**, so a script runs it per scene (`scene=forest mirrorcheck`). Eleven scenes have one: desert, mountains, outback, polar, savanna, tropical, meadow, forest, aurora, volcano, Moon.
- The volcano adds a line for the drawn ground, which includes scoria the mirror leaves out by design; the verdict is on the massing.
- **Run it on every mirrored scene after touching a terrain shader's height or its mirror.** Figures and current failures: "The terrain mirrors, in one copy and checked against the GPU" in `docs/scenes.md`.

## Hanging a map as rope or cloth: `softness=`

`softness=<Hz>[:<damping>]` (#690, a prototype) re-springs every ball-to-ball socket (damping 0.3 if omitted, so it swings), over the level's own `Softness`; otherwise sockets keep `BallsConstraintsBuilder.SPRING_SETTINGS`.

- The glass's anchor sockets stay as built, and landing shots join at the soft spring (`BallContactEventHandler.LatticeSpring`). The mechanism is `Prazsky.BS3D.Physics.LatticeSoftness`, pinned by `LatticeSoftnessTests`.
- ⚠ **The start swing (#617) restores the builder's spring only to sockets still owned by the top-level ball they were taken from** (`ClusterStartSwing`), because Bepu reuses a freed handle for the next constraint: a released anchor's handle could become a landing's ball socket, and the swing would stiffen it into a rigid knot.
- It logs `[softness] <n> lattice socket(s) at <Hz> Hz, damping <d>; the glass's own as built`, then every half second for twelve seconds `[softness] t=<s>s sag <d>, fastest ball <v> u/s` (a speed running away means it has come apart).
- Prototype maps: `Testbed/Maps/Sag_Chain.json`, `Sag_Bridge.json`, `Sag_Hammock.json`; `LevelGen --softness=` does the same. Results: "Soft lattices" in `docs/game-session.md`.

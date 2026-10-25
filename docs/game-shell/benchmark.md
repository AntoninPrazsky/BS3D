# The benchmark (Extras > Benchmark, #988)

Part of [The game: shell](../game-shell.md).

The owner: a benchmark in Extras "that both measures valuable data and is visually attractive", because nobody knows what a player runs the game on (Intel, Qualcomm, another AMD driver, the Pi and whatever follows it), and the 125 % desktop (#825) was found by luck on a second machine. It may have little to do with the game.

## What it does (first stage)

- **The show** (`BenchmarkPage`): it flies the chapter tour of each of `SEGMENTS` (Cavern, Neon City, Forest, Meadow: the dearest scenes on the desktop's High) against the front end's backdrop, the page standing aside while it flies (the Scene picker's mechanism). Each tour is played on a **fixed seed** (`BackdropScreen.PlayTour(Random)`, `SEED` + the segment), so two runs of one machine fly one path. About 85 seconds. The skip key stops it, keeping what was measured.
- **At the player's own tier, with the frame free** (`BS3DGame.BeginBenchmark`): `FrameLimitHz` is 0 and `PaceFrame` skips the compositor's wait (either alone holds the frame at the refresh: ⚠ the first run read 13.34 ms on every frame of every scene with the limit off and the wait still on), and `TuneQualityToFrameRate` returns at once, so the adaptive probe does not step the tier under the run. `EndBenchmark` puts the player's scene back.
- **Every frame measured** (`FrameStats`, pure, `FrameStatsTests`): the wall time between the page's updates. Per segment: mean, median, 95th and 99th percentiles, the **1 % low** (the frame rate of the slowest hundredth, averaged), the worst frame and the **share over the display's budget** (one frame of its refresh, 60 Hz where it reports none; over is past it by 2 %, `OVER_SLACK`, since a frame held to the refresh lands on it give or take the timer's jitter). A segment's first `SETTLE_SECONDS` (1.5) are the scene's first-use cost (shaders, the cavern's hall built off the frame) and are reported apart: the cavern's first frames reached 1.26 s on the desktop. The frame list is reserved once (half a minute at a thousand frames a second), so a running segment allocates nothing.
- **The machine** (`BS3DGame.BenchmarkSystem`): the build, the runtime identifier and OS, the OS and process architectures (x64, ARM64, x64 on ARM), the CPU's name (the registry on Windows, `/proc/cpuinfo` on Linux) and threads, the adapter's description and its vendor and device ids (where the platform's adapter has them: DirectX's does, DesktopGL's has neither, so they are read by name), the back buffer, the refresh, windowed or full screen, the tier, its supersample, the Potato path, and the display's scale and DPI awareness (#825). Nothing that names the player: no paths, no user name, as a note's context.
- **Where the result goes**: the page (each scene's median FPS, 1 % low and share over the budget, and the machine in a line), `[bench]` lines in the run log, and `Benchmark.json` in the player's profile (`AtomicFile`, no backup: a benchmark is retaken). Never `Progress.json`, never the settings, never online.
- **`benchmark`** (a launch argument) opens Extras > Benchmark at boot and starts it: one command for a machine nobody is sitting at, or one we do not own. With `userdata=<dir>` the JSON lands there.

## What it recommends

`BenchmarkVerdict.Recommend`, read from the numbers alone, never from the device's name, so a machine nobody has seen needs no code. **One step at a time**, as the adaptive probe steps, because the run measured one tier and a tier's cost is not a scale factor of another's: a tier that drops frames in any scene (more than `DROPS`, 5 %, of its frames over the refresh's budget) recommends the one under it, naming the scene; a tier whose every scene's 99th percentile stays under `ROOM` (0.6) of the budget recommends the one over it; anything between keeps the tier. The result page says so in a sentence and **offers** the tier as an entry ("Use Medium", "Try Ultra"), which puts it on as the Quality row's press does (`BS3DGame.ChooseQuality`: pinned, stored, Auto quality off) and leaves the page ready to run again on it. Nothing is applied unasked; a locked or Potato run is offered nothing (its renderer cannot change while it runs). `BenchmarkVerdictTests`. On the desktop at 1920 x 1080 Medium recommended High (every scene over 480 FPS).

## What it read on the desktop

RX 6900 XT, Ryzen 9 5900X, 1920 x 1080 windowed, High (ssaa 2), two runs: Cavern 346 and 361 FPS median, Neon City 196 and 196, Forest 173 and 175, Meadow 245 and 247; 0 % over the 75 Hz budget. A run's own spread is a few per cent.

## Not yet

The correctness probes (a known frame read back against ranges: a black or NaN frame, a clamped HDR target), the CPU's physics step apart from the GPU, a stress finale, a live frame-time graph, and the Pi's own segments (the Potato path cannot draw the SM 5 scenes; on GamePi today the page runs the same scenes through it).

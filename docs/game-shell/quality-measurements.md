# What the frame costs: the quality tier's measurements

Part of [The game: shell](../game-shell.md).

The figures a future tier change compares against (#64: measured, because intuition ranked the candidates wrongly). The **reference APU** is the ThinkPad (Ryzen 7 5700U, integrated Radeon, on AC) the ladder exists for; its budget is **16.1 ms** (`limit 62 (refresh)`). The **reference desktop** has an RX 6900 XT. Tier contents: [The quality tier](quality-tier.md).

## Measuring without lying

`logfps` prints a line a second with every setting that changes its meaning, then a `[pace]` line (the steps' evenness) and an `[alloc]` line (the frame thread's managed bytes a frame and the gen0 collections, BestPractices.md §3); `scene=`/`sky=` pin the backdrop (**the scene is random per launch**); `.claude/skills/benchmark` drives it (`tier-matrix.ps1`).

- **Attribution does not travel**: the same cuts price 0.2–1.0 ms on the desktop and 3–7 ms on the APU; price a lever on the weak machine.
- **The minimum over repeated runs is the frame's cost** (2026-09-23: a first pass lied by up to 25 % while background processes shared the APU's package). **Compare builds alternated in one run, never two afternoons**; inside one process use `alt=` or `mbflip=`.
- **Ways to measure nothing**: an unfocused window (the probe and Myra read a click only while `IsActive`, so the benchmark clicks the title bar); an A/B without `quality=`/`ssaa=`; the front end's orbit (a fixed window samples the expensive arc); a **stray process** from a dead harness (two instances gave 6 FPS and a scripted 100 ms click was never seen, Myra reading input inside `Draw`); **scripted input is not repeatable** (25, 28, 26 balls from one build), so prefer a categorical outcome.

## What the scene and each lever cost

- **The scene is the order of magnitude.** APU front end, 1600×900, FPS at `High`/`Medium`/`Low`: neon city 36.4 → 95.6 → 103.7, city 43.4 → 108.0 → 119.2, sea 68.5 → 125 → 125. The city's cost was overdraw, which the sort took ("Drawing the city near to far" in `docs/rendering.md`).
- **Supersampling is most of the ladder**: `Medium` is worth 12–22 ms (2026-08-27) wherever the pass scales with shaded pixels. The cavern, the dream and the big top are immune (#155, #1096: their backdrop is sized off the back buffer), so only reduced programs reach them (and, for the cavern, the coarse meshes: "Tiers" in `docs/scenes/cavern.md`).
- **MSAA.** Desktop 3840×1600, `ssaa` 1, 8× → none: mountain 1.12 ms, meadow 0.61, neon city 0.58; 8× → 4× free there. **On the APU it is not (#540)**: at `Medium`'s other settings (the play camera over `Maps/Full.json`, `alt=msaa=8;msaa=4;msaa=2` in one process, 83 and 84 cycles) 8× → 4× −0.31 ms on the mountain, −0.61 in the neon city; 8× → 2× −4.73 and −2.88. Hence `Medium` 4, `Low` 2.
- **Below native** (refused): 0.85× saves 0.59–1.05 ms on the desktop.
- **Reduced programs**: mountain 0.18 ms at `ssaa` 1, 0.58 at 2; Mars +0.39/+0.85 authored against +0.22/+0.49 reduced, 19.15 → 16.74 at `Low` with a 256-vertex grid; meadow +0.36/+0.19 against +0.11/+0.06; savanna +0.03/+0.04. **The volcano** (#540): the terrain is 5.16 of a 14.87 ms frame and plume, jets, ash 0.04, 0.04, 0.13 (`SceneRenderer.VolcanoLayers`, the Testbed's `volcano=all/-plume`, `-jets/-glow`, `-ash`, `-terrain`); `VolcanoReduced` (octaves, relief normal, the flows' gradient noise, a 256-vertex grid): **Caldera 18.31 → 14.49, Paroxysm 17.35 → 13.47 at `Low`**.
- **The arena** (#151) is 18–21 % of the APU's frame at `ssaa` 2, its stone cap 44–69 % of that ("What the arena costs" in `docs/scenes/island-surface-and-cost.md`); `SurfaceDetail` saves 0.336 ms at 1080p `ssaa` 4 on the desktop and **0.16 ms on the APU** at `ssaa` 1 (kept because it is free). **Ceiling refraction**: 1.2–1.6 ms per rung on the APU where the plate is in frame, ~0.1 ms on the desktop. **Motion blur**, `Medium` on the APU (2026-09-29, `mbflip=5`): +0.68 to +0.78 ms at rest, +0.86 to +0.91 sweeping.
- **The plant program's registers** (#1028): `Acacia.fx` carried the stone (#777), the bark's relief (#980) and the straw (#971) behind uniform branches in the one program every plant ran. Compiled out of the plain program, on the reference APU at `Low` (where none of the three is drawn): **−1.82 to −1.95 ms** on Amphora, **−1.41 to −1.54** on Balloon, nothing on Pendulum, Spyglass, Caldera or Umbrella. Captures of the meadow, the Moon's rocks and the savanna at High are identical at block level. `BestPractices.md` §7.
- **Ball materials are not the lever** (against vinyl: ice +1.24 ms, glass bubble +1.07, the rest under 0.3 ms). **Cluster wind** (#95) is the one CPU entry (975 balls: 0.83 → 10.1 ms of physics a frame), hence `High`/`Ultra` only.
- **Finer meshes** (`High`/`Ultra`), on the desktop at 3840×1600, `ssaa` 2: the Testbed's `alt=finemeshes=0;finemeshes=1` where the dial swaps one thing, the Game's builds alternated where it swaps several. ⚠ **The lever is more than meshes**: it also draws the cloud heaps (#989) wherever the sky stands as heaps (the savanna, the meadow, the mountains, the outback, the beach and the sea; their row is below), the savanna fires' volume (#886), the forest's whole High wood (#962), the volcano's flank program and Mars's baked ground (#1100, which rebakes on every switch back), so a Testbed pair prices them together. Under `weather=clear`, at the game-mode camera, the savanna's fork, thorn bushes and fires' volume are **+0.54 ms** on Heart and the meadow's fork and shrubs **+0.26** on Pennant, about what the Game's figures for them add up to; the forest's whole High wood is **+2.20** at its play camera. The details are in each scene's document. `Medium` and `Low` never draw them, so the APU pays nothing for the fine wood (the meadow's plugs, about +500 triangles a tree kind, it does pay: below).
  - The desert's beetles and scorpions (#1110): **nothing measurable**, +0.005 ms at the game-mode camera and -0.003 with a scorpion filling the frame (the Testbed's `alt=finemeshes=0;finemeshes=1`, 28 and 27 pairs, the sign either way; the desert draws no heaps, so the dial swaps only them). Detail: "The beetles and scorpions (#966)" in `docs/scenes/desert.md`.
  - The savanna acacias' grown fork (#758): **+0.09 ms** at `High` and **+0.06** at `Ultra` in the Game on Zebra (11.47 to 11.56 and 11.64 to 11.70 ms, the two builds alternated twice). Detail: "The fork" in `docs/scenes/savanna-trees.md`.
  - The meadow trees' grown fork (#889): **about +0.05 ms**. In the Game on Pennant, `High` goes 11.35 to 11.40 ms and `Ultra` 11.54 to 11.60, in both repetitions: that is the fine wood's cost. `Low` and `Medium` draw the coarse wood with its closed knees, +14 to +18 % triangles (about +500 a tree kind), not measured on the APU. Detail: "Two woods (#889)" in `docs/scenes/meadow.md`.
  - The savanna's thorn bushes (#883): **+0.03 ms** at `High` and **+0.06** at `Ultra` in the Game on Zebra (11.40 to 11.43 and 11.81 to 11.88 ms, three alternated runs); `Medium` 4.95 to 4.96. Detail: "The bush" in `docs/scenes/savanna-trees.md`.
  - The spruces' boughs (#733): **+0.66 ms** at the aurora's play camera with #1120's denser near crown, where the crown before it cost +0.44 at the same pins; in the Game at `Ultra` on Fish #1120 took the frame from 8.05 to 8.12 ms to 8.17 to 8.19. In the daytime forest they are priced only with the rest of its High wood (above). The bark stem they grow out of (#960) costs +0.08 ms on Fish at the game-mode camera. The rebuilt aurora's whole frame on Fish is in "Costs" in `docs/scenes/aurora.md`. Detail: "The spruces on High and Ultra" in `docs/scenes/aurora.md`.
  - The Moon's and the volcano's rocks (#777): **+0.19 ms** at the Moon's play pin and +0.21 among its rocks (two runs of 41 paired cycles each, the sign held in all), **+0.16** at the volcano's play vantage (+0.14 on screen, the planting's price at `finemeshes=1` less its price at `finemeshes=0` because the dial swaps the flank's program there too, and +0.02 for the casters). The cost is triangles at the primitive rate, so it does not fall where few rocks are in view. Detail: "High and Ultra's rocks" in `docs/scenes/moon.md`.
  - The mountain range (#781): **−4.7 ms** at `High` and **−4.6** at `Ultra` at the game-mode camera on Belfry under a clear sky; under the mountains' own broken sky the lever's net there is −3.3 and −3.2, the heaps (below) costing the difference. In the Game on Belfry, before the heaps, `High` went 14.73 to 11.31 ms and `Ultra` 14.67 to 11.32. Cheaper with more detail because its program cuts the island's hole in geometry instead of with `clip()`, so the depth test runs before the pixel shader: with the clip put back (a throwaway build, the same pins) it costs +3.2 ms over the coarse range instead. Detail: "High and Ultra's range" in `docs/scenes/mountains.md`.
  - The cloud heaps (#989), wherever the sky stands as heaps: the lever's pair under the scene's own sky less the same pair under `weather=clear`, at the game-mode camera in one session (29 to 31 paired cycles a run, the sign held in every one), **+0.53 ms** on Heart (the savanna) and on Pennant (the meadow) at `High` and `Ultra`, **+1.4** under the mountains' broken sky on Belfry. The Game draws them wherever the lever is on (`CloudVolumesShown`). Detail: "The clouds as volumes on High and Ultra (#989)" in `docs/rendering/weather.md`.

## The reference APU against shipped levels

Each chapter's heaviest level that is not its block's first (by balls; the city has two rows), in the Game: `level=<name> quality=<tier> nocap logfps windowed width=1600 height=900 mute nofocuspause nofps sceneseed=0`, with `userdata=` a scratch profile whose settings have online scores and the tutorial off. 45 s a run, the first six readings dropped, the median of the rest. **v0.5.0's code (`main` at be2c5420) measured beside v0.4.0's (9ed06dc4)** on 2026-10-27, `history-sweep.ps1 -ByLevel`: each level's two builds forward then reversed, back to back, and the minimum of each build's two runs (builds' `[build]` sets: v0.4.0 libraries `db10bfd5`, shaders `c56d60af`; `main` `c664fd3b`, `36dad7c5`). ms:

| level (scene) | `Low` v0.4.0 | `Low` v0.5.0 | step | `Medium` v0.4.0 | `Medium` v0.5.0 | step |
|---|---|---|---|---|---|---|
| Umbrella (aurora) | 14.27 | **18.03** over | **+3.76** | 20.59 | 29.27 | **+8.68** |
| Spring (cavern) | 15.74 | **18.03** over | **+2.29** | 30.95 | 27.00 | −3.95 |
| Amphora (meadow) | 14.71 | **17.91** over | **+3.20** | 28.28 | 33.70 | **+5.42** |
| Spyglass (mountain) | **16.73** over | **16.95** over | +0.22 | 26.02 | 25.20 | −0.82 |
| Caldera (volcano) | **16.15** over | **16.21** over | +0.06 | 33.53 | 33.57 | +0.04 |
| Balloon (savanna) | 14.55 | 15.90 | +1.35 | 27.16 | 29.48 | +2.32 |
| Diadem (dream) | 14.71 | 15.83 | +1.12 | 25.25 | 25.03 | −0.22 |
| SafetyNet (big top) | 15.33 | 14.42 | −0.91 | 20.22 | 19.43 | −0.79 |
| Ghost (neon city) | 13.97 | 13.90 | −0.07 | 19.87 | 20.04 | +0.17 |
| Pendulum (desert) | 12.80 | 12.51 | −0.29 | 19.82 | 19.85 | +0.03 |
| Girandole (city) | 12.44 | 12.56 | +0.12 | 21.07 | 20.66 | −0.41 |
| Tesseract (Grid) | 10.17 | 11.46 | +1.29 | **16.05** | 17.87 | +1.82 |
| Colossus (Moon) | 12.00 | 11.12 | −0.88 | 22.31 | 20.35 | −1.96 |
| Orrery (space) | 11.16 | 10.24 | −0.92 | **14.67** | **13.08** | −1.59 |
| Turbine (city) | 10.77 | 10.13 | −0.64 | 19.95 | 18.86 | −1.09 |

- **`Low` misses the 16.1 ms budget in five chapters, three of them since v0.5.0**: the aurora, the cavern and the meadow (the first chapter) were inside it on v0.4.0. **`Medium` fits only space** (the Grid fitted on v0.4.0). `High` is not in the table; on 2026-09-23 it missed the panel by 1.2–3.7×.
- **The steps and the merges between the two builds that touched the scene:** the aurora's is **#963** (its rebuild, the one such merge): the far stand, the collars and both terrain grids drawn whole at every tier, and on `Medium` the Moon's shadow map (2048). The cavern's span holds **#958** (the hall rebuilt on the CPU), #1017 and #984; on the desktop that hall is cheaper on every tier ("Cost" in `docs/scenes/cavern.md`), on the APU only on `Medium`. The meadow's holds a dozen: the brook (#892 twice, #924, #970, #979), the lawn (#944), the bales (#943, #971), the bench (#891, #1022) and the trees' fork (#889: `Low` and `Medium` draw the coarse wood, +14 to +18 % triangles). The Grid's own is the light cycles (#956). The savanna's holds the acacia leaves (#964), the wood shut at every joint (#938) and the campfires' mounds and light (#887, #885). **Moon, space, the big top and Turbine got 0.6–2.0 ms cheaper** at both tiers.
- **Since #1028 the meadow's and the savanna's `Low` rows are smaller**: beside v0.4.0 in one session, Amphora 15.18 → 18.04 on v0.5.0 → **16.22** with the plant programs, Balloon 15.01 → 16.35 → **14.81**. Of the meadow's step that is left, +0.97 is the brook's cut (#892); "On the reference APU at `Low`" in `docs/scenes/meadow.md` has the rest. **Then `Low` gave the grass's relief and the banks' texture up** (#1028, the owner's ruling): Amphora −0.71 ms and Diabolo −0.76 beside the build before, which brings Amphora inside the budget.
- **Since #1027 the aurora is back inside `Low`'s budget**: its far stand is drawn only where the lens looks, and beside the build before it Umbrella reads 17.78 → **14.39 ms** at `Low` (v0.4.0's 14.27 in the table) and 27.69 → 24.64 at `Medium`, Fish 18.07 → 14.71 and 28.00 → 24.63, Pendulum unchanged. The layers' costs are in "Costs" in `docs/scenes/aurora.md`.
- **Since #1029 the cavern is back inside `Low`'s budget**: `RockReduced` keeps three of the relief's five octaves, and beside the build before it Spring reads 16.80 → **15.31 ms** at `Low` (v0.4.0's 15.74 in the table) and Chest 15.18 → 13.67, Pendulum unchanged. "Cost" in `docs/scenes/cavern.md`.
- **#748's land put the volcano over `Low`'s budget again** (Caldera +0.61, Paroxysm +1.01, both in the terrain's vertex program), and **#1032 culled its grid to the lens**: Caldera 16.34 → **16.00 ms** at `Low`, on the line rather than over it. "Tiers and costs" in `docs/scenes/volcano.md`.
- **Since #1037 the volcano's `Medium` is under v0.5.0's**: #748 had cost it 5.9 ms, and `VolcanoMedium` keeps the authored pixel program over `Low`'s grid and vertex program. Caldera 38.00 → **31.93 ms** and Paroxysm 36.00 → 29.95 beside the build before, Colossus unchanged. "Tiers and costs" in `docs/scenes/volcano.md`.
- **Since #1038 the volcano has a margin at `Low`**: the flank's program skips a flow a pixel is past the reach of (every tier, no pixel changes; Caldera 15.67 → **14.99 ms**, Paroxysm 14.71 → 13.98, and at `Medium` 31.94 → 29.81 and 29.95 → 27.80), and `Low` draws half the eruption column (Caldera −0.16). "Tiers and costs" in `docs/scenes/volcano.md`.
- **Since #1035 the mountain is inside `Low`'s budget too**: every `TerrainPass` grid is drawn only where the lens looks, and `MountainReduced` takes its rock relief at one octave. Spyglass 17.32 → **15.44 ms** and Belfry 18.80 → 16.85 beside the build before, on an afternoon the whole machine read slow (Colossus 13.64 against 13.12 that morning). "Low's range on the reference APU" in `docs/scenes/mountains.md`.
- **The cities' `Low` rows did not move with #1066's tower plans or #1065's window reflections** (2026-11-06, the build before the plans, the plans with their review's fixes and the reflections with theirs, `-ByLevel`): Girandole 12.36 → 12.24 → 12.23 ms, Turbine 10.94 → 10.86 → 10.88. Ghost's runs spread 2-3.5 ms, and its minimum of four runs a build read 14.34, 13.91 and 14.06. "Tower plans (#1066)" in `docs/scenes/city.md`; the reflections' own cost at `High` is in "Window reflections" in `docs/rendering/city.md`.
- ⚠ **Balloon now and then reads about 3 ms cheaper for one whole run** (14.05 and 13.24 ms against 16.3-16.4, on both builds of one comparison), so a minimum of two runs can land on it: re-run before believing a savanna step.
- **Each build's two runs agree to 0.3 ms or better on eight rows of fifteen at each tier**; on the rest they spread 0.6–2.4 ms (the dream at `Low` 1.65, Girandole at `Medium` 2.38), so a step under about a millisecond there is inside the noise: the dream's +1.12 at `Low` is. The cavern's, meadow's, mountain's, volcano's, savanna's and dream's `Medium` pairs were measured in one session and the rest after the machine restarted, on AC both times.
- **The day's machine is not the code**: two DisplayLink monitors were attached, another session's JVM held four to nine cores for stretches (the frame barely moved with it: the APU is bound by its GPU here), and a pass-ordered sweep the same morning read the cheap levels up to 40 % lower in its first 25 minutes than the interleaved one did after (Pendulum on v0.5.0, 7.64 against 12.51 ms at `Low`). So a figure above is that afternoon's floor; **the steps are the comparison**. `colourcue=none` (the pre-#737 gores) is not the cheaper path on the APU: +2.5 ms on Girandole, +0.0 on Pendulum (2026-10-17).
- ⚠ **`Low` keeps the far plane #551 asked for (the owner's ruling, 2026-09-29), so the mountain runs over** (0.6–0.9 ms in the table). With only `DEFAULT_FAR_PLANE_DISTANCE` back at 500, Spyglass read 15.68 ms on 2026-09-29. **Do not shorten it to win the time back.**
- **Front end at `Low`** (2026-09-29, 55 s per scene, orbit spreads of 4–6 ms): five of twenty scenes miss 16.1 ms (Mars 17.88, savanna 17.57, tropical 16.83, outback 16.82, polar 16.18); **left by the owner's ruling: the menu is not worked on for `Low`.**

## The mid desktop: GTX 1060 6 GB, Ryzen 5 3600 (#1031)

The owner's spare desktop, between the APU and the 6900 XT, and the first NVIDIA in the loop: Ryzen 5 3600 (6 cores, 12 threads), GeForce GTX 1060 6 GB on driver 582.78 (32.0.15.8278), 32 GB DDR4 (a 3600 kit; the tables at 2667 MT/s, XMP off), a 1920x1080 60 Hz 8-bit panel, Windows 11 Pro 26300. The same levels and command as the APU's table (`tier-matrix.ps1`, Release, 45 s a run, the first six readings dropped, `nocap`, `userdata=` a scratch profile), at the panel's 1920x1080 as well as 1600x900; two passes, the second in reverse order, and the minimum of each pair. Builds' sets: libraries `2c25645c`, shaders `5d545040` (`main` at 09427ce0), 2026-11-14. The budget is the 60 Hz panel's, **16.7 ms**. ms:

| level (scene) | `Low` 900p | `Medium` 900p | `High` 900p | `Low` 1080p | `Medium` 1080p | `High` 1080p |
|---|---|---|---|---|---|---|
| Spyglass (mountain) | 4.69 | 7.12 | **17.30** over | 6.49 | 9.60 | **23.13** over |
| Orrery (space) | 5.05 | 5.40 | 16.36 | 5.93 | 6.18 | **19.13** over |
| Umbrella (aurora) | 4.52 | 6.70 | 13.66 | 5.89 | 8.85 | **18.97** over |
| Amphora (meadow) | 4.42 | 7.25 | 12.41 | 6.12 | 10.01 | **17.54** over |
| Caldera (volcano) | 4.26 | 8.60 | 12.64 | 5.66 | 11.21 | 16.03 |
| Girandole (city) | 3.34 | 5.99 | 12.51 | 4.87 | 7.79 | 15.67 |
| Balloon (savanna) | 3.99 | 6.66 | 10.18 | 6.02 | 9.18 | 14.78 |
| Turbine (city) | 3.00 | 5.35 | 11.48 | 4.41 | 7.04 | 14.32 |
| Spring (cavern) | 4.29 | 6.56 | 9.20 | 5.73 | 9.07 | 13.85 |
| Ghost (neon city) | 3.98 | 5.06 | 9.07 | 5.65 | 6.24 | 13.60 |
| Diadem (dream) | 5.08 | 8.66 | 9.74 | 6.66 | 11.99 | 13.59 |
| Colossus (Moon) | 3.84 | 5.28 | 7.79 | 5.39 | 7.34 | 10.24 |
| SafetyNet (big top) | 3.78 | 5.08 | 6.21 | 5.27 | 7.19 | 9.09 |
| Pendulum (desert) | 3.78 | 5.16 | 5.99 | 5.70 | 7.45 | 8.79 |
| Tesseract (Grid) | 2.50 | 3.26 | 5.76 | 3.45 | 4.40 | 8.34 |

- **`Medium` is this machine's tier at its own panel**: every chapter fits with at least 4.7 ms to spare (Diadem 11.99 is the dearest), and `High` misses in four chapters at 1080p (one at 900p). `Low` takes at most 40 % of the budget; the APU's `Low` (the table above, with its later rows) is 2–3.4× this card's at 900p.
- **The two passes agree to 0.1 ms on most rows**; the aurora at `High` 900p spread 1.1 ms, Caldera at `High` 1080p 1.1, Ghost at `Medium` 1080p 1.0.
- ⚠ **Space and the mountain are out of line at `High`**: Orrery reads 3.1× its `Medium` at 1080p and Spyglass 2.4×, where the other chapters read 1.1–2.2×. On the 6900 XT the swarm holds 88 728 balls in space at `High`; here the space scene alone is past the budget at 1080p ("The swarm" in `benchmark.md`).
- **What `High` costs here, attributed** (#1132; the Game at 1080p, two alternated passes, against `High` at `ssaa=1`): space's `High` was all supersampling of its one full-screen pass (Orrery `Medium` 6.20, `High` at `ssaa=1` 6.39, `High` 19.14), and since #1132 the pass shades all but its stars at the display's size: Orrery at `High` **8.51** (Ultra 8.45). The mountain's is two things: Spyglass `Medium` 9.58, `High` at `ssaa=1` 17.22, `High` 23.10, and in the Testbed (game-mode camera, `nopost`, one process) at `ssaa=1` `finemeshes` costs **+7.2 ms** (15.71 against 8.53): the fine range +4.9 (`weather=clear`, 13.34 against 8.45) and the cloud heaps +2.4. The range's cost is vertex work and hardly grows with the pixels (+0.94 over the coarse range at `ssaa=2`), where on the 6900 XT the same lever saves 3.3 to 4.7 ms. The shadow map at 4096 against 2048 costs nothing measurable (15.71 against 15.64), MSAA 8 against 4 at `ssaa=1` 0.4–0.5 ms. The aurora (11.12 → 18.92) and the meadow (12.43 → 17.48) pay `High` mostly in supersampling.
- ⚠ **Task Manager open beside the game costs 3–4 ms a frame on this card**, with the same clocks, power draw and 100 % utilisation: Pendulum at `High` 10.05 against 6.0 ms (1600x900), Orrery 20–23 against 16.2. Close it before measuring; the table above re-ran clean to 0.06 ms.
- **The adaptive probe lands where the table says**, with the window in focus and nothing pinned: Spyglass at 1080p read 47 FPS at `High` against its floor of 54 and stepped to `Medium`, then held 60. Amphora at `High` (17.5 ms, 57 FPS) is above the floor and is kept there, a frame short of the refresh.
- **The benchmark agrees** (Extras > Benchmark, full screen 1080p at `High`): it recommends `Medium` (Neon City 91.9 % and Forest 78.9 % of their frames over the budget). Its physics: Girandole's step 1.21 ms median on 12 threads with the memory at 2667 MT/s and 1.12–1.14 at 3200 (the tours did not move), 13.4–14.5 % of every frame, under the 5900X's 1.29 on 24 threads: the CPU is nowhere near the limit here.

## The phone: the vivo X300 Ultra through the desktop path (#1061)

Adreno 840, 3168×1440, the display held at 60 Hz by the phone (the game's limit 62, so 60 in the table is the display's and not the tier's; at 120 Hz see below), `desktop` with `quality=` and `render=`, the median of a run's last twelve `[fps]` lines. **Every run starts on a cool phone** (thermal status 0, battery under 36 °C). A warm one throttles and lies: a pass run back to back read the meadow at `Low` native at 31 FPS where a cool start reads 48.6.

| FPS (cool start, 2026-11-05) | City, Girandole native | City at 1080 rows | Meadow, Cannonball native | Meadow at 1080 rows |
|---|---|---|---|---|
| `Low` | 47.6 | 60.2 | 48.6 | 60.2 |
| `Medium` (shadow 2048, motion blur, the ceiling's refraction, full detail) | 37.5 | 52.1 | 33.3 | 47.2 |
| `High` at `ssaa=1` (shadow 4096, fine meshes) | 36.7 | 54.2 | 30.9 | 44.3 |
| `High` as it stands (`ssaa` 2) | | | neon city, Cabinet: 10.1 | 16.6 |

- **Native is fill-bound**: only `Low` at 1080 rows holds the display's 60 in both scenes. 720 rows reads the same 60 at `Low`.
- **`High`'s supersampling is out of reach** (four times the pixels), so a phone's `High` is `ssaa` 1, and Ultra (which adds a doubled shadow map to it) is not a phone tier.
- **The heat decides more than the first minute does.** Charging over USB, from a cool start at 1080 rows, the city:
  - `Low` held 60 for nine minutes and began to slip in the tenth (59.0, its low 47.3). It reached thermal status 2 by minute 7, the battery going from 35.7 to 42.3 °C.
  - `Medium` reached status 2 by minute 5 and throttled from 53 to 37.7 FPS by minute 7, the battery going from 35.9 to 42.7 °C. About 25 minutes of runs at native resolution took the phone to status 3 (skin 45.5 °C), and it needed 15 minutes idle to return to status 0. The Potato path held status 0 and 60 FPS for ten minutes in Girandole (#1048, battery 32.5 to 33.4 °C).

### The phone at the display's 120 Hz (#1070)

With BS3D allowed a high refresh rate in the phone's settings ("GameAndroid" in `docs/build-and-release.md`) the display stays at 120 Hz and the limiter reads `limit 124 (refresh)`. Measured 2026-11-09 as above, each run 36 s from thermal status 0 with the battery at 35.3-38.4 °C (it idles at 36.3 on USB), a scratch profile (`userdata=`), the levels as they load. `Medium` and `High` draw the ceiling's refraction and the motion blur, which #1063's fix brought back to the phone the same day (without them `Medium` had read 72.8 and 60.9, `High` 77.9 in the city: about 2 ms a frame).

| FPS at 120 Hz | City, Girandole | Meadow, Cannonball |
|---|---|---|
| Potato, native 3168×1440 | 120.4 | 120.4 |
| `Low` at 720 rows (`render=720`) | 120.3 | |
| `Low` at 960 rows (a phone's default) | 87.0 and 86.7 | 84.8 |
| `Medium` at 960 rows | 62.6 | 55.3 |
| `High` at 960 rows | 62.0 | 48.5 |

- **The display's 120 is held by Potato at native and by `Low` at 720 rows**; a frame's elapsed there spreads 0.4-0.6 ms (`[pace]` sd).
- **Between 60 and 120 the frames are uneven**: the display shows each for one refresh or two, so `Low` at 960 rows reads 87 FPS with a spread of 1.2-1.7 ms (9-27 ms a frame), where a frame held at the display's 60 Hz spreads about 0.8 (the menu at `Medium`, 60.2 FPS).
- **Two runs of Girandole at `Low` seven runs apart agree** (87.0 and 86.7), so the warmth of the session did not move the rest.
- **The heat at 120 Hz** (the city at `Low` and 720 rows, charging over USB, from a battery at 35.1 °C): 120 FPS for eight minutes, thermal status 1 in minute 4 and 2 in minute 6, the battery at 42.7 °C by minute 8. **From minute 9 a flat 90 FPS** (89.8-90.6 to minute 12, the battery 43.3 °C, still status 2): a frame's spread stayed 0.6 ms, the game's limit 124 and the display's mode the 120 Hz one, which reads as the phone's own cap on a warm game rather than a frame that got dearer. Held at the display's 60 Hz the same scene reached status 2 a minute later and kept its 60 into the tenth minute (above).

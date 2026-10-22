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
- **Supersampling is most of the ladder**: `Medium` is worth 12–22 ms (2026-08-27) wherever the pass scales with shaded pixels. The cavern and dream are immune (#155: their backdrop is sized off the back buffer), so only reduced programs reach them (and, for the cavern, the coarse meshes: "Tiers" in `docs/scenes/cavern.md`).
- **MSAA.** Desktop 3840×1600, `ssaa` 1, 8× → none: mountain 1.12 ms, meadow 0.61, neon city 0.58; 8× → 4× free there. **On the APU it is not (#540)**: 8× → 4× −1.38 ms on the mountain, −0.65 in the neon city; 8× → 2× −5.03 and −2.85. Hence `Medium` 4, `Low` 2.
- **Below native** (refused): 0.85× saves 0.59–1.05 ms on the desktop.
- **Reduced programs**: mountain 0.18 ms at `ssaa` 1, 0.58 at 2; Mars +0.39/+0.85 authored against +0.22/+0.49 reduced, 19.15 → 16.74 at `Low` with a 256-vertex grid; meadow +0.36/+0.19 against +0.11/+0.06; savanna +0.03/+0.04. **The volcano** (#540): the terrain is 5.16 of a 14.87 ms frame and plume, jets, ash 0.04, 0.04, 0.13 (`SceneRenderer.VolcanoLayers`, the Testbed's `volcano=all/-plume`, `-jets/-glow`, `-ash`, `-terrain`); `VolcanoReduced` (octaves, relief normal, the flows' gradient noise, a 256-vertex grid): **Caldera 18.31 → 14.49, Paroxysm 17.35 → 13.47 at `Low`**.
- **The arena's stone cap** (#151) is 10–14 % of the frame; `SurfaceDetail` saves 0.336 ms at 1080p `ssaa` 4 on the desktop and **0.00–0.05 ms on the APU** (kept because it is free). **Ceiling refraction**: 1.2–1.6 ms per rung on the APU where the plate is in frame, ~0.1 ms on the desktop. **Motion blur**, `Medium` on the APU (2026-09-29, `mbflip=5`): +0.68 to +0.78 ms at rest, +0.86 to +0.91 sweeping.
- **Ball materials are not the lever** (against vinyl: ice +1.24 ms, glass bubble +1.07, the rest under 0.3 ms). **Cluster wind** (#95) is the one CPU entry (975 balls: 0.83 → 10.1 ms of physics a frame), hence `High`/`Ultra` only.
- **Finer meshes** (`High`/`Ultra`), on the desktop at 3840×1600, `ssaa` 2, the Testbed's `alt=finemeshes=0;finemeshes=1`. `Medium` and `Low` never draw them, so the APU pays nothing for the fine wood (the meadow's plugs, about +500 triangles a tree kind, it does pay: below).
  - The savanna acacias' grown fork (#758): **+0.06 ms** at `High` and at `Ultra` alike, at the play camera on Heart, 42 paired cycles. Detail: "The fork" in `docs/scenes/savanna-trees.md`.
  - The meadow trees' grown fork (#889): **about +0.05 ms**. The Testbed pairs read −0.03 to −0.06 ms at `High` and `Ultra` (play camera on Pennant, the intro's path shot, 44 paired cycles, the sign held in 59 to 91 % of them), which is an order bias of that size and not a saving. In the Game on Pennant, `High` goes 11.35 to 11.40 ms and `Ultra` 11.54 to 11.60, in both repetitions: that is the fine wood's cost. `Low` and `Medium` draw the coarse wood with its closed knees, +14 to +18 % triangles (about +500 a tree kind), not measured on the APU. Detail: "Two woods (#889)" in `docs/scenes/meadow.md`.
  - The savanna's thorn bushes (#883): **+0.03 ms** at `High` and **+0.06** at `Ultra` in the Game on Zebra (11.40 to 11.43 and 11.81 to 11.88 ms, three alternated runs), +0.007 ms in a Testbed frame of 6 bushes against the acacias' fork alone; `Medium` 4.95 to 4.96. Detail: "The bush" in `docs/scenes/savanna-trees.md`.
  - The spruces' boughs (#733): **+0.32 ms** at the play camera on Fish (the aurora) and +0.42 from inside its wood. In the Game on Fish (the aurora before its rebuild, #963), `High` went 9.49 to 9.72 ms and `Ultra` 9.50 to 9.72. The daytime forest costs +0.11 ms (`High`) and +0.12 (`Ultra`). The cost is triangles, so a lower resolution does not shrink it. The bark stem they grow out of (#960) added +0.02 to +0.03 ms on Fish and +0.04 in the daytime forest. The rebuilt aurora's whole frame on Fish is in "Costs" in `docs/scenes/aurora.md`. Detail: "The spruces on High and Ultra" in `docs/scenes/aurora.md`.
  - The Moon's and the volcano's rocks (#777): **+0.21 ms** at the Moon's play pin and +0.22 among its rocks (21 uncontended paired cycles each, the sign held in all), **+0.08** at the volcano's play vantage. The cost is triangles at the primitive rate, so it does not fall where few rocks are in view. Detail: "High and Ultra's rocks" in `docs/scenes/moon.md`.
  - The mountain range (#781): **−3.26 ms** at `High` and **−3.58** at `Ultra` at the play camera on Belfry, 41 paired cycles; in the Game on Belfry, `High` goes 14.73 to 11.31 ms and `Ultra` 14.67 to 11.32. Cheaper with more detail because its program cuts the island's hole in geometry instead of with `clip()`, so the depth test runs before the pixel shader. Detail: "High and Ultra's range" in `docs/scenes/mountains.md`.

## The reference APU against shipped levels

Each chapter's heaviest level that is not its block's first; `level=<name> quality=<tier> nocap logfps windowed width=1600 height=900 mute nofocuspause nofps sceneseed=0`; 45 s, first 8 readings dropped. **`High`**: one first pass (2026-09-23, an upper bound). **`Medium`/`Low`**: minimum of two runs (2026-09-29, `main` at `e0c9a833`). ms:

| level (scene) | `High` | `Medium` | `Low` |
|---|---|---|---|
| Caldera (volcano) | 58.8 | 31.04 | 14.88 |
| Spyglass (mountain) | 54.3 | 23.97 | **16.55** over |
| Diadem (dream) | 29.2 | 24.25 | 15.04 |
| Spring (cavern, before #958) | 29.1 | 22.83 | 14.45 |
| Balloon (savanna) | 36.2 | 24.81 | 14.40 |
| Amphora (meadow) | 42.5 | 22.88 | 13.67 |
| Ghost (neon city) | 40.6 | 18.96 | 13.49 |
| Pendulum (desert) | 35.2 | 19.82 | 13.12 |
| Girandole (city) | 43.7 | 19.55 | 11.99 |
| Colossus (Moon), hand-drawn ⚠ | 22.5 | 17.42 | 11.79 |
| Turbine (city) | 40.4 | 17.36 | 10.95 |
| Orrery (space) | 35.8 | **12.49** | 10.75 |
| Tesseract (Grid) | 19.1 | **14.77** | 9.57 |

- **The aurora has no row**: its scene was rebuilt (#963) and has not been measured on the APU since. **Spring's row is the cavern before its rebuild (#958)**, here and in the re-measure below: the hall is rebuilt and has not been measured on the APU since (its desktop cost is in "Cost" in `docs/scenes/cavern.md`).
- **The Moon's row is the hand-drawn Colossus**, a solid 364-ball block. #849's 337-ball figure read **13.83 ms at `Low` and 21.23 at `Medium`** on 2026-10-17 (`main` at 188dda1b, the same conditions, `Low` the minimum of two runs), on a day the whole table read dearer (next bullet), so it sits with the city levels rather than under them.
- **`High` misses the panel by 1.2–3.7×; `Medium` fits only space and the Grid; `Low` fits everywhere but the mountain.** `Medium` is 8–43 % and `Low` 1–18 % dearer than #540's build (2026-09-29): refraction, motion blur, full-detail scene work.
- **Re-measured on 2026-10-17** (`main` at 188dda1b, the same conditions, `Low` the minimum of two runs): every `Low` row reads **4–20 % dearer than the table** (Caldera 16.31, Spyglass 17.78, Diadem 15.71, Spring 15.46, Balloon 14.97, Amphora 15.03, Ghost 14.71, Pendulum 14.61, Girandole 12.41, Turbine 11.40, Orrery 12.13, Tesseract 10.05, Anchor 13.57), **and the table's own build (e0c9a833), measured beside `main` that day, run for run, reads the same as `main`** (Pendulum 14.62 against 14.72, Girandole 12.10 against 12.41, Caldera 16.55 against 17.78 inside the volcano's ±1 ms run-to-run spread): the gap is the day's machine, not the code (trap 14 in `.claude/skills/benchmark`), so a figure above is that afternoon's floor, not a promise. On such a day the volcano misses 16.1 ms at `Low` on both builds. `colourcue=none` (the pre-#737 gores) is not the cheaper path on the APU: +2.5 ms on Girandole, +0.0 on Pendulum. `Medium`, one pass: 14.07–33.38 ms, 1–27 % over the table.
- ⚠ **`Low` keeps the far plane #551 asked for (the owner's ruling, 2026-09-29), so the mountain runs about half a millisecond over.** With only `DEFAULT_FAR_PLANE_DISTANCE` back at 500, Spyglass reads 15.68 and the hand-drawn Colossus 11.12. **Do not shorten it to win the time back.**
- **Front end at `Low`** (2026-09-29, 55 s per scene, orbit spreads of 4–6 ms): five of twenty scenes miss 16.1 ms (Mars 17.88, savanna 17.57, tropical 16.83, outback 16.82, polar 16.18); **left by the owner's ruling: the menu is not worked on for `Low`.**

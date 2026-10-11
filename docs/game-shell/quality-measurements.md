# What the frame costs: the quality tier's measurements

Part of [The game: shell](../game-shell.md).

The figures a future tier change compares against (#64: measured, because intuition ranked the candidates wrongly). The **reference APU** is the ThinkPad (Ryzen 7 5700U, integrated Radeon, on AC) the ladder exists for; its budget is **16.1 ms** (`limit 62 (refresh)`). The **reference desktop** has an RX 6900 XT. Tier contents: [The quality tier](quality-tier.md).

## Measuring without lying

`logfps` prints a line a second with every setting that changes its meaning; `scene=`/`sky=` pin the backdrop (**the scene is random per launch**); `.claude/skills/benchmark` drives it (`tier-matrix.ps1`).

- **Attribution does not travel**: the same cuts price 0.2–1.0 ms on the desktop and 3–7 ms on the APU; price a lever on the weak machine.
- **The minimum over repeated runs is the frame's cost** (2026-09-23: a first pass lied by up to 25 % while background processes shared the APU's package). **Compare builds alternated in one run, never two afternoons**; inside one process use `alt=` or `mbflip=`.
- **Ways to measure nothing**: an unfocused window (the probe and Myra read a click only while `IsActive`, so the benchmark clicks the title bar); an A/B without `quality=`/`ssaa=`; the front end's orbit (a fixed window samples the expensive arc); a **stray process** from a dead harness (two instances gave 6 FPS and a scripted 100 ms click was never seen, Myra reading input inside `Draw`); **scripted input is not repeatable** (25, 28, 26 balls from one build), so prefer a categorical outcome.

## What the scene and each lever cost

- **The scene is the order of magnitude.** APU front end, 1600×900, FPS at `High`/`Medium`/`Low`: neon city 36.4 → 95.6 → 103.7, city 43.4 → 108.0 → 119.2, sea 68.5 → 125 → 125. The cavern at `level=Chest` (nocap, #250) is 26.5 ms at `High`, 24.3 at `Medium`. The city's cost was overdraw, which the sort took ("Drawing the city near to far" in `docs/rendering.md`).
- **Supersampling is most of the ladder**: `Medium` is worth 12–22 ms (2026-08-27) wherever the pass scales with shaded pixels. The cavern and dream are immune (#155: their backdrop is sized off the back buffer), so only reduced programs reach them.
- **MSAA.** Desktop 3840×1600, `ssaa` 1, 8× → none: mountain 1.12 ms, meadow 0.61, neon city 0.58, cavern 0.30; 8× → 4× free there. **On the APU it is not (#540)**: 8× → 4× −1.38 ms on the mountain, −0.65 in the neon city; 8× → 2× −5.03 and −2.85. Hence `Medium` 4, `Low` 2.
- **Below native** (refused): 0.85× saves 0.59–1.05 ms on the desktop and would be the only lever that moves the cavern.
- **Reduced programs**: cavern 4.08 → 3.07 ms (desktop); mountain 0.18 ms at `ssaa` 1, 0.58 at 2; Mars +0.39/+0.85 authored against +0.22/+0.49 reduced, 19.15 → 16.74 at `Low` with a 256-vertex grid; meadow +0.36/+0.19 against +0.11/+0.06; savanna +0.03/+0.04. **The volcano** (#540): the terrain is 5.16 of a 14.87 ms frame and plume, jets, ash 0.04, 0.04, 0.13 (`SceneRenderer.VolcanoLayers`, the Testbed's `volcano=all/-plume`, `-jets/-glow`, `-ash`, `-terrain`); `VolcanoReduced` (octaves, relief normal, the flows' gradient noise, a 256-vertex grid): **Caldera 18.31 → 14.49, Paroxysm 17.35 → 13.47 at `Low`**.
- **The arena's stone cap** (#151) is 10–14 % of the frame; `SurfaceDetail` saves 0.336 ms at 1080p `ssaa` 4 on the desktop and **0.00–0.05 ms on the APU** (kept because it is free). **Ceiling refraction**: 1.2–1.6 ms per rung on the APU where the plate is in frame, ~0.1 ms on the desktop. **Motion blur**, `Medium` on the APU (2026-09-29, `mbflip=5`): +0.68 to +0.78 ms at rest, +0.86 to +0.91 sweeping.
- **Ball materials are not the lever** (against vinyl: ice +1.24 ms, glass bubble +1.07, the rest under 0.3 ms). **Cluster wind** (#95) is the one CPU entry (975 balls: 0.83 → 10.1 ms of physics a frame), hence `High`/`Ultra` only.

## The reference APU against shipped levels

Each chapter's heaviest level that is not its block's first; `level=<name> quality=<tier> nocap logfps windowed width=1600 height=900 mute nofocuspause nofps sceneseed=0`; 45 s, first 8 readings dropped. **`High`**: one first pass (2026-09-23, an upper bound). **`Medium`/`Low`**: minimum of two runs (2026-09-29, `main` at `e0c9a833`). ms:

| level (scene) | `High` | `Medium` | `Low` |
|---|---|---|---|
| Caldera (volcano) | 58.8 | 31.04 | 14.88 |
| Spyglass (mountain) | 54.3 | 23.97 | **16.55** over |
| Diadem (dream) | 29.2 | 24.25 | 15.04 |
| Spring (cavern) | 29.1 | 22.83 | 14.45 |
| Balloon (savanna) | 36.2 | 24.81 | 14.40 |
| Amphora (meadow) | 42.5 | 22.88 | 13.67 |
| Ghost (neon city) | 40.6 | 18.96 | 13.49 |
| Pendulum (desert) | 35.2 | 19.82 | 13.12 |
| Girandole (city) | 43.7 | 19.55 | 11.99 |
| Colossus (Moon) | 22.5 | 17.42 | 11.79 |
| Turbine (city) | 40.4 | 17.36 | 10.95 |
| Orrery (space) | 35.8 | **12.49** | 10.75 |
| Tesseract (Grid) | 19.1 | **14.77** | 9.57 |
| Anchor (aurora) | n/a | 16.60 | 12.04 |

- **`High` misses the panel by 1.2–3.7×; `Medium` fits only space and the Grid; `Low` fits everywhere but the mountain.** `Medium` is 8–43 % and `Low` 1–18 % dearer than #540's build (2026-09-29): refraction, motion blur, full-detail scene work.
- ⚠ **`Low` keeps the far plane #551 asked for (the owner's ruling, 2026-09-29), so the mountain runs about half a millisecond over.** With only `DEFAULT_FAR_PLANE_DISTANCE` back at 500, Spyglass reads 15.68 and Colossus 11.12. **Do not shorten it to win the time back.**
- **Front end at `Low`** (2026-09-29, 55 s per scene, orbit spreads of 4–6 ms): five of twenty scenes miss 16.1 ms (Mars 17.88, savanna 17.57, tropical 16.83, outback 16.82, polar 16.18); **left by the owner's ruling: the menu is not worked on for `Low`.**

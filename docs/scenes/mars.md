# Mars

Part of [The scenes](../scenes.md).

The sixteenth `SceneKind` (#277): rust-red cratered ground scattered with dark stone, under a dusty butterscotch sky with two small moons. Made of `MarsBackdrop`, `Mars.fx` and `MarsSceneConfig` (groups Terrain, Surface, Air, Moons). Launch: `scene=mars` (the front end: `scene=mars preview=One`).

## Family and reuse

- **Solid terrain, not a sky replacer.** Mars keeps a thin atmosphere (unlike the Moon, see "The Moon (Game)" in `docs/scenes.md`), so it is a solid-terrain scene under a real dome: island footprint cut out, pit shaft behind the drain's glass, no dive. `Weather` is `Clear`.
- **Not a red Moon.** The crater field (`CraterLayer`, `TurnCrater`, `CraterField`, `MareBase`) is lifted verbatim from the Moon, being generic height-field math; the Moon's highland belt and curvature are not, since they only close a horizon with no air. Mars closes it with a two-stage haze fade to `HorizonColor`, the one the outback had until #967.
- **Dome 19** (`MARS_DOME`) is data, generated from five colour stops by vertex height: **brighter near the horizon, dimmer at the zenith** (dust).

## Stones, moons, materials

- **The stone field** is the outback's `RockLayer` lattice ported a second time: boulders (`RockSpacing`/`RockChance`/`RockHeight`) and a finer, rotated pebble tier (`PebbleSpacing`), `ribDepth` 0 and low `maxElongation` (wind-worn, no gullies). The stones are **dark basalt** (`BoulderColorDeep`/`BoulderColorBright`), not darker rust: rock is the one thing on the plain that is not red, and that contrast is what makes litter read as rock. ⚠ A 3-6 unit boulder spans one or two grid cells, so its silhouette is softer up close than its shading; invisible at play distances, visible on any framing made to inspect a stone.
- **`MarsMoons`, not the Moon's Earth.** `MoonSky` replaces the whole sky; Mars keeps its dome, so `MarsMoons` shares the full-screen quad and `ViewRayBasis` (`SkyRay.Basis`), is **depth-read** against what `MarsTerrain` wrote and **alpha-blended**. Each moon is a `MoonBody` (#744): the Earth's ray-sphere test run on a ray scaled into an ellipsoid, the long axis on the line of sight as both moons are tidally locked to Mars, so the outline is a tilted potato (`PHOBOS_AXES`, `DEIMOS_AXES`); Stickney, three octaves of small craters and slow lumps tilt the normal analytically (no gradient op under the `[branch]`). Lit by Lommel-Seeliger mixed with Lambert, so the sunlit side stays bright to its limb, and by Marsshine (`MarsshineColor`) on the side the camera sees. Composited through the air: `Transmission` of the moon's own light, and `Airlight` of the dome still in front of it (premultiplied `BlendState.AlphaBlend`). ⚠ Drawn without the air, a dark moon on a daytime dome is a black hole cut in it, and was: the first Phobos had a reflectance of 0.095, its shadowed half near 0.005, far under the dome behind it. Neither the size nor the reflectance is true (true ones are sub-pixel and near-black): see `MarsMoonsConfig`.
- **Mesas** (`MesaField`; `MesaHeight`, `MesaInnerRadius`, `MesaThreshold`): thresholded low-frequency noise gives flat tops with cliffs; strata (`StrataColorPale`/`StrataColorDark`, `StrataFrequency`) band by world height. `MesaHeight` is low: tall mesas read as Australia.
- **Dark sand drifts** (`SandColor`, `SandCoverage`) carry ripples square to the wind (without them a drift reads as a cloud shadow); **pale slabs** (`SlabColor`, `SlabCoverage`) are cracked along noise zero-crossings. ⚠ Coarse terms must be gradient noise: hashes held constant over a square step at every edge (the "tiles").

## ⚠ The haze decided what the mesas looked like

`lerp(color, HorizonColor, haze^4)` with `haze = dist / HorizonHazeDistance` fades terrain to the dome's raw pinkish horizon, not rust. At 460 the mesa ring (300-440 units out) was up to 84 % dome colour, so no retuning of strata could show. `HorizonHazeDistance` is 700 (`MarsAirConfig`).

- **Brightening albedo was the wrong lever** (`BoulderColorBright`, `RockRelief`, `SandColor`): every small crater and pebble became a pale dot.
- **Height-blending `relief` and the coarse grain** did not cure the mesa cliffs' vertical streak, which is open (untested cause: the far-field ring's vertex spacing, `FarField.fxh`).

## Big craters

The field's biggest bowls were about 2.6 units deep, a dark ellipse from the ground, so big craters are their own lattices (`BigCraters`): a crater is a hole, not a texture.

- Three sparse single-cell lattices (turned so cells never line up), at most one crater a cell. **Depth and rim are shares of the crater's own radius** (`BIG_CRATER_DEPTH`, `BIG_CRATER_RIM`, with an ejecta blanket). None stands whose blanket reaches within `BIG_CRATER_CLEARING` of the arena (left out whole, never cut).
- **Dark inside**, which is what makes them read from a lens ten units up. The terrain casts nothing into the sun's map, so each crater computes closed-form light: the sky its walls hide (off the ambient) and whether the rim hides the sun (one quadratic). The nearest lattice is the densest, since a low lens sees into a bowl only within about ten times its height.
- **Evaluated once a pixel.** The profile is radial, so `BigCraterSample` evaluates all three lattices once and adds the closed-form gradient to the normal taps' slope. Summed into `MarsHeight` it cost +0.65 ms at High, +0.38 at Low.
- **CPU copy:** `OffworldGround.MarsBigCraters` (via `ShaderMath.Hash22`); `MarsCeiling` adds its positive part exactly, not at a bound (which would lift every intro lens by the biggest rims' height); the intro's mesa is `MarsMesa` against `MesaHeight`. There is **no `TerrainMirror` of `MarsHeight`** (nothing is planted), so Mars is not in `mirrorcheck`.

## Tiers and costs

- **`MarsTerrainReduced`**: `MarsTerrain(input, detail)` behind two techniques by `SceneDetail`, so the Game's `Low` takes it; it drops sand, slabs, the strata wobble and the coarse grain, keeps the mesas (geometry must light the same on every tier), and runs on a coarser grid (`MARS_GRID_N_REDUCED`, #540). Not taken: dropping the pebble lattice (-1.12 ms) emptied the plain, and a crater octave (-0.25 ms) lost the small craters.
- **Costs** (reference APU, Testbed, 1600x900 ssaa 2): Mars 31.2 ms against the outback's 31.7 (same pin, dome 13), the same band. The mesa ring +0.39 ms from the play level, +0.85 from above (`campos=0,25,20 camtarget=0,-13,120`). Big craters at the pixel: about +0.1 ms (Game front end, 1920x1080).

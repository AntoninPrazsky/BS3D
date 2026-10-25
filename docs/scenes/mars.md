# Mars

Part of [The scenes](../scenes.md).

The sixteenth `SceneKind` (#277): rust-red cratered ground scattered with dark stone, under a dusty butterscotch sky with two small moons. Made of `MarsBackdrop`, `Mars.fx` and `MarsSceneConfig` (groups Terrain, Surface, Air, Moons). Launch: `scene=mars` (the front end: `scene=mars preview=One`).

## Family and reuse

- **Solid terrain, not a sky replacer.** Mars keeps a thin atmosphere (unlike the Moon, see "The Moon (Game)" in `docs/scenes.md`), so it is a solid-terrain scene under a real dome: island footprint cut out, pit shaft behind the drain's glass, no dive. `Weather` is `Clear`.
- **Not a red Moon.** The crater field (`CraterLayer`, `TurnCrater`, `CraterField`, `MareBase`) is lifted verbatim from the Moon, being generic height-field math; the Moon's highland belt and curvature are not, since they only close a horizon with no air. Mars closes it with a two-stage haze fade to `HorizonColor`, the one the outback had until #967.
- **Dome 19** (`MARS_DOME`) is data, generated from five colour stops by vertex height: **brighter near the horizon, dimmer at the zenith** (dust).

## Stones, moons, materials

- **The stones are geometry** (#775, `MarsBackdrop`'s THE STONES): the volcano's broken block (`VolcanoPlanting.BlockMesh`, four shapes) planted as **dark basalt** in three sizes by a power law (`ROCKS_SMALL`/`MEDIUM`/`LARGE`), between `ROCKS_FROM` and the clearing's edge, where the plain is `LevelY` exactly (there is no CPU copy of the craters to stand them on further out), and `TRACK_CLEAR` off the rover's traverse. They cast into the sun's map. Rock is the one thing on the plain that is not red, and that contrast is what makes litter read as rock.
- **The height field's stone lattice** (the outback's `RockLayer` ported a second time: `RockSpacing`/`RockChance`/`RockHeight`, a rotated pebble tier `PebbleSpacing`, `ribDepth` 0) still shapes the ground, but **coloured as the plain's own dust**, not basalt. ⚠ As basalt, its stones were low dark mounds the grid gave no silhouette, and from any distance they read as dark ovals, holes in the ground: the owner's "do not look like the real photographs".
- **Gravel** (#775): a jittered dark point in a third of the 1.3-unit cells, sized by a power law, faded out by the footprint before it is a pixel and not evaluated past that (`[branch]` on the footprint, coherent across the frame). The full program's only.
- **`MarsMoons`, not the Moon's Earth.** `MoonSky` replaces the whole sky; Mars keeps its dome, so `MarsMoons` shares the full-screen quad and `ViewRayBasis` (`SkyRay.Basis`), is **depth-read** against what `MarsTerrain` wrote and **alpha-blended**. Each moon is a `MoonBody` (#744): the Earth's ray-sphere test run on a ray scaled into an ellipsoid, the long axis on the line of sight as both moons are tidally locked to Mars, so the outline is a tilted potato (`PHOBOS_AXES`, `DEIMOS_AXES`); Stickney, three octaves of small craters and slow lumps tilt the normal analytically (no gradient op under the `[branch]`). Lit by Lommel-Seeliger mixed with Lambert, so the sunlit side stays bright to its limb, and by Marsshine (`MarsshineColor`) on the side the camera sees. Composited through the air: `Transmission` of the moon's own light, and `Airlight` of the dome still in front of it (premultiplied `BlendState.AlphaBlend`). ⚠ Drawn without the air, a dark moon on a daytime dome is a black hole cut in it, and was: the first Phobos had a reflectance of 0.095, its shadowed half near 0.005, far under the dome behind it. Neither the size nor the reflectance is true (true ones are sub-pixel and near-black): see `MarsMoonsConfig`.
- **Mesas** (`MesaField`; `MesaHeight`, `MesaInnerRadius`, `MesaThreshold`): thresholded low-frequency noise gives flat tops with cliffs; strata (`StrataColorPale`/`StrataColorDark`, `StrataFrequency`) band by world height. `MesaHeight` is low: tall mesas read as Australia.
- **Dark sand drifts** (`SandColor`, `SandCoverage`) carry ripples square to the wind (without them a drift reads as a cloud shadow); **pale slabs** (`SlabColor`, `SlabCoverage`) are cracked along noise zero-crossings. ⚠ Coarse terms must be gradient noise: hashes held constant over a square step at every edge (the "tiles").
- **One muted tone across the plain** (#775, the references `775-zimage`, `775-klein`): the broad rust patches are gentle (`RustColor`/`RustColorPale` close together), a drift is only a little darker than the rust and soft-edged, and drifts and slabs thin out up a crater's wall (`level` from 0.78) instead of stopping on it. ⚠ Contrasty patches with hard edges were the owner's "blotches" and "torn" areas. A second octave in the drift field and a gravel layer evaluated at every distance cost about 0.3 ms together, and the soft single octave with the branched gravel looks the same.

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

## The rover's tracks (#745)

The owner: a rover's tracks as a small easter egg. The references (`745-klein`, `745-zimage`) run a twin chevron track across the plain to the dusty horizon, its ends lost under drifted dust. The Moon's mechanism ([The Moon](moon.md), #746: `RoverTracks.fxh`, `RoverTracks.cs`): `MarsBackdrop.TRAVERSE` comes in from the mesas, swings past the arena outside the clearing and runs off towards the far side's, pushed once at load. `MarsGroundAt` presses the ruts into the normal and darkens the ground the wheels turned over (`TRACK_DARKEN`, the darker ground under the dust skin every rover photograph shows), never over a rock or the island's bank. The full program's only: the reduced one keeps its budget. No level plays on Mars, so the tracks are for the menu's orbit, the chapter tour and the drop. Cost: none measurable (Testbed 3840x1600, ssaa 2, `campos=-60,-9,20 camtarget=-90,-13,-40`: 15.93 against 16.01).

## Tiers and costs

- **`MarsTerrainReduced`**: `MarsTerrain(input, detail)` behind two techniques by `SceneDetail`, so the Game's `Low` takes it; it drops sand, slabs, the strata wobble and the coarse grain, keeps the mesas (geometry must light the same on every tier), and runs on a coarser grid (`MARS_GRID_N_REDUCED`, #540). Not taken: dropping the pebble lattice (-1.12 ms) emptied the plain, and a crater octave (-0.25 ms) lost the small craters.
- **Costs** (reference APU, Testbed, 1600x900 ssaa 2): Mars 31.2 ms against the outback's 31.7 (same pin, dome 13), the same band. The mesa ring +0.39 ms from the play level, +0.85 from above (`campos=0,25,20 camtarget=0,-13,120`). Big craters at the pixel: about +0.1 ms (Game front end, 1920x1080). The stones and the gravel (#775, desktop Testbed 3840x1600 ssaa 2, paired): the planted stones about +0.04 ms; the whole change 15.89/15.93 against 15.86/15.93 ms from the play pose (`campos=0,-2,34 camtarget=0,-9,0`) and 15.51/15.52 against 15.37/15.41 from the ground (`campos=0,-9,60 camtarget=0,-11,200`).

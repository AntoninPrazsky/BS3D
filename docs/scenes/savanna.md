# The savanna

Part of [The scenes](../scenes.md).

Open golden grassland rolling gently to a wide horizon, with the island standing in a flat clearing. It grew out of the old Sahara dunes, which are now their own scene ([The desert](desert.md)). This page is the ground. What stands on it is in [The savanna's trees and plain](savanna-trees.md), the ring of campfires in [The savanna's campfires](savanna-fires.md), and the flock overhead in [The flock of birds](birds.md).

## What it is made of

- `SceneKind.Savanna`: `Savanna.fx`, drawn by `SavannaBackdrop`; next after the sea in the NumPad2 cycle, or `scene=savanna`. Drawn opaque, first in the scene block, `CullNone`, in every executable. Shader Model 5.0.
- A camera-centred grid (`SAVANNA_GRID_N` = 400 a side over `SAVANNA_EXTENT` = 1200, snapped to its cell so it does not swim), displaced by a smooth rolling field: flat in a clearing around the origin and rising into low rises with distance, flatter than the meadow's hills. `TerrainMirror.Savanna` is the CPU copy the scatter is placed by ("The terrain mirrors, in one copy and checked against the GPU" in `docs/scenes.md`). **Under each of the ring's eight fires the field is raised into a mound** (#887, `HearthMound` in the shader, `SavannaMound` in the mirror; the relief alone is `TerrainMirror.SavannaPlain`): see [The savanna's campfires](savanna-fires.md).
- It **enters under the set's warmest gold horizon**, the dome `SkyDome.SceneDefault(SceneKind.Savanna)` returns (dome 14), on `scene=savanna` or a NumPad2 switch into it, unless `sky=` overrode it. The ground is matte (sun plus the sky hemisphere, dimmed by the shared cloud shadow) and melts into the skyline over `HorizonHazeDistance`, so it takes the mood of every dome.
- The dials (terrain, grass, wind, relief, trails) are on `SavannaSceneConfig`, shared with the map editor.
- **Every piece of wood on the plain is shut, and everything with a flat underside rests on the ground** (#938): a free end is closed, a joint is one skin or a dome the next piece starts inside, a log lies along its slope. The rules, the check that holds them and the measurements are in [The wood is shut](savanna-trees.md#the-wood-is-shut-938) and [The dressing rests on the ground](savanna-plant-volumes-and-feet.md#the-dressing-rests-on-the-ground-938).
- ⚠ **The colours were greened deliberately** (a lusher field rather than all gold). The references' blond grass is not licence to undo that.

## The normal is per pixel

⚠ The normal is taken **per pixel** from the height field's own gradient (three cheap `TerrainHeight` taps in the pixel shader), not interpolated from per-vertex normals. Interpolating a coarse mesh's per-vertex normal is a normal discontinuity (a Mach band) at every cell edge, which showed a faint facet grid across the sand; that was what turned the Sahara dunes into the savanna. The meadow and mountains still take their normal per vertex because their per-pixel relief hides the seams. Reach for the per-pixel gradient if either ever shows the same grid.

## The grass material (#281)

The ground was three colour tones over a combed relief, matte, which is the recipe that reads as green stone. Every term of the material has its own `SavannaSceneConfig` dial:

- **Tips and hollows** off the relief that tilts the normal: pale straw tips (`GrassTipColor`, `GrassTipStrength`) where the combed field stands high.
- **Blades seen from the side**: a fine noise stretched along the ground towards the camera, which projects to upright strokes in front of the lens. This is what makes the near field read as fibre rather than a surface. They are the meadow's `GrassStrokes` (#1234): fixed to the ground, faded out as the view turns steep and band-limited across the grain ("The blades seen from the side" in [the meadow](meadow.md) has the construction and why). ⚠ **Never comb a noise about the world origin by the bearing to the lens**, `Fbm2Combed(xz * f, lens.xz - xz)`, which the savanna drew until #1234: every stroke is then a whole ray from the point under the lens, a fan fixed to the screen and re-rolled as the lens moves, and straight down a starburst round the middle of the frame.
  - **Wider than the meadow's**, because the savanna is seen from higher: `GRASS_STROKE_FREQUENCY` 20 to a relief feature, 40 a unit, a stroke a unit long. The intro's grass home runs 3 to 4.2 over the grass, so its near field begins about nine units out, and there strokes the meadow's width (96 a unit) are all but under the band limit: at 72 a unit the low shots lost most of theirs. At 40 the near field keeps the old strokes' width on the screen: the stroke layer alone (strokes on minus off) at the grass home's lens 10.2 s in (`level=11 seed=7 sceneseed=0`) has an across period of 9.8 px against the old 10.3 at 3840x1581 `ssaa` 2, 7.2 against 7.4 at 1080p. Farther out they fade, where the old fan, one width on the screen at every distance, ran on towards the horizon. `SavannaStrokesSourceTests` holds the call and that the grass home's nearest strokes keep at least half their strength.
  - **What they keep from one frame to the next** (Testbed, `scene=savanna sceneseed=0 nopost clockstill fov=42.86`, 1920x1080 `ssaa` 2; the stroke layer of two frames, the second warped onto the first by the two cameras' geometry over the plain): the grass home's own consecutive 75 Hz frames 10.2 s in, 0.24 units apart, keep 0.96 to 0.98 of the strokes' pattern over the ground from 9 to 20 units out and 0.92 out to 30; 11.0 s in, 0.31 apart, 0.96 to 0.90 from 13 to 30 units out. A lens 3.4 over the grass looking level moved 0.05 sideways keeps 0.97 to 0.98; one looking 35 degrees down from 3 over, 0.84 at 2 to 3 units out, 0.88 to 0.89 at 3 to 5, 0.94 to 0.98 beyond. The old strokes kept 0.0 to 0.2 in every case, and when the lens moved sideways 0.65 to 0.70 of their pattern stayed on the screen. Looking straight down from 4 the frame is the strokes-off frame but for faint strokes at its very edge (3 % of the pixels, 5/255 at the most): the fan measure (tangential over radial gradient energy round the middle of the frame) reads 0.84 with the strokes and without them, against the old strokes' 116.
- **Sheen** of a field seen edge-on (`GrassSheenStrength`) and **glow** looking into the sun (`GrassTranslucency`).
- **Bunches** (`TuftSize`, `TuftStrength`): each its own shade of dry gold or green, and no more. ⚠ Three literal layouts were tried and rejected: jittered discs with red earth round each read as polka dots; the same discs raised into domes in the relief read as pebbles (green stone again); the meadow's clump seams (dark lines along a noise's zero-crossings) drew a web of worm-like cracks under a high sun, which reads as dried mud. **What makes grass read as grass at a field's distance is fibre, not form.**
- Every term is switched off over the char round a hearth, which has no blades.
- **`SavannaReduced`**: the Game's `Low` tier gives up the bunches and the blade strokes and keeps the rest (`detail=0` in the Testbed shows it).

## The fine relief: never two crossed sines (#117)

⚠ `GrassRelief` (now shared with the meadow in `Grass.fxh`) was two crossed plane-wave sines, and two plane waves crossing **are** a lattice: at 93.4 degrees it was nearly square and read in perspective as a field of diamonds in the middle distance (periods 3.14 and 1.75 units at `GrassReliefFrequency` 2). It showed strongly because the field feeds `PerturbNormalFromHeight`: a pattern in the normal lands in the shading. This is the failure `Noise.fxh`'s opening documents ("a sum of plane-wave sines keeps its planes however many terms it has").

- It is three octaves of gradient noise through `Fbm2Combed`. Two things had to come with the swap. **The domain is stretched along the wind** (`GRASS_COMB_STRETCH`): one sine always dominated and supplied a grain for free, and isotropic noise has none, so a relief without one reads as gravel rather than grass lying over. **The amplitude had to be re-derived** (`GRASS_FBM_GAIN`): crossed sines have an RMS near 0.51 where three octaves of `Fbm2` sum to about 0.10, so keeping `GrassReliefStrength` unchanged left a fifth of the relief, which on a normal-tilting field is the effect gone.
- What stays per scene is the tuning (each scene declares its own `GRASS_COMB_STRETCH` and `GRASS_FBM_GAIN`); the function is one copy. The meadow had carried a line-for-line copy, lattice included.
- This is **not** the older grid artifact above (the coarse mesh's per-vertex normal, which stays fixed); it sat on top of that fix.

## Game trails

Bare red earth worn along the **zero contours of one low-frequency noise** (`TrailStrength`, `TrailWidth`, `TrailFrequency`; one `CloudNoise` tap behind a `[branch]` on the strength), which wander and fork like trodden paths. ⚠ **Never a sine**: it lays a trail in a row of identical bends.

- A trail takes the hearth's own route through the material (`worn = max(burn, trail)` turns the relief, tips, strokes and sheen off) but keeps its own colour: a hearth is ash and char, a trail is `GrassColorBare` made redder and lighter. Band-limited by hand against the footprint so it fades rather than shimmering along the horizon.
- `TrailWidth` 0.035 read as a road; 0.02 is a track.

## Shadows

Everything on the plain casts a shadow and receives one: the scatter and the hearth stones are drawn into one sun shadow map before the scene pass, and `Savanna.fx` and `Acacia.fx` read it. See "Sun shadows" in `docs/rendering.md` for the map, the bias, the call order and the cost (the dials live in `Shadows` on the base `SceneConfig`).

## Costs a change must compare against

Testbed, alternating pairs against `main`:

- **The grass material** (dome 13, 1600 x 900 at ssaa 2, `nocap`): +0.12 ms from the play camera (2.11 against 1.99), +0.20 ms at the near grass (`campos=10,-9,40 camtarget=30,-13,90`), +0.27 ms from above the field (`campos=0,25,60 camtarget=30,-14,140`). The reduced program adds +0.03 and +0.04 ms against a `main` that had none.
- **The three-octave relief** (front end, 1600 x 900, `quality=High`, dome 13, `nocap`): savanna 639.9 to 597.6 FPS (1.56 to 1.67 ms), meadow 672.0 to 625.2 (1.49 to 1.60): about +0.11 ms each, on a full-screen terrain shader.
- **The whole plain's contents** (dome 14, 1600 x 900 at ssaa 4, `nopost`, `fpscap=400`): +0.14 ms looking out from the island's edge (`campos=0,-5,-30 camtarget=0,-12,-200`), +0.29 ms from above, +0.34 ms at a fire with a canopy across the lens (`campos=52,-7,-34 camtarget=28,-12,-18`).
- **The blades seen from the side** (#1234: four anchors' eight noises where the strokes can show, two everywhere before). The Game at `Ultra`, 3840x1581, `level=11 seed=7 sceneseed=0 fpscap=1000`, the old build and this one alternated twice, the minimum of four runs: **the play view 7.21 to 7.02 ms** (the strokes reach only a thin band of its grass, 0.3 % of the frame changing by more than 1/255 with them, and the branch skips their noises everywhere else, where the old call worked its two out on every pixel), **the intro's dearest second 8.79 to 8.87** (the grass home's low run, 8.69 to 8.87 in its second second; the plain 0.6 cheaper). The Testbed (`nopost clockstill`, 3840x1581 `ssaa` 2, `alt=detail=1;detail=0`, two runs a build): the detail program over the reduced one +2.17 to +2.38 ms at the grass home's lens 10.2 s in, +0.88 to +0.76 at the play lens; the reduced program compiles to the same code in both builds (5.82 and 5.24 ms in both).
- **The campfires' mounds** (#887; 3840 x 1600 at ssaa 2, `nopost nooverc weather=clear`, `fpscap=1000`, ABBA): **+0.54 ms from the Game's play pose** (10.65 to 11.19), **+0.01 from above the ring**. The breakdown and what is not explained: "The mounds the fires stand on" in [The savanna's campfires](savanna-fires.md).
- ⚠ At ssaa 2 and 1600 x 900 both builds sat on the 400 cap (2.50 ms): a capped reading means "cheaper than this", never a cost. Use ssaa 4 or raise the load.

## Not built

"Something alive at distance" is still not built (named when the plain's content was reworked, #451).

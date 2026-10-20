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
- **Blades seen from the side**: a fine noise stretched along the ground towards the camera, which projects to upright strokes in front of the lens. This is what makes the near field read as fibre rather than a surface.
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
- **The campfires' mounds** (#887; 3840 x 1600 at ssaa 2, `nopost nooverc weather=clear`, `fpscap=1000`, ABBA): **+0.54 ms from the Game's play pose** (10.65 to 11.19), **+0.01 from above the ring**. The breakdown and what is not explained: "The mounds the fires stand on" in [The savanna's campfires](savanna-fires.md).
- ⚠ At ssaa 2 and 1600 x 900 both builds sat on the 400 cap (2.50 ms): a capped reading means "cheaper than this", never a cost. Use ssaa 4 or raise the load.

## Not built

"Something alive at distance" is still not built (named when the plain's content was reworked, #451).

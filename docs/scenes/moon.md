# The Moon

Part of [The scenes](../scenes.md).

## What it is

The twelfth `SceneKind` (#125): the island on a cratered regolith plain ringed by a highland belt, under a black starlit sky with the Earth at its real angular size. Nothing moves: `Moon.fx` takes no time uniform.

- Code: `Moon.fx`, `MoonBackdrop`, `MoonSceneConfig` (groups Terrain, Earth, Stars, Lighting; the shape is explained in [Space](space.md)), `MoonPlanting`, `TerrainMirror.Moon`. Grid `MOON_GRID_N` 360 over `MOON_EXTENT` 1200. Launch: `scene=moon`.
- It is in **both** families: `IsSolidTerrainScene` (footprint `clip()`ed out, dark pit shaft behind the drain's glass, no dive for the drop cinematic) and `ReplacesSky` (no dome, no cloud deck, black clear, its own light rig). No caller asks both questions of one scene ([registry](backdrop-architecture.md)).

## Draw order (⚠ measured)

Terrain first, then the sky quad depth-**READ** against it (`DepthStencilState.DepthRead`, quad at `z = w`). Drawing the sky first under `DepthStencilState.None`, as the other sky-replacing scenes do, measured **244 ms at `High` on the reference APU against 17 ms**, which neither pass shows alone and the Testbed never reproduced.

## The terrain

- **Craters are a single-cell lattice** (`CraterLayer`): each crater, skirt included, sits inside its own jittered cell (`margin = radius · 1.6`), so only the pixel's cell is read. ⚠ A 3×3 walk ran at 2 FPS on the reference APU, since the field is evaluated four times per pixel. The profile is near mean zero, or the clearing ramp becomes a visible bowl.
- ⚠ **The margin is the radius, so a crater that fills its cell is pinned to the lattice** (#240): the largest craters sat on their cell centres and the plain read as a stamped carpet of rings. Radii are small relative to the cell, **each octave is turned to its own bearing before `floor()`** (separate seeds stop two lattices sharing a crater, not their rows), and `chance` is per octave and is the count knob. Check from `campos=0,60,0 camtarget=0,-13.5,150`.
- ⚠ **A flat per-cell hash is a visible square once its cell covers more than a few pixels, and a band limit cannot save it** (#345): a band limit fades small cells, not large ones. The grain is one interpolated octave (`GradientNoise2`, gained by `GRAIN_SMOOTH_GAIN`) plus a 2 cm flat-hash octave, which is sub-pixel and stays grit.
- **Crater shadows are analytic** (#508): a point in a bowl is dark when the lip between it and the sun stands higher than the sun's ray climbs to it, with no march and no extra taps. Craters are mostly shallow (`depth = lerp(0.25, 1.0, roll²)`), since deep bowls all holding black read as a sponge.
- **The horizon is curvature, not haze** (`Curvature · distance²`). ⚠ The grid edge must stay behind the occluding skyline: halve the curvature and it shows as a dead-level, camera-locked line. The far plane is 2000 and the Moon takes no far ring ("The far field" in [the shadows document](shadows-and-far-field.md)).
- ⚠ **The highland belt exists because the play camera grazes the deck.** The lens is 1.1 over `ArenaIsland.TOP_Y` (`GameCameraFit.LENS_FLOOR_Y`), so the deck hides everything below ~1° of depression, while a curved plain's skyline sits ~2.5° below the eye: without relief the scene was black sky over bare stone, and neither `LevelY` nor `Curvature` reaches it. The mare is a basin with a rim (`HighlandHeight`, `HighlandInnerRadius` to `HighlandCrestRadius`); `HighlandSaddleFloor` stops the belt dipping into a notch.
- **The belt reuses `MareBase`'s noise**: `MoonHeight` runs four times per pixel, and a private massif field cost 2.0 ms of an 18.9 ms frame.

## Sky and light

- Stars are the shared `Stars.fxh`, moved verbatim out of `Space.fx`. ⚠ **The Earth ships at the true 0.95° radius (~30 px)**: an expectation-setting decision, not a bug. Its lit body stays under `GLARE_THRESHOLD` (a small bright disc flickers on the glare's sparse grid); only the rim blooms.
- **The ground half of the rig outshines the sky half** (`MoonLightingConfig`): sunlit regolith is the only diffuse source. The terrain's sun is `MoonTerrainConfig.SunColor`, never the frame's dome-derived one. Fill is a tiny non-zero `AmbientColor` (at zero, crater walls read as holes) plus earthshine; the same figures feed `TryGetMoonEarthshine`, so ground and island agree.
- **The sun is low**: `SunElevationDegrees` 16, `SunAzimuthDegrees` 40, asked for through `SceneRenderer.TryGetSunDirection`, so terrain, island, cluster and the Earth's phase answer one direction. It belongs to the scene, not a dome (#220).

## Rocks, on a mirror of the plain (#680)

- ⚠ **A painted boulder was rejected** (#508): a grazing view foreshortens the disc to a sliver and leaves the shadow, so it read as black dashes. A rock needs geometry, hence a CPU mirror.
- `TerrainMirror.Moon` is `MoonHeight` term for term; `Moon.fx` has a `HeightProbe` technique. ⚠ The rolls add the seed and the roll's own offset first (`cellX + (seed + 47.9f)`).
- The Moon **receives the sun's shadow map at full strength** (`ShadowConfig` 1).
- `MoonPlanting` lays casting `ScatterBucket`s through `PlantPass`: `FIELD_BLOCKS` and `FIELD_BOULDERS` out to `FARTHEST`, rim blocks round young top-octave craters (`OffworldGround.MoonCraters`) and `LANDMARKS` house-sized blocks out to `HighlandCrestRadius`. **The landmarks are the only rocks the play camera sees**; the deck hides the plain to ~300 units.
- ⚠ **Seat rocks on the ground as DRAWN** (`TerrainMirror.OnGrid`): the grid samples every 3.34 units and interpolates across triangles, so the exact field differs from the drawn ground by up to 2.3 units, and `mirrorcheck` compares formula with formula and cannot see it.

## High and Ultra's rocks (#777)

The owner, on High and Ultra: "triangular rocks on the ground. That is a bug." A block was a squashed icosahedron shaded flat: twenty faces of one size, one tone each, and under the 16-degree sun they read as cut crystals. **Low and Medium keep those meshes** (the owner's words); High and Ultra (`SceneRenderer.FineMeshes`, the Testbed's `finemeshes=`) draw another set off the same instances (`ScatterBucket.FineMeshes`, built by `FracturedRockSet`). References: `C:\Users\panrd\AI\sd\out\777-klein` (not in the repository).

- **The block is fractured, not faceted** (`FracturedRockMesh`): a subdivided icosahedron, each point pushed out to where its ray leaves a solid of 9 to 12 rolled planes, with the 2 to 4 corners that stand out furthest chipped by a small plane each. The planes meet in a soft minimum (`BLOCK_BEVEL`), so an edge is a narrow worn arris, and the faces undulate a little. A boulder is 6 to 8 planes and a wide rounding. Each stands in the frame of the coarse mesh it replaces (the block's centre 0.18 over the pivot, a boulder a lathe's radius 1 and height), so the planting's seat holds for both.
- ⚠ **Each vertex normal is the surface's own**, taken across two small steps over the sphere. Averaged over the triangles round a vertex, every edge turned over two rows of triangles and the blocks read as pillows.
- **The shapes roll off a seed of their own** (`seed * 31 + 777`), never the planting's stream, so the field lies where it lay and the Potato path (which builds no fine set) plants the same rocks.
- **Four densities, by size**: 80, 320, 1280 and 5120 triangles for a block under 0.9, 2.2 and 4.4 units and over (a boulder under 0.4, 1 and 2). One density is wrong at one end: 320 is a polygon round a landmark seen from twenty units, 5120 on two thousand pebbles is ten million triangles a frame. **The sun's map takes the next coarser sampling of the same rock** (`ScatterBucket.CasterMesh`): its points are the finer one's own, so it lies inside it and never shades the rock it stands for.
- **No face is one flat tone**: `Acacia.fx`'s stone (`ScatterBucket.Stone`, behind a uniform branch the coarse buckets never take) tilts the normal by dents laid out in the rock's own size and a grain and round pits at the world's, mottles the colour in patches, darkens the hollows and the pits, adds a few bright flecks and hairline cracks, and dusts the foot and the up-facing faces with the regolith's grey (`FootDust` into `FootEarth`; the ground over the pivot rides in the fine instances' `Custom.z`). Every octave fades as its period nears two pixels (`StoneOctave`). The fine buckets carry no dapple and no bark, so the bark's lichen stays off the Moon.
- ⚠ **A face square to the sun is lit at N·L near 1, where a tilt changes almost nothing**: what keeps it from one tone is the albedo (patches, pits, cracks), not the relief. The relief is what reads on the faces the sun grazes.
- **What it costs is the triangles**, paid at the primitive rate wherever the camera looks: the play pin, which shows a handful of rocks, pays what a pin among them pays. Desktop Testbed, Release, 3840x1600, `ssaa=2`, `nopost`, `fpscap=1000`, `sceneseed=0`, `alt=finemeshes=0;finemeshes=1`: the play pin (`campos=0,-4,45 camtarget=0,-9,0`) **6.70 to 6.91 ms (+0.21)**, a pin among the rocks (`campos=-31,-11.2,39 camtarget=-23,-13.4,31`) **6.62 to 6.84 (+0.22)**, the sign held in every one of the 21 uncontended cycles of each run (another process's GPU load took the other 23). The same set a density finer everywhere cost +0.54; the coarser casters took +0.27 to +0.21.

## Costs to compare against

- Game front end **~17.7 ms at `High`** (ssaa 2), reference APU, 1600×900, `nocap`; the belt is ~1.5 ms. No reduced program.
- Desktop Testbed, 1600×900 at ssaa 4, `nopost nooverc fpscap=400`: play pin `campos=0,-4,45 camtarget=0,-9,0` 5.94 ms; the plain (`campos=40,-9,120 camtarget=70,-14,160`) 5.42 ms.
- Rocks plus the shadow map, Game 1920×1080 `nocap`: `High` +0.40 ms (the coarse rocks, before #777). High and Ultra's rocks add +0.21 ms on the desktop at 3840x1600 ("High and Ultra's rocks" above). Run `scene=moon mirrorcheck` after touching the height or its mirror ([Capturing and measuring](../testbed/capture-and-measurement.md)).

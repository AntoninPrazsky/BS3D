# The noise library and the shared shader headers

Part of [Rendering](../rendering.md).

## `Noise.fxh`

`BS3DLibs/Prazsky.Shaders/Content/Shaders/Noise.fxh`: gradient noise (2D/3D, quintic fade), fBm with a **rotation between octaves**, ridged fBm, and jittered Voronoi (nearest distance, and the edge field `F2 - F1`, in 3D also the winning cell's id). Scene shaders and `InstancedModel.fx` include it. GPU-only: nothing plants objects on these fields, so no C# mirror is needed and the hashes are quality-first (Hoskins' sine-free hash). Callers budget noise evaluations per pixel.

**The look of a procedural image is decided by its fields first.** A sum of plane-wave sines keeps its planes however many terms it has and reads as a demo-scene plasma. Three things separate a modern result: **rotated octaves** (aligned octave lattices read as a grid), **domain warping with noise, twice** (and colour by the warp intermediates: the dream's marbling) where sines under a sine warp still slide as blobs, and **occlusion and contact** (SDF ambient occlusion, crevice darkening, normals perturbed by the field's own gradient so every light agrees).

## `PerturbNormalFromHeight`

The tangent-free normal tilt every height field ends in (terrain, sea chop, regolith, ball vinyl, island stone). ⚠ **It carries a determinant floor, and the guarded version must stay the only copy**: at a grazing angle `ddx`/`ddy` of the world position go near-degenerate and `normalize(0)` is a NaN speck on a far surface. Twelve copies once existed and only two had the floor (#297): a fix does not travel beyond the scene it was noticed in.

## Shared headers (#581)

- `Hash21` lives in `Noise.fxh`; ⚠ **keep its constants**: they placed every lit window and meadow flower. `Noise.fxh` also has integer hashes `HashMix`/`HashCell`/`HashSalt`/`HashBits` for what is rolled once a cell (#674).
- `Craters.fxh` (moon, Mars), `Rocks.fxh` (outback, Mars), `Grass.fxh` (meadow, savanna, each with its own `GRASS_COMB_STRETCH`/`GRASS_FBM_GAIN`), `ForestGround.fxh` (forest, aurora's night forest), `SceneFog.fxh` (#963: a scene's height mist and haze, closed form along the ray; the aurora's snow, far stand and sky, and `InstancedModel`'s triplanar programs, behind a branch on `SceneFogDensity`, 0 in every other scene).
- **A shared header holds functions and static constants only**; the uniforms it reads stay declared in each scene *before* the include, so no scene's constant buffer moves. Genuinely diverged copies stay separate (`CraterLayer`/`CraterField`, `RockLayer`, `Fbm3` in `Space.fx`, the other scenes' `TerrainHeight`s).

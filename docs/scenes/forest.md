# The forest

Part of [The scenes](../scenes.md).

A temperate woodland glade: a mossy, needle-strewn clearing whose hills dress in a dark conifer canopy, with procedural trees, rocks and stumps standing on it. `scene=forest`.

## What it is made of

- **Terrain**: `ForestBackdrop` / `Forest.fx` (`ForestSceneConfig`), the meadow's displaced-grid machinery. `SceneCatalog.IsSolidTerrainScene` lists it (the island's footprint is cut out, a dark pit shaft backs the drain's glass); one shared list, because two hand-kept copies once both omitted it.
- **The wood is shared**: `ForestScatter` decides where each piece stands, `ForestScatterRenderer` draws it, in all three executables. **The caller keeps the scene gate and the placement**: `Draw` touches no GPU state and draws whenever called, so each host wraps it in `if (scene == SceneKind.Forest)` and draws after the terrain, before the island. It passes `SceneRenderer.FineMeshes` to `Draw` and `DrawShadow`, which pick the spruces' crown.
- **Trees**: a **spruce** of tiers of branch whorls (each a drooping `LatheMesh` skirt with a shadowed tuck: the sawtooth says spruce, a smooth cone says toy), a broadleaf crown of overlapping lobes, rocks, stumps, snags and logs (#462; the daytime forest plants none). **On High and Ultra the spruce's crown is separate boughs** over a dark interior, laid out by the lathe's own whorls (#733), all growing out of one shaded bark stem that runs from the trunk's top into the leader (#960). It casts into the sun's map as drawn. See "The spruces on High and Ultra" in [The aurora](aurora.md); this wood plants no crown snow, so it draws no lumps.
- **Each kind is built at several proportions AND its own structural seed**, because the eye counts silhouettes and a uniform scale or yaw does not change one. `ForestScatter` splits instances between variants (`ModelInstance[][]`, one instanced draw each, two per tree); foliage tints are `ForestScatterRenderer.CONIFER_TINTS`/`BROADLEAF_TINTS` (a table shorter than its variant array throws). ⚠ Two lathes sharing an edge must share `irregularityPhase`, or the seam opens.
  - Variety is in the **mesh, not a per-instance stretch**: normals go through the world matrix with no inverse transpose, so a squashed tree would shade as the shape it was authored at. A **rotation** is safe.
- **Planting** is the savanna's clumped generator through `TerrainMirror.Forest`. ⚠ `MathHelper.SmoothStep` is not GLSL `smoothstep` (value-interpolate, not clamp-then-hermite): it once scattered the first forest six thousand units into the sky.
- **`ScatterSpacing`** keeps plants apart (one rule with the savanna: packing fraction 0.72, a retry budget, a signed clearance test; nothing is dropped for want of room). ⚠ Changing it **re-rolls a scatter at an unchanged seed**.
- **The wood takes the dome's mood by its pigment** (#108): its red and blue were near zero, so a coloured light had nothing to multiply. `ApplySkyTint` moves each tint `SKY_TINT_STRENGTH` towards the dome's **hue only**, re-encoded only on a dome switch.

## Traps from the first build (black eggs on the ground)

- A crown spans `[trunkHeight, trunkHeight + crownHeight]`, not `+-h` about the base. Lathe profiles run top, outside, underside (what `LatheMesh` documents): bottom-up they were inside out, and a shallow inside-out dome even reads *lit*. A span whose first ring is on the axis got a `+Y` normal for the whole span: take the tangent from the ring that is off the axis.
- Config colours are linear radiance, the material-diffuse uniform is sRGB: encode once at the boundary (`ColorSpace.LinearToSrgb`); a tinted draw multiplies luminance by 1.25, so meshes are built at `SCATTER_MATERIAL_DIFFUSE`. A procedural-mesh renderer defaults to white specular (wet obsidian): pass matte `BasicEffectParams`, a **zero** specular meaning no override, so matte is a dim non-zero colour.

## The terrain

- **Two programs over one body**: `ForestFloor(input, detail)` is called with a literal from `ForestPS` and `ForestReducedPS` (MGFX cannot parse a uniform argument in a `compile` statement). `SceneRenderer.SceneDetail` selects; **Low drops the needle strands and the tree shadows**. ⚠ **`SceneDetail` must re-select the technique on write**: as a plain auto-property the reduced program shipped and **never drew at any tier** while looking correct (caught by making it flat red for one run).
- ⚠ **The floor's cost is occupancy, not work**: removing any one piece saved nothing, pairs saved a lot, and a `[branch]` on a uniform measured 2.72 against 2.70 ms (the register footprint is fixed at compile time). Compile a second program.
- **The base normal is per pixel** (three `TerrainHeight` taps; an interpolated normal leaves a Mach band at every cell edge). **Hills** are five domain-warped sine octaves summing to 1 (so `HillHeight` keeps its meaning).
- **The floor is litter, not lawn** (#281): band-limited noise relief (`Fbm2BandLimited`, `NeedleRelief`; summed plane waves were the lines and waves); rusty needle litter (`LitterColor`, `LitterColorDark`) with bare `EarthColor`, and on it moss (`ForestColor`/`ForestColorDark`, `MossCoverage`), bilberry (`UndergrowthColor`), dry grass (`DryGrassColor`) and needle strands near the lens. **Three fields draw all of it** (dampness, litter mottle, a fine one): a field per layer cost +0.64 to +1.04 ms.
- **The canopy is colour, not geometry** (`TreelineColor`, its own ramp). **Haze is two-stage** and reaches `HorizonColor` exactly at `HorizonHazeDistance`, ⚠ **which must stay at or inside the grid's half-extent** or the mesh edge shows against the dome.
- ⚠ **`CloudNoise` clusters around zero** (sigma 0.18): `* 0.5 + 0.5` spans 0.35-0.65 and ACES flattens it to one tone. Spread by about 1.6 and saturate before using it as a mottle, anywhere.
- **`ForestShadow` plants a *virtual* tree per hash-grid cell** (the shader cannot know where the real ones stand); density rides `canopyRamp`, zero in the clearing. ⚠ **It drew squares until #648**: a cylinder crown tested only in the cell the ray step landed in (cut flat at the cell's edge), height compared with world y, `CloudHash22` (-1..1) used as 0..1. Now a 3x3 block of cells round where the sun ray reaches an average crown's height, each crown a **sphere** shaded by its chord length, its size from the hash's fractional bits (equal crowns are polka dots). To isolate such an artefact: `scene=forest sceneseed=0 campos=0,30,60 camtarget=0,-14,0 fov=60`, `shadow=0`, then `ForestShadow` returning 1.0. The checkered rectangle on the island's top under a high sun is the ceiling glass's caustic shadow (#553), by design.

## Costs a change must compare against

Testbed, dome 13, 1600x900 `ssaa` 2, `nocap`; play `campos=0,-4,30 camtarget=0,-2,-60`, across `campos=35,-8,45 camtarget=70,-14,110`, above `campos=0,25,60 camtarget=30,-14,140`.

- **The floor, High**: 2.43 ms (play), 2.42 (across), 2.85 (above); the reduced program saves 0.19 / 0.29 against it.
- **`ForestShadow`**: about +1 ms on the laptop (`benchmark.ps1 -Scenes forest -Ssaa 2`, `quality=high`, `sceneseed=0`).
- **High and Ultra's spruce boughs** (#733): +0.11 ms at High and +0.12 at Ultra (`shadowmap=8192`). Desktop, 3840 x 1600 `ssaa=2`, `sky=1`, the Testbed's play camera `campos=0,-4,30 camtarget=0,-2,-60` on `Maps\Full.json`, `alt=finemeshes=0;finemeshes=1`, 27 paired cycles. The frame is about 13.0 ms there, but no shipped level stands in this scene. The stem the boughs grow out of (#960) adds +0.04 ms there (34 paired cycles, 12.66 to 12.70).

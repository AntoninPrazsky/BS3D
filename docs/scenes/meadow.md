# The meadow

Part of [The scenes](../scenes.md).

The Windows XP *Bliss* hill: vivid green rolling to a crisp horizon, a flat flowering clearing where the arena stands, rolling up into hills. `SceneKind.Meadow`, `scene=meadow`, sixth in the NumPad2 cycle.

## What it is made of

- **`MeadowBackdrop`**, **`Meadow.fx`**, `MeadowSceneConfig`: the displaced grid (shared `AcquireGridMesh`, [Terrain grids](terrain-grids-and-mirrors.md)) with smooth rolling sines, normal by finite differences. `TerrainMirror.Meadow` is the CPU copy everything planted is placed by (`MeadowNatural` is the field without the pond's levelling).
- **Planting** (`MeadowScatter`, #609): old trees (below), shrubs, hedgerows, hay bales, post-and-rail fences (`FENCE_SCALE`, twice a field fence's size, #698), boulders and `DetailOnly` tufts, nothing nearer than `INNER` but the island's bank and what the path and brook arrive at. Drawn as the savanna's planting is (`ScatterBucket`s through `Acacia.fx`). ⚠ Colours are set against the vivid grass: at the savanna's canopy colour the first hedges photographed as black lines.
- **The path** (`MeadowPath`): `Meadow.fx`'s `PathLateral` and `MeadowPath.Lateral` are **the same arithmetic**; the planting keeps `PATH_CLEARANCE` off it. It **arrives at a knoll** (`PathEnd`, `KnollRadius`, `KnollHeight`: a bump in `TerrainHeight` and its mirror) with the biggest oak and a bench: a path leads somewhere.
- **The brook** (`BrookBearing`, `BrookWidth`, `BrookMeander`; `BrookLateral` in the shader and `MeadowPath`) is **shading on the field, not a carved channel**: the grid is about 5.5 units a cell, coarser than the water, so a channel would come and go as the grid follows the camera. ⚠ The first water mirrored the horizon's pale colour at grazing angles and read as a tan path: the reflection leans to the zenith and is cooled. It runs **into a pond** (`PondCentre`, `PondRadius`, `PondOutline`).
  - **The ground under the pond is levelled** (`POND_LEVEL_FROM`, `POND_LEVEL_FADE`; `PondLevel` computed on the CPU off `MeadowNatural`, pushed as a uniform) so still water lies level; `TerrainMirror.Meadow` carries both terms. The pond **mirrors the clouds** (the reflected ray crosses the cloud plane, `CloudCover` in `Clouds.fxh`). ⚠ Use the *flat* reflection's ray, not the rippled one: a low lens crosses the plane (about 200 up) thousands of units out, and ripple slope moved that point as far again: speckle.
- **The path's and brook's edge noises run only near their lines**, behind a `[branch]` on the lateral distance: `GradientNoise2` never leaves +-1, so past the widest edge plus `PATH_RAGGED`/`BROOK_RAGGED` every term is exactly zero. They had run on every pixel (-0.50 ms Low, -0.46 Medium, -1.02 High on the APU).

## The old trees

`MeadowTreeMesh`: oaks, limes and a willow, **grown from the crown inwards**: the dome first, then wood to it (limbs from the fork, boughs from each limb's knee, a branch into every clump), so no clump floats. Trunk and thick boughs are **swept, not stacked** (`TubeGeometry.AddSweep`: stacked end rings met in two planes, a sleeve over the wood).

- **Crowns are sprays with no ball inside** (#697): each clump has `TWIGS_MIN`..`TWIGS_MAX` twigs from its branch end over its shell (the detail-only `MeadowTreeMesh.Twigs`) and leaf cards hung along their outer part (`LEAVES_FROM`), **each card's stem on its twig**. `Acacia.fx`'s `BroadLeafMask` cuts sprigs of broad leaves. The Low tier keeps solid lumps, the only place a ball is drawn.
- ⚠ **`BroadLeafMask` drew a solid rectangle at every twig's tip**: `step(p.x, 0.74)` is zero past the stem's end, not negative, and `clip()` keeps a zero; it is -1 now (the acacia's `LeafMask` had the same fault).
- ⚠ **A sprig lies along its clump's shell** (`SPRIG_FLATTEN`) **and its front faces out whatever the jitter did** (`face . shell >= 0`): `Acacia.fx` shades a card seen from behind by its flipped normal, and sprigs pointing straight out made a third of the sunny side shade as if facing in.
- **The leaves cast the crown's shade** (the caster clips the card mask: dappled). ⚠ **Nothing solid inside a crown may cast**: anything inside a caster is in shadow from every direction and reads as a dark ball. The Low tier's lumps cast because they are what is drawn; at tiers that do not draw them they must not, or every clump shades its own sunny side.

## Flowers and the grass

- ⚠ **Roll on the cell's integer seed** (`HashCell`, `HashSalt`, `HashBits` in `Noise.fxh`), never `Hash21(cell + k)`: on whole-number cells that hash is periodic (`frac(p * 123.34)` is `frac(p * 0.34)`) and repeated the small flowers every 40 units, the big ones every 110 and every lily pad (#674).
- Flowers drift in `FLOWER_PATCH`es, each mostly one of five kinds, mostly small (`FLOWER_SMALLEST`..`FLOWER_LARGEST`).
- **A rosette has a shape of its own** (#127): petal domes and a boss eye summed into the height field the grass relief feeds `PerturbNormalFromHeight`, the grass's share faded out under it (#283). ⚠ **The weight is the smooth `petalProfile`, NOT `flowerMask`**: the mask is antialiased over a pixel and a half, and in a *differentiated* field that puts a pixel-wide step in the derivative and lights a hard ring round every rosette.
- **The grass has a material of its own** (#281; it read as green stone), each term a `MeadowSceneConfig` dial: clumps, tips and hollows (read off the relief that tilts the normal), blades seen from the side, dry patches, the velvet `GrassSheenStrength` (0.55 made the far hills pastel) and the glow `GrassTranslucency`. ⚠ **The clump seam is the zero set of TWO noises** (#674): one gradient noise is exactly zero at every integer lattice point, so its crossings tie a lattice of dark spots (what the lattice probe's `meadow-down` flagged).
- **`MeadowReduced`** (the Game's Low tier via `SceneRenderer.SceneDetail`) drops the clump seams and blade strokes; check it draws with `detail=0`.

## Costs a change must compare against

- **Grass material** (Testbed, dome 13, 1600x900 `ssaa` 2, `nocap`): +0.36 ms looking down at near grass (`campos=0,-7,45 camtarget=0,-13,60`), +0.19 from the play level (`campos=0,-4,30 camtarget=0,6,-60`); the reduced program +0.11 / +0.06.
- **Planting and trees** (Game, `level=Pennant`, 1920x1080, `nocap logfps`): High 4.32 to 4.80 ms, Low 1.31 to 1.43 (#609); the leaf cards are **pixel-bound** (halving them bought 0.09 ms, a smaller cover 0.43).

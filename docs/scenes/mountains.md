# The mountains

Part of [The scenes](../scenes.md).

A snow basin ringed by tall snow-capped peaks that rise with distance into alpine haze. `SceneKind.Mountain`, `scene=mountain`; all ten mountain levels name dome 8. Its frame cost is in [The mountain's frame cost](mountain-cost.md).

## What it is made of

- **`MountainBackdrop`** with **`Mountain.fx`** (`MountainSceneConfig`); the snow is `Snow.fx` through `Snowfall` (below).
- **Terrain**: the desert's machinery with another field: the shared `AcquireGridMesh` grid (`MOUNTAIN_GRID_N`, [Terrain grids](terrain-grids-and-mirrors.md)) displaced by `RidgedFbm2` (five octaves, 130-unit first spacing), low round the origin and rising with distance (`ClearingRadius`/`ClearingTransition`) so the basin stays below the platform glass. The base normal is three finite-difference taps per vertex; a band-limited `RockRelief` roughens it per pixel; the range dissolves over `HorizonHazeDistance`.
- ⚠ **Any terrain grid over 255 a side needs 32-bit indices** (`TerrainGridCache` builds them): 16-bit wraps past 65,536 vertices, and the tall peaks stretched the wrong triangles into regular dark bands across the whole sky, which read as a glare artefact and cost a long hunt down the wrong path.
- **Massing** (#86). ⚠ **Never plane-wave sines**: summed `1 - |sin|` waves keep their planes and have no high frequencies, so peaks were smooth cones. `RidgedFbm2` cannot go in raw (a white blanket): a 0.24 floor taken out and the rest **cubed** restores the height distribution `Height` and the snowline were tuned against. **Massifs**: one octave at `MASSIF_SPACING` scales height (`MASSIF_LOW`..`MASSIF_HIGH`, mirrored in `TerrainMirror`), or a ridged field is a saw of equal crests.

## Rock, snow and the light

- **Snow and rock are two materials with a crisp edge**: the facing band `RockSlope`..`SnowSlope` and the altitude snowline are each read through a second narrow smoothstep (`SNOW_EDGE`); read linearly, every mid-steep face was a lilac blend. The rock is near black (`RockColor`, `RockColorLight`); **the basin floor is snow**.
- ⚠ **Up-facing surfaces take 75 % zenith and 25 % horizon ambient** (`0.25 + 0.5 * (n.y * 0.5 + 0.5)`), not the zenith alone: dome 8's saturated violet zenith turned the snow floor blue-violet.
- **Steep faces are fluted** (`FluteSnow`: two gradient noises stretched down world Y shift the snow's threshold). ⚠ **Not by combing the rock relief down the fall line**: a combed domain is rotated about the world origin, so at every grid facet the coordinate jumps (mesh-following chevrons). ⚠ **Per vertex, not per pixel**: per pixel cost 0.9 ms of a 10.0 ms frame, still 0.3 behind a data branch (the branch keeps the registers).
- **Alpenglow**: at a low sun only the heights take the light (the sun's share climbs from `AlpenglowLow` to `AlpenglowHigh`); not a cast shadow.
- **Rock relief**: four octaves of `Fbm2Combed` with a grain (`ROCK_GRAIN`, `ROCK_STRETCH`); the old four plane-wave sines laid a plaid over the snow. The config frequency is an *angular* one and goes in as `f / 2pi`; amplitude is bought back by the shader gain `ROCK_FBM_GAIN`, not `RockReliefStrength` (hand-built levels pin it), doubled when the relief turned out to have been applied twice since #208 (#504).
- **Snow's sparkle** (`SnowSparkle`, gated on the snow mask). ⚠ **The lattice cell says only where a glint is; its size comes from the footprint**: a world-fixed cell is 30 pixels on the basin floor and lit white parallelograms across the ground nearest the player (#278). The drift relief was **cut at every tier** (#296: 1.08 ms of 10.99, indistinguishable photographed; partial cuts bought proportionally less on an occupancy-bound pass). **`MountainReduced`** (`detail=0`, Low) drops the sparkle and shades the rock on three octaves: 0.18 ms at `ssaa` 1, 0.58 at 2.

## The sky (#661)

Dome 8 is **generated** (colour by each vertex's height, stops in `SkyDome.Data.cs`) after a painted violet dusk made the whole scene violet: the rig reads the dome's top quarter as sky ambient and its bottom fifth as the sun's tint. ⚠ **That bottom fifth is the colour of the light on every lit face** (`KeyTint = lerp(1, HorizonLinear, SKY_TINT_STRENGTH)`): a peach there turned the range to sandstone, so it is a near-white rose.

## The snowfall (#654)

`Snow.fx` is a static buffer of billboard flakes animated in the vertex shader (fall, wind, sway, `frac` wrap in a box that follows the camera: it never pops). Drawn last, alpha-blended, depth-read, no depth write. Each scene (the mountain; the aurora with a gentler config and part of the buffer) draws through **its own clone** of `Snow.fx`; `Snowfall.Prepare` returns it with parameters cached. Dials: `MountainSceneConfig.Snow` (`SnowConfig`).

- ⚠ **`BlendState.AlphaBlend` is premultiplied.** The shader returned a straight colour, so a flake added its whole colour and no feather or opacity softened it: the "crisp crystal icons". It premultiplies now; the sea's spray and the storm's clouds share the shape.
- **A flake is what a lens makes of it**: nearer than `Focus` it is blurred by up to `Aperture`, **its alpha falling with the area it spreads over**; nearer than a quarter of `NearFade` it is dropped; it streaks over `Shutter` and is never under one pixel radius (0.6 px shimmered). The silhouette is a six-armed tumbling crystal (`cos(6 * theta)` by the double-angle identity: no `atan2` seam).
- **Colour sits just under `GLARE_THRESHOLD`**, darker than a bright sky. ⚠ Dim the colour, not the opacity, if flakes ever glow. `FlakeCount` 9000, drawn twice (near box, and `FarLayerScale` times larger as the distant veil).
- **The terrain takes a snowfall veil** (`SnowVisibility`: `1 - exp(-(d - 60) / SnowVisibility)`); haze and veil go to the colour **the dome is drawn in behind the pixel** (`FarSkyAt`, #551), not `HorizonColor`, which painted the range pink.
- Cost +0.06 ms (Game, `level=Belfry`, 1920x1080, `quality=high`, `nocap`); the play pin (`campos=0,2,45 camtarget=0,-4,0`, dome 8, `nopost nooverc`) is about 10.0 ms at 23 Mpix.

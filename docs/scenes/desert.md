# The desert

Part of [The scenes](../scenes.md).

A Sahara erg: dunes to a hazed horizon, the round island standing in the sand. `SceneKind.Desert`, `scene=desert`, next to the savanna in the NumPad2 / V cycle.

## What it is made of

- **`DesertBackdrop`** draws **`Desert.fx`** (SM 5.0); dials on `DesertSceneConfig`.
- **Real geometry**: the shared `AcquireGridMesh` grid (`DESERT_GRID_N` over `DESERT_EXTENT`, [Terrain grids](terrain-grids-and-mirrors.md)), snapped to its cell and displaced in the vertex shader by `DuneField`; flat in a clearing round the origin, rising with distance.
- ⚠ **`DUNE_MEAN` takes the field's mean back to zero.** It is multiplied by the clearing ramp: a field with a mean makes the desert a bowl with the island at the bottom. Re-sample it through `TerrainMirror` whenever the profile changes, then run `scene=desert mirrorcheck`.
- Opaque, **no point lights** (`SceneLightCount` set to nothing), **no default dome forced**; shares the savanna's [flock of birds](birds.md).
- **Haze and the dust veil** (`Clouds.fxh` noise) fade over `HorizonHazeDistance` into a murk of the horizon's *brightness* in the sand's hue (`HazeWarmth`), **not into `HorizonColor`**: under dome 13 that uniform is teal while the dome draws lilac-grey, and the far dunes stood as a teal band.

## The dunes: `DuneField`

- **A sawtooth along the wind** (`DuneProfile`): a long gentle windward slope over `DUNE_WINDWARD` of each `DUNE_SPACING` period, a short steep slip face (about 35 degrees) into a flat trough. Crests are sinuous, their height wanders, a **second set** 35 degrees off the wind takes over where taller; the wind is `DesertSceneConfig.Wind`.
- **Low dunes are let go, not kept small** (a low dune's full slip face drew as thin dark cracks under a low sun): height and the second set's presence go through a smoothstep.
- **The crest is sharp; only the points are rounded** (owner's ruling, #688). The rounding radius is **zero along a dune** and grows only where the two sets meet (`CrestRoundness`: `CREST_ROUND`, `CREST_REACH`, gated by the product of the heights, `CREST_GATE`), joined by a smooth maximum over `SET_BLEND`; a crossing stands lower than its crests, the saddle it should be.
- **Vertices cannot carry a sharp corner** (a row of spikes where a crest crosses the grid slantwise): vertices and the mirror use `CREST_GRID_WIDTH` (two cells), the pixel normal `min(2 * footprint, CREST_GRID_WIDTH)`, capped because footprint passes two cells far out.
- ⚠ **The meeting gap is read off heights already rounded at the grid's floor, not the sharp ones.** A sharp height has a corner on its own crest, so the merged radius changed slope on the line it should round and the surface inherited a crease (normal turning a median 2.9 degrees across a 0.002-unit hair; 0.006 after): the inputs must be smooth, not only the function.
- **The normal is analytic** (`GradientNoise2Grad`: same hash and quintic fade, so its value equals `GradientNoise2` to the bit and vertex and pixel shaders describe one surface); every derivative is exact, the rounding radius's included.

## The sand's surface

- **The base normal is per pixel** from the field's gradient; an interpolated per-vertex normal left a Mach-band grid.
- **Ripples are braided and static.** `SandLayer`: two sets of rounded lines at **+10 and -9 degrees**, each made wavy by perturbing its across-line **phase** with `Noise.fxh` gradient noise at about the line frequency, **screen-blended** with a wandering weight (one set is corduroy, an average a lattice, a mix darkens the crossings). Do not scroll them: a ripple is a ridge carved into the dune (#276).
- **One pattern layer, measured**: a second cost +56 % frame time against +32 % (low over the sand, `ssaa=2`: flat 47.6 FPS, two layers 30.5, one 36.1). Variety comes from feeding the wandering field back as a slow **bend of the line family**. ⚠ **Bend the position, never the frequency**: `coordinate * field` in the phase, the coordinate running to +-500, is a violent rainbow moire.
- ⚠ **`SandPattern` has no early exit once ripples are unresolvable**: the value feeds `ddx`/`ddy` through `PerturbNormalFromHeight`, so a branch diverges inside a quad. Use `lerp`. Crest sharpening fades **quadratically faster** than amplitude (the nonlinearity makes harmonics).
- **Colour**: one noise evaluation mixes `SandColor` with `SandColorPale` and varies brightness; hashed **grains** on a 60-cells-per-unit lattice, ⚠ whose fade must finish *before* cells reach pixel size or the last metres crawl; `SheenStrength` is gated on `ndotl` so lee faces do not glint.

## The sky (#681)

Dome 6 (desert) and dome 14 (savanna) are **generated** like dome 8 ([The mountains](mountains.md)); the **bottom fifth keeps the old palettes' lowest rings to the digit** (the light rig reads it as `HorizonColor`). ⚠ A saturated blue goes violet through the tonemap and turned the savanna's yellow grass green.

## The pyramids (#965)

The owner reversed his own ruling of 2026-10-02 (#677, dunes only): "pyramids, realistically placed and rendered after the real pyramids ... players must feel: wow, that is beautiful". `Pyramids` (drawn by `DesertBackdrop` after the dunes, opaque, writing depth) stands the Giza group far out over the dunes, shaded by `Pyramid.fx`; references in `C:\Users\panrd\AI\sd\out\965-zimage` and `965-klein`.

- **Giza's figures, compressed**: the sizes are the real ones at `SIZE_SCALE` 1.4 units a metre (Khufu 139 m today), the plan the real one (Khufu, Khafre, Menkaure on their diagonal, the queens east of Khufu and south of Menkaure, sides on the compass) drawn in by `SPACING_SCALE` 0.6, so the group stands inside the dunes' grid (extent 1000) with Khufu `NEAREST` 700 out on `BEARING`, behind the play view the Testbed opens on and the cluster's backdrop there. ⚠ The diagonal is turned `DIAGONAL_TURN` off the line of sight: straight away from the island, Khufu hid Khafre; the photographs show the three staggered side by side. Khafre's higher ground is added to its height (lifted, its foot stood on air). Each sits on the lowest dune under its corners and middle less `SINK`, so the sand meets every face.
- **The stone** (`Pyramid.fx`): golden limestone in courses (`COURSE` 1.5), a tone a course and a dark line under each, faded as a course nears a pixel; weathering in patches and blocks gone a course high and a few long, staggered as masonry is laid; Khafre's pale casing at the top; sand drifted up the foot in the dunes' colour with a ragged top. ⚠ Weathering along a fixed axis (x + z) runs along one face and streaks it: each face takes its own horizontal. ⚠ Square broken blocks read as tiles.
- **The light and the air**: the scene's sun on four flat faces and the sky's ambient kept low, so a lit face stands against a shaded one; the haze the desert's horizon colour at `HAZE` 1600, far lighter than the dunes' 420, or the group dissolves into the sky. No cast shadows (beyond the sun's map).

## Costs a change must compare against

Testbed, dome 13, 1600x900 `ssaa` 2, `nocap`, the pass against `main`: **-0.02 ms** from the play camera (`campos=0,-4,30 camtarget=0,-2,-60`), **+0.35** across the dunes (`campos=35,-8,45 camtarget=70,-14,110`), **+0.27** from above (`campos=0,25,60 camtarget=30,-14,140`); three finite-difference normal taps would cost +1.60 / +1.53. The Game at High, 3840x1600: 11.40 ms (Rope's play view).

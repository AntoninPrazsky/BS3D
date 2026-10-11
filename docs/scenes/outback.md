# The outback

Part of [The scenes](../scenes.md).

## What it is

The thirteenth `SceneKind` (#112): red-orange monoliths (bornhardts, Uluru and Kata Tjuta) on a wide near-flat spinifex plain, the island in a clearing.

- Code: `Outback.fx`, `OutbackBackdrop`, `OutbackSceneConfig` (groups Terrain, Surface, Air, Birds; the shape is explained in [Space](space.md)). Grid `OUTBACK_GRID_N` 400 over `OUTBACK_EXTENT` 1000 (finer than the desert's, because the silhouette is real); over 255 vertices a side, so the grid cache's 32-bit index buffer is load-bearing.
- Launch: `scene=outback`. No shipped level names it.
- `IsSolidTerrainScene`, takes the dome and **forces none**: the two-stage haze keeps its red under any dome. It shares the [flock of birds](birds.md).
- ⚠ `ProceduralAmbience.BakeScene`'s `default:` arm is a deliberately near-silent neutral, so a new scene with no bed is audibly missing (the arm once played cave drips on the Moon and vacuum here).

## The monoliths

- **Geometry on a jittered single-cell lattice** (`RockLayer`): a formation is held inside its cell by its own reach, so only the pixel's cell is read (the field runs four times per pixel). A formation is two lobes with their own bearing, elongation and squareness; a rotated second lattice carries the boulder outcrops. The largest formation's margin is 0.4419 of a cell against the 0.45 clamp, which leaves 0.116 of a cell of jitter (31 units of world wander); empty cells (`RockChance`) break the lattice.
- `RockSpacing` 270, `RockChance` 0.70 (#171): a wider cell put about one formation in the play frustum's wedge. ⚠ **The cell shrank rather than the formations growing**: radius is a fraction (0.10–0.16) of the cell and the margin already sits at the clamp, so a larger fraction breaks the single-cell read.
- **The clearing gates the rock per FORMATION**, from its centre: per pixel, a radial ramp slices a monolith and draws it half sunk. The plain's own swell is not ramped, so no field with a mean meets a radial ramp.
- **`Bornhardt` is a superellipse crown**, `sqrt(1 − t^p)`, multiplied to nothing over the last 0.30 of the radius (a wall of 3–5 cells). ⚠ Narrower does not hold: a lobe wall cut to its own smaller radius let facets wander across the crease and drew horizontal ripples, one per grid row, so a lobe's wall is widened to the same world width. ⚠ The outcrops **carry no gullies**: at 25 across and 5 tall they drew a starfish.
- ⚠ **The gullies are geometry, in the height field.** A per-pixel combed field through `PerturbNormalFromHeight` drew terraces, one band per row of grid triangles, because screen derivatives jump at each facet edge. They are radial, sampled on the **unit circle** of the formation's frame (no seam or pole, unlike an `atan2` rib count) and handed back out of the height field so varnish lands only in them.
- ⚠ **The heat shimmer's noise is keyed to the view ray's direction, never `worldPosition.y`**: on a near-vertical flank `y` is stripes one unit apart (the terraces' real cause).
- The colour selector's weights must land inside 0…1 or the near monolith pins to `RockColorBright`; the surface field folds in **world height**, since an XZ field is constant down a vertical face.

## The surface

- **Desert varnish** is a threshold of `GradientNoise3` over (bearing, height), behind a `[branch]` on `wallGate * rockMask`.
  - ⚠ Streaks run on the formation's **bearing**: a world-position field breaks into blobs on a leaning wall. A monolith's body always owns the fields it hands back, or a boulder on its wall fans them into a starburst.
  - ⚠ `rib` cuts the radius, so a high `rib` is a gully: varnish and occlusion take the sign that puts them there.
  - Band-limit off the **view ray** (`dist × fwidth(towardsEye)`), not the surface, whose `fwidth` steps per grid row.
  - `VarnishGloss` 0.04 (0.5 painted sunlit streaks white; a glossy plain reads wet), and `VarnishColor` is a warm near-black, because a neutral grey out-reflects a red wall under blue domes.
- **`SoilBounce`** 0.35 (light off `SoilColorPale`, weighted `1 − normal.y`): without it shaded walls go near black under a low sun.
- **The spinifex is cellular**, and a raw `Voronoi2` is a honeycomb. ⚠ Both fixes are needed: warp the domain at about a third of the hummock spacing, and let the same field jitter the threshold. Band-limit the **rim**, not the hummock, and fade to the field's **average cover**, not to bare sand.
- ⚠ Tussocks read as beans until the relief became a low dome `(1 − x²)²` (`SpinifexRelief` 0.06) with a frayed outline and a colour lighter than the sand (darker read as pits).

## Haze, in two stages

One colour cannot do both jobs: the mid-distance keeps the scene's red (else a teal-horizon dome such as 13 paints the far plain green) and the last stretch must reach the dome's exact `HorizonColor` (else a seam). Dust builds quadratically, a murk takes over on a quartic in the final fifth.

- The murk is the horizon's brightness in the dust's hue (`HazeTint`), leaned to by `HazeWarmth`, not `HorizonColor` itself: dome 13's is a teal the dome never draws above the skyline, which gave a teal band.
- The dust takes the sky's brightness in full and only half its hue (`ForestScatterRenderer.ApplySkyTint` run the other way).

## Costs to compare against

- Desktop (5900X / RX 6900 XT), front end, 1600×900, dome 13 (`sky=13`), ssaa 2, `nocap`: 2.26 ms against desert 1.95 and mountain 1.88, the dearest terrain scene here. The order does not hold across machines (on the APU the mountain is dearest), so no APU figure is claimed. No reduced program.
- Testbed, same settings: play camera 2.62 ms, to a near monolith 3.21, from above 3.30.

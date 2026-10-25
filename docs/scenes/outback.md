# The outback

Part of [The scenes](../scenes.md).

## What it is

The thirteenth `SceneKind` (#112, the horizon rebuilt in #967): red monoliths on a wide near-flat spinifex plain, buttressed loaves (Uluru) and clusters of domes (Kata Tjuta), the island in a clearing among them, under a deep blue sky of its own.

- Code: `Outback.fx`, `OutbackBackdrop`, `OutbackRockDiscs`, `OutbackSceneConfig` (groups Terrain, Surface, Air, Birds; the shape is explained in [Space](space.md)), the CPU mirror `TerrainMirror.Outback`. The plain's camera grid is `OUTBACK_GRID_N` 400 over `OUTBACK_EXTENT` 1000; over 255 vertices a side, so the grid cache's 32-bit index buffer is load-bearing.
- Launch: `scene=outback`. No shipped level names it; players meet it as a front-end start and in the Extras scene picker.
- `IsSolidTerrainScene`. **It brings its own dome, 21** (`SkyDome.SceneDefault`): a deep blue zenith paling to a warm white skyline over a gold sun at 21 degrees (azimuth -125, to the left of the opening view, so the formations' shadows fall across the plain towards it). The bottom fifth, the light rig's `HorizonColor` and so the sun's tint, is dome 14's gold a shade warmer. ⚠ Every one of the other twenty drew the outback garish (teal, lilac, magenta or olive over red ground) or flat. ⚠ **Judge its colour in the Game**: the Testbed draws this dome teal-green over a yellow skyline (it does the same to domes 1 and 14), so a Testbed capture says nothing about the outback's colours, only its shapes. It shares the [flock of birds](birds.md).
- ⚠ `ProceduralAmbience.BakeScene`'s `default:` arm is a deliberately near-silent neutral, so a new scene with no bed is audibly missing (the arm once played cave drips on the Moon and vacuum here).
- The Potato path has no outback backdrop: the Pi draws only the island.

## One height field, two kinds of mesh

`OutbackHeight` is the land everywhere (the plain's swell, the monoliths, the boulder outcrops) and `TerrainMirror.Outback` copies it term for term (`mirrorcheck`). Two kinds of mesh draw it:

- **The plain**: the camera grid and the [far ring](shadows-and-far-field.md), technique `Outback`. They carry the swell alone (`PlainHeight`) and clip every pixel inside a formation's **footprint**, an ellipse along its axis (`InsideFootprint`: four hashes and no noise). So the plain, most of every frame, pays for neither rock lattice.
- **The rock**: `OutbackRockDiscs`, one polar disc mesh instanced over every footprint (`TerrainMirror.OutbackFootprints`), technique `OutbackRock`, displaced by the whole field. Four instanced draws: monoliths within 650 of the arena on a fine disc (160 x 80) on High and Ultra (`SceneRenderer.FineMeshes`) and on the coarse one below, farther ones on the coarse one (96 x 40), boulders within 450 on 48 x 16 and farther on 24 x 8, every footprint within 1450 (`SEARCH_RADIUS`). The split by distance is made once at build, so a formation never changes mesh as the camera moves. **Only the footprints in the view are drawn**: each frame they are tested against the camera's frustum and copied into a dynamic instance buffer, because a disc's vertex shader runs the whole height field and most footprints stand behind or beside the lens. A boulder's disc clips where it overlaps a monolith's footprint, so no point is drawn twice.
- ⚠ **Why the rock left the grid.** A near-vertical wall needs several vertices across its fall: the grid's 2.5-unit cells held a wall no narrower than 0.30 of a formation's radius (narrower drew horizontal ripples, one per row of the grid), and past the camera grid (some 500 units from the lens) the far ring's 432 cells round the arena are about fifteen pixels each at 1080p, so a far formation came out as a flat-topped trapezoid with 45-degree sides, the "hats" of #967.
- **No crack and no line where they meet.** A footprint holds its formation apron included, so the land at its edge is the plain on both meshes. Each disc instance is the ellipse scaled by 1/cos(pi/segments) (its rim's chords circumscribe it) plus 0.5 units, and the grid stops on the exact ellipse: the two overlap by a sliver of flat plain and no ray can pass between them.
- The disc is a height-field patch drawn two-sided like the grids, not a solid, so `WindingCheck --openness` does not list it.

## The monoliths

- **Placed on a jittered single-cell lattice** (`RockRoll`/`RollRock`): a formation is held inside its cell by its full reach, so only the pixel's own cell is read. `RockSpacing` 270, `RockChance` 0.70, radius 0.10-0.16 of the cell, elongation to 1.8; a rotated second lattice carries the boulders (`OutcropSpacing` 110). ⚠ **The cell shrank rather than the formations growing** (#171): the margin already sits at 0.44 against the 0.45 clamp, so a larger fraction breaks the single-cell read. #967 moved no formation: the kind is a new roll (`RollF`).
- **The clearing gates the rock per FORMATION**, from its centre (`ClearingRadius` 95): per pixel, a radial ramp slices a monolith and draws it half sunk. The plain's swell is not ramped, so no field with a mean meets a radial ramp.
- **Two kinds**, rolled per formation (`ClusterChance` 0.4):
  - **A loaf** (`Bornhardt`): a superellipse crown, `sqrt(1 - t^p)` with p from 2.0 to 3.4, multiplied to nothing over the last `LOAF_WALL` 0.20 of the radius, and a second lobe for a shoulder. ⚠ At squareness 5 a far loaf read as a table.
  - **A cluster**: three to five round domes (radius 0.46-0.62 of the formation's, p 2.0-2.5, wall 0.42) turned round the centre by the golden angle, the first nearest the middle and the tallest; their union is a max, so the cleft between two is a V of their own walls. `ClusterHeight` 1.0. Each dome's rolls are arithmetic on the cell's rolls, not a hash of their own, and the golden angle is a rotation by literal cosine and sine: ⚠ a hash of a folded constant or a GPU `sin` would move a dome off its mirror.
- **The buttresses.** The gully field is gradient noise on the **unit circle** of the formation's frame (no seam or pole, unlike an `atan2` count), `RibCount` 3.2. A gully is where it crosses zero: `(1 - saturate(|n| x 2.4))^2` has a cusp there and rounds onto the face, so each buttress is a rounded column and each gully a sharp V (every reference drew this). They cut the radius by up to `RibDepth` 0.18, fade out towards the crest, and reach the silhouette. ⚠ The outcrops carry no gullies: at 25 across and 5 tall they drew a starfish.
- **The apron** is 0.05 of the height (`APRON_SHARE`), off the unribbed radius: at 0.10 a far loaf read as a hat with a brim.
- ⚠ **The heat shimmer's noise is keyed to the view ray's direction, never `worldPosition.y`**: on a near-vertical flank `y` is stripes one unit apart.
- The colour selector's weights must land inside 0…1 or the near monolith pins to `RockColorBright`; the surface field folds in **world height**, since an XZ field is constant down a vertical face.

## The formations' shadows

- **Baked once per sun on the GPU** (`OutbackBackdrop.BeforeShadowMaps`, technique `OutbackSunBake`): a `HalfSingle` target over 3072 units round the arena (2048 a side on High and Ultra, 1.5 units a texel; 1024 on Low and Medium), each texel walking 48 steps towards the sun through `OutbackHeight` with a soft edge. It runs in `Backdrop.BeforeShadowMaps`, the first thing `SceneRenderer.DrawShadowMaps` does, before any target of the frame is bound, and again only when the sun moves (a dome change) or the bake's size does (`Services.FineMeshes` turning over with the tier).
- Both meshes read it (`TerrainSunShadow`, 0.9 of the sun) and take the darker of it and the sun's [shadow map](shadows-and-far-field.md), which still holds the island, the gun and the bank near the lens. A sun under `SHADOW_MIN_SUN_HEIGHT` bakes nothing.
- ⚠ **Only the land receives it**: the island, the gun and the cluster read the sun's map alone, which the formations do not cast into. Under dome 21 no formation's shadow reaches the island; a dome with a low sun from another side could lay one up to the island's edge and leave the island lit inside it.

## The surface

- **Desert varnish** is a threshold of `GradientNoise3` over (bearing, height), behind a `[branch]` on `wallGate * rockMask`.
  - ⚠ Streaks run on the formation's **bearing** (a cluster's, on the dome that holds the point): a world-position field breaks into blobs on a leaning wall. A monolith's body always owns the fields it hands back, or a boulder on its wall fans them into a starburst.
  - ⚠ `rib` is high in a gully, and varnish and occlusion take the sign that puts them there.
  - Band-limit off the **view ray** (`dist × fwidth(towardsEye)`), not the surface, whose `fwidth` steps per facet.
  - `VarnishGloss` 0.04 (0.5 painted sunlit streaks white; a glossy plain reads wet), and `VarnishColor` is a warm near-black, because a neutral grey out-reflects a red wall under blue domes.
- **`SoilBounce`** 0.35 (light off `SoilColorPale`, weighted `1 − normal.y`): without it shaded walls go near black under a low sun.
- **The spinifex is cellular**, and a raw `Voronoi2` is a honeycomb. ⚠ Both fixes are needed: warp the domain at about a third of the hummock spacing, and let the same field jitter the threshold. Band-limit the **rim**, not the hummock, and fade to the field's **average cover**, not to bare sand.
- ⚠ Tussocks read as beans until the relief became a low dome `(1 − x²)²` (`SpinifexRelief` 0.06) with a frayed outline and a colour lighter than the sand (darker read as pits).

## What grows on the plain (#749)

The owner: "there is no vegetation at all", only the painted spinifex. `OutbackPlanting` stands on the plain what the references (`749-zimage`, `749-klein`) stand on red earth, through the scene's `PlantPass`, casting into the sun's map: spinifex hummocks (the bulk), silvery saltbush, bleached grey timber lying about, and a few mulga and dead trees (`AcaciaMesh`'s young and dead kinds), two thirds of them at a formation's foot. Nothing stands on a footprint (`TerrainMirror.OutbackFootprints`, the ellipses the plain stops on) or on the island's bank. Its own seed, so nothing else moves.

- **A hummock is a dome of needles** (`OutbackPlanting.SpinifexMesh`): thin triangles from one root out to the dome, each emitted once per face, its normal half the face's and half the dome's, so the hummock shades as a mound and each needle still reads.
- ⚠ **`PlantPass` draws every instance every frame, into the scene and into the sun's map.** At one 180-needle mesh for all of them the first cut cost the plain 2.5 ms. So the hummocks are dense within `SPINIFEX_NEAR_TO` (140) and sparse past it, where the painted spinifex carries the plain; the near band has `NEEDLES` (130) at High and Ultra and the far band's `FAR_NEEDLES` (50) below that; the far band always has 50; every hummock casts as a `CASTER_NEEDLES` (24) stand-in.
- **The saltbush is the sea grape's build** ([The tropical beach](tropical.md), #753): the scrub dome at Low, Medium and on the Potato path, the meadow's shrub with round leaves at High and Ultra.
- ⚠ The timber was bone-white at first, and a log with its stubs read as a toy aeroplane on the red earth: it is weathered grey (`TIMBER`).
- **Cost**, the Game's front end as in the table below (one run each, `sceneseed=3`): `High` 11.95 against 11.45 ms, `Medium` 5.11 against 4.83, `Low` 3.83 against 3.79. In the Testbed at 3840x1600, ssaa 2: a low camera among the hummocks (`campos=-20,-9,40 camtarget=-35,-12,60`) 11.14 against 9.97, the plain from above (`campos=40,10,60 camtarget=120,-12,180`) 14.37 against 13.93.

## The air

**An extinction by distance and nothing else** (#967): the light from a surface falls off as `exp(-d / HazeDistance)` (3000) and the air replaces it, the colour the dome is drawn at the skyline (`FarSkyAt(0)`, what a horizontal ray of endless air looks like) leaned towards `HazeTint`'s hue by `DustStrength` 0.3. A formation 1000 units out keeps seven tenths of its own colour.

- ⚠ **What made the hats.** The two-stage haze before it reached a pale murk by `HorizonHazeDistance` 480 whatever stood there, and the far fade then carried each pixel to the sky drawn *behind* it. Measured under dome 13 (`campos=0,-9,40 camtarget=0,-11,400`): far formations (151,137,138) against the plain in front of them (128,59,42), their red gone (saturation 0.09 against 0.67) at twice the plain's brightness, the farthest the lilac of the dome's skyline band. After it, in the Game under dome 21: a formation (200,133,102), its shaded side (132,97,91), the sky beside it (193,199,210).
- **The rock is hazed towards the sky behind it**, the plain towards the skyline, blended by the rock mask (which fades out over the apron, where the two directions are the same within a degree). ⚠ Hazed towards the skyline band, the brightest sky there is, a formation 1200 units out stood paler than the sky round it.
- **The last fade**: the plain takes the far field's (`FarFadeToSky`, from 600, which hides its edge); the rock takes its own, `ROCK_FADE_START` 1000 to `ROCK_FADE_END` 1450 towards the sky behind it, inside the distance the discs are laid out to, so a far formation keeps its colour and then dissolves rather than standing up as a ghost. Blended by the rock mask, so a disc's own plain matches the grid's at the footprint's edge.
- The air's colour is the drawn sky, never the `HorizonColor` uniform: that one is the dome's bottom fifth, the sun's tint, which some domes never draw above the skyline. Without a far sky (the map editor) it falls back to `HorizonColor`.
- `HorizonHazeDistance` 600 is now only where `FarField` may begin the plain's last fade (and the berm plants' haze distance).

## Costs to compare against

Desktop (RX 6900 XT), the Game's front end on the outback, 3840 x 1581 windowed, `fpscap=1000`, `sceneseed=3 seed=1`, each build under the dome it enters with (13 before #967, 21 after), 44 s runs, the median of the `[fps]` lines from 18 s, before and after alternated three times, the range of the run medians:

| Tier | Before #967 | After |
|---|---|---|
| `High` | 11.69 - 12.15 ms | **11.14 - 11.24** |
| `Medium` | 4.88 - 5.02 | **4.78 - 4.88** |
| `Low` | 3.81 - 3.83 | **3.73 - 3.79** |

- **Cheaper on every tier**: the plain no longer evaluates either rock lattice (four times a pixel before), which pays for the discs' vertices. ⚠ **Without the frustum cull the discs cost**: drawing every footprint every frame measured `Low` 3.74-3.84 to 4.14-4.26 and `Medium` 4.85-5.02 to 5.21-5.28 (the vertex shader runs the whole field on some 530 000 vertices); the cull and the coarse disc below `High` took it back.
- The bake is once per sun and not in these figures: one pass at the scene's first frame and on a dome change.
- Not measured on the APU or the laptop.

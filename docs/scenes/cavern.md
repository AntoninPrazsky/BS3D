# The cavern

Part of [The scenes](../scenes.md).

The eleventh `SceneKind`, one of the sky-replacing scenes ([The sky-replacing family](space.md#the-sky-replacing-family)): an irregular limestone hall over a still black lake, its vault pocketed and hung with curtains of stalactites, flowstone columns standing in the water, clusters of translucent crystals glowing cyan and magenta on rock islands, at the shore and on ledges up the walls, a constellation of glowworms overhead and two shafts of pale light falling through cracks in the vault. It plays under The Reveal (levels 51-60); `scene=cavern` on either command line, the Game's scene menu and random pick.

**Every surface is real geometry**, built once on the CPU and shaded by `Cavern.fx`: `CavernChamber` (the hall's shape and where everything stands), `CavernGeometry` (vertex arrays at a detail, no device), `CavernMeshes` (the buffers), `CavernBackdrop` (the draw) and `CavernSceneConfig` (named groups `Rock`, `Water`, `Air`, `Crystals`, `Lighting`). The lake is at `LevelY` -34, **below the drain's kill plane**, so a ball that falls through the funnel is culled before it reaches water it can never splash. `OpenBelow` includes it; it is not an `IsSolidTerrainScene`.

## The hall (`CavernChamber`)

- **The shell is one star-shaped surface round a centre 24 units over the lake** (`CENTRE_ABOVE_WATER`): every direction out of the centre meets it once, at `ShellRadius(u)`, so it is closed by construction. Its mesh is an equi-angular cube sphere (five faces; straight down, the shell stands under the lake's bed), so it has **no pole and no seam**: the shape is a function of the direction alone, the faces meet on directions they share, and every vertex's normal is taken by central differences over a tangent frame that is itself a function of the direction.
- **What the radius is a function of is what makes it a cave and not an oval** (#778): an elongated plan (`HallRadius` 180, `Elongation` 1.3, turned by the seed), a vault `VaultHeight` 104 over the lake, and in the log of the radius, low frequencies first and strongest: lobes and bays a fifth of the radius and more, bulges, lumps, and creased crags a couple of units across. Domed **pockets** in the vault off a cellular field; **bedding ledges** on the walls, each bed rolling its own depth (squared, so most are slight) and only patches of wall carrying them (⚠ evenly ledged, the walls banded like contour lines); three **passages** at the waterline, soft cones along which the shell runs back into the dark.
- **The lake's bed is a height field of its own** (`FloorHeight`): deep in the middle, rising to beaches where the shore is gentle and staying under the water where the wall plunges (a slow field round the hall decides which), rubble along every waterline, and rising on past the wall, hidden behind it, so the bed closes the hall from below. Seven **rock islands** stand out of the water between the arena and the walls. Where the wall stands at the waterline is a table of 720 bearings built at construction (`WallAtWater`).
- **Placement** is all off the seed: crystal clusters on most islands, on beaches at the wall's foot and on ledges up the walls, where the play camera sees them over the island (twelve at most, one light each); five columns clear of each other and of the islands; stalactites hung in **curtains** along lines on the vault, the longest in the middle of a row (#757), and as singles where a slow drip field says the rock drips, never on a patch of vault that does not face down; stalagmites on the islands and the beaches; the glowworms on the vault's downward faces in patches; the two shafts slanting down to the lake beside the arena.
- **Nothing stands in the arena's air**: no formation's tip, its lean included, within `ARENA_AIR_RADIUS` (52) of the axis below `ARENA_AIR_TOP` (62), and no column or cluster inside that radius. `CavernChamberTests` holds it over four seeds, and holds every vertex of every mesh a number with a unit normal: ⚠ one NaN normal is a black blot the size of the bloom's kernel on the screen, which is what a stalagmite's crown collapsed to a point once drew.
- **`Clearance(p)`** says about how far a point stands from rock, water, a formation or a crystal: what the chapter intro checks its lenses against.
- **The seed is `Rock.Seed` plus the renderer's seed offset**, so a launch can roll another hall; `sceneseed=0` pins the shipped one.

## Built off the frame

- `CavernBackdrop` starts the build on a worker thread when the renderer is made (the chamber, then both details' arrays) and makes the buffers on the first draw at each detail. It logs `[cavern] hall built off the frame in N ms`: about 1.8 s on the desktop, long done before the cavern is first drawn. A caller that asks for `SceneRenderer.CavernChamber` sooner waits for it.
- **Two details** (`CavernGeometry.Detail`, chosen per draw by `SceneRenderer.FineMeshes`): High and Ultra's (176 cells along a cube face's edge, about 247 000 rock vertices, every stalactite, formations 14 facets round) and Low and Medium's (104 cells, the stalactites under 8 units left out, 9 facets).

## Drawing it (`Cavern.fx`)

`CavernBackdrop.Draw` draws the mirrored hall into the lake's reflection, then the formations and the rock (opaque and depth-writing; the formations first, so the shell behind them is rejected before it is shaded), the crystals (translucent), the glowworms and the shafts (added).

- **Under supersampling the hall is shaded at the display's resolution** into a target of its own and scaled up (`DrawsAtDisplayResolution`, #155), which for meshes is a target **with a depth buffer and four samples** (`DisplayResolutionDepth`, `DisplayResolutionMultiSampleCount`), so the stalactites' silhouettes keep the edges supersampling would have given them. At `ssaa` 1 (Low, Medium) the hall draws straight into the scene target, multisampled like everything else there, and its depth is the scene's.
- **The lake is not a mesh.** Every surface under the waterline shades itself as seen through the water (`SeenThroughWater`): the view ray crosses the surface on its way down, so the pixel is the surface's reflection over what the water lets through of the bed. With no water plane nothing fights the bed for depth along a shoreline, and the shore is continuous by construction: at the waterline the bed's colour and its reflection meet. Deep water does not shade the bed at all (a data branch on the path's optical depth).
- **The reflection is the hall itself**: drawn through the camera mirrored in the water's plane into a target half the size each way (`RockMirrored`, `FormationMirrored`, `CrystalMirrored`: cheaper programs that clip everything under the waterline; the culling flips, because a mirror flips the winding), sampled at the pixel's own screen position, bent a little by the ripple and **drawn out into vertical streaks** (seven taps down the screen, longer where the water stirs and towards grazing), which is what a lake's ripples do to every light over it. Fresnel over a floor of 0.2: a cave lake is a black mirror, because nothing comes up out of its depths. A lens under the water draws no reflection and reads a dark ramp.
- **The light is the crystals'**: every cluster is a point light (twelve slots, its colour on the cluster's own breath, paled half way to white: ⚠ at its own saturation it painted the columns magenta and teal), with a little wrap, a wet highlight and an inverse-square falloff that halves at 15 units. Over that, the vault's dim cool fill from above, the lake's teal from below fading with height, the vault's own faint glow where the glowworms hang (`VaultGlow`), the pools the shafts throw where they land and the shafts' openings blazing in the vault (`ShaftOpening`). Then distance fog into a dark blue-violet and the lake's steam, an exponential height fog integrated in closed form.
- **The rock**: five octaves of 3D relief from fifteen units down to a hand's width, each faded as its period nears the pixel, perturbing the mesh normal through `PerturbNormalFromHeight`; creased joints where the program is full; the mesh's own occlusion (a vertex in a recess of the relief, the bed's hollows, the wall's foot) times the relief's; sheets of paler flowstone streaked down the steep walls in patches; wet and darker within a few units of the water.
- **The formations** are calcite: cream and ochre in faint growth bands of uneven spacing (⚠ a band of one period read as a candy stripe), paler and wetter towards a stalactite's tip, which glows a little at its rim against the dark.
  - **Stalactites** are lathes round their own axis with the root buried in the rock: a taper, drip rings, lumps off world-space noise (so two of one shape are two formations), a soda straw at the tip, in three shapes (slender ringed, fat carrot, straw-tipped needle). **Stalagmites** have rounded crowns, never a point.
  - **Columns**: a stalagmite and a stalactite grown into one, a stack of rounded plates broad at the foot and shallow up the shaft, the stalactite's drapery folding down out of the vault, ribs of ridged noise down its length, broad lumps of wet flowstone for its relief, and darker where its head meets the vault it grew out of. ⚠ A smooth trunk fanned into the vault read as a crooked tree with fireflies round it (the owner's note that started #958), fine vertical relief on a column read as bark, a single ring frequency as a corkscrew and a regular `cos(n theta)` as a turned post.
- **The crystals** (#756) are six-sided prisms with pyramid tips, six to eleven to a cluster from one base at different lengths and leans. **Translucent**: each pixel traces the view ray through its own prism analytically (thirteen planes: six sides, six pyramid faces, the foot; the prism's frame rides on its vertices), so the light crosses a thick core and a sliver at the edges, where the cave shows through. A milky core where the ray passes the axis, veils and fractures sampled inside along the path, striations, bright arrises and a breathing pulse; the faces reflect the clusters' highlights. Blended `One, SourceAlpha`: the frame behind is kept by the transmittance and the crystal's light added over it.
- **The glowworms** (#507) are sprites, one quad a worm on the real vault, spread in the vertex shader by a size **in pixels** (1.6 at 1080 lines): a world-sized dot at the far end of the hall is a fraction of a pixel, and a field of those crawls. ⚠ **A world-sized dot on rock seen edge-on was the stretched "firefly" the owner saw.** Each is lifted a hand's width towards the lens in world units: ⚠ a bias in depth held at the near vault and put the far worms in front of the columns between them and the lens, because the depth buffer's resolution falls with the square of the distance.
- **The shafts** are cones round each beam drawn with no culling, the shader keeping only the far side (so the beam counts once whether the lens is outside it or in it): the light a ray gathers is the beam's gaussian at the ray's closest approach to its axis over the path it runs along it, enveloped down its length and broken by drifting dust.

### Formation depth (#755)

The arena draws after the backdrop. When the hall was shaded into the display-resolution target its depth stayed there, so `DrawDepth` draws the formations depth-only into the scene's own target, only for a lens off the axis by more than `ARENA_AIR_RADIUS` (the play camera and the menu's orbit never pay) and scissored to the arena's projected box (the island's radius + 4, from under the funnel to y 40). At `ssaa` 1 the hall's depth is already the scene's and nothing is drawn.

## Tiers

- **Low** (`SceneDetail` reduced) draws no mirror (the lake reads a dark ramp and the clusters' glints) and the reduced programs `RockReduced` and `FormationReduced`, without the creased joints and the finest octave.
- **Low and Medium** draw the coarse meshes (`FineMeshes` off); High and Ultra the fine ones.
- **The Potato path** (`PotatoBackdrop.Cavern`) draws the same hall off the same `CavernChamber` on the launch's seed at `Detail.Potato`: a shell of 18 cells a face edge and the bed in the rock's colour, the columns and up to fifty of the longest stalactites and stalagmites on 6 facets, every cluster's prisms in a nearly black casing radiating the cluster's colour, the lake as a plane with a little of its glow, under the cave's rig. No mirror, no glowworms, no shafts.

## The arena in the hall's light

- **The nearest clusters light the island, the gun and the balls** through the scene lights on the instanced effect (`SceneRenderer.CavernArenaLights`, [Scene point lights](../rendering/scene-lights.md)): every cluster within 130 of the axis, four at most so a blast's flash always finds a slot, on the breath its prisms glow on. `CavernCrystalConfig.ArenaLight` scales them; 0 keeps the arena on the rig alone.
- **The rig** (`CavernLightingConfig`) is dim and cool, the ground bounce carrying the lake's teal up onto the island's underside.

## The chapter intro

`Game/Effects/CavernIntroShots.cs`: *the hall* (an arc high on one side, looking across the hall low over the far water), *the crystals* (a glide four units over the lake onto a cluster at the water, its reflection under it), *the vault* (a slow rise towards a stalactite curtain a cluster lights, looking up at the vault it hangs from) and *the underside* (a crane under the island's funnel over the lake). **Every path is checked against the hall before it is used**: a shot rolls its place and keeps it only if every point stands clear by its margin (`CavernChamber.Clearance`: 8 units for the hall, 2.5 for the crystals, 4 for the vault), with a fallback near the axis where the hall is open air. `CavernChamberTests` flies six rolls over four seeds and holds every pose of every shot clear.

## Shaded at the display's resolution (#155)

**The cavern and the dream are shaded into a target the size of the back buffer and scaled up into the supersampled scene target** (`SceneRenderer.DrawBackdropAtDisplayResolution`; a backdrop opts in with `DrawsAtDisplayResolution`), a quarter of the pixels at `ssaa` 2.

- **Why:** both are pixel-bound backdrops covering the whole frame, and pixel count is the dial that moves them: the dream at fullscreen 3840x1600 `High` went from 18.6 ms to 5.8 on the reference 6900 XT ([The dream](dream.md)).
- **What it gives up is the backdrop's supersampling, not its resolution.** The scene still comes out at every pixel the display has, and the balls, the arena and the gun keep every sample they had. The cavern's meshes take four samples on that target for their edges (above).
- ⚠ **Measure such a backdrop by the back buffer's size, never with `ssaa`**: `ssaa` does not reach it, so an `ssaa` sweep says it costs nothing.
- ⚠ **Space must not take this path**: it sizes its stars in output pixels off `SceneRenderer.SupersampleFactor`, so shading it at display resolution would change how big and bright they come out.

## Cost

**Cheaper than the analytic pass it replaced, on every tier.** The Game, 3840x1600 fullscreen, `nocap`, desktop (RX 6900 XT), the whole frame, the minimum of the run medians of alternating runs against the build before the rebuild (`sceneseed=0 seed=1`):

| Pose | Tier | Before | This hall |
|---|---|---|---|
| `level=Chest` (the play pose) | High | 7.18 ms | 6.71 ms |
| `level=Chest` | Ultra | 7.16 | 6.71 |
| `level=Eleven`, after its intro | High | 7.04 | 6.70 |
| `level=Chest` | Medium | 6.20 | 4.81 |
| `level=Chest` | Low | 3.67 | 3.62 |

- The chapter intro peaks at about 11.4 ms on its last leg, over the island, where the analytic pass peaked at 11.2.
- **The geometry**: about 247 000 rock vertices at the fine detail; the coarse formations are 213 000 triangles (`Tools/WindingCheck`); everything opaque is drawn twice, once mirrored. The Potato path's hall is about 51 000 vertices a frame in 15 draws.

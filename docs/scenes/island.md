# The island: shape, material and berm

Part of [The scenes](../scenes.md).

The round stone island the gun stands on, in every scene. `Prazsky.Core.Render.IslandMesh` builds it, `ArenaIsland` assembles it; the Testbed and Game draw it, the map editor does not. Its shading and cost: [The island's surface and its cost](island-surface-and-cost.md); per-family dressing: [Dressing](island-dressing.md); the drain at its centre: [The drain funnel](drain.md).

## Structure and the rules that hold it

- **`ArenaIsland.TOP_Y` is the plane of the stone top's outer arris.** The walkable top falls `DISH_DEPTH` from it to the drain so a landed ball rolls in; the drain's rim, gold bead and the pit's mouth share the lower plane `TOP_Y - DISH_DEPTH`, and the collision floor runs between by construction. The top runs from `FUNNEL_TOP_RADIUS` out to `RADIUS`; the platform drops `EDGE_HEIGHT`.
- ⚠ **The shaping is all downward.** The physics floor mirrors the walkable top, so anything raised above it is a lip balls pass through. Every moulding (chamfer, coping wash, bullnose, drip, string course, plinth) is cut *below* the top face.
- **An edge reads through the hemisphere ambient, not a shadow**: an upward chamfer takes the sky (bright ring), the overhang's underside the ground bounce (dark ring). That pair is most of what makes a platform rather than an extruded circle.
- ⚠ **The plinth flares rather than tapers**, because its foot covers the hole the solid-terrain scenes cut in the ground (`TerrainHoleRadius`); the island floats half a unit over it, and a narrower foot opens a slot at grazing angles where the sky shows as a pale ring.
- ⚠ **The physics ring stops at `FLOOR_RADIUS`** (`IslandMesh.FloorRadius`, the dish's arris), not `RADIUS`: carried to the platform's widest point it would hold a ball on air over the coping.
- **Two meshes, two materials**, built from one polyline so they cannot drift: the stone cap and the concrete drum (cast: no joints, coarser relief, barely any reflection). `LatheMesh` is a surface of revolution with per-point creases and an optional radial irregularity that is a function of angle and *world height*, so two lathes sharing an edge are displaced identically. Triangles wind clockwise from outside, so ordinary back-face culling applies and shows a winding mistake.

## Material per scene (#404)

`ArenaIsland.LookFor(scene)` gives the cap's and drum's albedo, polish (`SpecularAmbientStrength`) and slab size, read off each scene's ground (sandstone, basalt, ice, coral limestone, marble, polished concrete, machined plates...). The meadow keeps the authored stone (`DEFAULT_LOOK`). **Material only**; geometry follows the shape.

- The colour rides the renderer's per-draw diffuse tint, which reduces the authored material to luminance times 1.25, so `TintFor` divides that out and the wanted albedo lands exactly.
- ⚠ **The look is resolved per draw from the scene `DrawIsland` is handed**, never pushed on a scene switch: callers set the scene in several places (startup argument, level load, scene page) and one that forgot to push would stand in the wrong scene.
- The glass is untinted everywhere ([The drain funnel](drain.md)). The change costs nothing measurable.

## Shape per scene (#533)

`ArenaIsland.ShapeFor(scene)` picks one of eight `IslandShape`s; `IslandMesh` builds a cap-and-drum pair for each.

- **Basalt** (volcano): two hard tiers, the drum's joint grid cut as columns (`ReliefFor`). **Ice** (polar, aurora): a lip turning under into a hollow, edge wandering, no joints. **Coral** (beach, sea): soft nose, pitted drum, no joints. **Machined** (space, grid): flat to a 45-degree chamfer, true cylinder, panel lines, no wander. **Plinth** (cities, big top): hard arris, straight side, expansion joints on top. **Monolith** (outback): a leaning sweep down to a splayed foot, flutes bent by `DrumWarp` so no two run parallel (a runnel is not a tile). **Pad** (Moon): a thin slab with a spoil bank spreading past the rim, craters as geometry ([Dressing](island-dressing.md)). **Stone** (the rest): the authored stone.

**What a shape may not move:** the arris at `IslandMesh.FloorRadius` stays a true circle at y = 0 (or the drawn floor reaches past the collision and a ball sinks), nothing stands above y = 0 at the rim, the bore's lip (where the gold bead closes) never wobbles, and the foot stays within a fifth of a unit of the rim underneath, or outside it (the pad's bank), to cover the terrain hole. `RADIUS`, `TOP_Y` and `FLOOR_RADIUS` never change, so `GameCameraFit`, the gun's walk, the drain and the plate read what they always read. A wandering rim wanders on cap and drum alike (`LatheMesh`'s seam rule).

**Swapping is free**: all pairs are built at load and `InstancedModelRenderer.SetMesh` re-points the two renderers, leaving material, relief and the renderers' places in every host's sky-lit list untouched (the volcano, with the most joints, measured +0.08 and -0.06 ms: the floor).

## No paving, and the desert's drift (#550)

- **The stone island is an outcrop** (`STONE_RELIEF`): no joint grid on top, a coarser deeper relief instead. The slab coursing came from a castle shader long gone and was never a design (the owner: nothing about it needed keeping). Only the plinth's expansion joints and the machined disc's panel lines are designed and stay (`IslandLook.Slab` is read by those two shapes).
- ⚠ **Joint grids never wander.** A grid bent by noise was ruled out ("wavy paving whatever it is called"); `SlabWarp` stays only for runnels on a drum. A crater is geometry, not texture.
- **The desert's drift is a mesh** (`SandDriftMesh`, own renderer, desert only, sky-lit): a bank against the face the wind (`DesertSceneConfig.Wind`, reversed) arrives from, `DRIFT_HEIGHT` up it, `DRIFT_REACH` out onto the sand, over `DRIFT_HALF_ARC` either side. Its inner edge (`DRIFT_INNER_RADIUS`) is inside the drum so no seam opens whatever the drum's wobble does; its toe is sunk a third of a unit. ⚠ **It receives the sun's map and does not cast**: a low sheet under a grazing sun shadowed itself as a checker of acne.

## The island sits in the ground: `IslandBerm` (#608, #646)

The drum used to stop on the grass with a hard line ("it does not sink into the ground"). `IslandBerm` builds a bank round the foot as geometry in world space off the scene's ground mirror: its inner ring rides up the drum (hidden inside it), its outer edge dips a quarter unit under the terrain so there is no gap. **Two meshes** (the plant material takes one colour a draw): an earthen band against the drum and a cover (turf, snow, dust, sand) over the rest.

- **Scenes:** meadow, savanna, forest, mountains, Mars, outback, aurora, tropical beach. The storm and dream float; the desert has its drift, the polar sheet its ice in ice.
- **Drawn through the scene's scatter** (`MeadowScatter`, `SavannaScatter`) so it casts and receives the sun's map. Every other planted scene (aurora, forest, Mars, mountains, outback, tropical beach) owns an `IslandBermPlanting` drawn and cast through `PlantPass` (the meadow's plant draw, shared by nine backdrops).
- ⚠ **`PlantPass` once left `BlendState.Opaque` set**; as the environment's last draw in those scenes, every translucent surface after it (the gun's window pane, the drain's glass) drew solid until #667.
- Things must stand *on* the bank: `IslandBerm.SurfaceY` answers its height, and scatters take the larger of it and the ground. References put grass against the stone and stones past the foot (`STONE_RADIUS_MIN`..`STONE_RADIUS_MAX`).
- ⚠ **Judge the bank's colour against the scene's own ground in the issue's frame**: moss read as a bright ring on the forest floor, Mars's pale pigment as a tan ring, and the aurora's bank glared white under the dome's sun because that ground is lit by its sky's glow, so `PlantPass` takes the scene's own lights there.

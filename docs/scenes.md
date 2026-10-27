# The scenes

The twenty-one backdrops the Game, the Testbed and the MapEditor all share, plus the island the gun stands on and the glass drain in the middle of it.

**Each scene is one `Backdrop` class under `Prazsky.Core/Render/Scenes`, registered in `SceneCatalog` and hosted by `SceneRenderer`**, and the setting around it (the island, the ceiling plate, the sky light, the scene lights, the scatters) exists in one copy that no executable keeps for itself. Read [Backdrops and the scene registry](scenes/backdrop-architecture.md) before adding or changing a scene, and the one document of the scene you touch. **A terrain's height is mirrored on the CPU** (`TerrainMirror`, what the scatters, lamps and lenses are placed by): after touching a terrain shader's height or its mirror, run `mirrorcheck` on every mirrored scene. **The MapEditor never loads `Sky.fx`**, so a per-scene sky feature cannot live there.

## Sub-documents

### Shared by every scene

- [Backdrops and the scene registry](scenes/backdrop-architecture.md) — what a scene is (`SceneCatalog`, `Backdrop` and its services), the traps for whoever adds the next scene, and how to check a scene change against captures.
- [The setting, in one copy](scenes/shared-setting.md) — what the executables share (island, ceiling plate and the cables it hangs by, sky rig, scene lights, scatters) and what stays with each one.
- [The island: shape, material and berm](scenes/island.md) — the arena island's structure and rules, its material and shape per scene, the unpaved variants and the berm.
- [The island's dressing per scene family](scenes/island-dressing.md) — the dials and the five dressing families, with their costs.
- [The island's surface and its cost](scenes/island-surface-and-cost.md) — surface shading, what the arena costs and the traps of measuring it.
- [The drain funnel](scenes/drain.md) — the glass funnel, its gold rims, its physics, the terrain hole and the pit shaft.
- [Shared effects: the wind, the launch seed, the fire](scenes/shared-effects.md) — the one wind, why every launch differs, and the campfire.
- [The sun's shadows and the far field](scenes/shadows-and-far-field.md) — the scenes' side of the sun shadows and the far field past the terrain.
- [Terrain grids, billboards and the CPU terrain mirrors](scenes/terrain-grids-and-mirrors.md) — the shared terrain grids, and the CPU mirrors that `mirrorcheck` checks against the GPU.
- [The scenes' observation points](scenes/viewpoints.md) — what each scene points the lens at, and the traps.
- [The flock of birds](scenes/birds.md) — the `BirdFlock` service: real geometry, the flap in the vertex shader, placement and cost.
- [Auditing the scenes against references](scenes/auditing.md) — the audit (#671) and where its findings went.

### The scenes

- [The city](scenes/city.md) — layout and draw, the window rhythm, the towers, the rooftops and the street.
- [The sea](scenes/sea.md) and [The sea under the surface](scenes/sea-underwater.md) — the surface, its shading and spray; what happens to a ball and the lens below the water.
- [The savanna](scenes/savanna.md), [The savanna's trees and plain](scenes/savanna-trees.md), [The savanna's campfires](scenes/savanna-fires.md) and [Savanna trails and planting](scenes/savanna-trails.md) — the plain, the acacias, the fires and the paths that bend round trees.
- [Space](scenes/space.md) and [The star lattice](scenes/space-stars.md) — the sky-replacing family, the marched volume, and the stars.
- [The dream](scenes/dream.md) — what holds the surreal scene together, and its glare policy.
- [The cavern](scenes/cavern.md) — the hall built on the CPU (shell, lake bed, formations, crystals, glowworms, shafts), the lake that mirrors it, the tiers and the intro.
- [The volcano](scenes/volcano.md) — the ground as the light, the flows, the eruption, the fumaroles, and the land past the field (#748).
- [The desert](scenes/desert.md) — the desert scene: `Desert.fx` dunes as geometry (`DuneField`, sharp crests and the rounding rules, `DUNE_MEAN`), the braided static sand ripples and their traps, the dust haze, the generated skies of domes 6 and 14, and its costs
- [The mountains](scenes/mountains.md) — the mountain scene: the ridged massing and its 32-bit-index trap, snow against rock, fluting, alpenglow, the rock relief and the snow sparkle, the generated dome 8, and `Snow.fx` (the premultiplied-blend trap, the lens-made flake, the snowfall veil)
- [The mountain's frame cost](scenes/mountain-cost.md) — why the "38 FPS mountain" was the present path (`FrameLimiter`, `fpscap=`), the mountain's real fullscreen cost and what is left of it, the other scenes' figures, and the two benchmark traps it left
- [The meadow](scenes/meadow.md) — the meadow scene: its planting, the path to a knoll, the brook into a pond, the old trees grown as twig sprays, the free-standing shrubs on stems, the flowers and the grass material, and its costs
- [The neon city](scenes/neon-city.md) — the relight of the city: the neon technique, the dark facades and the Arcade chapter's dome, and why its cost needs `fpscap=`
- [The forest](scenes/forest.md) — the forest scene: the shared wood (`ForestScatter`, `ForestScatterRenderer`), its variants and tints, the first build's traps, the two floor programs, the litter floor, `ForestShadow`, and its costs
- [The savanna's plant volumes, grounding and tree feet](scenes/savanna-plant-volumes-and-feet.md) — savanna rules that sat inside the old forest section: the slab volumes that keep plants apart (#653), planting trees at the lowest ground under the ring (#658), and the roots, soil heap and bark stain at every foot (#670)
- [The Moon](scenes/moon.md) — the Moon scene (terrain-and-sky-replacing at once): draw order, the single-cell crater field and its traps, the highland belt, the Earth, the low sun, the rocks planted on `TerrainMirror.Moon`, and its costs
- [The outback](scenes/outback.md) — the outback scene: its own dome 21, the plain and the formations as two kinds of mesh over one height field (the polar discs, the footprints), buttressed loaves and dome clusters, their baked cast shadows, the rock's surface (varnish, bounce, spinifex), the extinction haze, and its costs
- [The tropical beach](scenes/tropical.md) — the tropical scene: land, lagoon (`Sea.fx` clone) and palms draw order, the waterline profile, the sines-only height field, the palm mesh and its per-draw sway, tall and real palms, and its costs
- [The storm](scenes/storm.md) — the storm scene: why the clouds are billboard volume and never a height field, the cell shape and the premultiplied blend, the wind-aligned drifting band, the strike (schedule, the camera-aware cell pick, the vertex-shader channel, the glow lag), the thunder, and its costs
- [Mars](scenes/mars.md) — the Mars scene: what it reuses from the Moon and the outback, the stone field, the moons, mesas and materials, the haze distance that decided the mesas' colour, the big-crater lattices, and the reduced tier and costs
- [The polar icesheet](scenes/polar.md) — the polar scene: white in reflection and cyan in transmission, sastrugi as a surface, the tilted-plate pressure front, crevasses and blue ice sized to the grid, the dome choice, the open owner calls, and its costs
- [The aurora](scenes/aurora.md) — the aurora scene, rebuilt in #963: the snowfield (drifts, the bank, the mound map round the planting, two grids with no clip), the wood and the far stand, the night sky, the Moon and the arcs of the aurora, the light, the mist, the chapter intro, the Potato stand-in, and its costs
- [The Grid](scenes/grid.md) — the Grid scene: the flat floor and chained Hilbert curve, per-axis line filtering, the solids (placement, seams, face shading, Game of Life boards), the landmark ring, and its costs
- [The big top](scenes/circus.md) — the circus scene: the analytic tent, the depth-only floor and king poles, whole-period patterns, the spots, lamps, bulbs and footlights, the `SceneLights` slot budget, and its costs

## Where the old sections went

Code comments cite sections of this document by title; this table resolves them. The full text before the diet (#839) is `git show ad7af939:docs/scenes.md`.

| Old section | Now in |
|---|---|
| Auditing the scenes against references (#671) | [Auditing the scenes against references](scenes/auditing.md) |
| The setting, in one copy | [The setting, in one copy](scenes/shared-setting.md) |
| The city and the glass arena | [The city](scenes/city.md); the island and the arena in [The island](scenes/island.md) and [The island's surface and its cost](scenes/island-surface-and-cost.md) |
| The drain per scene, and the analysis that got it wrong (#404, second pass) | [The drain funnel](scenes/drain.md); the island's material per scene in [The island](scenes/island.md) |
| The island's shape per scene (#533) | [The island](scenes/island.md) |
| The island dressed: the volcanic and underground family (#535) | [The island's dressing per scene family](scenes/island-dressing.md) |
| The island dressed: the built family (#537) | [The island's dressing per scene family](scenes/island-dressing.md) |
| The island dressed: the cold family (#534) | [The island's dressing per scene family](scenes/island-dressing.md) |
| The island dressed: the coastal family (#536) | [The island's dressing per scene family](scenes/island-dressing.md) |
| The island dressed: the off-world and arid family (#538) | [The island's dressing per scene family](scenes/island-dressing.md) |
| The island without its paving, and the desert's drift as geometry (#550) | [The island](scenes/island.md) |
| The drain funnel (Testbed) | [The drain funnel](scenes/drain.md) |
| The shared scene backdrops | [Backdrops and the scene registry](scenes/backdrop-architecture.md), and the sub-documents below it |
| The wind, in one copy (#276) | [Shared effects](scenes/shared-effects.md) |
| Every launch is a different city, wood and plain | [Shared effects](scenes/shared-effects.md) |
| A campfire burns like a fire (#468) | [Shared effects](scenes/shared-effects.md) |
| A path goes round a tree (#476) | [Savanna trails and planting](scenes/savanna-trails.md) |
| The sun's shadows, in one copy (#469, #470, #471) | [The sun's shadows and the far field](scenes/shadows-and-far-field.md) |
| The far field (#551) | [The sun's shadows and the far field](scenes/shadows-and-far-field.md) |
| The terrain grids and the billboards, in one copy (#589) | [Terrain grids, billboards and mirrors](scenes/terrain-grids-and-mirrors.md) |
| The terrain mirrors, in one copy and checked against the GPU (#590) | [Terrain grids, billboards and mirrors](scenes/terrain-grids-and-mirrors.md) |
| The backdrop classes and the scene registry (#580) | [Backdrops and the scene registry](scenes/backdrop-architecture.md) |
| The scenes' observation points (#289) | [The scenes' observation points](scenes/viewpoints.md) |
| The sea (Testbed) | [The sea](scenes/sea.md), [The sea under the surface](scenes/sea-underwater.md) |
| Checked against photographs, and retuned (#503) | [The sea](scenes/sea.md) |
| The savanna (Testbed) | [The savanna](scenes/savanna.md), [trees and plain](scenes/savanna-trees.md), [campfires](scenes/savanna-fires.md) |
| Space (Game) | [Space](scenes/space.md), [The star lattice](scenes/space-stars.md) |
| Redrawn from references (#505) | [Space](scenes/space.md) |
| The dream (Game) | [The dream](scenes/dream.md) |
| Redrawn from references (#506) | [The dream](scenes/dream.md) |
| The cavern (Game) | [The cavern](scenes/cavern.md) |
| The volcano | [The volcano](scenes/volcano.md) |
| Redrawn from references (#509) | [The volcano](scenes/volcano.md) |
| Fumaroles on the lava field (#679) | [The volcano](scenes/volcano.md) |
| The land past the field (#748) | [The volcano](scenes/volcano.md) |
| The desert (Testbed) | [The desert](scenes/desert.md) |
| The sky, from references (#681) | [The desert](scenes/desert.md) |
| The mountains (Testbed) | [The mountains](scenes/mountains.md) |
| Redrawn from references (#504) | [The mountains](scenes/mountains.md) |
| The sky, from references (#661) | [The mountains](scenes/mountains.md) |
| The snowfall, from references (#654) | [The mountains](scenes/mountains.md) |
| The "38 FPS mountain" was not the mountain (#270) | [The mountain's frame cost](scenes/mountain-cost.md) |
| The same signature explained four more reports (#209, #167, #166, #165) | [The mountain's frame cost](scenes/mountain-cost.md) |
| The meadow (Testbed) | [The meadow](scenes/meadow.md) |
| The neon city (Testbed) | [The neon city](scenes/neon-city.md) |
| The forest | [The forest](scenes/forest.md) |
| The savanna keeps its plants apart by their volumes as well, since #653 (bold lead-in inside "The forest") | [The savanna's plant volumes, grounding and tree feet](scenes/savanna-plant-volumes-and-feet.md) |
| The trees' feet, since #670 (bold lead-in inside "The forest") | [The savanna's plant volumes, grounding and tree feet](scenes/savanna-plant-volumes-and-feet.md) |
| The Moon (Game) | [The Moon](scenes/moon.md) |
| Redrawn from references (#508) | [The Moon](scenes/moon.md) |
| Rocks, on a mirror of the plain (#680) | [The Moon](scenes/moon.md) |
| The outback | [The outback](scenes/outback.md) |
| The tropical beach | [The tropical beach](scenes/tropical.md) |
| Detail and variety, from references (#445) | [The tropical beach](scenes/tropical.md) |
| Tall palms (#555) | [The tropical beach](scenes/tropical.md) |
| Real palms, not paper (#557) | [The tropical beach](scenes/tropical.md) |
| The storm | [The storm](scenes/storm.md) |
| Checked against photographs at last (#510) | [The storm](scenes/storm.md) |
| Mars | [Mars](scenes/mars.md) |
| A second references pass, off a playtest complaint (#638, 2026-09-26) | [Mars](scenes/mars.md) |
| Big craters and low hills (#638, the verdict of 2026-09-29) | [Mars](scenes/mars.md) |
| The polar icesheet | [The polar icesheet](scenes/polar.md) |
| Checked against photographs (#511) | [The polar icesheet](scenes/polar.md) |
| The aurora | [The aurora](scenes/aurora.md) |
| A boreal night, from references (#462) | [The aurora](scenes/aurora.md) |
| A taller wood, from references (#647) | [The aurora](scenes/aurora.md) |
| The Grid | [The Grid](scenes/grid.md) |
| The lineage drawn on directly (#512) | [The Grid](scenes/grid.md) |
| The big top (Game) | [The big top](scenes/circus.md) |
| The relief on the cap (a paragraph cited by code) | [The island's surface and its cost](scenes/island-surface-and-cost.md) |
| What #609 costs on the APU (a paragraph cited by code) | [The meadow](scenes/meadow.md) |

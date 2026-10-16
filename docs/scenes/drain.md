# The drain funnel

Part of [The scenes](../scenes.md).

The centre of the island is a **glass funnel** shot balls fall into: they run down the cone, drop through the hole below the platform and the kill plane (`KILL_PLANE_Y`, below `ArenaIsland.FUNNEL_BOTTOM_Y`, so they fall a visible distance) removes them. The island itself: [The island](island.md).

## The glass

- **`FunnelMesh`**: a single-sided truncated cone from a rim flush with the dish's low inner lip (`TOP_Y - DISH_DEPTH`, radius `FUNNEL_TOP_RADIUS`) down to a hole at `FUNNEL_BOTTOM_Y`, on the drain family's own matrix (`_drainWorld`). Translucent at `FUNNEL_GLASS_ALPHA`, `CullNone` (reads from above and below), and one of the four renderers `ArenaIsland` hands the caller's sky-lighting pass.
- ⚠ **Back faces flip their shading normal (`TwoSidedNormals`, #291)**: otherwise the cone from below (open-below scenes, the drop cinematic's dive) read as a bright milky sheet under the grazing sky sheen. The uniform is 0 for every other renderer; the pit sets it too.
- ⚠ **The wall is `FUNNEL_GLASS_BANDS` concentric bands spaced geometrically (#423)**: one quad per facet was a 7.8:1 trapezoid whose interpolation does not follow the azimuth and drew a fan of light and dark wedges. The drawn cone has `FUNNEL_DRAWN_SEGMENTS` segments, apart from the physics' `FUNNEL_SEGMENTS` (at 64 the gold edge read as a polygon).
- ⚠ **The drawn glass stands outside the collision cone at the tip (#762)**: coincident sheets of no thickness let any contact penetration put a cap of ball through the glass from below. The drawn hole is `FUNNEL_DRAWN_HOLE_RADIUS` = `FUNNEL_HOLE_RADIUS` + `FUNNEL_GLASS_CLEARANCE` (mouth unchanged); the physics keeps `FUNNEL_HOLE_RADIUS`, so no ball's path changes. **Penetration grows with the pile**: 0.025 in a small drop, 0.055-0.069 under a Cut's 240-311 balls (lower third, r about 4), so the clearance went 0.1 to 0.2 (2026-10-07; no ball crossed in 900,000 samples). Re-measure with a probe over a big drop (`level=112 seed=3 powerups=cut:4` with `aim=`/`cut=`/`fire=`) if you touch it.

## The gold rims

`FunnelRimsMesh`: a polished-gold band round both circles in one mesh (shared drain matrix, one renderer); it is what makes the drain read at a glance.

- **Bands lie flat on the surface that meets them**: the top one on the dish at `DISH_GRADE`, the bottom one on the cone at its own grade. (Tori stood half a unit proud at the junction every released ball crosses.) Visual only.
- **A shallow crown with buried edges**: edges sink `EDGE_SINK`, mid-width rises `SURFACE_LIFT`. The lift stops coplanar z-fighting; a fully floating band showed a strip of surface under its inner edge.
- ⚠ **At the mouth, burying the edge draws the stone strip it was buying off (#237)**: the surface sits in front of the gold, moving its visible boundary in by `halfWidth * EDGE_SINK / (EDGE_SINK + SURFACE_LIFT)` (about 0.22 units of bare stone). The stone has to be covered, so the top band **wraps the lip**: it turns the crease and runs `COLLAR_DROP` down the glass, sunk into the glass. The mouth ring stands `LIP_LIFT` off the stone (the crease is coplanar with both).
- ⚠ **The collar's end sinks `COLLAR_SINK`**: at `EDGE_SINK` it crossed the dark pit sheath at a slope of ~0.15 and depth precision drew teeth along the inner edge in every solid-terrain scene. Moving the pit away was rejected (the collar then showed through the glass as a pale band).
- **Opaque, drawn before the glass, `CullNone` is load-bearing** (a zero-thickness annulus has one face); `ArenaIsland.DrawGlass` puts both draws in one `CullNone` sandwich. ⚠ **The Game draws the bands earlier, with the island** (`ArenaIsland.DrawRims`, from `BeginSceneDraw` right after the pit; `DrawSettingGlass` then passes `drawRims: false`): the savanna's fires and the fireworks are drawn straight after the setting, translucent and writing no depth, so bands drawn with the glass were tested against a buffer with no flame in it and painted the ring over a flame standing nearer the lens (the front end's wide leg looks across the fires' ring at the funnel). The funnel's glass stays after, and the ceiling glass's copy of the frame is taken after the bands, so it holds them. The Potato path keeps the bands in `DrawGlass` (it draws no fires, and its collar needs the glass first); the Testbed does too (it draws its fires after the glass). **Metallic**: `InstancedModel.fx`'s `Metalness` uniform (set every `Draw`, 0 elsewhere) makes the reflection F0 the specular colour, so gold reflects the sky in gold; `FUNNEL_RIM_SPECULAR` goes in as a per-draw override, `FUNNEL_RIM_COLOR` keeps it visible under a dark dome.

## Why the ring is tuned for three scenes (#404, second pass)

The band must separate from the cap it rings.

- ⚠ **The albedo analysis (CIEDE2000, hue) was convincing and wrong.** It condemned the warm rocks; **sampling real frames** (ring camera `campos=0,-1,24 camtarget=0,-9,0`, 1216x832, `nopost nooverc sceneseed=0`, ring against cap, two stations checked on a marked-up frame) says the failures are desert 5.7, tropical 7.9 and savanna 13.1 dE, while Mars 24.1 and outback 24.9 are fine (meadow 15.8).
- ⚠ **The sampler lied first**: its top station sat on the stone above the band. It was caught because the patch came back byte-identical between two builds whose pictures visibly differ.
- **The mechanism is polish**: the rims draw at `SpecularAmbientStrength` 1 with `Metalness` 1, a mirror that bleaches under a bright dome over pale stone. `BLEACHED_SKY_POLISH` (0.15) for the beach, desert and savanna in `ArenaIsland.DrainLookFor` lifts them to 23.6, 13.3 and 14.4 dE with the other scenes unchanged. Steel would be worse; the gold is kept in every scene.
- ⚠ **The response is V-shaped, so sweep a dial end to end** (desert: 1.0 gives 5.7, 0.45 gives 5.5, 0.15 gives 13.3). The glass is left alone everywhere. Later dressing passes kept every scene within a dE or two; 13 is where the desert failed.

## Physics

`Prazsky.BS3D.Physics.FunnelPhysics.Build` is a Bepu triangle `Mesh` and the island's whole floor: the cone wall plus the dished ring out to `FLOOR_RADIUS`, **double-sided** (each quad's triangles in both windings) so a ball collides whatever Bepu's one-sided convention, built from the very figures `ArenaIsland` draws so the two cannot drift. A ball on the ring rolls down the dish (spheres have no rolling resistance, so the ~6.4 degree grade is plenty), runs down the ~55 degree cone and falls through the hole; the dish is what made big maps drain, since on a flat ring a ball parked. ⚠ A ball that came to rest in the cone would be sleep-culled and vanish where it sits. A huge simultaneous release (the `End` debug key) briefly floods the funnel and spikes physics.

## The terrain hole and the dark pit shaft

Every `IsSolidTerrainScene` is a flat clearing at the island's foot whose ground would slice across the funnel under its rim (the city has nothing below, #275; the sea drains into water).

- **The hole:** the terrain shaders `clip()` the island's footprint (`IslandHoleRadius`, from `SceneRenderer.TerrainHoleRadius` = `TERRAIN_HOLE_RADIUS`, a couple of units inside the rim). ⚠ **It is 0 in the map editor** (no island); making an island available to a host must not hand the editor one.
- **The shaft:** a hole alone made it worse (the ~55 % glass showed bright sky-haze through, a glass ring on the ground), so a near-black pit (`DrawPit`, solid-terrain scenes only, opaque, `CullNone`, before the glass) backs it: a two-band `FunnelMesh` sharing the mouth, `PIT_SHEATH_CLEARANCE` outside the glass at the hole, then a narrow throat down to `PIT_BOTTOM_Y`, so a ball runs into darkness and is culled inside the throat. Near-matte (`SpecularAmbientStrength` 0.03), visual only.
- ⚠ **The pit must hug the glass at every depth**: a wide cone hides behind the dish's stone ring and leaves the bright hole showing, and until #291 a single straight cone to `PIT_BOTTOM_Y` read from under any terrain as a long dark spike ("the outer shell doesn't match the inside").

# The setting, in one copy

Part of [The scenes](../scenes.md).

Everything that stands in every scene is one copy in the libraries (#75), because a *shared* setting must not be able to differ between executables (the forest was once missing from both hand-kept `IsSolidTerrainScene` lists).

## What is shared

- `Prazsky.Core.Render.ArenaIsland`: stone cap, concrete drum, glass drain with its gold rims, the dark pit shaft, with every mesh, texture, renderer, matrix and figure ([The island](island.md), [The drain funnel](drain.md)).
- `CeilingPlate` (the glass a cluster hangs from); `SkyLightRig` (dome-derived light rig, `SKY_TINT_STRENGTH`, overcast palette); `SceneLights` (a scene's point lights); `FunnelPhysics`; `CloudField`'s look values; `ForestScatterRenderer` (the forest's wood: "The forest" in `docs/scenes.md`).
- Every question about a `SceneKind` (`IsSolidTerrainScene`, `OpenBelow`, `SceneCount`, `NextScene`, `SceneName`, `TryParseScene`) is a static on `SceneRenderer`, answered from `SceneCatalog` ([Backdrops and the registry](backdrop-architecture.md)).

## What stays with each executable

- **The frame sequence.** `ArenaIsland` hands its draws over in three slices, `DrawIsland`, `DrawPit`, `DrawGlass`, so each executable places them itself (the Game draws the gun, cluster and shot trails between the pit and the glass). A fourth, `DrawRims`, lifts the gold bands out of `DrawGlass` (`drawRims: false`) for a caller whose translucents must stand in front of them: the Game's, right after the pit, so a fire nearer the lens than the ring is not painted over (see [The drain](drain.md)).
- **Sky-lit enrolment** is each caller's own list (the map editor lights the balls, city and forest scatter; it has no island). **Every list leaves the pit shaft out**, so no dome bleaches the inside of a hole in the ground.
- **The ceiling glass is in neither list.** It stands against the sky itself, so the hosts call `SkyLightRig.ApplyToGlass`, which scales the three directional lights to the sky's own brightness; a sky-replacing scene's full-strength sun turned the plate into a bright slab over a near-black backdrop. The Game's plate renderer may be null mid-load; the push tolerates it.
- ⚠ **Scatter renderers** are walked as the one flat array `ForestScatterRenderer.Renderers`. A re-planted wood is thirty brand-new renderers, so whoever re-plants must re-light it, as a refitted `CeilingPlate` must be.

## The ceiling plate is cut crystal

`CeilingPlate` is a `CutSlabMesh`: the box with its edge ground in facets, `EDGE_PROFILE` in section (`BEVEL` under, a straight band, a three-facet `CROWN` over), each corner cut in `CORNER_FACETS` flat cuts round `CORNER_RADIUS`.

- The two caps still lie in the box's faces, so `FootprintFor`, `THICKNESS`, `TopFaceY`, `CentreYAbove`, the Bepu `Box`, the floor net, the camera fit and the HUD's side-cut bar read what they always read.
- **The top's cut is evaluated in the shader**, not modelled (it needs the facet normal where a ray *leaves* the slab): a border of fine V flutes (`RIM_BAND`, `FLUTE_PERIOD`, `FLUTE_SLOPE`, `RIM_LEAN`), a groove, and a diamond cut (`CUT_PERIOD`, `CUT_SLOPE`); `GlassRimAt` finds the outline analytically.
- ⚠ **The underside stays flat**: the ceiling socket anchors a ball's top to it and the top level's outermost balls touch it at least half a unit in (the underside's rim crosses a corner diagonal 0.38 in from both sides, where the corner ball touches 0.5 in).
- **Only the Game refracts** (it alone hands the renderer `GlassBehind`; "And the glass in it bends what is behind it" in `docs/rendering.md`); the Testbed and editor draw a plain translucent pane. It also casts a faint shadow ("The ceiling's glass shadow" in `docs/rendering.md`).

## Where two things meet (#974)

**The rule: where two things meet, the join is one continuous surface or shading. No open end, no part floating or buried, no edge where parameters jump between two meshes.** A player sees a trunk that ends in a ring you can look into, a palm that stands on a dune with a lip of tube under it, a twig whose base is a tilted ring on a branch's disc, and calls the game "AI slop"; none of it shows in a frame at the distance the play camera keeps, and all of it shows in a close-up and in the drop cinematic.

- **Meshes are held by a gate.** `Tools/WindingCheck --openness` builds every procedural mesh of every scene and exits 1 on one with an exposed open ring that its table (`Allowed`, with the reason of each row) does not name ("`--openness`" in `docs/formats-and-tools/checking-tools.md`). A new mesh builder is added to the tool's `Sources`; a new object drawn as several buffers to its `Assemblies`; an open end that is open on purpose to `Allowed`, with what hides it. The logic tests hold each closed construction without a device and show the old one found (`SeamAuditClosureTests`, `SavannaMeshClosureTests`, `MeadowMeshClosureTests`).
- **How an end is closed, by construction.** A run of wood is one `TubeGeometry.AddSweep` through all its points, a ring at every bend shared by the two runs and mitred to both; a free end is a disc fanned from its own ring's vertices, a point or a dome; what starts on an end starts **inside** it. A foot is sunk and closed with a floor, so a slope that falls away from it shows a skin and not a tube. A ring of facets that two meshes share has the same facet count in both (the lunar island's body takes its cap's finer cut). A tube round a closed path takes the same ring at both ends (`AddSweptTube(closed: true)`).
- **A prop that rests on the ground is placed by `GroundSettling`**, not at the height of its middle: see `docs/scenes/savanna-plant-volumes-and-feet.md` (the logs, the rocks and the mounds of the savanna are the ones placed so far).
- **Joins that are not meshes have no open edge to count** and are each their own subject: the brook's end against the pond, the tropical shore, snow on a slope, the sea's platforms, a cavern's ceiling against its wall. They are looked at in a close-up at the join, from both sides, not found by a gate.

## Rules the reconciliation left

- ⚠ **`InstancedModelRenderer.GroundHeight` is `ArenaIsland.TOP_Y` for the balls (`BallRenderSet`'s default); do not "restore" the Testbed's old -9.49** (a deleted ground block's height: it halved a resting ball's contact darkening). The balls' anchor stays `TOP_Y` even though the walkable top dishes below it, because the shader's term saturates under it; the gun's renderers anchor each frame to `ArenaIsland.FloorHeightAt(radius)`, the dish under the wheels.
- **`OpenBelow` is the complement of `IsSolidTerrainScene`**, not a second list: the opaque pit shaft those scenes need closes the view from below, so a scene wanting the shaft cannot also give the drop cinematic a vantage under the stone. (The Moon is in both `IsSolidTerrainScene` and `ReplacesSky`: "The Moon (Game)" in `docs/scenes.md`.)
- **`SceneFrame.SunColor` and the clouds' lit side are one number**, `SkyLightRig.SunRadianceTinted`, taken off the **dome** and never off `KeyTint` (a sky-replacing scene states its own key tint because a dome-derived one would be a lie). The cloud hook is set once at load on the rig: a method group at the call site allocates a delegate per frame.

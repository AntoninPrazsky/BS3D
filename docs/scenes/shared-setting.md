# The setting, in one copy

Part of [The scenes](../scenes.md).

Everything that stands in every scene is one copy in the libraries (#75), because a *shared* setting must not be able to differ between executables (the forest was once missing from both hand-kept `IsSolidTerrainScene` lists).

## What is shared

- `Prazsky.Core.Render.ArenaIsland`: stone cap, concrete drum, glass drain with its gold rims, the dark pit shaft, with every mesh, texture, renderer, matrix and figure ([The island](island.md), [The drain funnel](drain.md)).
- `CeilingPlate` (the glass a cluster hangs from) and `CeilingRigging` (the cables and winches it hangs by, #896); `SkyLightRig` (dome-derived light rig, `SKY_TINT_STRENGTH`, overcast palette); `SceneLights` (a scene's point lights); `FunnelPhysics`; `CloudField`'s look values; `ForestScatterRenderer` (the forest's wood: "The forest" in `docs/scenes.md`).
- Every question about a `SceneKind` (`IsSolidTerrainScene`, `OpenBelow`, `SceneCount`, `NextScene`, `SceneName`, `TryParseScene`) is a static on `SceneRenderer`, answered from `SceneCatalog` ([Backdrops and the registry](backdrop-architecture.md)).

## What stays with each executable

- **The frame sequence.** `ArenaIsland` hands its draws over in three slices, `DrawIsland`, `DrawPit`, `DrawGlass`, so each executable places them itself (the Game draws the gun, cluster and shot trails between the pit and the glass). A fourth, `DrawRims`, lifts the gold bands out of `DrawGlass` (`drawRims: false`) for a caller whose translucents must stand in front of them: the Game's, right after the pit, so a fire nearer the lens than the ring is not painted over (see [The drain](drain.md)).
- **Sky-lit enrolment** is each caller's own list (the map editor lights the balls, city and forest scatter; it has no island). **Every list leaves the pit shaft out**, so no dome bleaches the inside of a hole in the ground.
- **The plate's rigging is in every list** (`CeilingRigging.Renderers`, the same four renderers for the host's life: built once, only placed by `Fit`). It is steel in the scene's light, not glass.
- **The ceiling glass is in neither list.** It stands against the sky itself, so the hosts call `SkyLightRig.ApplyToGlass`, which scales the three directional lights to the sky's own brightness; a sky-replacing scene's full-strength sun turned the plate into a bright slab over a near-black backdrop. The Game's plate renderer may be null mid-load; the push tolerates it.
- ⚠ **Scatter renderers** are walked as the one flat array `ForestScatterRenderer.Renderers`. A re-planted wood is thirty brand-new renderers, so whoever re-plants must re-light it, as a refitted `CeilingPlate` must be.

## The ceiling plate is cut crystal

`CeilingPlate` is a `CutSlabMesh`: the box with its edge ground in facets, `EDGE_PROFILE` in section (`BEVEL` under, a straight band, a three-facet `CROWN` over), each corner cut in `CORNER_FACETS` flat cuts round `CORNER_RADIUS`.

- The two caps still lie in the box's faces, so `FootprintFor`, `THICKNESS`, `TopFaceY`, `CentreYAbove`, the Bepu `Box`, the floor net, the camera fit and the HUD's side-cut bar read what they always read.
- **The top's cut is evaluated in the shader**, not modelled (it needs the facet normal where a ray *leaves* the slab): a border of fine V flutes (`RIM_BAND`, `FLUTE_PERIOD`, `FLUTE_SLOPE`, `RIM_LEAN`), a groove, and a diamond cut (`CUT_PERIOD`, `CUT_SLOPE`); `GlassRimAt` finds the outline analytically.
- ⚠ **The underside stays flat**: the ceiling socket anchors a ball's top to it and the top level's outermost balls touch it at least half a unit in (the underside's rim crosses a corner diagonal 0.38 in from both sides, where the corner ball touches 0.5 in).
- **Only the Game refracts** (it alone hands the renderer `GlassBehind`; "And the glass in it bends what is behind it" in `docs/rendering.md`); the Testbed and editor draw a plain translucent pane. It also casts a faint shadow ("The ceiling's glass shadow" in `docs/rendering.md`).

## What the plate hangs from (#896)

**A steel cable at each corner of the plate, rising straight up out of the picture to a crane nobody sees, and a winch at its foot that pays it out** (`CeilingRigging`, the owner's choice C of four drawn concepts, 2026-10-18). The reason it is cables: nothing of them stands where the gun walks or the lens orbits. The plate never rotates and the cables leave it upwards, so from any camera under them they run off the top of the frame and end in empty sky (`CABLE_RISE`, 600 over the top face).

- **The winch** stands `CORNER_INSET` in from both edges, on the flat top inside the crown's grind (`CeilingRiggingTests` holds that for every footprint from 10 to 17 cells). It is a drum (a closed lathe, 880 triangles: two flanges and the core with eight wraps of cable, a little uneven so its turn shows), an axle and two cast-iron pillow blocks. The drum's axle lies across the corner's diagonal, so the cable leaves its outer side and rises clear of the glass.
- **The drum turns with the plate's height and nothing else**: `DrumTurn(y) = -y / CORE_RADIUS`, so a plate lower by one unit has let out one unit of cable, from the side the cable leaves. It is a pure function of the pose: it cannot drift, needs no clock, and the brake's lift winds it back by the same law. `CeilingRiggingTests` follows a point of the core through a step and fails on the opposite sign.
- **Drawn with the setting, before the host grabs the copy the plate's glass bends** (`GrabCeilingBackground`). From under the plate the winches stand behind the glass and must be in that copy. The Game draws one rigging per plate (the session's, the front end's preview), and the Testbed draws one with its opaque scene. It casts no sun shadow.
- ⚠ **From the play camera it is nearly out of sight**: the plate fills the top of the frame and the rigging stands over it. It reads from the result page's swing-out, the menu's wide orbit, an intro's long shots, and any camera above or beside the plate.
- **Cost**: four instanced draws a plate (cables, drums, axles, blocks). The cable and the axle are one 48-triangle rod scaled per instance; side normals are radial and cap normals axial, so a scale along the axis and an equal one across leaves them true.

## Where two things meet (#974)

**The rule: where two things meet, the join is one continuous surface or shading. No open end, no part floating or buried, no edge where parameters jump between two meshes.** A player sees a trunk that ends in a ring you can look into, a palm that stands on a dune with a lip of tube under it, a twig whose base is a tilted ring on a branch's disc, and calls the game "AI slop"; none of it shows in a frame at the distance the play camera keeps, and all of it shows in a close-up and in the drop cinematic.

- **Meshes are held by a gate.** `Tools/WindingCheck --openness` builds every procedural mesh of every scene and exits 1 on one with an exposed open ring that its table (`Allowed`, with the reason of each row) does not name ("`--openness`" in `docs/formats-and-tools/checking-tools.md`). A new mesh builder is added to the tool's `Sources`; a new object drawn as several buffers to its `Assemblies`; an open end that is open on purpose to `Allowed`, with what hides it. The logic tests hold each closed construction without a device and show the old one found (`SeamAuditClosureTests`, `SavannaMeshClosureTests`, `MeadowMeshClosureTests`).
- **How an end is closed, by construction.** A run of wood is one `TubeGeometry.AddSweep` through all its points, a ring at every bend shared by the two runs and mitred to both; a free end is a disc fanned from its own ring's vertices, a point or a dome; what starts on an end starts **inside** it. A foot is sunk and closed with a floor, so a slope that falls away from it shows a skin and not a tube. A ring of facets that two meshes share has the same facet count in both (the lunar island's body takes its cap's finer cut). A tube round a closed path takes the same ring at both ends (`AddSweptTube(closed: true)`).
- **A prop that rests on the ground is placed by `GroundSettling`**, not at the height of its middle: see `docs/scenes/savanna-plant-volumes-and-feet.md` (the logs, the rocks and the mounds of the savanna are the ones placed so far).
- **Joins that are not meshes have no open edge to count** and are each their own subject: the brook's end against the pond, the tropical shore, snow on a slope, the sea's platforms. They are looked at in a close-up at the join, from both sides, not found by a gate.

## Rules the reconciliation left

- ⚠ **`InstancedModelRenderer.GroundHeight` is `ArenaIsland.TOP_Y` for the balls (`BallRenderSet`'s default); do not "restore" the Testbed's old -9.49** (a deleted ground block's height: it halved a resting ball's contact darkening). The balls' anchor stays `TOP_Y` even though the walkable top dishes below it, because the shader's term saturates under it; the gun's renderers anchor each frame to `ArenaIsland.FloorHeightAt(radius)`, the dish under the wheels.
- **`OpenBelow` is the complement of `IsSolidTerrainScene`**, not a second list: the opaque pit shaft those scenes need closes the view from below, so a scene wanting the shaft cannot also give the drop cinematic a vantage under the stone. (The Moon is in both `IsSolidTerrainScene` and `ReplacesSky`: "The Moon (Game)" in `docs/scenes.md`.)
- **`SceneFrame.SunColor` and the clouds' lit side are one number**, `SkyLightRig.SunRadianceTinted`, taken off the **dome** and never off `KeyTint` (a sky-replacing scene states its own key tint because a dome-derived one would be a lie). The cloud hook is set once at load on the rig: a method group at the call site allocates a delegate per frame.

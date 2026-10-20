# The Grid

Part of [The scenes](../scenes.md).

The twentieth `SceneKind` (#393): an early-1980s computer-graphics backdrop drawn from **named mathematics rather than noise**, flat-shaded, cheap because of that. Made of `GridBackdrop`, `Grid.fx`, `GridSceneConfig` (groups Terrain, Towers, Landmark, Lighting) and `GridLife`. Launch: `scene=grid` (`tron` parses the same). It has a chapter since #420.

**In both families, after the Moon and the aurora**: `IsSolidTerrainScene` (flat floor, footprint cut out, pit shaft) and `ReplacesSky` (no dome, black clear, its own rig through `TryGetLightRig`); see "The sky-replacing family" in [Space](space.md).

## Rules and traps

- **⚠ Pin `sceneseed=` for any A/B**, or it is two different scenes (the Testbed re-seeds per run); `sceneseed=0` is the shipped arrangement. **⚠ `fpscap=400` measures nothing here** (both halves sat at the cap): use `fpscap=2000`.
- **Light comes only from the seams**: a near-black body lit by its own bright seams (grid lines, the Hilbert trace, every solid's edges), fed to the same bloom (`GLARE_THRESHOLD`). The model is TRON's backlit compositing; the solids are plain `BoxMesh` prisms.
- **Never an `atan2`/azimuth construction** (spirals, rings): an unwrapped angle jumps at its wrap line (Mars's gullies, the aurora's ribbons).
- **Backdrop-only**: balls, island and gun stay on the ordinary lit path, tinted by `GridLightingConfig`'s cool rig; there is no emissive shading mode. No seventh `SceneLights` branch and no `TryGetSceneEvent`. The viewpoint is low and close (overhead flattens the pattern to a swatch). The ambience is an electrical hum (explicit `case SceneKind.Grid` in `ProceduralAmbience.BakeScene`).

## The floor

- **It is genuinely flat**: `TerrainHeight` is the constant `GridLevelY`, the normal always up, so the mesh is coarse (`GRID_MESH_N` over `GRID_EXTENT`) and there is **nothing to reduce**: no reduced-tier technique. The sky is `VoidColor` and a dither (no `Stars.fxh`), not exactly zero (a frame at zero reads as a hole).
- **A Hilbert curve traced along a plain grid.** `GridHilbertIndex` is `xy2d` unrolled over `GRID_HILBERT_BITS` (a 64-node tile). Nodes are lattice **vertices** on every `TraceStride`-th line, and a segment is lit where its two end nodes are **consecutive** along the curve: three index lookups a pixel.
  - **⚠ Light the segment, not the cell boundary**: lighting the boundary between consecutive cells, which the curve crosses at right angles, gave a ladder of rungs and thousands of dead ends. A trace is widened (`AccentWidthScale`), or it is invisible from play distance.
  - **The tiles chain**: lighting the extra link `GRID_HILBERT_LAST` to 0 makes each row of tiles one infinite curve (every node exactly two lit segments, no seam). `GridMod` is a bitwise AND, valid only for a power-of-two tile.
- **Lines are filtered per axis**: one isotropic `fwidth` smeared lines running away from the camera into a bright band. `GridAxisFootprint` measures each axis; `GridLineMask` never draws a line thinner than a pixel but dims it by the share it covers, fading to average coverage once a pixel spans half the spacing. The solids' seams use it too.

## The solids

- **Placement.** `GridTowerConfig`: `Count` solids on a ring (`RadiusMin`/`RadiusMax`), drawn from `Seed` so every executable stands the same ones; `BuildGridTowers` retries up to twenty times. `CubeFraction` are large near-cubes (so a face shows most of a 32x32 board), the rest towers: a tall narrow prism reads as a building. Every solid has a roof (free cameras look down).
- **⚠ Keep clear of the landmark ring as a hard rule.** The ring is built after the solids, so four seeds in ten put a solid on it. `BuildGridTowers` asks `GridLandmarkShape` (what `BuildGridLandmark` builds from) and rejects draws within a 15-unit gap of the ring's footprint (`GridRing.FootprintDistance`), up to 200 draws.
- **⚠ A solid needs seams, or it is a pattern floating in the dark**: `BodyColor` is within a few codes of the void, so every edge of every face glows (`EdgeWidth`, `EdgeColor` past the glare threshold; each vertex carries its face-local position and size, `FaceLocal`).
- **⚠ One value a face, or a cube is a wireframe, not a volume**: `GridTowerVertex` carries the outward normal and the body is `BodyColor * (floor + (1 - floor) * N.L)` against `FaceLight`, the floor well above zero so a face never vanishes into the void.
- **Life on the windows.** `GridLife` (`SIZE` 32; a row is one `uint`, so a generation is carry-save addition of shifted rows, `StepRows`, bit-exact, zero allocation). Each board ticks every `LifeStepInterval` on a staggered phase.
  - **⚠ Named patterns, not random soup**: soup on a 32x32 torus settles stale and random flips wreck oscillators. Each board is stamped, centred, from a deck (convoys, pentadecathlons, pulsars, Kok's galaxy, methuselahs, glider fleets), turned so ships travel up or down a tower. Nothing in a deck has period 1 or 2.
  - **A board moves on rather than being stirred**: stale for four generations, it takes its next pattern; one also yields after `LifePatternGenerations`. Boards are seeded from the placement stream.
  - **The board wraps round the solid like a label**; ⚠ **centre on the pattern, not the board** (an odd-width pattern sat half a pane aside): `GridLife.CentreX`/`CentreY` report where it landed and `GridLifeCentreOffset`, pushed per draw, corrects it. A cell alive only a generation ago fades as `exp(-age / PhosphorDecay)` (`GridLifeAge` per draw).
  - **One shared buffer, one draw call per solid** (`_gridTowerRanges`): only `GridLifeTexture` and `GridLifeAge` change between calls, which is what forces a call per solid.

## The landmark ring

`GridLandmarkConfig`: a ring on edge, far out, with its own Life round its band: the one silhouette nothing else here has (every other solid is a box). A faceted torus of `Segments` trapezoid segments, facing the arena. **⚠ Segment joins must not glow, or the ring is a barrel of ribs**: the seam shader lights all four borders of a quad, so each quad reports a face-local X pinned mid-way across a wide face and only the long borders draw, as two rails.

## Costs

Desktop, Testbed, `sceneseed=0`, 3840x1600 ssaa 2, `nopost nooverc`, `fpscap=2000`: **1.81 ms** at the play pose, 1.26 where the ring fills a third of the frame; the flat shade and the landmark were free.

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

BS3D is a 3D Puzzle-Bobble-style game (shoot balls at a hanging 3D cluster of balls) built with MonoGame (DesktopGL) and BepuPhysics 2. Repo: https://github.com/AntoninPrazsky/BS3D — tasks are tracked in GitHub issues.

## Build and run

```powershell
# Build everything (libraries + Testbed game)
dotnet build C:\Projects\Testbed.sln

# Run the game testbed / the map editor
dotnet run --project C:\Projects\Testbed\Testbed.csproj
dotnet run --project C:\Projects\MapEditor\MapEditor.csproj

# Testbed can load a map right at startup (useful for testing)
Testbed.exe C:\Projects\Testbed\Maps\Full.json
```

There are three solutions: `BS3DLibs.sln` (libraries only), `Testbed.sln` and `MapEditor.sln` (each executable plus the libraries). There are no test projects and no lint configuration.

MonoGame content (`Content/Content.mgcb` in Testbed and MapEditor) is compiled automatically during build by `MonoGame.Content.Builder.Task`; editing the .mgcb itself is normally done with the MonoGame Pipeline Tool. Testbed and MapEditor target `net10.0-windows` and use WinForms interop, so the executables are Windows-only; the libraries target plain `net10.0`.

## Architecture

Three layered libraries under `BS3DLibs/`, consumed by two executables:

- **Prazsky.Core** — game-agnostic 3D infrastructure: `BasicCamera3D`/`ICamera`, model/bitmap/info renderers, `InstancedModelRenderer` (GPU instancing), `SkyDome`, `World3D`, and `Tools/Constants.cs` (named float constants like `HALF`, `SQRT_TWO` used throughout).
- **Prazsky.BS3D** — game logic without physics: the ball grid (`GameStructure/BallsMap.cs`), `StaticBall`, `BallType`, JSON map (de)serialization via Newtonsoft, input helpers, `Cannon`.
- **Prazsky.BS3D.Physics** — BepuPhysics representation: `PhysicsBall` (body reference + constraint handle slots + array position) and `BallsConstraintsBuilder` (builds the constrained ball structure).
- **Testbed** — the actual playable game loop: simulation setup, shooting, contact handling, backdrops, HUD. Test maps live in `Testbed\Maps\*.json`.
- **MapEditor** — visual editor for those map JSON files (supports drag-and-drop of a map file onto the window).

### The ball grid

The central data structure is a 3D array `[x, z, level]` where **level is the vertical (Y) axis**. Odd levels are shifted by +0.5 in X and Z (hexagonal-like packing); vertical spacing between levels is `1/√2`. `BallsMap.GetRealPosition` maps array coordinates to world positions, and `Center()`/`ComputeCentered` translate between the raw grid frame and the centered world frame. A cell has 4 neighbours on its own level and up to 4 on each adjacent level; which diagonal offsets are the true neighbours depends on level parity (`GetNeighbouringCells` encodes this).

`BallsMap` holds the logical state (`StaticBall`s); `BallsConstraintsBuilder.BuildBallsStructure` mirrors it into a parallel `PhysicsBall[,,]` of dynamic Bepu bodies connected by `BallSocket` constraints to their neighbours, with the top level constrained to a kinematic ceiling body. The whole cluster hangs from the ceiling and jiggles physically.

The play field is larger than the initial ball layout: map JSON stores field dimensions (`sx`/`sz`/`l`) separately from the layout array, the layout is placed at the **top** of the field and the empty bottom levels are room for shot balls to attach into. Legacy map files without field dimensions get 5 extra bottom levels on load. The Testbed ceiling repositions/resizes itself to the loaded map (`FitCeilingToMap`).

### Constraint handle bookkeeping

Each constraint is shared by two balls, so `PhysicsBall` stores handles in three slot groups (`HandlesBottom`/`HandlesMiddle`/`HandlesTop` = balls below / same level / above + ceiling; four slots each, filled via `TryStore` in no particular order — a ball touches at most four neighbours per group). Every pair gets exactly one constraint by construction: the build pass connects same-level pairs only towards +X/+Z from each ball, and cross-level pairs only from the **even** (unshifted) level of each pair — adjacent levels always differ in parity, and neighbour index offsets depend on it (`diagonalShift`). The runtime attach path for a newly shot ball (`AttachBallToStructure`) instead connects all four same-level directions plus both adjacent levels, since the new ball has no constraints yet. Anchor offsets must be rotated into body-local space (`WorldToLocalOffset`) since bodies no longer have identity orientation once the simulation has run.

### Shooting flow (Testbed)

Contact detection uses `Testbed/Contacts/ContactEvents.cs` (adapted from the Bepu demo); Bepu callbacks fire on worker threads, so contact events are queued and processed on the main thread. When a shot ball hits the structure, it is snapped into the nearest free neighbouring cell (`BallsMap.PutBallAtClosestEmptyPositionNextTo`) and then wired into the physics structure with `AttachBallToStructure`.

### Ball rendering (Testbed)

Balls are procedurally generated spheres (`SphereMesh` in Prazsky.Core) drawn with GPU instancing at three LOD resolutions picked by camera distance: `CollectBallInstances` fills one bucket per `BallType` × LOD (no camera culling — off-screen balls must still cast shadows) and `InstancedModelRenderer` issues one `DrawInstancedPrimitives` call per bucket. Per-instance data (`ModelInstance`, TEXCOORD1–5) carries the world matrix plus the ambient-occlusion vector (direction towards occupied neighbour cells + base factor, recomputed every frame from the grid). The shader (`Testbed/Content/Shaders/InstancedModel.fx`) does per-pixel Blinn-Phong with a positional key light ("sun"), hemisphere ambient tinted by the sky dome palette (`SkyDome` extracts zenith/horizon colours from the dome's vertex colours; `ApplySkyLighting` distributes them, `DefaultLighting` holds the base rig), directional AO, and a per-type diffuse tint (`GetDiffuseTintByType` — material colours reduced to luminance × tint). Shadows: an `InstancedDepth` technique renders balls into a 2048² shadow map from the key light; `ShadowOverlay.fx` darkens a translucent quad on the ground plane by that map. The scene objects (ground, ceiling, cannon, castle) render through the same shader as single-instance draws (`InstancedModelRenderer.Draw(camera, world, effectParams)` with per-part material colours/specular read from the model), so the whole scene shares one lighting model; `SkyLitRenderers()` enumerates every sky-lit renderer for `ApplySkyLighting`. The ceiling is a procedurally generated translucent glass box (`BoxMesh` in Prazsky.Core), regenerated at the exact field size whenever a map is loaded (`RecreateCeilingRenderer`) and drawn after the opaque scene. Models whose materials carry a texture use the `InstancedModelTextured` technique. Models without one can set `DetailTexture` on their renderer — a texture that only modulates the material colours — mapped either world-space (`DetailMapping.Triplanar` + optional procedural masonry joints, used by the castle, which has no UVs) or through the model's own UVs (`DetailMapping.ModelUVs`, used by the cannon: moving objects must not use the world-space projection or the texture swims across them). Testing CLI args: `autoshoot` (logs FPS + LOD distribution once per second), `nocap` (no vsync), `sky=<n>`, `switchmap=<path>` — see `.claude/skills/verify/SKILL.md`. `Maps/Dense20x10x15.json` (3000 balls) is the perf stress map; the frame budget ceiling there is the CPU physics (~300 FPS uncapped), not rendering.

### Conventions

- Two vector types coexist: `Microsoft.Xna.Framework.Vector3` in game/render code and `System.Numerics.Vector3` in Bepu code, with `ToNumerics()` conversions at the boundary.
- The user (sole author) communicates in Czech; code and comments are in English.

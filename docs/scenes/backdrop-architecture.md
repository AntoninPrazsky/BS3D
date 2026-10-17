# Backdrops and the scene registry

Part of [The scenes](../scenes.md).

How the twenty-one scenes are organised: `SceneRenderer` is a host that dispatches to one `Backdrop` class per scene over shared services, and `SceneCatalog` is the one table of facts about each `SceneKind` (#580). Read before adding a scene or touching what several scenes share. Scene settings are **shared between the game and the map editor**, so a map looks in the editor as it will play.

## What a scene is

- Every scene except the two cities is a `Backdrop` in `Render/Scenes/`, owning its effects, meshes, buffers and tuning constants. Each shader lights itself from a per-frame `SceneFrame` (camera, sun direction, sky palette in linear radiance, the sun's tinted radiance, wall-clock time, optional cloud-apply hook); the shaders are the one compiled copy `Prazsky.Shaders` puts beside every executable (#618). The Testbed passes `_clouds.ApplyTo` as the cloud hook; the editor passes null (full sun, no cloud shadow).
- **Draw slots:** `DrawEnvironment` (terrain, water, scatter, birds), `DrawGrounded` (ground-level things the ceiling glass must bend: the savanna's fires, [Shared effects](shared-effects.md)), `DrawOverlays` (foreground weather last: snow, spray, ash). Hosts call `SceneRenderer.DrawShadowMaps` before binding the scene target ("Sun shadows" in `docs/rendering.md`).
- **Sky-replacing scenes** (`ReplacesSky`: space, dream, cavern, Moon, aurora, Grid, big top) are one full-screen pass with no grid, cloud hook or dome, each stating its own light rig (`TryGetLightRig`). The Moon, aurora and Grid write real ground first and read the sky's depth against it; space, dream and cavern draw depth-off over nothing.
- **The two cities are host-owned** ([The city](city.md)): `_backdrops` is null for them.
- **The cycle.** Every `SceneKind` is on the Testbed's NumPad2 and the editor's V cycle: `SceneCatalog.NextScene` runs off the enum, so a new scene is reachable the moment it is declared. ⚠ A fixed prefix (`CycleLength` of 7) went stale: half the shipped levels were authored past it and the editor could not return such a level to its own backdrop.

## `SceneCatalog`

`Render/SceneCatalog.cs` holds per kind: display name, parse key (what a level file writes and `scene=` takes), aliases (`ice`, `tron`, `bigtop`) and the classifications `ReplacesSky`, `IsSolidTerrainScene` (`OpenBelow` is its negation) and `AnimatesLightRig`. It replaced five parallel tables (one positional array that mislabelled every scene after a reordered member).

- **The type refuses to initialise unless every enum member has exactly one row at its own index and no two spellings collide**, so an executable with a missing row does not start; `Tests/BS3D.Tests/SceneCatalogTests.cs` feeds the check each malformed table.
- It has no GPU type, so `SceneNameJsonConverter` reads and writes scene names off it alone: **the level format does not depend on the renderer.** `SceneRenderer`'s statics forward to it.

## `Backdrop` and the services

- **`Backdrop`** (internal) is one scene's share: config, effects, push, `Draw`, and hooks for what only it can answer (`DrawDepth`, `DrawOverlays`, `DrawGrounded`, `DrawsAtDisplayResolution`, `TryGetLightRig`, `TryGetSunDirection`, `TryGetViewpoint`, `TryGetSceneEvent`, `TryGetGroundGlow`, `ShadowReceivers`, `TryShadowFit`, `HasShadowCasters`/`DrawShadowCasters`, `TryGetTerrainProbe`, `OnDetailChanged`, `Dispose`). Every default is the answer for a scene that has none.
- **`BackdropServices`** carries what several scenes share: device, full-screen quad, supersample factor, seed offset, `TerrainHoleRadius`, the terrain grids ([Terrain grids and mirrors](terrain-grids-and-mirrors.md)), billboard builders, and the `Snowfall`, `FarField` and `BirdFlock` services. **`TerrainPass`** is the shared terrain skeleton (one effect, grid, extent, time uniform and ten per-frame parameters resolved once, one `Draw(frame, holeRadius)`).
- **The renderer dispatches and falls back only to the defaults.** It holds the frame types, the backdrop array, the services, the sun's shadow map and the ceiling glass's, the display-resolution target for the dream and cavern, `ApplyKillPlaneFade` (the hosts' physics rule, no scene's) and the scene-specific public queries (`StormFlash`, `GridSolids`, `VolcanoLightPosition`, `SavannaCampfirePosition`, `SavannaCampfireLightPosition`, `TropicalPalms`...) as one-line forwarders, because the backdrops are internal. The volcano's flows, vents and lamps are their own class (`VolcanoLava`, #795) so the Raspberry Pi's Potato path, which builds no backdrop, solves the same lamps.

## Traps, for whoever takes the next scene

- ⚠ **A reduced technique is picked only when `SceneDetail` changes (`OnDetailChanged`), not at load**, and the renderer starts at detail 1: until a host lowers it, the dream and cavern draw their effect's *default* (first-declared) technique. Scenes that assign a technique every frame (Mars, meadow, forest, savanna) are handed the starting tier and select at load.
- ⚠ **`SupersampleFactor` and `TerrainHoleRadius` are written by the host after construction**, so they live in `BackdropServices`; a backdrop reads them at draw time and **never copies them in its constructor** (every scene would draw at factor 1).
- ⚠ **A backdrop takes its grid from the cache and never disposes it**, and frees only what it built. An effect loaded from the content manager is the manager's; a *clone* (the mountain's and aurora's `Snow.fx`, the sea's and lagoon's `Sea.fx`) is its owner's and is disposed. Clones share compiled shaders and let a scene push its config once instead of re-pushing a shared effect every frame.
- ⚠ **A service the renderer builds mid-constructor must exist before the first backdrop using it**: `Snowfall` and `Birds` are set where the snow and flock stood, so an earlier scene can use them only at draw time. The flock is sized to the largest of the four scenes that draw it (a desert flock was once silently capped to the savanna's count), so those scenes are built before it.
- **Shared helpers are `internal static` on the renderer, not copies** (`SafeNormal`, `AtBearing`, `DirectionFromElevationAzimuth`, `Hash01`). The far ring's tier is `FarField.SceneDetail`, written only by the renderer's `SceneDetail` setter.
- **The volcano does not draw through `TerrainPass`** (it leaves blend state as found where the pass states opaque and restores alpha-blend). The sea sets depth-read around its pass call and puts `Default` back.
- ⚠ **A shadow caster must set its own map matrix**: the beach's palm caster never set `ShadowViewProjection` on `Palm.fx` and drew with the previous frame's, invisible on a still camera and a frame of lag on a moving one.

## Checking a scene change against captures

- ⚠ **Pixel identity cannot be judged frame against frame**: the Testbed runs a variable time step, so frame *n* of two runs of one build lands at different wall-clock seconds and every animated scene differs (the sea in 117,532 pixels). Judge a new-build capture set against the spread of two old-build sets, re-run a flagged scene on both builds, and for the tightest test compare against the previous commit's build.
- **The rig:** `sceneseed=0 nopost nooverc weather=clear`, `campos=0,6,60 camtarget=0,-8,0`, `at=0:F5,0:F12`, `shotframe=150`, 1280x720 minimized without focus; all scenes at `ssaa=2`, the sky-replacing ones again at `ssaa=1`, a few at `detail=0`. Extra noise: the neon city's windows switch on a clock; the sea and beach differ by 30-45k pixels between runs of one build. The Game needs `sceneseed=0` too (`BS3D.exe userdata=<scratch> windowed scene=<name> shot=8`), since it rolls a seed each launch ([Shared effects](shared-effects.md)).

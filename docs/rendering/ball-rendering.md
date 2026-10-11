# Ball rendering: instancing, LOD, emission, dissolve

Part of [Rendering](../rendering.md).

Balls are procedural `SphereMesh`es drawn with GPU instancing. The meshes, LOD ladder, buckets and every figure of the balls' look are `Prazsky.BS3D.BallRenderSet`'s, one copy for all three executables; the walk that fills a frame is `ClusterCollector.Collect` (simulated cluster) or `BallDrawFrame.AddMap` (static map) ("The rest of the hardware, in one copy" in `docs/testbed.md`). Materials: [Ball shading styles](ball-styles.md), [Special ball kinds](ball-kinds.md); colours: [The palette](ball-palette.md).

## Buckets and per-instance data

- A frame fills one bucket per `BallType` x LOD (no camera culling: it saved nothing) and `InstancedModelRenderer` issues one `DrawInstancedPrimitives` per bucket. `BallRenderSet.TYPE_COUNT` is `BallTypes.Count`. ⚠ It was once a hand-pinned constant, and a type added without repointing it existed in logic and physics but was silently never drawn.
- `ModelInstance` (TEXCOORD1-5) carries the world matrix and the **occlusion vector** (direction towards occupied neighbours plus a base factor, recomputed each frame). The factor also carries **burial** (#303): `AirDepthField` runs a multi-source BFS from every air cell once a frame, so it falls from 0.45 with a full first shell to 0.10 `AirDepthField.MAX_DEPTH` shells in (117 us a frame); a neighbour count saturated at the first shell.
- The shader does per-pixel Blinn-Phong with a positional key light, a dome-tinted hemisphere ambient (`ApplySkyLighting`), directional AO and a per-type tint (`GetDiffuseTintByType`); a new type needs no shader change.

## Pattern, emission and the heartbeat

- `InstancedModelPattern` draws the beach-ball pattern in **object space** (gores alternating tint and white, white polar discs; `PatternGoreCount`, `PatternGoreWidth`), so it turns with the ball.
- Balls are **emissive and alive**: they radiate the ball's own `primary` tint (not the blended pattern colour: white gores made half of every ball radiate white) at `BallRenderSet.EMISSION` on a `Heartbeat` curve (two gaussian bumps), phase-offset by world position along `PulseDirection` so the beat travels up the cluster. `PulseTime` is wall-clock and is `BallRenderSet.Draw`'s own argument, so it survives a paused or slowed simulation.
- **The resting emission follows occlusion, squared** (`BallEmission`, #303): added flat it was a floor under the whole cluster that AO could not lower, the biggest reason burial did not read. The heartbeat's swing rides unoccluded; the ripple and the ceiling's alarm are added after shading and never dimmed. Plasma and lava occlude linearly. `TranslucencyStrength` adds light carried through the shell. ⚠ Do not stack two Fresnel sky terms on a sphere (the old "vinyl sheen" plus the specular ambient bleached the balls).

## The LOD ladder is calibrated in display pixels

`BallRenderSet.LOD_RESOLUTIONS` is `{64,32}`, `{40,20}`, `{24,12}`, `{14,7}` (slices, stacks = half). A level is picked by **projected radius**: `SILHOUETTE_BUDGET_PIXELS` (0.35) is how far an outline may sag off a circle, giving thresholds {113.5, 40.9, 14.0} px **derived from the mesh table**; `SolveLodDistances` turns them into squared camera distances once a frame (scale from `Projection.M22`, so precise aim's lens sharpens meshes for free). A raw-distance ladder once drew every ball of the Game's play camera (stand-off ~33.2) at 10 slices, a 0.88 px sag: visibly faceted. No measurable cost. ⚠ **The height is the back buffer's, not the bound target's**: the executables bind the scene target at different points, so a `Viewport` read would be display in one and supersampled in the others.

## The dissolve dithers in display pixels

`ModelInstance.Dissolve` (TEXCOORD6) clips against a hash of `floor(SV_POSITION.xy / DissolvePixelSize)`, a block of the **screen** (object-space cells read as 3-D mottling). ⚠ A cell one *target* pixel wide is averaged back into a cross-fade by the supersample box filter; a whole display pixel or more is not. `BallRenderSet.DISSOLVE_DISPLAY_PIXELS` (1) is multiplied by `BallRenderSet.SupersampleFactor` (set by the Game in `SetSupersampleFactor`); three display pixels read as a mosaic. **A `Dissolve` below -1 is not a dither but the aim ghost's size** (#794): `ModelInstance.GhostDissolve(scale)` is `-(1 + scale)`, `PatternVS`'s `GhostScale` draws the ball at that share of its radius, and every style's clip keeps all of it. Detached balls and colour cross-fades stay dithers.

## Relief, supersampling and the scene's other objects

- The vinyl's relief tilts the normal through `PerturbNormalFromHeight`; each octave **band-limits against the screen-space pixel footprint** (not camera distance), because the derivative-driven perturbation becomes a checkerboard near pixel size. So the scene renders supersampled (`ssaa=<n>`, default 2) and box-filtered; MSAA fixes edges, not shading, so it is off while supersampling.
- The island's surfaces (`SurfaceReliefStrength`/`Frequency`) use world-space gradient noise on the triplanar planes (#674); the earlier sines **wove on a flat face** ("The relief on the cap" in `docs/scenes.md`). Noise fades against the geometric mean of the pixel's two extents (the longer one made a grazing floor go smooth at once). ⚠ That admits an octave undersampled along the view at steep grazing angles: possible crawl or sparkle on the far cap under a moving camera, mostly without supersampling; not seen in stills.

## Testbed arguments for judging balls

`autoshoot` (logs FPS and LOD distribution each second), `aimcheck`, `aimshoot` ("Cannon controls" in `docs/testbed.md`), `nocap`, `sky=<n>`, `scene=<name>`, `ssaa=<n>`, `exposure=<f>`, `switchmap=<path>`, `campos=x,y,z`/`camtarget=x,y,z`, `width=<n>`/`height=<n>`, `nopost` (zero grain and aberration: use it before judging any shader change), `nooverc` ([The palette](ball-palette.md)). `Maps/Dense20x10x15.json` (3000 balls) is the perf stress map; its ceiling is CPU physics (~300 FPS uncapped).

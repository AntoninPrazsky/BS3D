# Sun shadows

Part of [Rendering](../rendering.md).

One orthographic shadow map from the sun, fitted round the camera, drawn by `SceneRenderer.DrawShadowMaps` and read by every receiver through `Shadows.fxh` (#469, #471). **What a scene pays is the receiver, not the casters**, so the map costs about the same everywhere. (The cloud shadow, `Clouds.fxh`'s `CloudSunlight`, is separate.)

## The map

- **`SunShadowMap`**: a 32-bit `Single` target (clip depth in red; MonoGame exposes no sampleable depth buffer) and **no depth buffer** (#591): the caster pass keeps the nearest caster with `BlendFunction.Min` (`SceneRenderer.ShadowCasterBlend`). Saved 64 MB at 4096 and 0.35 ms in the forest (laptop).
- **Fit:** each frame `Fit` fits the window to a box (`ShadowConfig.Extent` 260 units square, a height range) in light space, centred **ahead of the lens** by `SHADOW_LOOK_AHEAD` (0.3 of its width: shadows reach ~208 units instead of 130, #662).
- ⚠ **Snap the window to whole texels in light space AND anchor the light's view on a fixed point (the world origin)** (#566). The sun is fixed per dome, so only the box's centre moves; fractions of a texel re-rasterise every caster edge and shadows swim. Anchored on the camera the snap rounded a constant and the window slid.
- **Size and tier:** `ShadowConfig.MapSize` 4096 (0.063 units a texel); `QualityPreset.ShadowMapCap` holds Medium and Low to 2048 (a *cap*: a tier only takes away, #298); `Ultra` doubles to 8192 via `QualityPreset.ShadowMapScale` -> `SceneRenderer.ShadowMapSizeScale`; Testbed `shadowmap=` (`ShadowMapSizeOverride`) pins a size over both. The Game's `[fps]` line prints `shadow 4096`/`off`. The Testbed has no tier; **pre-#484 figures were at 2048, compare through `shadowmap=2048`**.
- **Cost** (savanna play vantage, 1920x1080 ssaa 2, 5900X / RX 6900 XT): 2048 3.45-3.49 ms, 4096 3.50-3.53, 8192 3.66-3.70; `Ultra` at 3840x1600 **+0.22 ms desert, +0.30 savanna**. **Memory stops High at 4096**: 8 bytes a texel = 33.5 MB at 2048, 134 MB at 4096, **537 MB at 8192**, so the adaptive probe never picks Ultra (only the player: the Settings row or `quality=ultra`). A finer map sharpens the penumbra, it does not soften it.
- ⚠ **Drop the first windows of a measurement launched minimised without activation** (`SW_SHOWMINNOACTIVE`, the project's way of launching without stealing focus): Windows resizes such a window once, about twenty seconds in, and only the `[fps]` windows after the `[camera] … aspect` line that reports it measure the real frame. Averaging them in once made 4096 and 8192 read as equal.

## Drawing and casters

- **`DrawShadowMaps(scene, camera, sun)` runs in all three executables before the scene target is bound** (a mid-frame target switch costs a multisample resolve; the Game calls it first in `BeginSceneDraw`) and leaves the back buffer bound. **No-op** without a map, at `SceneDetail` <= 0.5, at `ShadowStrength` 0, and with the sun under `SHADOW_MIN_SUN_HEIGHT` (0.08): receivers then get strength 0.
- **Casters:** `Acacia.fx`'s `ShadowCaster` (scatter, hearth stones, culling off) and `InstancedModel.fx`'s `InstancedDepth` via `DrawDepth` (island `ArenaIsland.DrawShadow`, gun `CannonRig.DrawShadow`, cluster). Both use `Shadows.fxh`'s `ShadowViewProjection`, so caster and receiver cannot disagree.
- **The glass does not cast, as a rule** (a map has no opacity: the drain's glass would cast as stone). The ceiling's glass casts by arithmetic in every receiver (below).
- **The host draws the island and gun.** In the Game `GameplayScreen` registers as `BS3DGame.StandingGun` when `BuildLevel` builds a session and **`TearDown` must clear it** (else the front end casts a phantom gun); it also draws the gun in the front end ("A session kept for Continue leaves its gun standing" in `docs/game-shell.md`). The editor registers nothing.
- **The cluster casts** (`BallRenderSet.DrawShadow`; Testbed `ballshadow=<0|1>`). The ball collection must have run first (Game screens collect before `BeginSceneDraw`; `DrawSetting` opens an empty one, #672; the Testbed collects at the top of `Draw`). ⚠ **What is not on the map is collected after `BeginSceneDraw`, so it casts nothing**: the aim ghost (#894) and the kind card's ball (#735). `DepthVS` reads only the world matrix, so it draws every ball whole, and the ghost's beat (a size in the dissolve channel, read by `PatternVS` alone) would never reach its shadow. Both are still bucketed, LOD-picked and drawn with the rest, and receive the sun and the map. ⚠ **Merge buckets per LOD (at most four `DrawDepth` calls), not per bucket**: every LOD's renderer wraps the same `Effect`, so each technique switch lands on shared state. Laptop (`Giraffe`, 420 balls): +0.06 ms at `ssaa=1`; not re-measured on the desktop.

## The receivers

- `Shadows.fxh` is the one copy of `ShadowMap`, `ShadowViewProjection`, `ShadowTexel`, `ShadowStrength`, `ShadowBias`, `SunShadow()`. Receivers multiply the **sun term** only; `ShadowReceiver` pushes the parameters. `InstancedModel.fx` folds it into `ShadePixel`'s `sunlight` (island, drain, gun, city and balls receive from one push, so balls pay for taps even where nothing shadows them: gate with a per-renderer flag like `DirLightStrength` if it matters). `Savanna.fx` reads its **base** normal, not the combed one.
- **Filter:** 3 x 3 box, bilinear-weighted over 4 x 4 (sixteen point taps, #674), faded over the last 6 % of the map; thin fronds still leave square holes in the depth map. `tex2Dlod`, not `tex2D` (the taps sit under a `[branch]`).
- **Bias:** the lookup is lifted along the normal (`SHADOW_NORMAL_OFFSET_TEXELS` 1.5) plus slope-scaled `SHADOW_BIAS_UNITS` (0.12 world units, x3.5 edge-on). ⚠ **0.45 with no normal offset (until #642) is a ball's diameter at x3.5**: a ball on the island's dish threw no shadow at its foot and read as levitating.

## Which scenes, and the fit

- **`ShadowConfig` (`Strength`, `Extent`, `MapSize`) is on the base `SceneConfig`; `Strength` 0 = no map at all.** ⚠ **The receivers `RegisterShadowReceivers` collects (`Backdrop.ShadowReceivers`) must match the scenes `Backdrop.TryShadowFit` fits**: fitted-not-receiving casts into a map nobody reads; receiving-not-fitted gets 0 every frame.
- **Ten backdrops have a map** (savanna, meadow, forest, mountains, desert, outback, beach, volcano, Mars, polar ice; strength 0.9). **Out:** the sea (a shadow inside its Fresnel/foam terms is an unrequested look decision), the storm (no ground), the six sky-replacing scenes (no sun over the horizon).
- **The city and neon city** are not `SceneRenderer`'s (`GetSceneConfig` answers null); both hosts call `SetHostShadowScene(scene, shadows, groundY, below, above, receivers)` once at load. The neon sun is under `SHADOW_MIN_SUN_HEIGHT`, so it costs nothing and the day/night split stays in one place.
- Casters by scene: savanna scatter and hearth stones; tropical 110 palms and rocks (`Palm.fx`'s own caster); forest via `ForestScatterRenderer.DrawShadow` (host); island and gun in all (host). ⚠ **`Palm.fx`'s caster must sway too** (`Sway()` is one function both techniques call), and **the tropical rocks cast with sway strength 0** (their `TEXCOORD0.x` is a circumference this shader reads as sway weight).
- **The fit is computed per scene** (`TryShadowFit`): a height range stored in `ShadowConfig` would be a second copy of the terrain proportions drifting silently. `SHADOW_FIT_MARGIN` 10, `SHADOW_ISLAND_HEADROOM` 12 over `ArenaIsland.TOP_Y`. Mountain and volcano take **half** their relief (a range fitted to a peak coarsens the bias at the gun's feet).
- **A map nobody casts into is skipped** (gate: the `extraCasters` callback): the editor draws no island, gun or wood and would pay taps to learn everything is lit.
- **Cost** (dome 8, 1600x900 ssaa 4, `nopost nooverc`, `fpscap=400`, `campos=0,6,60 camtarget=0,-8,0`, 5900X / RX 6900 XT, 2048 map): forest +0.19 ms, tropical +0.20, meadow +0.19, city +0.22; a cluster receiver +0.05 (`Giraffe`, 315 balls). ⚠ **The meadow (one caster) costs what the forest (380) does**: lower the tap count or the terrain branch, not the casters.

## The city's shadows

**Towers cast and receive; the street does not receive.** All towers cast (an off-screen tower's shadow falls on a facade in view). ⚠ **The street receiver was built and removed**: `CityStreets.fx` already darkens a canyon by an occupancy term at every sun height (#399); the map's tap answered a settled question, so `SetHostShadowScene` gets no `receivers` and `CityStreets.fx` does not include `Shadows.fxh`. Judge it from `campos=300,80,-300 camtarget=0,-40,0`, dome 8, `arena=none` (the play camera sits in a canyon with no cast shadow in frame).

## The ceiling's glass shadow (#553)

Computed **in every receiver** by `Shadows.fxh`'s `CeilingGlassShadow`, which `SunShadow` multiplies in (a transmittance map cannot say where the cut sends the light, so no caustic).

- **Analytic slab footprint:** each receiver carries its ray to the sun up to the axis-aligned slab's underside and top and tests both landings against `CutSlabMesh`'s rounded-rectangle outline (union, for a low sun entering the side). Read off the plate's renderer (`GlassHalfExtents`, `GlassCornerRadius`, `GlassCutPeriod`/`GlassCutSlope`, filled by `CeilingPlate.Fit` for the refraction), so shadow and glass cannot disagree.
- **Zones:** uncut glass takes `CeilingPlate.SHADOW_TAKE` (0.2) of the sun; the crown's rim is darkest (`GLASS_SHADOW_RIM`); the diamond field draws caustics (`GLASS_SHADOW_CAUSTIC`).
- ⚠ **It acts on the light the map lets through**: `1 - ShadowStrength x (1 - lit x glass)`; multiplying the factors drew the caustic lattice into the cluster's umbra.
- **Gated** inside the receivers' `ShadowStrength` branch and by `CeilingShadowCentre.w` (0 = no plate); **stated per frame**: `SceneRenderer.CastCeilingShadow(renderer, centre)` before `DrawShadowMaps` (Game: `BS3DGame.CastCeilingShadow` from `GameplayScreen.Draw` and `BackdropScreen.Draw`); a screen that stops drawing a plate stops shadowing silently. Testbed `glassshadow=0|1`. **Cost about +0.05 ms wherever a plate is stated, in view or not** (30 cycles, 5900X / RX 6900 XT: +0.04 to +0.07); not measured on the APU.

## Checking a shadow

- **`shadow=<0..1>`** pins `SceneRenderer.ShadowScale` over `Strength`; 0 is the same code path as `Strength` 0. Alternate it (`alt=shadow=0;shadow=1`): two worktrees of `main` return pairs differing in more than the thing under test. `detail=0` is not a substitute. Nothing in the Game writes `ShadowScale`. "Sweeping variants inside one process" in `docs/testbed.md`.
- ⚠ **Photograph a shadow under a sun that is up, and a low one to be sure.** At the savanna's dome 14 the terrain has the island's footprint cut out, so the island's shadow falls into the hole it casts from ("nothing happened" is geometry); a lower dome shows the ellipse. But a sun can be too low: a meadow capture at dome 5 drew no map (under `SHADOW_MIN_SUN_HEIGHT`). **Dome 8 (14 degrees) is the one to check a new scene with.**

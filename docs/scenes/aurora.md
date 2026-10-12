# The aurora

Part of [The scenes](../scenes.md).

The nineteenth `SceneKind` (#205): a snowy boreal wood at night under a strong, clearly pulsing aurora, the stars showing through and around it. Made of `AuroraBackdrop`, `Aurora.fx` and `AuroraSceneConfig` (groups Terrain, Aurora, Stars, Lighting). Launch: `scene=aurora` (`sceneseed=` pins the planting); shipped levels name it (`Game/Levels/Anchor.json`, `Bell.json`, `Cat.json`).

**It is in both families, after the Moon:** `IsSolidTerrainScene` (forested ground, footprint cut out, pit shaft, no dive) and `ReplacesSky` (no dome or cloud deck, black clear, its own rig through `TryGetLightRig`); see "The sky-replacing family" in [Space](space.md). It is the one scene whose rig **moves** (`SceneCatalog.AnimatesLightRig`). It has a `TerrainMirror` (`mirrorcheck`).

## The ground and the wood

- **The ground is Forest.fx's reduced floor.** `TerrainHeight`, `TerrainNormal` and `NeedleRelief` live in `ForestGround.fxh`, one copy since #581, kept in one change with `TerrainMirror.Forest`; ⚠ the earlier hand copy lapsed (the aurora kept the pre-#281 "lines and waves" relief). `ForestPS`'s triplanar variation and tree shadows do not come along. The ground's `SunColor`/`ZenithColor`/`HorizonColor` are pushed **every frame** from `SceneRenderer.AuroraGlowColor`: a dome-derived sun here would be a frozen lie.
- **The wood is a second, independent planting** of `ForestScatterRenderer` (`_auroraScatter`, from `AuroraSceneConfig.Terrain`, its own `ForestSceneConfig`), because `ApplySkyTint` bakes a tint into the meshes' cached material colours and two scenes retinting one cache would fight. It costs a second set of meshes in every host and lets this wood be retuned without moving a daytime tree.
- **⚠ Pure conifer** (`ConiferFraction` 1): a shrunken broadleaf reads as "little mushrooms with a brown leg and a green cap" at any size.
- **⚠ The value structure is pale floor under near-black spruces** (#462). Dark moss under green-lit trees was one black mass; every reference put the wood on **snow**. The floor is a cool white (`ForestColor`, needle relief cut to a third: it reads as grit on snow), `ConiferColor` near-black, and `GroundStarlight` (neutral, cool) joins the hemisphere term, because a white floor lit only by the near-pure-green glow is a green-painted floor.
- **Boreal spires**: the **count of whorls** says "boreal" far more than proportions (`ConiferTiers`, `ConiferTierSpread`, `ConiferRaggedness` on `ForestTreeConfig`; a regular stack reads as a pagoda). **Every new dial defaults to what the forest was built with and consumes the seed exactly as before**, so the daytime wood does not move.
- **Dead wood**: `SnagMesh` and the savanna's `DeadwoodMesh`, via `ForestSceneConfig.Snags`/`Logs` (**count 0 by default**). ⚠ **Scatter them after the four original kinds**: all draw from one rng stream and anything earlier re-plants the daytime forest. ⚠ Thin pale snags read as bamboo: thick and dark cool grey.
- **A taller wood** (#647): a wall two to four times the height of the gap, `MinRadius` far enough out that a broadleaf's crown does not hang over the play camera. The stand stands on **bare stems** (`ConiferStandTrunk`) while the edge is clothed to the ground. **Snow lies on the whorls** (`CrownSnow`, `CrownSnowColor`) through the triplanar path's settled-dust term (#535), with no shader change. Forest broadleaves are `MeadowTreeMesh.ForForest` (solid clumps: `InstancedModel.fx` cuts no cards).

## The sky

- **The Moon's sky pass verbatim**: `AuroraSkyVS`/`AuroraSkyPS` are `MoonSkyVS`/`MoonSkyPS`, on `BackdropServices.FullScreenQuad`, three `StarLayer` calls off `Stars.fxh` ([The star lattice](space-stars.md)), dithered. **Draw order is the Moon's measured one**: terrain first, depth-writing, then the sky quad depth-**read** (the opposite interleave was an 8x blow-up).
- **The ribbons are additive over the stars** (`sky += Aurora(...)`, never a `lerp`), so stars can be dimmed but never erased. `Aurora()` bands the sky by elevation, folds it into curtains, colours low to high (`ColorLow` green, `ColorHigh` violet) and pulses on two clocks. **Rays** (`RayScale`, `RayStrength`) are one more high-frequency octave that **cuts into** the curtain and fades near pixel size (moire).
- **⚠ Never feed an unwrapped `atan2` azimuth to noise.** `atan2(dir.x, dir.z)` jumps on one fixed vertical line in the world, a seam the camera sweeps past: the third ship of the fault. `AuroraDriftSpeed` rotates the sample **direction** and the streaks are `Fbm3` on it (`AuroraCurtainWarp`). Check by orbiting the free camera a full circle.
- **Motion**: `DriftSpeed` is slow (a fast one turns the field as a carousel), `MorphSpeed` slides the noise domain so folds **re-form where they hang**, `PulseSpeed` is 0.4. ⚠ **A still cannot judge motion** (two stills five seconds apart looked identical at a slow pulse; play read too fast at a quick one): the owner's eye is the test.

## Light

- **Two lights stand between sky and wood; cut both together.** `AuroraGlowColor` (the ground, and through `ApplySkyTint` the trees) and `AuroraLightingConfig`'s rig (balls, island, gun, the trees' base shading). The rig's "~1 per channel" `KeyTint`/`BackTint` convention was never tested against a lit wood. Judge at **ground level**.
- **The island and gun take the aurora's hue but do not pulse.** `GlowTint` carries the key light and sky ambient towards the current hue (`AuroraBackdrop.TowardsHue`), the bounce half as far, the back light not at all (so edges are not all one colour). `SkyLightRig.StepSceneLight` is stepped every frame by all three hosts. **It steps, not slides** (`AURORA_RIG_STEPS`): the Game's re-light walks its renderers through an iterator and would allocate every frame.
- **⚠ The hue drift must be in the sky.** `DriftHueSpeed` once swung only the ground's light (lavender ground under a green curtain). One number (`AuroraBackdrop.AuroraHueShift`, also the shader uniform) now shifts the sky's ramp in the shader, and the ground and rig read the same shift around `AURORA_LIGHT_MIX`.
- **No `SceneLights` branch, no `TryGetSceneEvent`** (the source is the whole overhead sky, and the pulse is continuous). **Ambience:** an explicit `case SceneKind.Aurora` in `ProceduralAmbience.BakeScene` (never leave a domeless scene to the default arm): the forest's wind quieted, under a faint shimmer.
- **Snow:** its own `Snow.fx` clone (#580) and `SnowConfig`; `FlakeCount` is clamped to `_snowFlakeCapacity`. ⚠ The defocus lens (#654) made near flakes big pale discs against the night, so they fade from five units and are dimmer.
- **Intro:** `AuroraIntroShots`; see "The volcano (#530) and the aurora (#531)" in [Chapter intro prologues](../game-feedback/chapter-intro-prologues.md).

## Costs

Desktop, Testbed, `scene=aurora sceneseed=0 nopost nooverc`, 1600x900 ssaa 2, `logfps nocap`: about 2.9 ms at the play vantage (`campos=0,-4,30 camtarget=0,-8,0`; the winter wood and rays cost +0.21) and +0.32 ms from above (`campos=80,25,140 camtarget=0,-5,0`). The taller wood +0.07 ms. ⚠ A local LLM on the same card read 10-17 ms.

**Open:** ground mist between the trunks is not built; the motion and the snow's cast are the owner's eye.

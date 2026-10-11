# Shared effects: the wind, the launch seed, the fire

Part of [The scenes](../scenes.md).

Three behaviours several scenes share, each with a trap that cost a round to find.

## The wind, in one copy (#276)

The meadow, savanna and forest show the wind through `Noise.fxh`'s **`WindGust`**: one octave of combed noise advected downwind, giving irregular patches drawn out along the wind. It replaced a copy-pasted infinite plane wave (`sin(dot(xz, WindDirection) * 0.15 + t * 1.4)`, wavelength about 42 units, wider than the visible field, so the whole ground brightened under one straight edge: "visibly crawls and does not read as anything").

- **The ground's fine texture does not translate.** Sampling the relief at `xz + WindDirection * time * 0.7` slid the blades' texture across the ground (mean change between frames 0.6 s apart: 10.3 of 255 on the meadow, 16.0 on the desert). Grass now **leans** by the gust field (bounded to about an eighth of a feature); the forest floor, litter rather than blades, does not move. After: meadow 2.7 (the gust and clouds, which belong), desert 0.26.
- ⚠ **One octave is a measured ceiling.** At two, the savanna went 8.496 to 27.933 ms and the forest 12.788 to 28.011 (6900 XT, 1600x900 at ssaa 4, `fpscap=400`), while the cheaper meadow merely began to dip. These passes are occupancy-bound, so cost steps when the shader crosses a register threshold. **Anything added to `WindGust` must be re-measured on the savanna and the forest, never on the meadow.** At one octave the whole effect costs 0.05-0.20 ms.

## Every launch is a different city, wood and plain

**One offset is rolled once per launch and added to every seeded arrangement:** both cities, their rooftop equipment, the forest's wood, the aurora's, the savanna's planting, the beach's palms and the Grid's Life boards. The owner's ruling: lean into procedural generation so it looks different each time (a scene is not a tune; there is no "out of key" for a skyline).

- **An offset, not a seed.** Each generator keeps its own constant and adds this one, so the scenes' arrangements stay uncorrelated.
- **Rolled once, not per build.** A quality step (`ApplyQuality` rebuilds the city) or a scene change reads the offset back and gets the *same* city; a skyline that rearranged because the player opened Settings would read as a fault.
- ⚠ **`sceneseed=<n>` keeps the instruments working.** A capture pair or an A/B must look at the same city, wood and planting on both halves. The Testbed and the Game both take it and print `[sceneseed] <n>` either way, so a frame filed with its log can be generated again. **`sceneseed=0` is the arrangement everything before the roll was photographed against**: exactly 1777 buildings and 7075 rooftop pieces (rolled runs gave 1812/1803/1860 and 7096/7084/7416 pieces).
- The **map editor** rolls and has no pin, on purpose (an author judging whether a map reads wants varied backdrops). ⚠ Its forest, aurora wood and fireflies did not roll until #400: they were built on `ForestScatterRenderer.DEFAULT_SEED` alone.

## A campfire burns like a fire (#468)

The savanna's fires are three tongues with a turbulent body and rising sparks, drawn by the savanna backdrop.

- ⚠ **The obstacle to a red flame was the blend, not the colour ramp.** Drawn additive, whatever red is added the sky's own green and blue stay underneath, so a daylit savanna's fires wash to white by arithmetic. The shader already returns colour premultiplied by coverage, so `BlendState.AlphaBlend` (`One`/`InverseSourceAlpha`) is one word: the dense body replaces what is behind it while thin edges still add. It still blooms (the colours are linear radiance over 1 and glare reads the scene target). Check a blend change against a bright dome and a dusk one (14 and 5).
- **The white core is gated on height as well as heat**: `h` carries a `(1 - v)` boost, so heat alone put full core over most of the body; the gate cuts it above the bottom third so the yellow-white is the fire's seat and the tongues cool through orange to red.
- ⚠ **The fires are `Backdrop.DrawGrounded`, drawn before the ceiling's glass, not weather (#641).** The glass writes no depth and bends a copy of the frame taken when it draws (#541; `GrabCeilingBackground`), so a fire drawn as an overlay after everything was painted unbent over the pane, and from under the plate (the play camera's side) the copy had no fire in it at all. `SceneRenderer.DrawGrounded` (the host's `DrawTranslucentsBehindGlass`) is drawn by every Game screen right after `BeginSceneDraw`, with the setting and before the copy. That puts it before the cluster and gun, right only because nothing a screen draws stands behind a fire (the ring stands 33 units out, beyond the play camera's 30-unit stand-off). The weather stays an overlay: a flake or spray drop is as often between lens and plate as behind it. The Testbed and editor draw the slot just before their overlays; their plates bend nothing.

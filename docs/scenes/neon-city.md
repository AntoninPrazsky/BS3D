# The neon city

Part of [The scenes](../scenes.md). The layout, towers, rooftops and streets are in [The city](city.md); this is the relight.

`SceneKind.NeonCity`, `scene=neon`. The same procedural city relit: `CityNeon` on `InstancedModelRenderer` switches `InstancedModel.fx`'s city technique to a neon night (`CitySceneConfig.Neon`; the look in `NeonLook`, the layout in `NeonLayout`). The map editor draws it too (V).

- **Near-black facades, glowing windows**: one dominant hue per tower (magenta, cyan) with the odd accent, about one window in six the opposite hue, sign bands in the contrast colour, some buzzing. Window brightness is driven well over `GLARE_THRESHOLD` so every lit pane blooms into a sign.
- The facades stay dark under any dome, so a bright dome only fights the neon. **The Arcade chapter plays here under dome 16**, the darkest of the twenty.
- **It is the heaviest scene** (the whole city, sign and flicker work, bloom): a 588-ball level held 60.0 FPS at High (`ssaa` 2x, 1600x900, vsync) on the weakest machine, which says nothing of headroom; measure with `fpscap=N` (`TestOptions.FpsCap`), one heavy job at a time (#250: the desktop reset twice under a capped run).

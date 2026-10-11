# Colour decoding and the lighting model

Part of [Rendering](../rendering.md).

## Where a colour is decoded

**Adding a colour to the frame without linearising it is the recurring bug here** (it happened twice: the balls' sheen, the ambient scales).

- **Textures and model material colours** are sRGB, linearised in the shader at the point of use (`SrgbToLinear`, a cubic fit; always decodes).
- **The light rig and sky palette** are decoded on the CPU by `Prazsky.Core.Tools.ColorSpace` in `SkyLightRig` (one copy, `SkyLightRig.ApplyTo`), because everything applied to them (the 1.3 / 0.75 zenith and bounce scales, tint lerps) is multiplication and `SrgbToLinear(a*b) != SrgbToLinear(a)*SrgbToLinear(b)` (display-space scaling once made the ambient ~38 % too bright). `SkyColor`/`GroundColor` hold **linear**, unclamped values (`LinearLightRig`, true everywhere); the editor's `SkyDome` uses `linearVertexColors: true` since its gradient is drawn through `BasicEffect` into the HDR target.

## Blinn-Phong plus specular ambient

- `SkyRadiance` evaluates the dome analytically (two-colour gradients: sampled along the normal for diffuse and the reflection for specular, roughness lerping towards the sky average). **Fresnel is Schlick over a dielectric F0 of 0.04** modulated by the specular colour; ⚠ handing `SpecularColor` straight to Schlick makes F near 1 everywhere and veils the scene.
- **Per-renderer uniforms that every `Draw` must set**, because a leftover value carries to the next surface drawn through the shared effect:
  - `Metalness` (0 except the funnel's gold rims) lerps F0 up to the specular colour (a gold rim reflects gold).
  - `SpecularAlphaWeight` (#228): how far specular is scaled by the material's alpha. 1 everywhere, 0 for the crystal trophy alone: reflection is light off the *front*, and unattenuated in a premultiplied target it composites as light added over the background.
  - `DirLightStrength` (#156): how much of the three-light rig reaches the surface; 1 except the ceiling glass, where `SkyLightRig.ApplyToGlass` dims it to the sky's own brightness (Rec. 601 luminance of the sky ambient over a daylight reference), so the plate is not a sunlit slab over a sky-replacing scene's black backdrop. Per-renderer because the `DirLight*` colours are one set per scene. Scene point lights sit above the scale on purpose.

## Polished metal: the result page's cups (#602)

`InstancedModelRenderer.PolishedMetal` selects `InstancedPolishedMetal` (`InstancedModel/PolishedMetal.fxh`) for the bronze, silver and gold trophies: **no diffuse**, `SpecularColor` as F0 through Schlick, the rig's highlight, and a sharp environment (`smoothstep` horizon, sky 1.9x the dome's ambient at the horizon falling to 0.55x overhead, half the dome's saturation since a whole cyan sky made gold olive, the sun as a glint); `SkyRadiance`'s ramp makes polished things read as painted. It declares no uniform and is included last, so no other program changes.

- **The edge is drawn** (#640): Schlick takes a metal to a white mirror at the silhouette that dissolves a bowl rim into the sky behind it (pale wedges read as holes). Below `dot(N, V)` = `PolishedRimWidth` (0.22) the result fades to `PolishedRimBody` (0.12) of F0, a dark shade of the alloy. A 0.1 width was one pixel of a 200-pixel bowl. Measurements: "The cup the player just won" in `docs/game-feedback.md`.

---
name: shaders
description: How to add and wire custom HLSL effects (.fx) in BS3D — content pipeline, instancing, matching BasicEffect lighting. Load before any shader/graphics work (issues #8, #39, #40, #41).
---

# Custom shaders in BS3D (MonoGame WindowsDX)

## Adding an effect

1. Put the `.fx` under `Testbed/Content/Shaders/` and register it in `Testbed/Content/Content.mgcb`:
   `importer:EffectImporter`, `processor:EffectProcessor`, `processorParam:DebugMode=Auto`.
   The MapEditor draws balls through `InstancedModel.fx` too and builds it from that same source with
   `/build:../../Testbed/Content/Shaders/InstancedModel.fx;Shaders/InstancedModel.fx` in its own .mgcb —
   a source outside the content root is fine, so shader changes reach both executables at once.
2. It compiles during `dotnet build` (MonoGame.Content.Builder.Task). Load with `Content.Load<Effect>("Shaders/<Name>")`.
3. Both executables are on **WindowsDX** now, so everything builds for DirectX at **Shader Model 5.0**
   (`vs_5_0` / `ps_5_0`) — there is no OPENGL/mojoshader build any more, and `#if OPENGL` is gone from
   `InstancedModel.fx`. Both run with `GraphicsProfile.HiDef`. A shader that only the Testbed uses (each
   scene shader, `Sky.fx`) it registers in its own `.mgcb`; a shader the MapEditor also needs (the shared
   `InstancedModel.fx`, plus `Tonemap.fx`/`Glare.fx` for its own linear+tonemap pipeline) is registered in
   both `.mgcb`s, the editor building it out of the Testbed content dir with the `/build:../../Testbed/…`
   form so there is one source. MSAA is off while supersampling is on (the scene renders into an HDR target).

## Existing shader

`Testbed/Content/Shaders/InstancedModel.fx` + `BS3DLibs/Prazsky.Core/Render/InstancedModelRenderer.cs`
draw all balls (see the "Ball rendering" section in `docs/rendering.md`). Facts that took effort to get right:

- **Instancing**: per-instance world matrix rides in a second vertex stream as four `Vector4`
  with `VertexElementUsage.TextureCoordinate`, usage indices 1–4 → HLSL `TEXCOORD1..4`.
  Rows are stored in XNA row-major layout, so `float4x4(r1,r2,r3,r4)` needs **no transpose**
  and `mul(vec, world)` matches `Vector3.Transform`. Bind with
  `SetVertexBuffers(new VertexBufferBinding(modelVB, vertexOffset, 0), new VertexBufferBinding(instanceVB, 0, 1))`
  and `DrawInstancedPrimitives`. To add per-instance data (e.g. an AO factor for #40), append a fifth
  `Vector4` element (usage index 5) to `INSTANCE_VERTEX_DECLARATION` and a matching struct/`TEXCOORD5` input.
- **Lighting parity**: the shader replicates `BasicEffect.EnableDefaultLighting()`; the exact
  three-light rig values live as constants in `InstancedModelRenderer`. BasicEffect folds
  `ambientLight * materialDiffuse + materialEmissive` into the emissive uniform on the CPU — the renderer
  does the same, so ambient changes (hemisphere ambient for #39) belong on the C# side or need a new uniform.
- **Materials**: the ball model (`Balls/DebugSphere.dae`) has ~6 mesh parts with different material
  diffuse colors (beach-ball patches); the renderer reads them from the model's `BasicEffect`s at load.
  Per-type tint (eight types: red/green/blue/white + cyan/magenta/yellow/black) comes from `BasicEffectParamsProvider` (ambient+specular only).
- **Beach-ball pattern**: balls use the `InstancedModelPattern` technique — procedural gores +
  polar discs evaluated in *object space* (so the pattern turns with the ball; rotation stays
  readable), antialiased with `fwidth` so distant balls don't shimmer. Enabled by
  `PatternGoreCount > 0` on the renderer (untextured opaque parts only); the `diffuseTint` passed
  to `Draw` becomes the primary gore color, `PatternSecondaryColor`/`PatternCapExtent`/
  `PatternGoreWidth` are renderer properties. `PatternGoreWidth` is the fraction of each pair of
  segments the color takes (0.5 = even); the shader thresholds `sin(azimuth)` at `-cos(pi * width)`,
  which is exact and, unlike a fraction-of-period coordinate, stays continuous across the atan2
  branch cut. The white type (tint = white) intentionally renders plain.
- **Inflatable-ball surface** (same technique): a height field — four summed sines along mixed
  directions for the molded micro-relief, plus a groove along every gore boundary and disc rim for
  the panel welds — tilts the normal via `PerturbNormalFromHeight` (Schueler's tangent-free bump,
  the height-field sibling of `CotangentFrame`), so the highlight breaks up instead of reading as a
  perfect sphere. A Fresnel term adds the grazing-angle sky sheen, scaled by `SurfaceOcclusion` so
  balls buried in the pile stay dark. Two traps, both hit while building it: *multiplying* sines
  lays down a regular crosshatch (sum them instead), and any wave approaching pixel size aliases
  into a hard checkerboard, because the perturbation is driven by `ddx/ddy`.
- **Band-limiting is per octave, not global.** `ReliefOctave` fades each wave against *its own*
  wavelength (`saturate(1 - footprint * f / pi)`), where `footprint` is the screen pixel size in
  surface-distance-over-ball-radius units — the object-space radius comes free from
  `length(ObjectPosition)`, so nothing has to be told how big a ball is. A single global fade has to
  be tuned for the finest octave and therefore flattens the whole surface at arm's length; per-octave
  attenuation lets fine detail stay fully present while the pixels resolve it and drop out silently
  when they cannot. Measuring the *footprint* rather than camera distance is what makes it hold at
  any resolution, FOV or ball size — and it is what makes supersampling pay off (below): more samples
  shrink the footprint, so the fine octaves survive further out instead of the same mush getting
  smoother. Keep the height branchless — `ddx/ddy` need every pixel of a quad on the same path.
- The scene objects (ground, ceiling, cannon, castle) render through the same effect as
  single-instance draws (`InstancedModelRenderer.Draw(camera, world, effectParams)`); per-part
  material diffuse/emissive/specular and alpha are read from the model's `BasicEffect`s and
  premultiplied by alpha like BasicEffect does. `ModelRenderer` + `BasicEffect` remains only for the
  MapEditor's selector gizmo (its balls go through the instanced path like the game's). Textured mesh parts automatically use the
  `InstancedModelTextured` technique (UVs in TEXCOORD0; same `ShadePixel` lighting, texture
  modulates the non-specular color like BasicEffect) — a retained library feature with no shipped
  model on it today (the last one, `GroundMarble.fbx`, was dead content and is deleted). Models with no texture of their own can instead set
  `DetailTexture` (+`DetailScale`/`DetailStrength`/`DetailBoost`) on their renderer — it only
  modulates the material colors — with `DetailTextureMapping` choosing how it lands:
  - `DetailMapping.Triplanar` projects it along the world axes, needing no UVs, plus optional
    procedural masonry joints (`MasonryStrength`). The castle uses this with
    `Backdrops/CastleStone.png`, a seamless tile mirrored out of the stone half of `Ground_8.png`
    (that source PNG is half black filler with a watermark — do not tile it directly).
  - `DetailMapping.ModelUVs` samples the model's own UVs. **Anything that moves or rotates must
    use this** — the triplanar projection is fixed in world space and would swim across the
    surface. The cannon used it with `GameObjects/CannonMetal.png` before it became a procedural
    `CannonMesh` (a plain-steel barrel with no UVs); nothing uses this path now. It also accepts a
    `DetailNormalMap` (+`DetailNormalStrength`) for real relief: the tangent frame is derived
    from `ddx/ddy` in the pixel shader (`CotangentFrame`), because the instance vertex streams
    carry only position/normal/UV and the procedural meshes have no tangents to give.
    Register normal maps with `TextureFormat=Color` (DXT blocks wreck normals) and
    `PremultiplyAlpha=False`/`ColorKeyEnabled=False`.
  Procedurally generated tiles must be seamless: build them from integer-frequency sine waves
  (see the generator approach used for CannonMetal). Waves along a single axis show up as a
  plaid once tiled — mix directions for a mottled, direction-free surface. Blender FBX gotchas: exported texture
  paths may be relative to the .blend (patch to resolve from the FBX's own directory, or export
  with Path Mode: Strip Path), and Blender cm units make models 100× too big — fix with
  `/processorParam:Scale=0.01` in Content.mgcb. Sky domes are `Skyes/SkyDome1..18.dae`, switched with NumPad1,
  drawn by `SkyDome` (Prazsky.Core) — the place to sample zenith/horizon colors for #39.

## Procedural surface relief on the scene objects

`SurfaceReliefWorld` is the ball relief's world-space sibling, driven by `SurfaceReliefStrength` (peak
height in world units) and `SurfaceReliefFrequency` (base waves per world unit) on the renderer, and fed
through the same `PerturbNormalFromHeight`. It is wired into the textured, detail-UV and triplanar paths,
so ground, cannon and castle all get it, and it composes with a `DetailNormalMap` rather than replacing
it — the cannon keeps its mapped grain and gains casting unevenness the map's texels cannot hold.

- **Use seven octaves, not four.** Too few waves spaced too far apart interfere into a regular diagonal
  weave rather than a surface; the cannon barrel showed it plainly at frequency 28. Ratios are ~1.47
  apart and irrational. Slope ≈ `strength × frequency × 3.0`, which is the number to reason with when
  tuning: ~0.2 reads as believable stone, past ~0.4 it looks like crumpled foil.
- **A model is not one material.** `SetMeshSurfaceStyle(meshName, SurfaceStyle)` — `Masonry`, `Wood` or
  `Plain` — keyed on the model's own mesh names, which the renderer now carries in `MeshPartData`.
  The castle is `Castle_Castle_wall1..3` / `_wood` / `_glass` / `_top`; before styles existed the
  coursing was drawn over the lot and its timber door came out clad in stone. Undeclared meshes default
  to `Masonry`, which is what a stone-all-over model did before. Dump the names with a throwaway loop
  over `model.Meshes` when adding a model — guessing which part is which wastes more time than the loop.
- **Joints and seams are recesses, not paint.** The masonry mortar and the gaps between boards are cut
  into the height field (`MortarDepth`, `BoardGrooveDepth`) with a world-space bevel, so they light and
  shadow from the side. Give them a real width — collapsed to a one-pixel crease they just alias.
- The style select is branchless `step`/`lerp`. `SurfaceStyle` is a uniform so a branch would not
  diverge, but the derivatives downstream want every pixel of a quad on the same path regardless.

## Supersampling

The Testbed renders the 3D scene into a `_supersampleFactor`× `RenderTarget2D` and box-filters it onto
the back buffer (`PostProcessPipeline.EnsureTarget` / `.Resolve`, shared from Prazsky.Core), factor 2 by default, `ssaa=<n>` to override.
MSAA would not do instead: it antialiases geometry edges only, and the ball relief is *shading*. At factor 2
a bilinear tap lands exactly on the corner shared by four source pixels, so the resolve is an exact box
filter; higher factors reach only four of the source pixels and would want a real downsample pass. The
back-buffer MSAA is switched off whenever supersampling is on — the scene never touches the back buffer
then, and 8x at 4K is hundreds of megabytes for nothing. Draw the overlay and any 2D sprite **after** the
resolve (`base.Draw` already runs last) or the downsample softens the text. Recreate the target on
`ClientSizeChanged` *and* in `SetGraphics`, or a resize or F11 leaves it at the old size.

## Verifying shader work

Use the `verify` project skill (launch recipe, autoshoot logging, window screenshots). Lighting changes
are best judged from screenshots across several sky domes — NumPad1 cycles them, or temporarily set
`_skyModelNumber` in `Testbed.cs`.

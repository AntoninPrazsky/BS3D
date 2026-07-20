---
name: shaders
description: How to add and wire custom HLSL effects (.fx) in BS3D — content pipeline, instancing, matching BasicEffect lighting. Load before any shader/graphics work (issues #8, #39, #40, #41).
---

# Custom shaders in BS3D (MonoGame DesktopGL)

## Adding an effect

1. Put the `.fx` under `Testbed/Content/Shaders/` and register it in `Testbed/Content/Content.mgcb`:
   `importer:EffectImporter`, `processor:EffectProcessor`, `processorParam:DebugMode=Auto`.
   The MapEditor draws balls through `InstancedModel.fx` too and builds it from that same source with
   `/build:../../Testbed/Content/Shaders/InstancedModel.fx;Shaders/InstancedModel.fx` in its own .mgcb —
   a source outside the content root is fine, so shader changes reach both executables at once.
2. It compiles during `dotnet build` (MonoGame.Content.Builder.Task). Load with `Content.Load<Effect>("Shaders/<Name>")`.
3. The platform is DesktopGL, so only the `#if OPENGL` branch matters: `vs_3_0` / `ps_3_0` (mojoshader).
   The game runs with `GraphicsProfile.HiDef` and 8x MSAA (`Graphics_PreparingDeviceSettings`).

## Existing shader

`Testbed/Content/Shaders/InstancedModel.fx` + `BS3DLibs/Prazsky.Core/Render/InstancedModelRenderer.cs`
draw all balls (see the "Ball rendering" section in CLAUDE.md). Facts that took effort to get right:

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
  diffuse colours (beach-ball patches); the renderer reads them from the model's `BasicEffect`s at load.
  Per-type tint (red/green/blue/white) comes from `BasicEffectParamsProvider` (ambient+specular only).
- **Beach-ball pattern**: balls use the `InstancedModelPattern` technique — procedural gores +
  polar discs evaluated in *object space* (so the pattern turns with the ball; rotation stays
  readable), antialiased with `fwidth` so distant balls don't shimmer. Enabled by
  `PatternGoreCount > 0` on the renderer (untextured opaque parts only); the `diffuseTint` passed
  to `Draw` becomes the primary gore colour, `PatternSecondaryColor`/`PatternCapExtent` are
  renderer properties. The white type (tint = white) intentionally renders plain.
- The scene objects (ground, ceiling, cannon, castle) render through the same effect as
  single-instance draws (`InstancedModelRenderer.Draw(camera, world, effectParams)`); per-part
  material diffuse/emissive/specular and alpha are read from the model's `BasicEffect`s and
  premultiplied by alpha like BasicEffect does. `ModelRenderer` + `BasicEffect` remains only for the
  MapEditor's selector gizmo (its balls go through the instanced path like the game's). Textured mesh parts (e.g. `GameObjects/GroundMarble.fbx`) automatically use the
  `InstancedModelTextured` technique (UVs in TEXCOORD0; same `ShadePixel` lighting, texture
  modulates the non-specular colour like BasicEffect). Models with no texture of their own can instead set
  `DetailTexture` (+`DetailScale`/`DetailStrength`/`DetailBoost`) on their renderer — it only
  modulates the material colours — with `DetailTextureMapping` choosing how it lands:
  - `DetailMapping.Triplanar` projects it along the world axes, needing no UVs, plus optional
    procedural masonry joints (`MasonryStrength`). The castle uses this with
    `Backdrops/CastleStone.png`, a seamless tile mirrored out of the stone half of `Ground_8.png`
    (that source PNG is half black filler with a watermark — do not tile it directly).
  - `DetailMapping.ModelUVs` samples the model's own UVs. **Anything that moves or rotates must
    use this** — the triplanar projection is fixed in world space and would swim across the
    surface. The cannon uses it with `GameObjects/CannonMetal.png`. This path also accepts a
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
  drawn by `SkyDome` (Prazsky.Core) — the place to sample zenith/horizon colours for #39.

## Verifying shader work

Use the `verify` project skill (launch recipe, autoshoot logging, window screenshots). Lighting changes
are best judged from screenshots across several sky domes — NumPad1 cycles them, or temporarily set
`_skyModelNumber` in `Testbed.cs`.

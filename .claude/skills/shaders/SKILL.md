---
name: shaders
description: How to add and wire custom HLSL effects (.fx) in BS3D — content pipeline, instancing, matching BasicEffect lighting. Load before any shader/graphics work (issues #8, #39, #40, #41).
---

# Custom shaders in BS3D (MonoGame DesktopGL)

## Adding an effect

1. Put the `.fx` under `Testbed/Content/Shaders/` and register it in `Testbed/Content/Content.mgcb`:
   `importer:EffectImporter`, `processor:EffectProcessor`, `processorParam:DebugMode=Auto`.
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
- Everything else in the scene still renders through `ModelRenderer` + `BasicEffect`
  (`BasicEffectParams` in Prazsky.Core). Sky domes are `Skyes/SkyDome1..18.dae`, switched with NumPad1,
  drawn by `SkyDome` (Prazsky.Core) — the place to sample zenith/horizon colours for #39.

## Verifying shader work

Use the `verify` project skill (launch recipe, autoshoot logging, window screenshots). Lighting changes
are best judged from screenshots across several sky domes — NumPad1 cycles them, or temporarily set
`_skyModelNumber` in `Testbed.cs`.

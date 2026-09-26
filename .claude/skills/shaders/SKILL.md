---
name: shaders
description: How to add and wire custom HLSL effects (.fx) in BS3D — content pipeline, instancing, matching BasicEffect lighting. Load before any shader/graphics work (issues #8, #39, #40, #41).
---

# Custom shaders in BS3D (MonoGame WindowsDX)

## Adding an effect

1. Put the `.fx` under `BS3DLibs/Prazsky.Shaders/Content/Shaders/` and register it in
   `BS3DLibs/Prazsky.Shaders/Content/Shaders.mgcb`: `importer:EffectImporter`, `processor:EffectProcessor`,
   `processorParam:DebugMode=Auto`, `/build:Shaders/<Name>.fx`. That is the only place a shader is registered
   (#618): the library compiles every effect **once**, and its `.xnb` files are Content items that MSBuild
   copies into the output (and the publish folder) of every project referencing `Prazsky.Shaders` — the
   Testbed, the MapEditor, the Game and `Tools/WindingCheck` — under `Content/Shaders/`. The executables'
   own `Content.mgcb` files carry no shaders. (Until #618 the sources lived in `Testbed/Content/Shaders`
   and each executable compiled them itself.)
2. It compiles during `dotnet build` (MonoGame.Content.Builder.Task, run by the library). Load with
   `Content.Load<Effect>("Shaders/<Name>")`.
3. All three executables are on **WindowsDX**, so everything builds for DirectX at **Shader Model 5.0**
   (`vs_5_0` / `ps_5_0`) — there is no OPENGL/mojoshader build any more, and `#if OPENGL` is gone from
   `InstancedModel.fx`. They run with `GraphicsProfile.HiDef`. `Prazsky.Shaders.csproj` sets
   `MonoGamePlatform` to `Windows` itself (it references no MonoGame platform package, and the libraries'
   private DesktopGL one would compile OpenGL effects). Every effect reaches every executable, used or not
   (the MapEditor never loads `Sky.fx`). MSAA is off while supersampling is on (the scene renders into an HDR
   target).
4. **Do not copy a helper out of another shader — include it.** `Noise.fxh` (noise, hashes incl. `Hash21`,
   `PerturbNormalFromHeight`), `Clouds.fxh`, `Shadows.fxh`, `FarField.fxh`, `HeightProbe.fxh`, `Stars.fxh`,
   and since #581 `Craters.fxh`, `Rocks.fxh`, `Grass.fxh` and `ForestGround.fxh` hold what two scenes share.
   A shared header carries functions and static constants only; the uniforms it reads are declared by each
   includer before the `#include`, so the includer's constant buffer does not move. Copies that were meant
   to stay in step drifted apart within days (#579) — that is why these exist. An `.fxh` edit rebuilds
   every `.fx` that includes it.

## Existing shader

`BS3DLibs/Prazsky.Shaders/Content/Shaders/InstancedModel.fx` + `BS3DLibs/Prazsky.Core/Render/InstancedModelRenderer.cs`
draw all balls (see the "Ball rendering" section in `docs/rendering.md`). Since #581 the `.fx` is a thin list
of includes — `Shaders/InstancedModel/*.fxh`, one file per concern (`Common`, `Lighting`, `SceneRelief`,
`BallCommon`, one `Ball<Style>` per shading, `Triplanar`, `City`, `Depth`, `Glass`); still ONE Effect. **The
include order is the `$Globals` layout**: never reorder the includes or move a uniform between files in a change
meant to be pure, and check such a change by comparing the compiled bytecode (see "The instanced effect's
files" in `docs/rendering.md`). A new ball style is a new `Ball<Style>.fxh` after `BallHeavy.fxh`. Facts that
took effort to get right:

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
  Per-type tint (thirteen types: red/green/blue/white + cyan/magenta/yellow/black + orange/brown/silver/navy/olive) comes from `BasicEffectParamsProvider` (ambient+specular only).
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
  the height-field sibling of his normal-mapping cotangent frame), so the highlight breaks up instead of reading as a
  perfect sphere. The grazing-angle sky sheen is `ShadePixel`'s specular ambient, scaled by `SurfaceOcclusion` so
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
- The scene objects (island, drain, ceiling, cannon, trees, trophies) render through the same effect as
  single-instance draws (`InstancedModelRenderer.Draw(camera, world, effectParams)`). Every renderer is built
  from a procedural `IProceduralMesh` with one material colour and alpha, premultiplied by alpha like
  BasicEffect does. `ModelRenderer` + `BasicEffect` remains only for the MapEditor's selector gizmo (its balls
  go through the instanced path like the game's). **There is no loaded-model or UV path any more** (#581): the
  `Model` constructor had no caller, so the `InstancedModelTextured`, `InstancedModelDetailUV` and
  `InstancedModelDetailUVNormal` techniques, the normal map, `DetailMapping.ModelUVs` and the parallax and
  relief self-shadow marches (`ParallaxScale`, `ReliefShadowStrength`) were unreachable and are deleted. If a
  loaded model with UVs ever comes back, it comes back with its own technique and a reason.
- A renderer can set `DetailTexture` (+`DetailScale`/`DetailStrength`/`DetailBoost`): it only modulates the
  material colour, and it is projected along the three world axes (`InstancedModelTriplanar`, plus its coarse
  copy and the `capprobe=` probes), so it needs no UVs — but it is fixed in world space, so it only suits
  objects that never move (the island, the forest's trunks and boulders). `SurfaceTexture` builds those tiles
  procedurally; tiles must be seamless — integer-frequency sine waves, mixed directions (waves along a single
  axis show up as a plaid once tiled). Sky domes are procedural since #113 — one shared dome geometry and
  twenty stored palettes in `SkyDome`/`SkyDome.Data.cs` (Prazsky.Core), switched with NumPad1.

## Procedural surface relief on the scene objects

`SurfaceReliefWorld` is the ball relief's world-space sibling, driven by `SurfaceReliefStrength` (peak
height in world units) and `SurfaceReliefFrequency` (base waves per world unit) on the renderer, and fed
through the same `PerturbNormalFromHeight`. The triplanar paths read it through `SceneSurfaceHeightGroove`,
together with the slab joints (`SlabSize`/`SlabJointWidth`/`SlabJointDepth`, `SlabGroove`), which are cut into
the same height field so they are real recesses that light and shade from the side.

- **Use seven octaves, not four.** Too few waves spaced too far apart interfere into a regular diagonal
  weave rather than a surface; the cannon barrel showed it plainly at frequency 28. Ratios are ~1.47
  apart and irrational. Slope ≈ `strength × frequency × 3.0`, which is the number to reason with when
  tuning: ~0.2 reads as believable stone, past ~0.4 it looks like crumpled foil.
- **Joints are recesses, not paint**, with a world-space bevel widened against the pixel's footprint
  (`SlabGrooveAxis` carries the #351 story). Give them a real width — collapsed to a one-pixel crease they
  just alias.

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

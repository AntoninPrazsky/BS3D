# The shared effects on OpenGL ES (#1057)

Part of [Rendering](../rendering.md).

Every effect in `Prazsky.Shaders` compiles for two targets from one source: **DirectX 11 at Shader Model 5.0**, the desktop's (built by `Prazsky.Shaders` itself, for the Testbed, the MapEditor and the Game), and **OpenGL ES through MojoShader at Shader Model 3.0**, the Android build's (`GameAndroid`'s content build compiles `Shaders.mgcb` for Android, #1056). MonoGame's effect compiler defines `OPENGL` for a GL platform and nothing for DirectX, so what the two need apart is written under `#if OPENGL`.

**The DirectX build is not to move.** Every GL change is written so the DirectX side compiles to the same bytes: a macro that expands to the desktop's own tokens there, or the desktop's code kept whole under `#else`. Compare the `.xnb` hashes of `BS3DLibs/Prazsky.Shaders/Content/bin/Windows/Shaders` before and after (all 58 rebuilt and identical, at #1057's merge).

## The profile header: `ShaderTarget.fxh`

Every `.fx` includes it **in place of defining its profiles**, before anything else. It defines:

- `VS_SHADERMODEL` / `PS_SHADERMODEL`: `vs_5_0`/`ps_5_0`, or `vs_3_0`/`ps_3_0` under GL.
- `FRONT_FACE(name)` among a pixel shader's parameters and `FRONT_FACE_BEGIN(name)` opening its body, for the side of a triangle: SM 5.0 hands a `bool` (`SV_IsFrontFace`), SM 3.0 a float (`VFACE`) whose both signs are non-zero, so a `bool` read straight from it is always true.
- `BRANCH_UNIFORM_GRADIENTS`: `[branch]` on DirectX, `[flatten]` under GL, for a branch on a uniform with a derivative inside. SM 3.0 cannot tell a uniform from a varying there and refuses the branch (X3528, "can't force branch with gradients on non-inputs"); flattened, it pays for both sides. Acacia's leaf cards and its stone, bark and straw passes.
- `ARRAY_LOOP(i, count, max)`: a walk over the live part of a uniform array. DirectX loops to the count (`[loop]`); GL unrolls to the array's size and gates each step by the count, so every index is a constant. Savanna's hearths and lights, Volcano's river and flows, Cavern's lights and the scene lights on every instanced draw (`Lighting.fxh`).
- `COUNT_LOOP(i, count, max)`: a loop to a uniform count with no array to index. GL loops to `max`, a literal (at most 255, Shader Model 3.0's longest `rep`), and breaks at the count. The tonemap's supersample box (`MAX_SUPERSAMPLE` 4) and the motion blur's tile passes (`MAX_TILE_SIZE`).

## What Shader Model 3.0 and MojoShader refuse, and what is done instead

| Refused | Symptom | Under GL |
|---|---|---|
| `asuint`, bitwise operators | X3535 "Bitwise operations not supported" | arithmetic: a bit of a non-negative whole number is its quotient's parity, `(x / s) % 2`; `(3 * rx) ^ ry` of two bits is `3 * rx + ry - 2 * rx * ry` (the porcelain's and the grid's Hilbert curves); a power-of-two mask is the floored modulus (`GridMod`) |
| a `uint` it cannot prove non-negative | X3548 "uints can only be used with known-positive values" | `HASH_SALT(x)`, Noise.fxh: a uint cast on DirectX, nothing under GL |
| more than 224 constant registers in a pixel program | X4507 | an unrolled loop gives every iteration's folded literals registers of their own: the zap's six bolts and their forks walk in a `[loop]` under GL |
| a branch with a derivative inside | X3528 | `BRANCH_UNIFORM_GRADIENTS` |
| a relative address MojoShader cannot translate | "relative address needs replicate swizzle" (Savanna, Volcano) | `ARRAY_LOOP`; an index computed in the loop (Savanna's nearest fire) is replaced by carrying its values out of the loop |
| two uniform arrays indexed in one program | none: it compiles, and every slot reads another array's (GamePi's Fireworks and Blast, #789) | **one array**: `BlastData`, `ShellData` (GamePi's layouts, which `Blasts.cs` and `Fireworks.cs` fill when the effect has them), `VentData` and `FumaroleData` (a place and a strength in one `float4`, which `VolcanoBackdrop.SetPlaces` fills). Read through accessor macros that are the desktop's arrays on DirectX |
| ⚠ a loop to an `int` uniform | none: it compiles, and the loop runs no times. Shader Model 3.0 keeps the count in an integer register (`uniform ivec4` in the GLSL), and MonoGame's GL runtime uploads only float vectors (`glUniform4fv`, never `glUniform4iv`), so the count reads 0: the tonemap's frame came out black, and the scene lights, Cavern's lights and the motion blur's tiles added nothing | `ARRAY_LOOP` or `COUNT_LOOP`. **Any `int` or `bool` uniform that reaches a GL program reads 0**; one used only in arithmetic sits in a float register and is safe |
| ⚠ a `static const` array read at a computed index | none: MojoShader makes it a uniform (`vs_const_array_…`) that MonoGame never fills, so it reads zeros. The savanna's flames had no size and drew nothing (#1063) | its entries picked at constant indices under GL (`SUBFLAME_PICK` in `Flame.fx`); `FinishGlEffects` refuses a program that declares one |
| a texture read by its own method (`Reflection.SampleLevel(ReflectionSampler, …)`) | the GL effect names the texture parameter after the sampler, so `Parameters["Reflection"]` is null: the cavern crashed at Medium, where its lake mirrors (#1062) | `tex2Dlod` on the sampler under GL, whose `Texture = <…>` names the parameter as the desktop's does |
| a sampler in a vertex program (vertex texture fetch) | "invalid DCL register type for this shader model" | none: the aurora's snow is drawn without the mound map's height (its slope and open sky are still read per pixel). The planting still stands on the mounds, which the phone's aurora has to settle (#1062) |

`GameAndroid`'s build fails on a GL program that indexes two arrays (GamePi's `compile.ps1` check over every effect), that declares an integer or boolean uniform, or a desktop effect left at mediump (the `FinishGlEffects` task in `GameAndroid.csproj`), and CI's `gameandroid` job builds it, so a refused construct and the three silent ones all stop a push.

## The integer hash's float twin (#1058)

`Noise.fxh`'s cell hashes (`HashCell`, `HashSalt`, `HashUnit`, `HashBits`) mix 32-bit words on DirectX. SM 3.0 has no integers, and **a float hash of a float word does not work**: a highp float keeps 24 bits and the `frac` of a large product ten or fewer, so a seed hashed to a float and then salted or sliced came out coarse and lopsided (64 bins over four million cells held chi-squared 3,500 to 200,000, the integer hash 50 to 90).

So under GL a seed is a `HASH` that is **its key**, not a hash: `float4(cell x, cell y, the salts so far, the bit slice)`, all whole numbers a float holds exactly. Every `HashUnit` or `HashBits` hashes the whole key afresh through `HashKey`, a four-lane "hash without sine": each lane `frac(key * lane constant)`, mixed by a dot product, brought back to [0, 1) and folded by one modest product. Callers hold seeds in a `HASH` (a `uint` on DirectX) and read them only through those two, so they are shared by both builds.

- **Measured** in float32 over 2048 × 2048 cells, near the origin and 6000 cells out: chi-squared 49 to 92 over 64 bins, over 400,000 distinct values, neighbours and two rolls of one seed correlated under 0.001, and `Tools/LatticeProbe` scoring the fields as it scores the integer hash's (29 tiles over its null across twenty fields, against the integer hash's 42). The window fields (a cell offset by its building) hold 65 to 277, the far ones the highest.
- ⚠ **Hoskins' own lane constants repeat**: his 0.1030 is 103/1000, and a field through it repeated every 1000 cells in y (correlation 0.974 at that distance). `HASH_LANES` repeats at no distance under 10,000.
- ⚠ **Keep the fold's `frac` before the product.** Without it the product of the raw lanes reaches tens of thousands and keeps seven to nine bits, the coarseness the word hash had.
- **Salts are whole numbers under 2^24**: the deepest chain is two (the meadow's lily pads, `HASH_SALT_STRIDE`), and the city's window buzz salt is 65521 under GL (the desktop's `0x7fffffff` rounds to 2^31 in a float and would land on salt 0). A city window's key is its cell offset by `WINDOW_KEY_STRIDE` (257) cells a building, which keeps every window apart while a facade has fewer cells than that along each axis.
- Each roll costs one `HashKey` (about a dozen operations) where the desktop cuts its bits from one word. No CPU code mirrors it: the city's facade kind is `City.FacadeRoll`'s on the CPU and reaches both builds' shaders in the instance.

## Precision: highp, not MonoGame's mediump

**MonoGame writes `precision mediump float;` at the head of every OpenGL ES pixel program**, and an Adreno (the X300 Ultra's 840) computes mediump in 16-bit floats: ten bits of mantissa, so a world position past 1024 units is two units coarse, and the noise, the float hash and the lighting come apart with it. `FinishGlEffects` rewrites that line to `precision highp float;` in the desktop's effects (the same length, so nothing else in the file moves; 183 pixel programs across the 49). The Potato path's nine keep mediump, which they were written for and measured at.

## The desktop path on a phone (#1059)

The Android build draws through the desktop's renderer, its tier picked as on the desktop (#1060, "On a phone" in `docs/game-shell/quality-tier.md`); Potato is a choice on the Quality row. It was a launch argument, `desktop`, while the path was young (#1059). MonoGame's Android backend lacks some things the desktop takes for granted, **whatever OpenGL ES the phone has**. Each is handled where the desktop path meets it:

| Missing | Symptom | Handled by |
|---|---|---|
| a uniform no program reads | GL drops it, DirectX keeps it in the constant buffer, so `Parameters["X"]` is null and `.SetValue` throws a NullReferenceException (the outback, the volcano) | `?.SetValue` where an effect declares a uniform it never reads. CloudField and TerrainPass already did. ⚠ **A new `Parameters["X"].SetValue` on a uniform the shader does not read crashes the phone at start** (every backdrop is built at load) |
| `Texture3D` | "OpenGL ES 2.0 doesn't support 3D textures" | `GraphicsSupport.VolumeTextures`: the cloud volumes are not built and the savanna fire keeps its billboard flames. A 2D slice atlas for both is #1063 |
| resolving a multisampled target | a NullReferenceException in `SetRenderTarget` (MonoGame never loads `glBlitFramebuffer`) | `GraphicsSupport.MultiSamples`: every multisampled target asks through it, and on Android it is 0 |
| a min or max blend (`BlendFunction.Min`/`Max`) | an ArgumentException from `GetBlendEquationMode` on the first draw with one: the sun's shadow map, so every scene with a sun crashed at Medium and up (#1061) | `GraphicsSupport.MinMaxBlend`: on Android the map has a 24-bit depth buffer and its casters a depth test, as before #591 |
| (not understood yet) the ceiling glass's bend | the pane drew as an opaque, overexposed pink slab at Medium and up (#1063) | `GraphicsSupport.CeilingRefraction`: off on Android, so the plate draws as at Low |
| a base instance | "requires at least OpenGL 4.2" (the aurora's spruce stand) | none needed: `InstancedDraws` has no base-instance overload, and a draw from the middle of a buffer binds it from that instance (`InstanceBinding.Set`'s `firstInstance`) |
| a global initialised from `gl_FrontFacing` | the driver refuses the program ("non-constant global initializer"): MojoShader writes VFACE that way | `FinishGlEffects` rewrites it to a `#define` |

A GL program the phone's driver would refuse can be found without the phone: pull the GLSL out of the `.xnb` (it starts at `#extension GL_OES_standard_derivatives` or `#ifdef GL_ES`) and run the Android emulator's `glslangValidator` (`%LOCALAPPDATA%\Android\Sdk\emulator\lib64\vulkan`) on it with `#version 100` in front. That is how the VFACE fault was found in 11 programs at once.

**Measured** on the X300 Ultra at Low, 2026-11-05:
- In play, Cannonball (meadow) and Girandole (city, 1,204 balls): about 48 and 47 FPS at native 3168×1440 (20.7 and 21.2 ms), and the display's 60 at 1080 and at 720 rows (`render=`).
- The front end at 1080 rows, every scene: 60 in the city, the sea and the grid; between 32 (Mars) and 53 (the aurora) in the others.

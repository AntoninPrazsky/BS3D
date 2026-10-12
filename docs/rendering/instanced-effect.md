# The instanced effect's files

Part of [Rendering](../rendering.md).

Everything the game draws that is not a scene backdrop (balls, island, drain, gun, ceiling glass, towers, trophies) goes through **one** effect, `BS3DLibs/Prazsky.Shaders/Content/Shaders/InstancedModel.fx`, because `InstancedModelRenderer` switches technique over one shared parameter set. The `.fx` is the header, `Clouds.fxh`, `Noise.fxh`, `Shadows.fxh`, and an ordered list of includes under `Shaders/InstancedModel/` (#581):

- `Common.fxh` (transforms, rig uniforms, vertex layouts, `MainVS`); `Lighting.fxh` (rig, scene lights, `SkyRadiance`, Fresnel, occlusion, `ShadePixel`, the plain technique); `SceneRelief.fxh`; `BallCommon.fxh`, then `BallColourCue.fxh` (the colour cue prototypes' figures, #737: functions only, so it moves no offset), then one `Ball<Style>.fxh` per shading and `BallTint.fxh` ([styles](ball-styles.md), [kinds](ball-kinds.md)); `Triplanar.fxh` (island surfaces, `capprobe=` probes); `City.fxh`; `Depth.fxh` (shadow caster); `Glass.fxh` (both refraction passes); `PolishedMetal.fxh`; `BallColourCueTechniques.fxh` (the prototypes' one uniform and six programs, last so nothing before it moves).

No renderer is built from a loaded `Model`; the textured/UV/normal-mapped techniques and relief self-shadow marches are gone.

## Rules

- ⚠ **The include order is the constant buffer's layout.** Uniforms land in one `$Globals` buffer in declaration order and the compiler vectorises reads by packing: deleting four dead uniforms changed the code of two programs that never read them. A uniform stays in the file where it was first declared, and **a change meant to be pure is checked by bytecode, not by reading**: compile offline with `d3dcompiler_47`, compare every entry point's disassembly (reflection included), and compare the built `.xnb` (the 38 programs were byte-identical across the split).
- Editing any `InstancedModel/*.fxh` rebuilds the effect (MGCB records the includes).

## The shaders' own warnings (#713)

They show only when the shaders really compile (fresh clone, CI, or after deleting `BS3DLibs/Prazsky.Shaders/Content/obj/Windows`); nineteen became eleven. **Left on purpose:** constant-octave X3557s, non-negative-by-data X3571s (`Blast`, `Flame`), and six X4000 not provably picture-neutral. ⚠ **Any recompile of `Space.fx`, even splitting `Planet` alone, moved the nebula by up to 19 levels of 255** (a baseline against itself moves 1): the volume's march is that sensitive to scheduling. **Pixel equality needs care**: two runs of one build differ unless `nopost` is on and still where anything moves, so use a control run and a plain-background region, and compare instruction streams as opcode multisets. Nothing gates the warning count in CI (`BestPractices.md` §10).

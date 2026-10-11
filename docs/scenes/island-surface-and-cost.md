# The island's surface and its cost

Part of [The scenes](../scenes.md).

How the island's stone is shaded, what the arena costs a frame (#151), and the traps in measuring it. Read before touching the relief, any stone surface's albedo, or the arena's quality entry. Shape and material: [The island](island.md).

## Surface shading

- ⚠ **`Ground_8.png` does not tile and never did**: the stone covers only the left ~1073 columns of its 2048x2048 canvas, the rest is black, so the triplanar projection multiplied half of every surface by zero. The island uses `Prazsky.Core.Render.SurfaceTexture`: procedural stone and concrete on a noise lattice that wraps, with a linear-space mip chain.
- ⚠ **The broken texture was silently an exposure control** (151 grey with it, 181 with a texture whose mean is 1). **An albedo carried over from a surface that used it arrives overexposed.** `DetailBoost = 1 / SurfaceTexture.LinearMean` makes the texture a pure modulation about 1, so the albedo is honest and `DetailStrength` a pure contrast dial.
- ⚠ **A neutral vertical surface reads warm here, and that is the light rig, not the material**: the key is carried halfway to the horizon colour and half a vertical face's hemisphere ambient is the ground bounce. Do not push an albedo blue to cancel it; the concrete is a plain grey near `CannonRig.STEEL_COLOR`. The relief is a sum of waves and past about 0.02 amplitude reads as waves, not roughness; and the specular ambient is not multiplied by albedo, so it, not the colour, decides how *polished* a face reads.
- **The cap's relief is projected gradient noise (#674)**: `SurfaceReliefWorld` is four octaves of `GradientNoise2` on the triplanar planes, rotated between octaves, gain set to the old sines' slope (`SceneRelief.fxh`). ⚠ Seven sines wove: on a flat cap only their projections survive and three gratings crossing are a weave (diagonal ripple bands in every stone scene). ⚠ 3D noise was the first cut and cost too much (Game at High, 3840x1600, Pennant: 12.97 to 13.80 ms, over the 75 Hz budget 13.33; the 2D field costs +0.24 ms). That baseline is itself at the budget's edge.

## What the arena costs (#151)

- **The stone cap is the arena.** Desktop (5900X + RX 6900 XT), meadow, dome 13, 1600x900 at ssaa 4, `nocap`: of 1.240 ms for the whole arena in a 7.74 ms play frame (`campos=0,-4,30 camtarget=0,-8,0`), the cap is 1.091 ms (88 %; 71 % top-down); glass, pit, drum and rims are at or under noise. The weak machine (5700U APU, ssaa 2) agrees on attribution: the arena is 10-14 % of the frame, the cap about 80 % of it. The pass is **fill-bound** (0.242 ms at ssaa 2, 0.989 at ssaa 4).
- ⚠ **A deleted castle was a quarter of the cap's cost**: `TriplanarPS` computed coursed joints, timber seams and a second seven-octave relief (`WoodGrain`) on `isPatterned`/`isWood` that were always zero. Deleted (`MasonryStrength`, `SurfaceStyle`, `SetMeshSurfaceStyle`, `PixelFootprint`); `MortarDepth` survives as `CavityHeadroom`. Saved 0.26 ms, all of it the cap.
- **What the cap's pixel shader spends** (`capprobe=N`, five cut-down copies via `InstancedModelRenderer.TriplanarProbe`; desktop, 1920x1080 at ssaa 4, play camera, `nopost`, `fpscap=150`): shipped 10.971 ms; three relief octaves instead of seven -0.336; no height field -0.660; one triplanar tap -0.029, none -0.137; a constant colour -1.490; not drawn -1.204. The relief is 0.66 of the shader's 1.49 ms and the rest is `ShadePixel`; the texture taps are not what it waits on. The saving follows the shaded pixel count.
- ⚠ **Removing a thing can be cheaper than shading it flat** (the cap occludes the terrain behind it), so `arena=all,-cap` understates it and the flat-colour probe is the honest bound.
- **Quality entry: `ArenaIsland.SurfaceDetail`** (1 authored, below it a coarse three-octave field), wired from the Game's tier as `SceneRenderer.SceneDetail` is, reduced at **`Low` alone** (#298, and Potato). The cap only (the drum measures 0.004 ms: coverage). It is a **second compiled technique** (`InstancedModelTriplanarCoarse`), not a uniform, because a runtime branch over an alternative shading path costs the union of both register allocations in every wavefront of an occupancy-bound pass (#155). **On the weak machine at ssaa 1 it buys 0.00-0.05 ms of 13 (a coin toss): never quote it as a saving that class of machine collects.** No relief at all cannot ship: the coarse field is the stone's only structure at a visible scale.

## Measuring the arena: traps

- ⚠ **`campos=0,0,0 camtarget=0,80,0` draws nothing**: it looks along the up vector, `Matrix.CreateLookAt` degenerates, and two scenes through it are byte-identical clear-colour frames. It is not a sky-only control.
- ⚠ **Two runs of one unchanged variant disagree by more than the effect on the weak machine** (33.6 against 25.7 ms; an integrated GPU shares a 15 W budget). Compare variants inside one process with `alt=` ("Sweeping variants inside one process" in `docs/testbed.md`); the `[fps]` arena field is a comma-separated flags enum, so a harness regex stopping at the first comma drops multi-member runs.
- ⚠ **A maximised window renders at the panel's size** (a 3x frame cost; only the size on `[fps]` says so). Use the **median** of per-second lines: a stray process collapses a run from 91.8 to 31.5 FPS.
- ⚠ **`[build]` cannot tell a library-only change apart** (`BuildStamp` stamps the entry assembly and shaders; `ArenaIsland` is in `Prazsky.Core`): check the library's `LastWriteTime`.
- The grid idles under 2.5 ms, so `fpscap=400` reads the cap there: use `nocap`.

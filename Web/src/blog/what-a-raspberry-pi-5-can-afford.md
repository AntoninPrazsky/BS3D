---
title: What a Raspberry Pi 5 can afford
date: 2026-10-19T11:00:00Z
topics: [Raspberry Pi, Rendering]
summary: "The Pi version draws through a second, much plainer renderer: nine shaders, no HDR, edges drawn instead of filtered. What each choice cost, in milliseconds."
thumb: /img/blog/thumbs/what-a-raspberry-pi-5-can-afford.jpg
---

The Raspberry Pi 5 version of Bubble Shooter 3D is the same game: the same levels, the same physics, the same leaderboards. It does not draw the same way. It goes through a second, much plainer renderer that we call Potato, for two reasons.

The first is a wall. The Pi build uses MonoGame's OpenGL backend, whose shader compiler stops at Shader Model 3.0, and every effect the Windows game has is 5.0. The second is the Pi's graphics chip, a V3D, which could not have afforded the Windows renderer anyway. So Potato has nine effects of its own: one under every model, one for the sky, one that scales the picture up, and plain ports of the shot trail, the laser grid, the blast, the wormhole, the fireworks and the confetti. There is no HDR, no bloom and no shadows. The frame is drawn forward into an 8-bit back buffer, because a 16-bit float target and its resolve cost 9.0 ms against 4.5 drawn straight.

## Shaders only Windows can compile

Compiling an effect for OpenGL means running its HLSL through Microsoft's D3D compiler to Shader Model 3.0 bytecode, then through MojoShader to GLSL. Off Windows that needs Wine, and there is no Wine for ARM64. So the compiled effects are committed binaries, and a CI job on a Windows runner recompiles them on every push that touches the sources and fails when the result differs from what is committed. The compile is deterministic, so the comparison is exact.

One trap there is silent: **a GL shader may index at most one uniform array.** The compiler lays the uniform block out in register order, while MojoShader's GLSL reads each indexed array from a base of its own, and with two arrays the two disagreed. Fireworks and Blast read every array from another's slots, and compiled without a word. Each now takes one packed array, and the compile script refuses any program with more than one `ARRAYBASE_` define.

## The balls were the frame

Girandole has 1,204 balls. At 1080p on the Pi it first took 30.8 ms a frame, and 12.3 ms with the balls not drawn. Coarser spheres brought it to 25.0. Then we drew the balls nearest first and took the `clip()` out of their shader: 26.8 ms with it, 15.5 without, because a shader that can discard defeats the GPU's early depth rejection. The balls that do need a dissolve go in a second run through a technique of their own, so only they pay.

MonoGame also compiles effects without a preshader, so an expression that depends only on uniforms is evaluated for every pixel. We now multiply out the sRGB decodes, the hemisphere terms and the colours on the CPU before the draw. The ball shader fell from 391 to 250 instructions, and Girandole at native resolution from 16.56 to 13.58 ms. The instrument is `V3D_DEBUG=shaderdb`, which makes the driver print its own instruction counts, so there is no run-to-run noise to argue about.

## Edges are drawn where they are

A screen-space anti-aliasing filter guesses the edges from colour and pays for every pixel. FXAA cost 3.3 to 3.8 ms at 720p and four-times MSAA 1.8 ms, and neither held the refresh rate. But Potato knows where its edges are, because it draws them. Every ball gets a ring on its exact silhouette circle, blended, opaque on the outline and fading to nothing a pixel outside (0.3 to 1.2 ms). The island, the drain and the cannon get fins: collapsed quads that open a pixel outward wherever an edge is a silhouette or a sharp convex crease (about 0.7 ms at native).

## Per pixel is too dear

The skies of the space, aurora and dream scenes are smooth, so they do not need a pixel's worth of work each. Computed per pixel (a hash lattice of stars, a curve and a few sines on every pixel of the frame) they cost 12 ms at 1080p, taking Analemma from 13.4 to 25.9 ms. Now a vertex shader computes them on a sphere round the camera and the rasteriser interpolates, as it does for any gradient. The stars are a batch of small quads, since a point is a quad. The ground behind the island is lit at the vertex too: lit per pixel, the same ground cost 3 ms.

<figure>
  <img src="/img/blog/pi-vent-before-after.jpg" width="800" height="900" loading="lazy" alt="Two frames of the same level from the Pi. In the first a cluster hangs over the cannon against a plain dusk sky. In the second a dark volcano with orange lava rivers stands behind it.">
  <figcaption>Vent on the Pi, without a backdrop (top) and with one (bottom): a volcano and its lava rivers, as ribbons that glow. At 1080p the frame went from 15.21 to 16.00 ms.</figcaption>
</figure>

Most of these figures are means of the last six one-second readings of a 26-second run on one Pi 5 at 1080p: good for comparing two builds, not for promising anyone a frame rate. Even the setup moves them, since a VNC client attached to the Pi costs 3 to 5 ms by itself.

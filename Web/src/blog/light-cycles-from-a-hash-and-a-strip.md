---
title: Light cycles from a hash and a strip of triangles
date: 2026-10-30T11:00:00Z
topics: [Rendering, Shaders]
summary: How the Grid's three light cycles are drawn, with no stored path, no sorting and no light but their own edges - a run that is a pure function of a clock, a wall that is a strip of triangles, and a body that is only seams.
thumb: /img/blog/thumbs/light-cycles-from-a-hash-and-a-strip.jpg
---

Antonín's note while we were adding light cycles to the Grid scene was that "how to mathematically render light cycles" deserved a post of its own. Here it is. A light cycle is two things, a rider that moves in right angles and a wall it leaves behind, and both turned out to be a few lines of arithmetic. What follows is what the code does today.

<figure>
  <img src="/img/blog/light-cycles-from-a-hash-and-a-strip/ring-800.jpg" srcset="/img/blog/light-cycles-from-a-hash-and-a-strip/ring-800.jpg 800w, /img/blog/light-cycles-from-a-hash-and-a-strip/ring-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="The Grid seen straight down from a great height: a dark floor with a lattice of pale lines, brighter lines tracing a space-filling curve, the arena's island at the middle, and three thin coloured outlines, orange, blue and red, that are the walls of three light cycles.">
  <figcaption>Capture, from the Testbed: the Grid seen from 200 units up, ten seconds into the scene. The floor is a lattice with a brighter Hilbert curve traced along it; the three cycles are the orange, blue and red walls, each seen edge-on as a line.</figcaption>
</figure>

## A floor that already has a lattice

The Grid's floor stores nothing. A pixel is beside two lattice segments that share a corner node, and three Hilbert indices, three calls of `xy2d` (six bit-folding steps each, because a tile is 64 by 64 nodes), answer for both: a segment is lit when the indices of its two ends are consecutive. The curve is traced along the lattice, never across it, so every lit segment joins to the next. The nodes are 16 units apart, and that 16 is the one number the cycles borrow: they ride on the same lattice, though not along the curve.

## A run is a hash

A cycle's path is a list of nodes. Each new node comes from the one before, a seed, and the node's place in the run, and nothing else:

```csharp
uint h = (uint)_seed * 0x9E3779B1u ^ (uint)_made * 0x85EBCA6Bu;
h ^= h >> 13; h *= 0xC2B2AE35u; h ^= h >> 16;
float roll = (h & 0xFFFF) / 65535f;

Vector2 first = roll < STRAIGHT_SHARE ? ahead : (roll < 0.5f + STRAIGHT_SHARE * 0.5f ? left : -left);
```

`STRAIGHT_SHARE` is 0.6, so at every node a rider goes straight on six times in ten and turns left or right two times in ten each. It never goes back the way it came, and never leaves the ring between 56 and 124 units from the axis: inside that, the island's hole; outside it, the solids, which start at 140. If the first choice would leave the ring, the rider tries another way, and goes back only when nothing else is left.

Where the rider is follows from the clock, not from the last frame. The scene's time times `SPEED` (24 units a second) is a distance along the run, and the position is a linear interpolation between the two nodes it falls between:

```csharp
long segment = (long)(distance / LATTICE);
position = Vector2.Lerp(Node(segment), Node(segment + 1), distance / LATTICE - segment);
```

So there is no integration, no velocity to carry, and no way for a slow frame to put a rider somewhere a fast one would not. A rider spends 16/24 of a second, two thirds, on each segment. Only the last ten nodes are kept, in a ring indexed by `index % KEPT`, enough for the wall behind the rider; the rest of the run is made when the clock reaches it, so nothing is allocated in a frame. If the clock goes backwards the run is made again from its start. The clock is the wall clock, so the cycles ride on while the game is paused.

The turn is as plain as the path: the heading is `(b - a) / LATTICE`, a unit vector along an axis, and the body is turned to it with one `atan2`. At a node the heading changes between one frame and the next. Nothing eases it.

## A wall is a strip

The wall behind a rider is one triangle strip. Each pair of vertices is a foot and a top, 2.6 units apart, one pair at every node the wall passes through and one at each end: the first starts at the back wheel, and the last 110 units further back or at the start of the run. A corner costs nothing, because the strip folds at the node's pair of vertices. Nodes are 16 units apart and a wall is 110 long, so at most seven nodes lie on it: a wall is at most nine pairs, 18 vertices, 16 triangles.

<figure>
  <img src="/img/blog/light-cycles-from-a-hash-and-a-strip/run-800.jpg" srcset="/img/blog/light-cycles-from-a-hash-and-a-strip/run-800.jpg 800w, /img/blog/light-cycles-from-a-hash-and-a-strip/run-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="An orange wall of light standing on a dark lattice floor and bending at right angles, with a small cycle with two ringed wheels at its head, and glowing blocks in the distance.">
  <figcaption>Capture, from the Testbed: the orange cycle from a little above and beside, in the same frame as the picture above. The wall turns where the run turned.</figcaption>
</figure>

Each vertex carries two numbers instead of a colour: its age, 0 at the cycle and 1 at the tail, and its height, 0 at the foot and 1 at the top. The pixel shader makes the glow from those:

```hlsl
float edges = smoothstep(1.0 - TRAIL_EDGE, 1.0, height) + (1.0 - smoothstep(0.0, TRAIL_EDGE, height));
float fade = pow(saturate(1.0 - age), 1.5);
return float4(CycleColor * (TRAIL_RADIANCE * (0.35 + 1.2 * edges) * fade), 1.0);
```

The wall is added over the frame and writes no depth. Addition does not depend on order, so the three walls need no sorting against each other. One trap: the additive blend state scales the source by its alpha, and the first version of the wall did not set its alpha to 1, so it added nothing.

## A body that is only its edges

The scene's one rule is that light comes from the seams. A cycle is a near-black body of three boxes (the body, the cowl, the rider's hood) and two twenty-sided wheels, 196 triangles in all, and what you see of it is where its faces end. Every vertex carries its place on its own face, a quad's `(u, v, width, height)` or a disc's `(x, y, radius, -1)`, and the shader measures how far the pixel is from the nearest border:

```hlsl
float quad = min(min(uv.x, size.x - uv.x), min(uv.y, size.y - uv.y));
float r = length(input.Face.xy);
float disc = min(abs(input.Face.z - r), abs(r - input.Face.z * HUB_SHARE));
float seamDistance = input.Face.w < 0.0 ? disc : quad;
float footprint = max(fwidth(seamDistance), 1e-4);
float seam = 1.0 - smoothstep(EDGE_WIDTH, EDGE_WIDTH + footprint * 1.5, seamDistance);
```

A seam has a half-width of 0.07 units and is softened over one and a half times the pixel's footprint in that distance, so far away it widens to a pixel or two instead of falling between pixels. Both measures are always evaluated and the face then picks one, so the derivatives are taken the same way on every pixel. A wheel's cap has two seams: its rim and a hub ring at 0.38 of its radius. The tyre's facets pin their `u`, so a facet's two side borders never glow and only the two rims of the tyre do. Everything else on the body is one flat dark value a face.

<figure>
  <img src="/img/blog/light-cycles-from-a-hash-and-a-strip/body-800.jpg" srcset="/img/blog/light-cycles-from-a-hash-and-a-strip/body-800.jpg 800w, /img/blog/light-cycles-from-a-hash-and-a-strip/body-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="A close view of the orange cycle: two wheels ringed in orange at the rim and the hub, joined by a thin dark body with glowing edges, and the orange wall beginning behind the back wheel.">
  <figcaption>Capture, from the Testbed: the same cycle closer. The body is almost black; the wheels are ringed at the rim and the hub, and the wall starts at the back wheel.</figcaption>
</figure>

## Why the walls bloom

The glow past the edges is the game's bloom, which keeps the part of a pixel's luminance (Rec. 709 weights) that is over 0.55 and glares in the pixel's own colour. The three cycles are blue (0.45, 1.60, 2.40), orange (2.40, 1.05, 0.18) and red (2.30, 0.30, 0.22) in linear light, a luminance of 1.41, 1.27 and 0.72, all over the threshold. The wall's middle is 0.9 x 0.35 = 0.315 of that colour, which is 0.45, 0.40 and 0.23 of luminance: under the threshold, so the pane itself does not bloom. Its top and foot are 0.9 x 1.55 = 1.395 of it, 1.97, 1.78 and 1.00: over, so the two rims of a wall are what glows. That is arithmetic on the shader's constants. We have not measured it in a frame, and the fade pulls a rim under the threshold as the wall ages: by the same arithmetic, about 36 units back for the red and 60 to 63 for the orange and the blue.

## What we do not know

We have not priced the cycles on their own. What we can count is that a frame draws at most six things for them, three bodies and three walls, and uploads at most 54 vertices of 20 bytes, one wall's 18 each. The Raspberry Pi build draws none of them: its path has no scene renderer. We have not yet watched the turns in motion with the look in mind, and a camera placed inside a cycle is not handled: the chapter's opening flight keeps its lenses near the island and the solids, away from the ring.

The look is not our invention. The shading rule, a near-black body lit only by its own bright seams, is the backlit compositing that gave the 1982 film its glowing costumes, and the Grid's shader credits it. We only borrowed it, and wrote down what it costs in arithmetic.

# The flock of birds

Part of [The scenes](../scenes.md).

A flock circles overhead, the only moving creature in the world and still no sprite sheet. **One flock is shared by the savanna, the desert, the outback and the tropical beach**: `BirdFlock` (a `BackdropServices` service since #580; one rest-pose `BirdMesh`, each bird's orbit, bank and cycle seeded once, one draw per bird), the shader `Birds.fx` and the orbits live once in `BirdFlock` (`SceneRenderer` only builds it, sized to the largest of the four configs). Which scene's `BirdsConfig` it draws with is the caller's, every frame. The savanna's ground is in [The savanna](savanna.md).

## Real geometry, not billboards (#235)

Birds were camera-facing billboards with a `clip()`ed silhouette and a `sin` flap, and failed as #202's acacia billboards did, plus once more: a flat quad shows one silhouette however the camera moves, so a bird could never **bank into the turn it was flying** and its wings could only ever be the straight V two line segments make.

- **`BirdMesh`**: a spindle body with a head on a neck, a fanned tail and wings that end in separated fingered primaries. Shaded from the scene's own sun and dome exactly as `Acacia.fx` and the terrain are.
- **`BirdsConfig.Color` is an albedo, not a finished radiance**: from below against a bright sky the ambient term alone brings it out near black, which is what a backlit bird is, and the sun puts a flash on its back as it banks.
- Drawn **opaque and depth-writing** (a bird is a solid; its breast has to hide the far wing) under `CullNone`, since the wings, primaries and tail are single sheets whose normal the pixel shader turns towards the camera.
- The count, size, place and colour sit in `BirdsConfig` (`Count`, `Wingspan`, `Bob`, `Color`, `FlockCenter`).

## The flap is in the vertex shader

The shape is the rest pose and the whole flap is in the vertex shader, off two per-bird uniforms; each vertex carries its signed spanwise station and its distance forward of the wing's mean line (a twist is a rotation about that line). Nothing is tracked between frames: a bird's whole flap is a function of the wall clock. What stops the motion reading as a machine:

- **The wing is an arc, not a V**: every spanwise station turns by its own angle, a curve that grows faster than the span does.
- **The beat travels out along the wing**, so the tip lags the shoulder and the stroke is a wave rather than a hinge. The stroke is skewed (`theta - K*sin(theta)`, monotonic for `K < 1`, so it retimes the beat without ever running it backwards) into a fast downstroke and a slow recovery.
- **The wrist flexes on the way up** (the hand pulls in and sweeps back outboard of the wrist) and **the hand twists nose-down as it comes down**, which is what a wing does to make thrust. The body rises and falls against its own wings; a body held still while the wings work is the giveaway of a puppet.
- **A bird glides most of the time**: one short burst and a long glide per cycle, about 13 % of the time beating, at 2.1 to 3.2 beats a second, the wings only breathing in the glide.
  - ⚠ The burst's **length is derived** from how many beats it is and how fast that bird beats. Rolled independently, a two-beat burst spread across a long window came out at 0.8 Hz, which reads as slow motion, not as a bird.
  - Both edges of a burst are continuous **by construction, not by smoothing**: a burst runs a whole number of beats so it ends on the neutral point it started on, the glide sweeps exactly one slow turn of the same phase, and the envelope is floored at the value the glide holds. Check: the wing tip's angle step across an edge scales in exact proportion to the sample interval (0.083 degrees at 0.5 ms, ten times less at 0.05 ms).

## Placement

`BirdsConfig.Count` birds orbit `FlockCenter` over the field, each at its own radius, altitude, phase and unhurried speed, all off the wall clock (the kettle wheels slowly and never beats in time).

- **The nose goes where the bird is going**: the circle's horizontal tangent plus the climb its own drift is doing at that instant, so a bird tips up as it rises and down as it sinks, off a derivative and not a dial.
- **The bank leans the bird's up vector towards the inside of its circle** (`Matrix.CreateWorld` squares the basis up from there, so there is no hand-rolled cross product to get the handedness wrong), and it is taken from the **radius** (30 degrees on the tightest orbit to 15 on the widest, wandering slowly; `BIRD_RADIUS_MIN`/`BIRD_RADIUS_SPAN` seed the band), not from `atan(v^2/g*r)`: this flock circles far slower than a real kettle of vultures, and that formula would ask for about four degrees on the wide orbits and seventy on the tight ones.
- One draw per bird of the one shared mesh, because only three uniforms vary and there are nine birds at most: an instance buffer and a vertex format would be machinery for nothing.

## Cost

Nothing measurable. Against the billboard it replaced (6900 XT, 1600 x 900, vsync off): savanna 1228 to 1232 FPS, desert 1220 to 1246, while the city, which has no birds, moved 1449 to 1489 across the same pair of runs. That control is what makes the pair readable: every difference sits inside the run-to-run noise it establishes.

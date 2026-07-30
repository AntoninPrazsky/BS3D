# Best practices

What a full-codebase performance-and-comments review (July 2026) established, written down so new code
follows it from the start instead of being fixed to it later. Every rule here earned its place: each one
names the incident that taught it, in this codebase, and most were verified by measurement — applying the
lot took the Testbed's dense stress map (`Dense20x10x15`, 3000 balls, `autoshoot nocap`) from ~242 to
~294 FPS on the development laptop with zero visual change.

The scene-specific lore lives in `CLAUDE.md` and the documents it points at — the winding conventions in
`CLAUDE.md` itself, colour management and the cloud architecture in `docs/rendering.md`, the per-scene dials
in `docs/scenes.md`. This file is the *general* discipline for frame-loop code and shaders.

## 1. Effect parameters and techniques are looked up by linear scan

MonoGame's `Effect.Parameters["name"]` and `Techniques["name"]` indexers walk the collection comparing
strings — on `InstancedModel.fx` that is a scan over ~70 parameters per lookup. A by-name lookup is fine
at load; per frame it multiplies out fast (the worst case found: `SetLightTint` re-tinting ~9 renderers
every frame through the shared effect = ~54 scans, thousands of string compares a frame).

- **Cache `EffectParameter`/`EffectTechnique` references in fields, resolved once at load.**
  `InstancedModelRenderer.InitializeEffect` and `CloudField.ApplyTo` (a per-`Effect` slot cache that
  preserves null-skip semantics for shaders declaring only part of the field) are the two patterns to copy.
- **Set never-changing values exactly once, at load.** A parameter's value persists on the effect between
  frames — re-sending a compile-time constant (`GlareThreshold`, `Exposure`, trail widths…) every frame
  buys nothing.
- **Keep texture parameters and texel sizes per-frame** (through the cached references): render targets
  are recreated on every resize/fullscreen switch, and a cached *value* would go stale where a cached
  *reference* cannot.

## 2. GPU state objects are native resources — never `new` one per frame

`DepthStencilState`, `SamplerState`, `BlendState` and `RasterizerState` wrap native D3D11 state objects
and are finalizable. Constructing them per frame is a steady leak of finalizer-queue objects and native
state creation — `SkyDome.Draw` was making three of them every frame, in all three executables, for years.
The framework's cached statics (`DepthStencilState.Default`/`.None`, `SamplerState.LinearClamp`,
`BlendState.AlphaBlend`…) express nearly every combination the project needs; a custom state that is
genuinely needed gets built once and kept in a field.

## 3. No managed allocations in the frame loop

GC pressure is frame hitches. The offenders found were all small and all per-frame, which is exactly the
kind that accumulates unnoticed:

- **Iterator methods (`yield return`) allocate their enumerator on every call.** `SkyLitRenderers()` was
  fine when it ran on dome switches; the overcast lerp made it per-frame and the allocation went with it.
  The fix that survives renderer recreation: fill and return one reused `List<>` field, keeping the method
  the single source of truth (no rebuild bookkeeping to forget).
- **String building belongs behind the change, not the frame.** The FPS overlay built `"FPS: " + n` every
  frame for a value that changes once a second.
- Watch also: LINQ in Update/Draw, closures capturing locals, `params` arrays, boxing structs through
  interfaces. None were found hot — keep it that way.

## 4. No console I/O on load or gameplay paths

A console `WriteLine` with an attached console is a synchronized, formatted OS call — around a millisecond.
`BallsMap.PutBallAt` logged every placement: ~3000 lines on a dense map load (seconds of Debug load time)
and one line on every landed shot, mid-gameplay. `SerializeAsJson` dumped the entire serialized map to the
console on every save, in Release too. `BasicCamera3D.MoveCircular` wrote four lines per frame while the
free camera orbited, distorting every orbit-related perf observation made on this machine.

Diagnostics that are worth keeping go behind an opt-in flag or a once-per-second throttle. `#if DEBUG` is
not the answer — Debug is the configuration development actually runs in.

## 5. One input snapshot per device per frame

`Keyboard.GetState()` rebuilds the state from the platform key list on every call; `GamePad.GetState()` is
a real XInput OS query. Beyond the wasted calls, two reads in one frame can *disagree* about a key pressed
between them — and if edge detection (pressed-this-frame) compares against a different snapshot than the
one the action read, an input can be double-fired or lost.

Take one `KeyboardState`/`MouseState`/`GamePadState` snapshot at the top of the frame and pass it down
(the Game's `Update` → `UpdateInput`/`UpdateAim` is the pattern). Found and removed: nine keyboard queries
per frame in `CameraInputHelper`, double pad polls in both the Testbed's and the Game's frame.

## 6. Matrix composition in per-instance loops

Composing `rotation × Matrix.CreateTranslation(p)` is a full scalar 4×4 multiply (64 mul + 48 add) plus a
second matrix construction — and `R × T` is *exactly* `R` with its fourth row set to the translation,
because R's fourth row is `(0,0,0,1)`. In a once-per-ball-per-frame loop (3000 balls on the stress map),
build the rotation once and write `world.M41/M42/M43 = p.X/Y/Z`. Bit-exact, not an approximation. The same
applies to `Identity × T` (just use the translation) and to any orientation known to carry zero translation
(`Matrix.CreateWorld(Vector3.Zero, …)`) — but say so in a comment, since the substitution silently breaks
if someone later gives the orientation a translation.

## 7. Shader work a scene never uses goes behind a `[branch]` on a uniform

A branch on a **uniform** is non-divergent — every pixel takes the same path — so it costs almost nothing
on DX11 and is derivative-safe as long as no `ddx`/`ddy`/`fwidth`/implicit-gradient sample sits inside it.
Two cases found:

- `CityPS` computed the entire neon block (~7 hashes per pixel) on every city pixel and lerped it away by
  `CityNeon = 0` — in the *default* scene. Now `[branch] if (CityNeon > 0.0)`, with the plain-city defaults
  in the else path and the lerps kept inside so a fractional `CityNeon` stays bit-identical.
- `CloudSunlight` evaluated eight hash calls per lit pixel only to multiply the result by a coverage gain
  of zero in the MapEditor (which never sets the cloud uniforms). One early-out line.

The counter-lesson matters equally: know *why* a "branchless on purpose" comment exists before overruling
it. Both of these functions sit near derivative-driven code, and the original comments guarded real
derivative-coherence concerns — which a uniform branch does not violate, but a data-dependent one would.

## 8. Comments are load-bearing — keep them true

This codebase's comments carry the *why*; a wrong one actively misleads, and the review found several that
would have re-taught documented mistakes:

- The launch-smear field comment said "brightest and widest at the muzzle" — the exact bug the feature's
  history records being fixed, stated as the design.
- `CreateGridMesh`'s doc said "16-bit indices, keep n at 256 or below" — the precise constraint whose
  violation caused the mountain sky-band hunt, surviving as advice *after* the code went 32-bit.
- A snow-flake colour doc claimed it sat "under the glare threshold" when its luminance is over it — a
  designer tuning against that guardrail had none.
- `SkyDome` claimed the palette "gets linearized in the shader" when it is decoded on the CPU — in the one
  area (color management) where wrong claims have already caused bugs twice.

The discipline:

- **Update comments in the same change as the behaviour.** A retuned constant, a removed feature, a shared
  file gaining a second consumer ("Testbed-only") — the comment is part of the diff.
- **Reference constants instead of repeating their values** where staleness would mislead: "tops out at
  `MaxElevation` (~80°)" survives a retune; "tops out at 45°" did not.
- **Never leave a wrong *why* standing.** A comment asserting a mechanism ("kept under the threshold so it
  does not bloom") must state the actual mechanism or no mechanism at all.
- XML docs are part of the API: a `<param>` for a parameter that no longer exists is a compiler-visible lie.

## 9. Judgment: not every confirmed inefficiency is worth fixing

Three verified findings were deliberately *not* applied, and the reasons are the practice:

- Caching ~70 scene-shader parameters across ten draw methods: verified win ~1–5 µs/frame — churn and
  regression surface out of all proportion to the gain.
- Deduplicating a ~10-ALU occlusion call in the ball shader: requires touching a function shared by every
  technique, for a win under the noise floor.
- Caching the editor's per-ball occlusion between edits: a missed invalidation draws silently stale AO — a
  visual bug traded for microseconds at realistic map sizes.

A performance fix must clear the bar of **measured impact against regression risk**. Measure before and
after (`autoshoot nocap` on the dense map is the standing benchmark), and verify visually in every scene
the change can touch — the review's rule of thumb was: byte-identical output or a screenshot proving the
difference is intended.

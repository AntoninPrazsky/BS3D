# The 3D title (TitleWordmark)

Part of [The game: shell](../game-shell.md).

The game's name is a 3D object standing in the front end's scene, not a label over it (#248). `Game/Effects/TitleWordmark.cs` sets it in round tube letters: one word to a line in the top-right corner, the last a badge, each letter its own rainbow colour with a keyline, the block drifting and breathing.

## The letters

- `Prazsky.Core/Render/LetterMesh` sweeps a hand-authored mono-line alphabet (`LetterShapes`) as tubes, with `TrophyMesh.BuildHandle`'s fixed frame. Not the real Anton outlines extruded: counters need a polygon triangulator with no precedent here, and an extruded face is one flat plane, while a round section carries the gradient, streak and Fresnel rim that make balls read as spheres.
- `TUBE_RADIUS`: heavier closes B's, O's, D's and 3's counters.
- ⚠ S and 3 are two arcs meeting at the waist with a common horizontal tangent, differing only in which way each bowl is travelled; swept wrong they make a spiral.

## Where and when it is drawn

- **In the HDR scene pass, last, in front of everything (#600)**, not in the sharp foreground layer the cup uses: the front end never defocuses, so that layer would only add a supersampled target to the screen the quality probe measures. The letters get the scene's exposure, tonemap, grain and bloom.
- **Nothing may cut into it.** `BackdropScreen` hands its draw to `BS3DGame.FinishSceneDraw` as the `drawOnTop` slot, run after the weather and fireworks, straight after **clearing the depth buffer**: a clear, not a depth state that ignores the scene, because the letters need depth among themselves (the shells below).
- **The gate is one active-page test in `BackdropScreen`.** `Screen.Enter`/`Leave` fire on push and pop only, and Settings, Scene, About and the picker are pushed over the main menu without popping it, so `Present`/`Hide` on `MainMenuPage` would leave the title behind them.
- It grows in as the splash's logo leaves ([The splash](help-about-splash.md)).
- **"Loading" on black (#637)**: `Game/Platform/LoadingIndicator.cs`, drawn from `LoadContent` between its steps (`ShowLoading`), peeking the message queue so Windows does not call the window hung.

## One composition, and its framing

- **One composition from the first frame (#601).** `Place` solves it once; hue and wave ride reading order. `Draw` takes the camera, the wall clock and `presence`, which the caller derives from the flight (floor `WORDMARK_ASIDE_SCALE`), never a size; the block scales about its anchored corner.
- **Size and anchor come from the camera's own projection, at the sway's extreme**: half-extents `d / M11` and `d / M22` (`TrophyPodium`'s trick). Fitting flat put the last letter on the frame's edge, since sway carries it nearer the lens; the depth bow is zero there and not in the reach.
- **The yaw is biased towards the frame's centre and never crosses back** (`YAW_CENTRE` against the smaller `YAW_SWAY`), since a symmetric sway spent half its time angled away. ⚠ The sign is easy to get backwards: with +x screen right and +z towards the lens, `CreateRotationY` carries +z to `(sin, 0, cos)`, so a positive angle faces the frame's edge and a negative one its centre. The fit takes bias plus sway as its reach; check at 16:9, 1104×861 and 1744×721.

## A rainbow is the second deliberate exception

A rainbow here is the second deliberate exception to the greyscale rule and must argue for itself, or the next accent gets waved through on its example. #180's flat rainbow badge failed (its cyan stop vanished over the meadow). This one is admissible because:

1. It is **not chrome**: it is the game's own name, meant to be looked at.
2. It is a **lit solid**, so it reads by shading and silhouette even where its hue matches the backdrop.
3. Every letter has a **dark keyline**, a weaker leg than it was: the near-black constant one was ruled out as ink from another game, so it plays the rainbow now. It stays dark against a bright scene; on a dark one the aura's floor carries the word.

## Colour spaces, beat and dome light

- ⚠ **Two colour spaces; backwards is silent.** The hue goes in through `Draw`'s `diffuseTint`, which the shader decodes (`SrgbToLinear`): sRGB. The glow goes in through `EmissiveTint`, which it neither decodes nor premultiplies: linear radiance, scaled to one Rec. 709 *luminance* per letter.
- `GLOW_REST`/`GLOW_PEAK` swing the emissive across the glare threshold. ⚠ The trough is a floor: 0.08 failed over the Moon and in space, where the glow is nearly the only light. The crest stays low because the flat emissive term swamps the shading.
- The letters are enrolled in `SkyLitRenderers` at full strength (the cup takes a floored push instead: "The cup the player just won" in `docs/game-feedback.md`). ⚠ Build it in `LoadContent` immediately before the startup `SetScene`, whose first `ApplySkyLighting` a later renderer would miss.

## Three shells per letter

### The keyline

- A fatter tube off the same skeleton drawn with **front faces culled**: its far surface lies behind the letter, so only the ring outside it survives. ⚠ `SpecularAmbientStrength` must be zero: a back face's normal points away from the lens, Fresnel reads grazing, and the ring mirrors the sky.
- Its colour is the letter's hue advanced `OUTLINE_HUE_SHIFT`, held dark at `OUTLINE_VALUE`, and arrives as **emissive** with diffuse black, so the orbiting lights cannot make it breathe. Each letter is its own draw (colour is a per-draw uniform).

### The aura

- A third tube, fatter again, drawn **additively** behind the keyline: a band of the letter's colour the bloom turns into a halo.
- ⚠ **Its width has a geometric ceiling.** A tube whose radius reaches the path's radius of curvature folds through itself inside a bend: at 0.215 against `LetterShapes.MIN_BEND_RADIUS` the folds filled the counters of B, O, D and 3 with flat patches. `LetterMesh` now refuses such a tube; `AURA_WIDTH` is 85 % of the bound less the letter's own radius.
- `AURA_REST`/`AURA_PEAK` are tied to the width by their **product** (the bloom integrates width times radiance), and the crest is bounded by the letters: the full-frame bloom lays blurred light back over its letter, so a brighter halo means a paler word.

### Order and facets

- ⚠⚠ **The order is letter, aura (the "glow" of the code comments), keyline, and it is load-bearing.** The letter goes first because it alone writes depth, so early-Z rejects both unlit shells where it covers them: the aura drawn first cost **2.6 ms of a 26 ms frame**, after the letters 1.16 ms, same picture. The keyline goes last, over the aura: drawn first, the aura's inner edge was the keyline's silhouette, two rasterisations cannot meet exactly, and a thread of backdrop made the outline read dashed. Neither unlit shell writes depth.
- ⚠ **All three shells share one facet count** (`TUBE_SIDES`) so their silhouettes nest. An *n*-gon's silhouette lies between `r·cos(pi/n)` and `r`; with different counts the bands interleave, and each crossing leaves a sliver of backdrop that reads as a dashed outline.

## Cost

About 1.16 ms of a 24 ms frame, front end only (pinned rig: `preview=` fixing the map, orbit frozen). ⚠ Compare only runs with the same preview map at the same orbit bearing.

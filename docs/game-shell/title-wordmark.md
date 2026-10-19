# The 3D title (TitleWordmark)

Part of [The game: shell](../game-shell.md).

The game's name is a 3D object standing in the front end's scene, not a label over it (#248). `Game/Effects/TitleWordmark.cs` sets it in round tube letters: one word to a line in the top-right corner, the last a badge, the block drifting and breathing. It has **two builds on the tier's one switch** (`QualityPreset.FineMeshes`, read per draw): at **High and Ultra the PNG logo** in three dimensions ([below](#high-and-ultra-the-png-logo-929)), at Low and Medium the rainbow wordmark the rest of this page describes. Potato has only the rainbow.

## The letters

- `Prazsky.Core/Render/LetterMesh` sweeps a hand-authored mono-line alphabet (`LetterShapes`) as tubes, with `TrophyMesh.BuildHandle`'s fixed frame. Not the real Anton outlines extruded: counters need a polygon triangulator with no precedent here, and an extruded face is one flat plane, while a round section carries the gradient, streak and Fresnel rim that make balls read as spheres.
- `TUBE_RADIUS`: heavier closes B's, O's, D's and 3's counters.
- ⚠ S and 3 are two arcs meeting at the waist with a common horizontal tangent, differing only in which way each bowl is travelled; swept wrong they make a spiral.

## Where and when it is drawn

- **In the HDR scene pass, last, in front of everything (#600)**, not in the sharp foreground layer the cup uses: the front end never defocuses, so that layer would only add a supersampled target to the screen the quality probe measures. The rainbow build's letters get the scene's exposure, tonemap, grain and bloom; the logo build's are display-referred and skip all but the bloom (below).
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

## High and Ultra: the PNG logo (#929)

The owner: the wordmark at High and Ultra was "not detailed and sharp enough, and does not match the PNG logo" (`Images/logo/bs3d-logo-2048.png`, the splash's). So those two tiers draw the logo itself in 3D, through an effect of its own (`Prazsky.Shaders/Content/Shaders/Wordmark.fx`) rather than the instanced one:

- **The logo's colours, not the travelling rainbow**: `PNG_LETTERS`, one sRGB colour per letter in reading order, sampled off the PNG (each letter's median lit pixel); BUBBLE runs magenta to green, SHOOTER red to violet, the 3 orange, the D cyan. A title of any other length falls back to the ramp. The Konami dance still turns the wheel once, as a hue rotation of the table.
- **Fatter, tighter letters**: `FINE_TUBE_RADIUS` 0.145 against the rainbow's 0.13 and `FINE_DAYLIGHT` 0.04 against 0.10, so neighbours' violet lines run together into one outline round the word, as the PNG's do.
- **The logo's rims**: a dark violet keyline (`FINE_KEYLINE_WIDTH`), a violet line outside it (`FINE_OUTLINE_WIDTH`) and the coloured aura outside both (`FINE_AURA_WIDTH`). All three are shells as in the rainbow build (far faces, depth-read, outside in: aura, line, keyline) and the outermost stays under the fold's ceiling (`LetterShapes.MIN_BEND_RADIUS`), so neither the weight nor the rims can grow without the B's counters closing. The badge's letters take one thick dark rim instead.
- **The badge is a violet disc** (a lathe with a half-round rim, `DISC_*`), shaded as a gradient from magenta-violet at the upper left to blue-violet at the lower right with a dark rim of its own; it rises `DISC_OVERLAP` behind the line above and stands `DISC_BEHIND` the letters' plane. ⚠ **The badge's letters stand `BADGE_LIFT` in front of its face**: their rims are drawn by their far halves, and nearer the disc those sink behind its face and the letters lose their outline. Lines are **centred**, as the PNG stacks them; the fit and the corner anchor are the rainbow's.
- **A studio light, fixed in view space**: a key from the upper left lays a crisp white streak along every stroke, the colour runs deep and saturated into the shade, and a faint fill streak lines the underside. The streak is the half vector **with the stroke's own axis taken out**, which `LetterMesh` writes into the texture coordinate when asked (`tangentCoordinates`); ⚠ with the isotropic half vector a horizontal stroke on the right of the frame never reaches the threshold and carries no streak at all, which is also why the eye is taken straight down the view axis rather than per pixel. Finer meshes than the rainbow's (`FINE_TUBE_SIDES` 32, `FINE_CAP_RINGS` 8, `FINE_ARC_TOLERANCE`): one crisp band shows the facets the soft gradient hid.
- **Display-referred, so it comes out as the picture**: every opaque pass writes a display-linear colour with a **negative alpha**, and the resolve lays those pixels over the mapped frame without the exposure, the ACES curve, the grain or the chromatic aberration ("The display-referred layer" in [Post-processing](../rendering/post-processing.md)). Through ACES a saturated channel at full brightness is infinite radiance, so the PNG's colours could not otherwise be reached; and at the frame's corner the aberration split a letter's edge about two pixels either way at 4K, with the grain strongest in exactly a saturated letter's mid-tones. The aura alone stays light (alpha zero), so the bloom still makes a halo of it. `Draw` returns whether it drew this build and `BackdropScreen` then calls `BS3DGame.MarkDisplayReferredLayer`; `FinishSceneDraw` clears the mark every frame, so no other frame pays for the read.
- **The scene's light does not reach it**: the logo build is not enrolled in `SkyLitRenderers` and looks the same over every dome, as the PNG does. The player's Brightness does not move it either, as it does not move the menus.

## Cost

About 1.16 ms of a 24 ms frame for the rainbow build, front end only (pinned rig: `preview=` fixing the map, orbit frozen). ⚠ Compare only runs with the same preview map at the same orbit bearing.

The logo build costs what the rainbow did: +0.03 and +0.07 ms of a 10.5 ms frame at Ultra, 3840×1581, on the meadow's front end (`preview=1 sceneseed=1 nocap`, the same 50 s of the orbit in each run, two before/after pairs on an idle desktop; the per-second differences spread 0.2 ms). ⚠ Another process on the GPU moves a whole run by a millisecond: check that nothing else is drawing before trusting a pair.

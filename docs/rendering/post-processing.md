# The post-process pipeline: resolve, lens, bloom, defocus, layers

Part of [Rendering](../rendering.md).

The frame is rendered in **linear radiance** and leaves it exactly once. The plumbing (scene target, bloom pyramid, resolve, cached parameters) lives once in `Prazsky.Core.Render.PostProcessPipeline`, run by all three executables; each passes its look figures through `required` properties so none can be forgotten or re-sent per frame.

## The resolve

- The scene draws into a half-float HDR `RenderTarget2D` (`SurfaceFormat.HdrBlendable`, back buffer x supersample factor). `Tonemap.fx` resolves it in one pass: box filter, exposure (`PostProcessPipeline.DEFAULT_EXPOSURE`; `exposure=<f>` overrides as a raw multiplier), ACES, sRGB encode. At a factor of exactly 2 the box filter is **one bilinear tap** (0.74 ms cheaper than four point taps on the laptop APU at 1600x900, #591). Text and crosshair draw afterwards in display space.
- A helper that samples inside a branch must use `tex2Dlod` (a gradient instruction in a branch is illegal even on a uniform condition): why the box filter lives in `SampleScene`.

## Chromatic aberration

Red sampled outward, blue inward, growing with the **square** of the distance from the centre, so the cluster and gun stay registered; peak displacement `CHROMATIC_ABERRATION` (0.0015), resolution-independent.

- ⚠ **One point sample per channel makes three displaced copies of an edge, not a fringe**; on thin lines (the island's slab joints) it read as a flickering rainbow lattice (#126). The resolve samples the **spectrum**: `ABERRATION_TAPS` steps weighted into RGB by overlapping triangles at 0, 0.5 and 1. ⚠ **Never below three taps**: one lands on the green weight alone and the frame renders green. Three costs what three point samples did. Check a strength against the island's joints from a fixed camera (clean at 0.0015, fringing at 0.002).
- The Game's **Aberration** row at Off writes a zero that skips the whole branch; the Testbed and editor run the shipped constant.
- **The slab-joint beads (#351)**: ⚠ `PerturbNormalFromHeight` uses `ddx`/`ddy`, one value per 2x2 quad, so a bevel climbing inside one or two pixels tilts whole quads (rows of 2x2 blobs along grazing joints). Fix in `SlabGrooveAxis`: the bevel's run-out widens at **2.5x** the footprint (the joint's width at 0.5x). Dead ends: Toksvig measures zero variance here; it is the direct highlight, not sub-pixel.

## Film grain

Monochrome per-output-pixel noise re-rolled every frame (seed off the wall clock handed to `Resolve`), applied **after** ACES and before the sRGB encode, weighted by `4 * luma * (1 - luma)` so blacks and highlights stay clean. Peak `FILM_GRAIN` (0.10; 0.05 was invisible); one grain per *output* pixel whatever the supersample factor (`OutputSize`). The **Film grain** row works like the aberration's.

## The display-referred layer (#929)

Pixels the scene pass writes with a **negative alpha** hold a colour that is already the picture (display-linear), not light: where `PostProcessPipeline.DisplayReferredLayer` is set, `MainPS` reads the scene's alpha at the pixel and lays those pixels over the mapped frame **without the exposure, the curve, the grain or the aberration** (the glare, mapped on its own, still lands on them). The Game's logo wordmark (every tier but the Potato path's) is the one writer ([The 3D title](../game-shell/title-wordmark.md#the-png-logo-929)).

- **The sign is the mask** because nothing else writes one: opaque writes land in 0..1, alpha blending keeps them there, additive blending only adds. The multisample resolve and the supersample filter average the layer's -1 with the frame's alpha at its edges; the cover is `saturate(-alpha)`, so a pixel the layer only half covers keeps the frame's own path. ⚠ A new pass that writes a negative alpha joins the layer on the front end.
- **Off is free**: the flag is a uniform `[branch]` and the host clears it every frame before the on-top slot, so the read happens only on a frame that drew the layer. The Testbed and the MapEditor never set it.

## The bloom pyramid (`Glare.fx`)

Bright pass over `GLARE_THRESHOLD` (tinted by the pixel's own colour) into a **half**-size target, dual-filter down to a thirty-second and additive tent upsampling back; `Tonemap.fx` adds the head **before** ACES. `GLARE_INTENSITY` is 0.1. ⚠ **The levels must be `PreserveContents`**: as `DiscardContents` (#69 to #565) MonoGame cleared each level on its second bind, so the head was only the foot upsampled and no tight halo existed; preserved, the head carries five times the energy, hence 0.1.

## Defocus (#178)

The frame taken out of focus by the caller's amount (result screen, pause, and precise aim's periphery, #214). Only the Game asks. "The end of a level goes out of focus" in `docs/game-feedback.md`.

- `defocusFocus` 0 = whole frame alike; 1 (precise aim) = zero dead centre, growing with the square of the distance to the edge (the landing ghost's dither does not survive averaging).
- **Done in linear radiance before the glare add and ACES**: averaging tonemapped pixels smears a firework spark's clipped white into grey. Glare is added afterwards from the sharp scene.
- Runs at a quarter of the back buffer per axis (`DEFOCUS_DIVISOR`) so a 13-tap separable Gaussian spans a huge width; two dual-filter steps down; corner taps placed off the **destination's** texel (the scene target is `SupersampleFactor` larger and would alias). Tap count fixed, **spacing** grows (`DEFOCUS_MAX_STEP`), crossfade early (`DEFOCUS_MIX_IN`), else it reads as a double exposure.
- Targets are lazy (`EnsureDefocusChain`, never in the tools) and bound to the tonemap there, so a resize cannot leave a disposed target bound.
- **Cost:** nothing measurable at ssaa 1 (meadow, 1600x900). At ssaa 2 in play: held precise aim ~48.1 against ~50.5 FPS with `ADS_DEFOCUS` zeroed (~1.0 ms, weakest machine, level 20 Mountain), only while held; the adaptive probe steps such a machine down.

## The sharp foreground and the overlay layer

- **Foreground (#225):** the result page's trophy draws into `ForegroundTarget` (a second HDR target like the scene's, lazy), is bright-passed additively into the bloom head, and `CompositeForeground` tonemaps it onto the resolved frame (same exposure, ACES, grain) through **premultiplied** blending (alpha = box-filtered coverage), so a translucent crystal cup shows the *resolved, defocused* frame through it. It also carries the campaign-complete confetti (#242): **anything joining it must be premultiplied** and gets no scene depth.
  - ⚠ **Draw it in `BeginSceneDraw` BEFORE the scene target is first bound**: MonoGame clears a `DiscardContents` target on bind, so a mid-frame re-bind wiped the scene and left the result page over black on the MSAA path. (The scene target itself is `PreserveContents` since #541 for the ceiling grab.)
  - **A page can present into it too (#856)**: `MenuPage.PresentsForeground`/`DrawForeground`, drawn by `BS3DGame.DrawPageForeground` from the backdrop's and the session's frames **after** their balls and glass (Help's odd balls go through `BallRenderSet.DrawAside`, a second small frame after the main one that keeps the first's motion record, so a session's velocity pass still finds its balls under a Help opened from the pause), which binds the layer mid-frame, clears it transparent, draws, erases everything outside the page's `ForegroundClip` with opaque zero-coverage quads (so a ball scrolled past a frame's edge is cut as its text is, in layer pixels = back buffer × factor) and takes the scene target back — safe since #541 made it `PreserveContents`, photographed intact on both the MSAA (Low) and supersampled (Ultra) paths. The three presenters never share a frame (the result page's two, a menu page's one), so the layer's one bind a frame holds; a presenting page keeps the layer allocated like the cup does. Potato has no layer: `DrawPotatoForeground` lets the page draw straight over the finished frame, uncut, so a page draws only what stands whole inside its frame there.
  - ⚠ **The composite is the last picture of the frame**, after the Myra desktop and before the screenshot writer: `FinishSceneDraw` only records the layer and `BS3DGame.CompositeForegroundLast` spends it at the end of `Draw` (it clears it, so a frame that never reaches the close leaves nothing stale). It must stay ahead of `ServiceScreenshots`.
- **Overlay (#438):** the HUD and crosshair are display-space passes the resolve's defocus never reached. Under a page's blur the session draws them into `OverlayTarget` (back-buffer size, `SurfaceFormat.Color` finished sRGB, no depth, **cleared transparent by the caller**: MonoGame's discard clear is opaque purple); `DefocusOverlay` runs the scene's chain over it (`DrawDefocusChain`, one copy, its own quarter-size target) and `CompositeOverlay` blends it through `Glare.fx`'s `OverlayComposite` under the premultiplied `One / InverseSourceAlpha` blend. `BloomDownPS` and `DefocusBlurPS` carry all four channels (premultiplied coverage must spread as far as colour). The layer is the *first* thing `GameplayScreen.Draw` draws and its blur runs before the scene target is bound.

Glass that bends what is behind it: [Glass](glass.md).

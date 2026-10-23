# The end of a level goes out of focus

Part of [The game: feedback](../game-feedback.md).

A level's ending is the game's own spectacle (fireworks, the camera swinging round the island, the cluster running down the drain), so **`ResultPage.DimsFrame` is false**: no scrim, unlike a pause (a game *put down*; `PAUSE_SCRIM`). What keeps the page legible over the moving arena is the frame **going out of focus** a few seconds in, plus an outline on every line of text standing on the arena.

## The result page's defocus

- **Watched first, softened afterwards.** `BLUR_DELAY_SECONDS` (16 s sharp) is well past the camera's release (`ORBIT_EASE_SECONDS`) and the last star (`RevealTotalSeconds`); then `BLUR_EASE_SECONDS` (16) of smoothstep, zero rate at both ends.
- ⚠ **Reason about 0.3 of the ease.** The blurred copy has taken over by `PostProcessPipeline.DEFOCUS_MIX_IN` (0.3), so the visible change is the first third; a 4 s ease read as a switch (#200), and slowing it by eye spends three quarters of the constant on a change nearly over.
- **Driven from the page.** `MenuPage.FrameBlur` is the whole interface (zero for every other page); the host reads it off the **active** screen at resolve time; it is a pure function of one clock the page's `Update` advances. The session underneath keeps running.
- **The blur is in linear radiance, before the tonemap** ("Color management" in `docs/rendering.md`). Blurring the finished picture would smear a clipped spark to grey; in linear light it stays a glowing orb, and the glare keeps bright things blooming.
- **The page's chrome stays sharp** (drawn after the resolve), so the in-play HUD must already be gone: `GameplayScreen.Draw` stops it when the level is decided, except on a **loss**, where the side cut and magazine strip stay sharp (#639).
- Look at it with `result celebrate` ("Controls" in `docs/game-session.md`).

## Text over a live arena: `ShadowedLabel` (#465, #521)

*Backdrop-dependent legibility is not a dimmer shade of legible*. Each line carries its own backing.

- **`ShadowedLabel`** is a `Label` whose `InternalRender` draws the string first through FontStashSharp's **Stroked** glyph effect (`SHADOW_STROKE`, 3 design pixels) in a dark shadow colour (not black or grey, so the scene's colour shows through), then the type over it. A drop shadow is one line away (`ShadowedLabel.Style` takes an offset and `FontSystemEffect.None`).
- **A shadow, not a plate**: the star row sits between the level line and "New best", so a plate is two or three, and the heading stays unreadable over a bright sky.
- ⚠ **FontStashSharp 1.5.6 has no `FontSystemSettings.Effect`**: an effect is an argument of each `DrawText` call and Myra's `RenderContext.DrawString` passes none. What works is Myra's `DrawRichText` with the effect as a rich-text command before the string (`/es3`; `/eb` blurs).
- ⚠ A `RichTextLayout` given `Width = 0` wraps at every glyph; `null` means no wrapping.
- **NEW BEST is the one line of the stack that is not a `ShadowedLabel` (#926)**: its fill changes colour across each glyph, which a label cannot draw, so it draws its own outline and fill over the page (`NewBestBanner`). ⚠ **The outline is the plain word drawn twelve times round a circle of the stroke's radius (7 design pixels, `OUTLINE_RING`), not FontStashSharp's Stroked effect** (#986): the effect left a stroke-sized square empty at every outer corner of every letter, which at the banner's size the owner saw as squares cut out of the black corners. The copies overlap, so the outline is nearer opaque than `OUTLINE`'s alpha; that shows only in the 0.07 s fade-in. The page's own `ShadowedLabel` lines keep the effect: at 3 units, offset as a shadow, the corners do not read as cut. [The result page](../game-shell/level-picker-and-result.md#the-result-page) has how.

## A pause blurs too

- **It starts at once**: nothing is arriving behind a pause. `PausePage` ramps from zero over `BLUR_SECONDS` (0.35) and **keeps its scrim**; its clock is the wall clock (the `FrameBlur` bullet in `docs/game-shell.md`). Resume takes the blur off at once, since the page that owned the ramp has left the stack.
- **The HUD softens with it (#438).** The defocus is a *resolve* parameter and the HUD a `SpriteBatch` pass over the finished picture, so it was never inside it; pulling it into the HDR pass would tonemap and supersample it every frame. Instead, while the **page's** blur is above zero, the HUD and crosshair go into `PostProcessPipeline.OverlayTarget` (transparent, premultiplied), through the scene's defocus chain (`DefocusOverlay`, alpha-carrying) and back (`CompositeOverlay`, on the `DEFOCUS_MIX_IN` curve) in the slot the direct draw takes. The layer is the **first** thing `GameplayScreen.Draw` draws, before the scene target is bound (DEFOCUS notes in `docs/rendering.md`).
- **Only a page's blur reaches the layer** (`GameplayScreen.OverlayBlur` is zero while the session is active): precise aim's lens is one the *scene* is seen through, the readouts are on the glass in front. The chapter intro's HUD fade uses the layer too ([The chapter intro](chapter-intro.md)).

## Precise aim blurs the periphery (#214)

Holding the aim leans the lens in ("Precise aim" in `docs/game-session.md`): the periphery goes out of focus while the **centre stays registered** (the aimed cell, the landing ghost, whose display-pixel dissolve dither does not survive averaging, and the crosshair).

- The shape is the resolve's `defocusFocus`; the amount rides `PreciseAim.Blend` up to `GameplayScreen.ADS_DEFOCUS`.
- Gates that clear the lean with the session active take the blur out on the blend's ease. A screen pushed **over** the session is a **cut**, accepted: a covered session stops updating and the incoming page's ramp starts from sharp.
- The session is the third `FrameBlur` answerer (`docs/game-shell.md`).

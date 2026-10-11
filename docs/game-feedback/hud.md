# The in-play HUD

Part of [The game: feedback](../game-feedback.md).

What the scoring rules ("Scoring" in `docs/game-session.md`) look like while a level is played, and how they move. Siblings: [magazine strip, chips and muzzle marks](hud-magazine-and-muzzle.md), [the ceiling-warning side cut](hud-cluster-profile.md), [the tutorial card](tutorial-card.md).

## Structure

- **`Game/Screens/PlayHud.cs`** is its own class so the feel's dials are visible together. It owns no score: it is handed the `ScoreKeeper` each frame.
- Drawn in one `SpriteBatch.Begin/End` **after the tonemap resolve**, in display space (a HUD downsampled with the scene is soft). Under a page's blur it goes into the overlay layer and is softened with the frame (#438, "The end of a level goes out of focus" in `docs/game-feedback.md`).
- **Not Myra** (no animation, consumes input). **No shader over the text**: each glyph is its own quad, so a blur is clipped at its edge. Use `FontSystemEffect.Blurry` (rasterized into the atlas) and repeated draws for smear.
- **Corners** are the only space free in both camera poses: score top right (the FPS line owns top left), balls left bottom left, magazine strip bottom right, cluster profile down the left edge. Scaled by `Viewport.Height / 2160` through `Scaled()`. `HUD_MARGIN` must clear the **halo**, or a glow against the frame's edge is cut in half.

## Authored loud

A game HUD is not a data readout (an overlay-sized first pass read as barely there): type runs 140/112/76 design units (score/award/label). The halo is drawn `HUD_GLOW_PASSES` times because **one blur pass is always fainter than its glyph**, and **centred by its own measurement** (`MeasureString` with the effect and amount), since a blurred glyph is a larger bitmap with its own render offset.

## `Pulse`: "this number just changed"

A scalar resting at 1, **displaced** by a kick and sprung back (a second kick mid-settle adds to the first). Snapped to rest under an epsilon, **sub-stepped at 1/120 s** (an explicit spring this stiff goes unstable near 2/ω ≈ 0.11 s, and weak hardware runs at ten frames a second) and saturating at `MAX_OFFSET` (`HUD_FONT_SCORE` is chosen so a fully kicked score clears the frame). **The direction of the kick is the information**: score and streak grow, the ball count shrinks.

## Awards and the score

- A popup is born on its cell, held `POPUP_HOLD`, flown to the corner over `POPUP_FLIGHT` from a source **reprojected every frame**; **landing is what pays**. `_scoreShown` chases `score.Score` *less the points still in the air*, recomputed from the list, so a level torn down mid-flight or a completion bonus with no popup (`FlashScore`) still agree.
- **Every score-like figure goes through `ScoreText`** (`Game/Screens/ScoreText.cs`): thousands grouped with a **space** (`12 340`), locale-independent (`"N0"` once printed one number two ways in a frame), rebuilt only when the number changes.

## Colour: one accent, meaning gain

Amber (`HUD_ACCENT`) survives every scene palette; it goes on the score's arrival flash, the streak and the last two balls. The only other colour is a popup's at birth: the balls' colour **as the lit ball shows it** (`TypeColor`, #153), turning amber in flight.

**Balls left escalates in three steps, only the last a colour:** at `HUD_LOW_BALLS` it grows (`HUD_LOW_EMPHASIS`, a *size*: Anton has one weight) and breathes (motion alarms better than hue); at `HUD_CRITICAL_BALLS` it takes the accent. Breathing, not blinking, off the **play** clock.

## Legibility: a blurred black backing under every readout

⚠ **An offset shadow darkens only ONE side of a letter.** The count sits on the island's concrete deck in every scene, where `MENU_TEXT_DIM` (for asides on a dark plate) is darker than the ground, so the caption has its own `HUD_CAPTION`. The backing is `Blurry` (two passes, in `DrawString`), **tighter than the flash** (`HUD_BACKING_BLUR` against `HUD_GLOW_BLUR`): it must hug the letterform, or it becomes the plate the corners refuse to have.

## The streak

- Warms ×2 to ×5 (`MENU_TEXT` to `HUD_ACCENT`) over `ScoreKeeper.MaxMultiplier`; held back at ×1; a break is **held** `STREAK_BREAK_TIME`, sliding and fading.
- **At the cap** the badge changes face (the loud label), takes a travelling colour sweep and flares once. The sweep is a **hot arc, not a rainbow** (a full wheel's cyan vanished against the meadow's sky); stagger a sixth of the ramp per glyph (a third read as two colours), so it is drawn glyph by glyph, dropping kerning. The glow keeps a floor, or a held ×5 looks ordinary.
- `streak=<n>` pins what the readout **shows**, never the scoring.

## When it stops

A **won** level drops the HUD (`!LevelOver`, #582): the result page states the score, and an award in flight would hang frozen (it ages on the play clock, which the covered frame does not step, #241). A **lost** level keeps the side cut and the magazine strip (#639): `PlayHud.Draw` with `previewsOnly`, drawn **sharp**, never in #438's blurred layer.

## Testing it

- A popup lives about 1.05 s: capture in a burst, cropped to the corner.
- **Drive menus with Enter alone, never Down** (a held Down passes `NAV_REPEAT_DELAY` and can open the Scene page), and confirm focus first (`UpdateMenuChrome`'s `IsActive` block).
- A scripted `rmbdown` never released stays held at OS level after the process is killed; the next run's menu clicks vanish.

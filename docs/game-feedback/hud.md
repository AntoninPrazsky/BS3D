# The in-play HUD

Part of [The game: feedback](../game-feedback.md).

What the scoring rules ("Scoring" in `docs/game-session.md`) look like while a level is played, and how they move. Siblings: [magazine strip, chips and muzzle marks](hud-magazine-and-muzzle.md), [the ceiling-warning side cut](hud-cluster-profile.md), [the tutorial card](tutorial-card.md).

## Structure

- **`Game/Screens/PlayHud.cs`** is its own class so the feel's dials are visible together. It owns no score: it is handed the `ScoreKeeper` each frame.
- Drawn in one `SpriteBatch.Begin/End` **after the tonemap resolve**, in display space (a HUD downsampled with the scene is soft). Under a page's blur it goes into the overlay layer and is softened with the frame (#438, "The end of a level goes out of focus" in `docs/game-feedback.md`).
- **Not Myra** (no animation, consumes input). **No shader over the text**: each glyph is its own quad, so a blur is clipped at its edge. Use `FontSystemEffect.Blurry` (rasterized into the atlas) and repeated draws for smear.
- **Corners** are the only space free in both camera poses: score top right (the FPS line owns top left), balls left bottom left, magazine strip bottom right, cluster profile down the left edge. Scaled by `Viewport.Height / 2160` through `Scaled()`. `HUD_MARGIN` must clear the **halo**, or a glow against the frame's edge is cut in half.

## Authored loud

A game HUD is not a data readout (an overlay-sized first pass read as barely there): the score is 140 design units and its labels 76, and an award zooms well past the score (below). The halo is drawn `HUD_GLOW_PASSES` times because **one blur pass is always fainter than its glyph**, and **centred by its own measurement** (`MeasureString` with the effect and amount), since a blurred glyph is a larger bitmap with its own render offset.

## `Pulse`: "this number just changed"

A scalar resting at 1, **displaced** by a kick and sprung back (a second kick mid-settle adds to the first). Snapped to rest under an epsilon, **sub-stepped at 1/120 s** (an explicit spring this stiff goes unstable near 2/ω ≈ 0.11 s, and weak hardware runs at ten frames a second) and saturating at `MAX_OFFSET` (`HUD_FONT_SCORE` is chosen so a fully kicked score clears the frame). **The direction of the kick is the information**: score and streak grow, the ball count shrinks.

## Awards and the score

- **An award zooms, is read, shrinks and is thrown** (#846, `Game/Screens/AwardZoom.cs`, the curves pinned by `AwardZoomTests`). Born on its cell (reprojected every frame, rising over it), it zooms from near nothing to **1.5 times the score's type for a bare match and up to 3 times for a big release** (`AwardZoom.Peak`, logarithmic in the points, 2 000 and up being big) and is **read for 0.55 s up to 1.2 s**, zoom included, swelling a little. The read (`Hold`) grows with the *square* of that weight, because it is the time a number stands over the cluster being aimed at: a match of three on the capped streak (150) is read 0.65 s, a 1 260 release 1.07 s. It then **shrinks where it stands** to half the score while the throw's wind-up (`HudEase.InBack`) is still behind its source, and is thrown into the corner over `AwardZoom.FLIGHT` (0.55 s), turning amber, with a smear. **Landing is what pays** and kicks the score. `_scoreShown` chases `score.Score` *less the points still in the air*, recomputed from the list, so a level torn down mid-flight still agrees.
  - ⚠ **One big number at a time, in turn: handed on, not merged.** A landing is one award (a bomb's chain is scored with the landing that set it off), so two awards together are two shots landing close behind each other. The newer one takes the read and every award still being read leaves for the corner now, once it has been read for `MIN_READ` (`AwardZoom.HandOff`), so each is seen whole, on its own cell, in its own colour with its own multiplier, and the corner is hit once per shot. Two big numbers stand together only for `MIN_READ` less the gap between the landings: none at 0.4 s apart, about 0.1 s for shots fired 0.3 s apart (photographed: the newer one is drawn over the older, which then shrinks off). A merged total was rejected: it would have to show one colour and one multiplier for two shots born on two cells, and the player would lose what each shot was worth.
  - **The completion bonus has an award too**: white (no ball earned it), captioned "bonus", born in the balls-left count and rising out of it once the clearing shot's award has been read (`AwardZoom.BonusWait`, at most 0.6 s), sending that award on its way as a newer one would. It is often the biggest figure of the level and the reward for playing well, and without an award it counted up in the corner out of nowhere while the collapse played. Wait, longest read and flight come to 2.35 s, inside the 2.5 s cleared beat, so it lands before the page (and long before it under a drop cinematic, which holds the beat while the HUD keeps ageing).
  - **Its type is a ladder of faces** (`PlayHud.AwardFace`): the label's 76, 112, the tutorial card's 200, 320 and `HUD_FONT_POPUP_PEAK` at the top of the zoom's overshoot, the smallest at least as big as the size drawn, no two rungs more than 1.8 times apart. One face for every size would be shrunk six times on landing (the atlas has no mips, so it shimmers) or stretched and soft at the peak. The backing's blur and the shadow's offset follow the size drawn, not the face's scale, so nothing steps between faces.
  - Held `POPUP_EDGE` inside the frame at the size it has, so a zoomed number born near the top is not cut off.
- **Every score-like figure goes through `ScoreText`** (`Game/Screens/ScoreText.cs`): thousands grouped with a **space** (`12 340`), locale-independent (`"N0"` once printed one number two ways in a frame), rebuilt only when the number changes.

## Colour: one accent, meaning gain

Amber (`HUD_ACCENT`) survives every scene palette; it goes on the score's arrival flash, the streak and the last two balls. The only other colour is an award's at birth: the balls' colour **as the lit ball shows it** (`TypeColor`, #153), turning amber in flight (the completion bonus is born white).

**Balls left escalates in three steps, only the last a colour:** at `HUD_LOW_BALLS` it grows (`HUD_LOW_EMPHASIS`, a *size*: Anton has one weight) and breathes (motion alarms better than hue); at `HUD_CRITICAL_BALLS` it takes the accent. Breathing, not blinking, off the **play** clock.

## Legibility: a blurred black backing under every readout

⚠ **An offset shadow darkens only ONE side of a letter.** The count sits on the island's concrete deck in every scene, where `MENU_TEXT_DIM` (for asides on a dark plate) is darker than the ground, so the caption has its own `HUD_CAPTION`. The backing is `Blurry` (two passes, in `DrawString`), **tighter than the flash** (`HUD_BACKING_BLUR` against `HUD_GLOW_BLUR`): it must hug the letterform, or it becomes the plate the corners refuse to have.

## The streak

- Warms ×2 to ×5 (`MENU_TEXT` to `HUD_ACCENT`) over `ScoreKeeper.MaxMultiplier`; held back at ×1.
- **At the cap** the badge changes face (the loud label), takes a travelling colour sweep and flares once. The sweep is a **hot arc, not a rainbow** (a full wheel's cyan vanished against the meadow's sky); stagger a sixth of the ramp per glyph (a third read as two colours), so it is drawn glyph by glyph, dropping kerning. The glow keeps a floor, or a held ×5 looks ordinary.
- **A lost streak breaks, and is heard breaking** (#845), so the player notices the loss and wants to chain shots or reach for the Swap: the badge **cracks** (`STREAK_BREAK_CRACK`, 0.16 s: it swells about its right edge, jolts sideways, flashes white-hot, dark cracks run across it) and **shatters**: its glyphs are thrown apart (the × to the left, the digit dropping) and tumble down with shards of it, keeping the colours they wore for a beat, then cooling to `STREAK_LOST` grey and going out, while **"×5 lost"** rises where it stood in the loud face. `ProceduralAudio.PlayStreakBreak` plays the falling run ([sound effects](sound-effects.md#a-streak-breaking-845)). **Everything scales with what was lost** (`StreakCues.Severity`, ½ for ×3, 1 for the cap): a lost ×3 swells to 1.6 times, throws 11 shards and lasts 1.1 s; a lost ×5 swells to 1.9 times, throws 16 and lasts 1.4 s, and its sound is two notes longer and louder.
  - ⚠ **Size is what makes it seen.** The first cut squeezed the badge as it cracked and threw label-sized pieces that cooled to grey at once: at 1600×900 the whole break was a smudge twenty pixels high, pale on a pale sky. The badge is the HUD's smallest figure, so it grows as it breaks, the pieces keep its size and colour for a while, and every shard has a dark copy of itself under it.
  - **The session decides whether it is told** (`GameplayScreen.AnswerLostStreak`, at the very end of both doors a shot resolves through without scoring, `OnShotSpent` and `OnBallLanded`): never once the level is decided or its last shot spent (`StreakCues.BreakIsTold`), where it would play over the result; there the badge simply goes.
  - Every piece is a closed-form throw from a hash of the break's seed, drawn from the break's age alone: nothing listed, nothing allocated per frame.
- `streak=<n>` pins what the readout **shows**, never the scoring, **until a shot breaks it**: the first shot that resolves without scoring breaks the pinned streak as it would a real one and lifts the pin, so a script can photograph the break of a ×5 nobody earned.

## When it stops

A **won** level drops the HUD (`!LevelOver`, #582): the result page states the score, and an award in flight would hang frozen (it ages on the play clock, which the covered frame does not step, #241). A **lost** level keeps the side cut and the magazine strip (#639): `PlayHud.Draw` with `previewsOnly`, drawn **sharp**, never in #438's blurred layer.

## Testing it

- An award lives 1.1 s (a bare match) to 1.75 s (a big release): capture in a burst. A scratch level loaded with `levelfile=` (a rock lid with one blob of one colour under it) makes a one-shot big release, and with nothing else removable the same shot clears the field and shows the bonus. Shots `fire=`d 0.3 s apart at three cells photograph the hand-on.
- ⚠ **State the aim** (`aim=4.5:33.8:0` is the rest pose): a run with `fire=` and no `aim=` has fired from a gun turned off its rest pose and missed, where the same command landed on another day. Something live on the desk (the cursor, a pad's stick) still steers a gun no `aim=` holds.
- **The break** (#845): `level=23 seed=1 streak=5 aim=5:-5:0 fire=7 logshots` fires into the drain, and `[streak] x5 lost at 7.29 s, told` says when the break began on `shot=`'s clock; a 1600×900 burst at 7.33, 7.42, 7.6, 7.8 and 8.1 catches the crack, the shatter and the words. At 3840×1600 take one shot a run: a PNG that size blocks the frame long enough to move the next.
- **Drive menus with Enter alone, never Down** (a held Down passes `NAV_REPEAT_DELAY` and can open the Scene page), and confirm focus first (`UpdateMenuChrome`'s `IsActive` block).
- A scripted `rmbdown` never released stays held at OS level after the process is killed; the next run's menu clicks vanish.

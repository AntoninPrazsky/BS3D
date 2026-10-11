# Block and campaign endings

Part of [The game: feedback](../game-feedback.md).

A cleared level is an ordinary ending; finishing a **block** of levels (a chapter) and finishing the **campaign** each get their own, so the fanfare and fireworks ([The victory fireworks](victory-fireworks.md)) are not the same three components over and over. Both decisions are taken once, in `GameplayScreen.BeginClearedBeat` (called from `CheckLevelCleared`), and stamped into `_blockCompleted` and `_campaignCompleted`.

## Finishing a block (#184)

What a block is lives in the set (`LevelSetEntry.Block`; "The campaign is nine blocks of five" in `docs/formats-and-tools.md`): a run of consecutive entries sharing a name. Everything here is gated on a set naming blocks, so a set that does not (every set authored before #184) plays as it always did.

### The rule

- **Finishing a block is every level of it cleared, not "the last level of it cleared".** The picker gates on the campaign's star total, so a block can be played out of order; asking whether the whole run is cleared is right however the player got there, and the milestone fires on the clear that completes it even if one level was left for later. A level counts as cleared when it holds at least one star (the picker's own test).
- **It fires on the transition, not the state.** `BS3DGame.WouldCompleteBlock` asks "would clearing this now finish it": every other level of the run cleared and the run not already complete. Without the second half it would fire every time the player went back for a better rating.
- **The decision is taken once, early, and that is forced.** The fireworks and fanfare start the instant the field empties and the result page arrives `LEVEL_CLEARED_BEAT` later (the clear is written into progress on the way), so a decision taken on the page reaches the effects too late and asking twice asks two different questions (before and after the record). Three consumers read the one flag; they must never disagree.
- **Never both milestones at once**: `LevelResult` suppresses the block when `CampaignComplete` is set. The bigger wins.

### What it looks, sounds and says like

- **The heading is the chapter's own name** (`THE TOWER`, upper-cased at display so a set may name blocks in prose) where an ordinary clear says `CLEARED`, with `Block 4 of 7 complete` under it (counts read off the live set). A different string over an identical page was the `CAMPAIGN COMPLETE` precedent that #184 was raised about. The second line is the only new widget on the page, `Visible = false` on every other ending.
- **A longer opening barrage, not a longer display** (`BLOCK_CELEBRATION_OPENING`, 8 s against the ordinary 2.2). The opening is the part the player is watching and the launch interval is already at its minimum there, so it is the one dial that reads as bigger rather than longer; 8 s is about 115 shells against the 32 the shader carries at once, so the sky stays saturated instead of filling and emptying. `Fireworks.Celebrate` takes the **longer** of two openings (and the longer duration), so a second call can never cut a display short.
- **The fanfare plays at full intensity whatever the level scored** (`PlayVictory(score, grand: true)`), so a chapter closed on a scraped two-star clear does not sound like a poor win.
- **The log line**: `[level] Cleared …` ends with `[block 4/7 'The Tower' COMPLETE]`; it names the block either way, so a clear that did not complete one still says which chapter is open. This is the only way a real play-through reports whether it fired.

### Checking it

`blockdone` makes the `result` page a block milestone (finishing a block cannot be reached without playing five levels). Not machine-verified: the five-levels-then-fire path end to end. The set's block queries and the loader's refusal of a non-contiguous block are checked against `LevelSet`, and that check caught a real defect: `null == null` made every entry of an *unblocked* set one run, so `BlockRange` returned the whole campaign and `BlockCount` said 1.

## The campaign ends in confetti (#215)

Clearing the last uncleared level of the set starts paper confetti falling around the camera for `CONFETTI_SECONDS` (105 s), on top of the fireworks. `Game/Effects/Confetti.cs` owns it and `Confetti.fx` draws it; it follows the fireworks' structure exactly (on the host, one static vertex buffer, one draw call, everything animated in the vertex shader out of one clock).

- **The decision is `BS3DGame.WouldCompleteCampaign`: every other entry cleared and the campaign not already finished** (`WouldCompleteBlock`'s question one scale up, so replaying the finale is an ordinary clear). ⚠ It used to read "the last *entry* of the set" (#347): unlocks are gated on a star total, so a player could open and clear entry 105 with earlier levels never played and be told the campaign was complete. `ShowResultScreen` reads the stamped flag rather than deriving it again, and stays gated on `cleared` because a *loss* on the last level reaches that line without the decision ever being taken.
- **Why a new effect and not a bigger barrage**: by a block's end the opening is at 8 s of already-maximum density with `MAX_SHELLS` saturated for all of it, so there is nothing above it. The last ending needs a different *kind* of thing; the two run together.
- **It outlasts the fireworks on purpose** (105 s against 60): the display thins itself after `RELAXED_AFTER`, so exactly when the player reads the campaign's last score the sky is quiet. Paper costs nothing to keep falling and never shouts. It has no delay of its own (the fanfare is protected by the display's). The clock is **never reset**: every piece's position is `frac()` of it, so a reset would teleport the whole field. The fade in (0.7 s) and out (3.5 s) do the starting and stopping.
- **9 000 pieces (`PIECES`), and 4 000 was too few**: individually legible, evenly spaced, the eye counted them. Density is what a celebration is read as big by, before size, brightness or duration. The ceiling is the 16-bit index buffer (16 384 pieces); 9 000 is 36 000 vertices.
- **The quad is not a billboard, and that is the only reason it reads as paper.** It is built on a real world-space basis rotating about the piece's own tumble axis, so it flips broadside, foreshortened, edge-on as a thin bright line and broadside again; that flashing between a wide chip and a line says paper rather than dot. The tumble axis is biased horizontal (a chip spinning about a vertical axis never presents an edge). `Snow.fx` keeps its flake facing the lens, which is right for a speck.
- **The light is the effect's own**: a two-sided `abs()` lambert on the piece's normal over an ambient floor (`AMBIENT`), two-sided because confetti is printed on both faces and a one-sided term would blink half the field to black every turn. It carries its own direction, not the scene rig's, so it does not go black in the cavern.
- **Alpha-blended, not additive**: paper is an object that hides what is behind it, and being tonemapped with the frame keeps it lit rather than luminous. The palette sits mostly under `GLARE_THRESHOLD`, so only the brightest broadside flashes of white and gold glint.
- **The box follows the camera and wraps with `frac`** (`Snow.fx`'s trade: a little parallax for never popping as the camera crosses a boundary). It is tighter than the snow's (58x48x58 against 70x55x70) and the near fade much tighter (`NEAR_FADE` 1.15 against 7), because a big piece rushing past the lens is most of what sells it.
- **It draws into the sharp foreground layer, outside the HDR pass** (#242), so the result page's defocus does not melt it: the owner wanted it as sharp as the trophy.
  - **It keeps the tonemap**: the composite runs the layer through the same exposure, curve and grain, and its bright pass still feeds the bloom.
  - **Accepted losses**: it no longer covers a firework it crosses (the shells are behind the whole layer); it loses scene occlusion (the layer's depth is not the scene's, so the island and cluster no longer hide a chip falling behind them); and it is drawn *before* the cup, so the cup still covers it (#225's ruling that the presented object is the nearest thing).
  - ⚠ **It had to become premultiplied to live there**, in both `Confetti.fx` and `Confetti.cs`: the layer is cleared transparent and composited by coverage, and `BlendState.NonPremultiplied` squares the alpha (`a*a + dst*(1-a)`), so partly covered chips under-report. Straight alpha was correct only while this drew over an opaque scene.
- **Cost: nothing measurable** (meadow, 1600x900 at ssaa 2x, paired repeats: 1.722/1.737 ms with none, 1.734/1.738 ms with all pieces falling; the spread between identical runs was twice the difference).

`confetti` on the command line starts the fall on the front end. The shot worth keeping is `result celebrate confetti` over the mountains, the one scene that already has falling snow: confetti and snowflakes in one frame read as different things, which confirms the billboard decision. Verified across the defocus ramp at both `High` and `Medium` (ssaa 1 + MSAA 8x), plus a run with confetti and no result page at all, which must be the frame it always was.

# The "fire" hint for a player who has stopped

Part of [The game: feedback](../game-feedback.md). The cards it must not talk over: [the tutorial](tutorial.md), [the tutorial card](tutorial-card.md).

A card leaves when its action is done, and nothing watched the player after that: a newcomer who has just learned to turn the gun can stop with nothing to say fire (the owner's playtest of Cannonball, 2026-10-18). **In the first five levels of the first chapter** (`FireHint.LEVELS`, `FireHint.CarriesHint`: Pennant to Pinwheel, `Tutorial.TryPlace`'s chapter 0 and level 0 to 4), a bold chip says **"Fire!"** beside the fire button's glyph when no shot has left the gun for a while, pulses once a second, leaves the instant a shot is fired and returns in that level only after a much longer pause. `Game/Screens/FireHint.cs` holds the rules (pure, pinned by `FireHintTests`); `PlayHud.DrawFireHint` draws.

## The pause is measured, not guessed

- The owner's own play of the first chapter (his `Stats.json`, 2026-10-18; `seconds` is the level's play clock, so it includes everything that is not aiming and overstates the gap): the five levels cleared in **46 shots over 231.8 s, 5.0 s a shot**; the levels' own averages run from 2.9 s (Pinwheel) to 13.5 s (Zigzag).
- **`FIRST_PAUSE_SECONDS` is 10, twice the typical gap**: past what a player who is playing takes between shots, short of "has stopped". **`LATER_PAUSE_SECONDS` is 30** (the pause before it comes back in that level, once shown) and **`STAND_SECONDS` is 12** (how long it stands if nobody fires). `FireHintTests` holds the first pause against the measured figure.
- Only a **shot** ends the pause (`NoteShot`, from `Shoot`): turning, walking and aiming, the precise-aim hold included, are what a hesitating player does.

## Nothing else is speaking, or it does not come

`FireHint.Distractions`, filled by `GameplayScreen.FireHintDistractions` each frame: the Tutorial row off, a decided level, a camera takeover (drop cinematic, chapter tour, line loss), the release still held for the tour, **a tutorial card up, arriving, leaving or queued (`Tutorial.CardStands`: the gap between two cards counts)**, the ceiling's alarm (the glass sliding or its flash lit), the floor's net lit, a shot still in the air. **While any holds, the chip is taken down and the count stays at zero**, so the chip never arrives on the heels of a card or an alarm, and never over one; a chip that was up and is taken down counts as shown (the longer pause applies). The pause page and the result page never get here (the screen is covered and not updated); a lost level draws the field's parts only, which has no chip.

## How it looks

- **The tutorial card's own two faces** (`HudFontPrompt` for the glyph, `HudFontTutorial` for the word, so the loudest standing type in the game), the word in the accent over the white glyph, the HUD's blurred backing under both. It arrives with the card's pop-in (`HudEase.OutBack`), leaves in `LEAVE_SECONDS` (0.15 s: the shot has been fired).
- **The beat** (`FireHint.Beat`: a kick at each whole second of its age and a cubic settle): the chip swells `BEAT_SWELL` (14 %) and the accent's halo flares and thins, once a second. Drawn from the age alone, nothing allocated.
- **Where:** centred, its top edge `StreakCues.ClearancePixels` below the middle of the frame (the lost streak's own clearance, mirrored: clear of the crosshair at its most swollen, in the crosshair's scale), so the beat grows away from the aim point. At the rest pose that is on the barrel under the muzzle's ring.
- **The glyph is the fire button's for the hand in use** (`Tutorial.SkipGlyphFor`: the left mouse button, the right trigger), the hand a level starts from being the menus' ([the tutorial card](tutorial-card.md)).
- The word is held to the typesetting rules (`FireHintTests` runs `CopyRules.Fault` over it).

## Testing it

Scripted runs pass `userdata=<dir>` whose `Progress.json` has the lessons already taught (`aim`, `fire`, `match`, `lean`, `ceiling`, `line`), so only Cannonball's own card (the orbit) stands. `turn=5:12` completes that card the way A held does (`turn=` and `walk=` report to the tutorial since #925), and the log's `[firehint] up at <s>` and `down at <s>` lines, on `shot=`'s clock, say when the chip was up.

```
BS3D.exe level=Cannonball nosplash windowed width=1600 height=900 seed=1 sceneseed=1 userdata=<dir> turn=5:12 shot=14,18.4,19.0
```

The card leaves at about 7.9 s, the chip is up at about 17.9 s and down at about 29.9 s (`STAND_SECONDS`); `fire=` shots every 8 s keep it away, and `level=Diabolo` (the sixth) never shows it. `pad` shows the trigger.

## Not done

- The pause is one figure measured from one player's totals, a fast one. A newcomer's gap between shots has not been measured (no outside player has played yet, #768); the constants are the first thing to retune.

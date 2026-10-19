# The tutorial: lessons, ladder and save

Part of [The game: feedback](../game-feedback.md). The card's look, device and detection: [the tutorial card](tutorial-card.md).

The first chapter teaches the game **as it is played**, one thing at a time, as a **card over the play HUD** (a keycap, mouse button or trigger, a line of what to do, a smaller line under it); an action lesson is joined by a word of praise when done. Teach **slowly** (a player buried in instructions stops reading), make it **fun** (not a manual), keep an **off switch**, on by default.

- `Game/Screens/Tutorial.cs` owns lessons, order and state and **draws nothing**; `PlayHud.DrawTutorial` draws; `GameplayScreen` feeds it events. Pinned by `TutorialTests` (the real class, walking the shipped set) and `TutorialCardLayoutTests`.
- The second chapter's first two levels teach the score (#666). The power-up cards (`swap`, `brake`, `cut`): "The tools' cards" in `docs/game-session.md`.

## One thing a step, in plain imperatives (#930)

The owner: players have a short attention span, respond to quick clear instructions and do not enjoy deciphering text; the cards are for ordinary players, not for him or an agent. So:

- **A card says what to do, with the glyph for the hand in use, and nothing about why** (the Help page holds the why). The big line is one imperative ("Hold to look down the barrel", "Match three of a colour"); the small line, when there is one, is a limit or a how in a few words ("One swap a level", "Shoot beside two of the same colour"). A notice that is not an action (the glass, the line, the score) is one plain statement or an imperative in the same few words.
- **A card never asks two things.** One glyph names one control (or one pair of keys on one axis, A and D, W and S); the lines hold no second clause (no "and", comma, colon or dash joining two thoughts, no second sentence) and few words. `TutorialCopyTests` holds every instructing step of both hands to it, and fires on the strings it replaced. The send-off (a reward) and the kind cards (they say what a ball is, checked against the Help page by hand) are not steps.
- **A card that has to ask two things is two steps** (`Definition.Second`). Today only the combination (`combine`, Diabolo): step one **"Hold to look down the barrel"** with the right button or left trigger (the same words as the precise-aim card, which taught it four levels before), step two **"Now walk the gun round"** with A D or the left stick sideways. The card **turns in place** to the second step when the hold has been kept 0.35 s and the first step has stood `STEP_MIN_SECONDS` (1.2 s, so a button already down cannot flash it by), with the chime and the whole card's kick; it is **praised ("Together!") and recorded when the second step is done** (the button and a movement on one frame, 0.35 s), and a card left half done times out unrecorded like any action. If the button is let go for `STEP_BACK_SECONDS` (0.5 s) the card goes back to step one, which is the one that says what to hold. The lesson's key is `combine`, unchanged, so a save that taught it before the split is not taught it again. The reel (`tutorial=demo`) shows each step for its clock.

## The ladder

Each lesson has the level it first becomes eligible at; the opener carries only the three that make the game a game. A lesson the player did not get to **follows them into the next level**, and into the next chapter, until the chapter's send-off has been read: deferred, never skipped. Past the second chapter nothing is offered (`Tutorial.TryPlace`; `TUTORIAL_CHAPTERS`, `UNCHAPTERED_LEVELS`).

"7 s" is `INFO_SECONDS`, "0.35 s" `HOLD_SECONDS`; the kinds are explained below. Action and informational cards show when the level opens (after the card before them).

| key | from level | kind | shown on / done when |
|---|---|---|---|
| `aim` | 1 · Pennant | action | aim travelled `AIM_TRAVEL` (~3.5°) |
| `fire` | 1 | action | a shot leaves the barrel |
| `match` | 1 | action | a landing scores |
| `lean` | 2 · Rainbow | action | precise aim held 0.35 s |
| `ceiling` | 2 | contextual | a *pressure* step of the glass (never a feed step); read 7 s |
| `line` | 2 | contextual | the floor alarm's net coming **on** (never a `lasers` run's pinned net); read 7 s |
| `traverse` | 4 · One | action | A/D or left stick held 0.35 s |
| `walk` | 5 · Pinwheel | action | W/S or left stick held 0.35 s |
| `combine` | 6 · Diabolo | action, **two steps** | step 1 precise aim held 0.35 s; step 2 precise aim AND A/D-or-W/S **together** 0.35 s (#460, #930) |
| `streak` | Gallery 1 · Heart | contextual | multiplier first above ×1 after a landing (the badge lights); read 7 s |
| `budget` | Gallery 2 · Smiley | informational | read 7 s |
| `linerule` | Saturn (chapter end −3) | informational | read 7 s |
| `graduated` | Amphora (chapter end −1) | informational, **celebrating** | read 7 s, after `linerule` |

- **The closing cards are counted from the chapter's END** (`FromLevel` −3 and −1): a literal index drifted when the opening changed. The campaign opens on flat sheets (#603, "The flat opening" in `docs/formats-and-tools.md`), so walking the gun waits for **One**, the first level with a far side.
- **A short chapter crowds its cards rather than cutting any.** `line` says what to do in the moment ("Drop a group to lift the cluster"), `linerule` what to keep off ("Keep the cluster off the line"): both stand. The `ceiling` caption is built per level from `ceilingStep`; a level whose glass holds still never arms it.
- **The score waits for the second chapter (#666):** even ScoreSim's sloppy player two-stars every Meadow level (what `MinStarsAt` asks), so the multiplier works unexplained. `streak` and `budget` sit on the Gallery's first two levels, **one idea a level**; a lesson names its chapter (`Definition.Chapter`). ⚠ `tutorial` (Force) on a Gallery level replays the first chapter's ladder first.

### The send-off

`graduated` reads **"That’s the basics — you know how to play"** over "On to the adventure!"; a card with no praise row.

- **Nothing is shown after it (#605):** a send-off followed by "The glass steps down every 7 shots" is a lie. `Tutorial.NothingAfterTheSendOff` queues any contextual lesson still untaught as a plain card **ahead** of it, **except `line`** (a warning about a moment, false at the start); after it nothing more is taught in the chapter. A fresh run's queue on Amphora: `… combine, linerule, ceiling, graduated`.
- **It closes the first chapter's ladder only.** An owed lesson follows the player into the second chapter (`Tutorial.Eligible`): losing Amphora early means `linerule` and the send-off on Heart. **A later chapter's cards wait for an owed send-off.**

## Three kinds of lesson, told apart by how they end

- **Action:** stays up until the game reports it done, then is **joined** by its praise word (*Nice!*, *Boom!*, *Perfect!*, *Sharp!*, *Smooth!*, *Closer!*, *Together!*): amber, the score's blurred halo and spring (`PlayHud.KickTutorial`), the rating's chime (`PlayStarEarned`). It **never blocks** and gives up after `ACTION_TIMEOUT` (22 s) **unrecorded**, returning next level.
- **Informational:** holds `INFO_SECONDS`, recorded as read.
- **Contextual** (glass, line, streak, the rainbow ball, the brake): armed at the level's start, shown when the event fires. It **interrupts** the card up (`StepAside`: back to the queue's front, unrecorded) and fires at most once a level.

### Praise: joined, held, and nothing already read moves

- ⚠ **Joined, with a minimum (#466).** The card used to *flip*, and the fastest-finished lessons are the first met (a nudge cleared the aim card in a fraction of a second). Now the instruction stands `MIN_READ_SECONDS` from arrival **whatever happens**, the praise holds `PRAISE_SECONDS` from the action, and the card leaves when **both** have run; the action is recorded, and chime and spring fire, on its own frame.
- **The praise goes under the detail** (a row above must be reserved from arrival, or shoves the instruction down).
- ⚠ **When the praise lands, nothing already read moves (#673).** Once it joined the block the card was centred and scaled as, so the block re-centred (17 to 48 px at 1080p), the bob stopped and the kick swelled everything. Now `TutorialCardLayout` lays the instruction out from glyph, caption and detail alone; the praise hangs under it and takes the kick **from the middle of its left edge** (a swell about its centre pushed *Together!* into the glyph's column) with its own strip clamp; the bob keeps its clock; an action done while the card is **still arriving** lets the arrival finish under the praise.

## What the save remembers (#715)

- A completed lesson is written at once to `PlayerProgress.Lessons` (`"lessons"`; an older build ignores it and teaches again) via `BS3DGame.RecordLessonTaught`, and is not offered again as owed.
- **Starting a level shows again the cards that level itself introduces** (`Tutorial.OwnLevel`), while the Tutorial row is on. **Once per entry:** a Retry or Restart remembers (`BuildLevel(index, retry: true)`, from `BS3DGame.RetryLevel` alone), every other start clears. A replayed send-off does not make the player "not yet sent off". **A replay writes nothing** (`Teach` records only what the save lacks).
- **The Tutorial row under CONTROLS** (`GameSettings.Tutorial`) is read **every frame** (`BS3DGame.IsTutorialEnabled`; the page opens from the pause): off takes the card down at once, unrecorded; on resumes it. It hides cards and forgets nothing; **Reset progress** clears the record.

## A card for every kind, the first time it is met (#735)

- Eleven lessons (`Lesson.Kind…`, saved as `kind-bomb`, `-zap`, `-glass`, `-stone`, `-buckshot`, `-acid`, `-frozen`, `-infectious`, `-gravity`, `-heavy`, `-wildcard`; **stable names: a rename re-teaches every player**), each a shortened line of the Help page's entry checked against `BallKind.cs`.
- **They belong to no chapter** (`Definition.Kind`): `BeginLevel` gets the level's kinds (`kindsOnMap`, from `GameplayScreen.ReadKindsOnMap`) and queues a card for each not yet taught, **ahead** of the ladder's, on any level (the first is level 42's glass, the Silhouettes' Umbrella, #736), in campaign order (glass, buckshot, bomb, zap, stone). The send-off's rules are the ladder's alone (`OnLadder`). The wildcard's card is contextual, fired by the first wildcard in the bore's front slot; the Cutter is its tool card's.
- **The ball on the card is live** (a still would age as kinds are redrawn): `HUD_TUTORIAL_BALL` keeps a square where a keycap stands, `PlayHud.TryGetTutorialBall` hands its place to the next frame, `GameplayScreen.CollectTutorialBall` adds a ball `TUTORIAL_BALL_DEPTH` in front of the lens, after the shadow pass.
- ⚠ **It must go into the motion record, pinned** (`_motionPinnedBack`): left out, `MotionBlur.fx` gives an empty velocity pixel the camera's motion at the cluster's depth, and the ball smears while the text stands sharp.
- **Not done:** a LevelGen gate that a level introducing a kind introduces nothing else; the Help page and the cards are kept in step by hand.

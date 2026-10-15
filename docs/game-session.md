# The game: a session

What a level is while it is played: the simulation, the cluster, the gun, the level files and the score. The host (`BS3DGame`) outlives a session; `GameplayScreen` is the session. The rules a player lives by (what a power-up costs and from which chapter, what scores, what unlocks) are the owner's decisions; each sub-document keeps the rule with its reason.

## Sub-documents

- [The session and the host](game-session/session-and-host.md): host versus session, the partial `GameplayScreen.*.cs` files, `SessionTestOptions`, the seeded generator.
- [Controls, launch arguments and driving the Game](game-session/controls-and-testing.md): controls, every testing argument, `shot=`/F12, resize, cursor capture, test traps.
- [Recoil, precise aim and the arena](game-session/recoil-and-presentation.md): shake and recoil, ADS in the Game, clouds, the occlusion direction, the dished island.
- [The shot, the landing and the landing preview](game-session/landing-and-preview.md): how a shot lands, the ghost and beam, why preview and landing agree, the elevation clamp's rubber.
- [Levels and the campaign](game-session/levels-and-campaign.md): `LevelSet`, the fourteen blocks, set-entry rules, authoring rules.
- [A level from start to end](game-session/level-flow.md): winning, `LevelPhase`, losing (line, grace, ceiling sum), the start swing, soft lattices.
- [The field, the camera fit and tall levels](game-session/field-and-camera.md): `FitFieldToMap`, the world offset, the solved camera and gun, tall columns.
- [Scoring, stars and progress](game-session/scoring-and-stars.md): `ScoreKeeper`, streak, star rating, `RulesVersion`, unlocks, the sequence rule, the skip.
- [The magazine](game-session/magazine-and-transmute.md): which colours may be loaded, transmute, the slot, the dither cross-fade.
- [Special balls: the kinds a level places](game-session/special-balls.md): rock, glass, bomb, zap, acid, frozen, infectious, buckshot; what ships.
- [Special balls: shot and mass](game-session/special-balls-shot-and-mass.md): gravity, heavy, the wildcard.
- [Power-ups](game-session/powerups.md): Swap, Brake, Cut: grants, prices, chips, cards, the cutter's rules.
- [Online scores](game-session/online-scores.md): what is sent, losses as attempts, the outbox, which refusals are final, the worker.
- [Boards and High Scores](game-session/online-boards-ui.md): the result plate, level boards, the High Scores page.
- [Play statistics](game-session/play-statistics.md): starts, retries, clears, losses, quits and skips per level in `Stats.json`, the report, and the anonymous batches sent with online scores on.
- [Crates](game-session/crates.md): the reflecting wall and its level field.
- [Physics in the game](game-session/physics-in-the-game.md): fixed step and frame, lattice versus world frame, the contact handler, culling, wind.

## Where the old sections went

Code comments cite sections by title; this table resolves them. Quoted titles are bold paragraphs and bullets that were cited or are searched for.

| Old section | Now in |
|---|---|
| The game: a session (title) | this page |
| The game (Game) | [Session and host](game-session/session-and-host.md), [Controls](game-session/controls-and-testing.md), [Recoil](game-session/recoil-and-presentation.md), [Landing](game-session/landing-and-preview.md) |
| Levels in the game (Game) | [Levels](game-session/levels-and-campaign.md), [Level flow](game-session/level-flow.md), [Field and camera](game-session/field-and-camera.md), [Magazine](game-session/magazine-and-transmute.md) |
| Tall levels: a column framed from the bottom (Game) | [Field and camera](game-session/field-and-camera.md) |
| Scoring (Game) | [Scoring](game-session/scoring-and-stars.md), [Magazine](game-session/magazine-and-transmute.md), [Special balls](game-session/special-balls.md), [Shot and mass](game-session/special-balls-shot-and-mass.md), [Power-ups](game-session/powerups.md) |
| Online scores (Game) | [Online scores](game-session/online-scores.md), [Boards](game-session/online-boards-ui.md) |
| Crates: a wall to bank a shot off (Game, #257) | [Crates](game-session/crates.md) |
| Physics in the game (Game) | [Physics](game-session/physics-in-the-game.md) |
| "Recoil and camera shake", "Precise aim", "The clouds are the Testbed's weather, minus its ambient half", "The ambient-occlusion direction", the dish and washer bullets | [Recoil](game-session/recoil-and-presentation.md) |
| "Controls", "The cursor", `userdata=<dir>`, the frame-saving, resize and test-trap bullets | [Controls](game-session/controls-and-testing.md) |
| "The command line's testing levers..." (`SessionTestOptions`), "The session's generator is seeded per level" | [Session and host](game-session/session-and-host.md) |
| "The landing preview", "A shot the ghost promised a cell to", "The landing is the preview's own sweep", "Both marks blink red (#431)", the swept-sphere, snap, glass (#432) and beam bullets | [Landing](game-session/landing-and-preview.md) |
| The pattern-pack and campaign-block paragraphs, "The order lives in the set", "Nothing about a missing or broken level is fatal" | [Levels](game-session/levels-and-campaign.md) |
| "A cluster that merely swings has not lost", "Soft lattices", LevelPhase, clearing and the implied-limit sum | [Level flow](game-session/level-flow.md) |
| "The camera and the gun are solved to the field", the field-fit bullets, the Tall levels bullets | [Field and camera](game-session/field-and-camera.md) |
| "a destroyed ball is a third scoring category", "Every shot resolves exactly once", the star, progress and skip paragraphs | [Scoring](game-session/scoring-and-stars.md) |
| "Neither may the barrel show more loaded rounds than there are shots left", "A ball already in the barrel...", the colour-census and dither bullets | [Magazine](game-session/magazine-and-transmute.md) |
| "Special ball kinds" and the `BallKind` bullets Rock to Buckshot | [Special balls](game-session/special-balls.md) |
| The `BallKind` bullets Gravity, Heavy, Wildcard | [Shot and mass](game-session/special-balls-shot-and-mass.md) |
| "The Swap is in the campaign since #213", the brake, "The anchor cut", Cutter, "The tools' cards" | [Power-ups](game-session/powerups.md) |
| "High Scores", "The boards, shown (#547)" | [Boards](game-session/online-boards-ui.md) |
| "The air over a scene" (wind), the #241, #634, #265 and kill-plane bullets | [Physics](game-session/physics-in-the-game.md) |

# The scoring simulator (Tools/ScoreSim)

Part of [Formats and tools](../formats-and-tools.md).

`Tools/ScoreSim` plays **every shipped level several ways through the real `ScoreKeeper` and `StarRating`** and **refuses a scoring rule that rates them wrongly**. In `Game.sln`, plain `net10.0`, and it **goes in front of a commit** (non-zero exit on a refusal; a malformed option exits 1).

```
dotnet run --project Tools\ScoreSim\ScoreSim.csproj [levels directory] [--ceilings out.json] [--ceiling-scale f]
dotnet run --project Tools\ScoreSim\ScoreSim.csproj -- --stats [Stats.json]
```

With no directory it walks up from its bin directory to the first `Game\Levels`. It exists because #173's rating was **ordered backwards** (the score rose with shots taken) while every number looked reasonable: the fault shows only in the ordering *between* play styles. **It models none of the scoring, it calls it**, so it cannot drift, and it reads ball counts and budgets from the shipped set.

## The refusals (all non-zero exit)

- **Every style is timed** (#1045): the clean clears in 4, 8 and 16 shots at half par (`FastPars`) and again at twice par (`SlowPars`, the time bonus gone), the whole budget less one clean and the whole budget with a third missing at `ParRule`'s pace (10 s + 2.5 s a shot). The row prints the level's par and the slow clear beside the fast ones.
- **INVERTED**: an efficient clear rates no better than spending the whole budget. **UNREACHABLE**: the best clean clear at half par cannot earn four stars. **UNEARNED**: a clear missing one shot in three still earns four.
- **TIME INVERTED** (#1045): a clean clear at half par rates no better than the same clear at twice par, which is what a level with no par does. **SLOW FOUR STARS**: a clean clear at twice par still earns four stars (four stars need a fast clear, the owner's ruling).
- **GATE TOO STEEP** (#1045): an entry's `minStars` asks for more than `GateStarsPerLevel` (1.5) stars for every entry before it (the second may ask for one). Under rules v2 a newcomer's clear earns 2.2 stars on average, and a ramp of two a level walled a short run of one-star clears.
- **OVER CEILING** (#549): a simulated clear reaches `ScoreKeeper.ScoreCeilingAt` its own time (since #1045 the ceiling at the claimed time, which is what the service checks a clear against). The service refuses a score over it, so this means the bound is missing a term.
- A level with an **unlimited budget** is skipped (nothing to rate).
- **STAR FLOORS** (#1024): a level's least score for 2, 3 or 4 stars does not sit exactly where `StarRating.Rate` turns, or the three do not rise. The service rates a clear by them, so a floor one point off would refuse real clears.
- **`--ceiling-scale <f>`** multiplies every ceiling before the check and writes nothing, so the gate can be seen to fire anywhere (`0.25` refuses every level, exit 1).

## A save's play counts (`--stats`, #709)

**`--stats [path]` prints a `Stats.json` instead of simulating anything** (the player's own, `%LOCALAPPDATA%\BS3D\Stats.json`, without a path; exit 1 when there is none): per level in play order the starts, the retries with their share of the starts, the clears, the losses with their share of the attempts that reached a result page and split by cause, the quits, the skips, the starts a first clear took, and the shots and seconds an attempt. A counted board the set does not name as shipped (an older version of a level, another rules version, a `levelfile=` run) is listed after the set. How the counts are kept: [Play statistics](../game-session/play-statistics.md).

## The ceilings (#543, #549)

Every row ends with the level's `ScoreCeiling`, the best simulated score as a fraction of it, and `minShots`; the header names `ScoreKeeper.RulesVersion`.

- **The ceiling is a bound above every reachable score, not the best clean clear**: each term at its maximum (the level's balls *and* every ball the budget can fire, at the richest rate and ×5, plus the completion bonus for all but one unused shot). The derivation is on `ScoreCeiling`, so the next rule change re-derives it there.
- **`minShots`** is `ClearProbe`'s anchor-course floor, stricter: a top-course ball can only leave in a match and one shot matches one colour; **any special anywhere drops it to 1** (a blast takes anchor balls, a zap empties a colour, an infection repaints).
- **The time floor and the ceiling at the claimed time** (#1045): a time in the score makes a forged short duration worth points. `minSeconds` is 0.15 x the level's par (`MinSecondsShareOfPar`; one player's gap between two clears after v0.6.0 held a clear and its result page inside 0.57 par), and the service checks a clear against the ceiling **at its claimed time**: `ceiling - timeBonus + timeBonus x max(0, 1 - seconds / (timeBonusZeroAtPars x par))`, `ScoreKeeper.ScoreCeilingAt`. The game counts whole seconds begun, so the exact claimed time never refuses a real clear.
- **`--ceilings <out.json>`** writes the table the score service loads: `"format": "bs3d-ceilings"`, the rules version, the hash length, `timeBonusZeroAtPars` (#1045) and one row per budgeted level in play order: `file`, `name`, `hash`, `rulesVersion`, `shots`, `ceiling` (the whole time bonus included), `minShots`, (#695) the level's `block` when the set names one (nothing ranks or checks by it), (#1024) `starFloors`, the least score for 2, 3 and 4 stars, and last (#1045) on a level with a par `par`, `minSeconds` and `timeBonus` (the most the time bonus pays, `ScoreKeeper.TimeBonusMax`). Still format version 1: a reader that does not know the time fields ignores them, and the `ceiling` it does know is still a bound above every clear. A level with an unlimited budget has no row. `release.yml` attaches it as `BS3D-<version>-ceilings.json` to every release. **It is written even when a gate failed** so it can be looked at; the exit code refuses. Hashes are the game's `LevelIdentity` ([Formats](level-formats.md)).
- **`starFloors`** (#1024) let the service refuse a clear whose stars its score does not earn (AntoninPrazsky/BS3D-API#16): the game rates a clear by its score alone, and the service rates it one star plus one for each floor the score reaches. Each floor is `StarRating.LeastScoreFor`, found by asking `Rate` itself (it compares a `float`, so re-derived arithmetic can land a point off), over the ball count the game rates by: the map's `GetBallsCount()`, which is its `InitialBallCount`. **A retune of `StarRating` needs no `RulesVersion` bump**: it moves the floors under an unchanged board key, and the service accepts the floors of any table that names a board, since a release already out keeps rating by the old ones. A service that predates them ignores the field, and a table without them leaves the stars unchecked.

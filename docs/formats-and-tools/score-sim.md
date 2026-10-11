# The scoring simulator (Tools/ScoreSim)

Part of [Formats and tools](../formats-and-tools.md).

`Tools/ScoreSim` plays **every shipped level several ways through the real `ScoreKeeper` and `StarRating`** and **refuses a scoring rule that rates them wrongly**. In `Game.sln`, plain `net10.0`, and it **goes in front of a commit** (non-zero exit on a refusal; a malformed option exits 1).

```
dotnet run --project Tools\ScoreSim\ScoreSim.csproj [levels directory] [--ceilings out.json] [--ceiling-scale f]
```

With no directory it walks up from its bin directory to the first `Game\Levels`. It exists because #173's rating was **ordered backwards** (the score rose with shots taken) while every number looked reasonable: the fault shows only in the ordering *between* play styles. **It models none of the scoring, it calls it**, so it cannot drift, and it reads ball counts and budgets from the shipped set.

## The refusals (all non-zero exit)

- **INVERTED**: an efficient clear rates no better than spending the whole budget. **UNREACHABLE**: the best clean clear cannot earn four stars. **UNEARNED**: a clear missing one shot in three still earns four.
- **OVER CEILING** (#549): a simulated clear reaches `ScoreKeeper.ScoreCeiling`. The service refuses a score over it, so this means the bound is missing a term.
- A level with an **unlimited budget** is skipped (nothing to rate).
- **`--ceiling-scale <f>`** multiplies every ceiling before the check and writes nothing, so the gate can be seen to fire anywhere (`0.25` refuses every level, exit 1).

## The ceilings (#543, #549)

Every row ends with the level's `ScoreCeiling`, the best simulated score as a fraction of it, and `minShots`; the header names `ScoreKeeper.RulesVersion`.

- **The ceiling is a bound above every reachable score, not the best clean clear**: each term at its maximum (the level's balls *and* every ball the budget can fire, at the richest rate and ×5, plus the completion bonus for all but one unused shot). The derivation is on `ScoreCeiling`, so the next rule change re-derives it there.
- **`minShots`** is `ClearProbe`'s anchor-course floor, stricter: a top-course ball can only leave in a match and one shot matches one colour; **any special anywhere drops it to 1** (a blast takes anchor balls, a zap empties a colour, an infection repaints).
- **There is no time floor** (#549 asked for `minSeconds`): nothing spaces shots out (`Shoot` has no interval; several fly at once), so the only floor is one flight, a tenth of a second. A real floor needs a fire interval, the owner's call.
- **`--ceilings <out.json>`** writes the table the score service loads: `"format": "bs3d-ceilings"`, the rules version, the hash length and one row per budgeted level in play order: `file`, `name`, `hash`, `rulesVersion`, `shots`, `ceiling`, `minShots` and (#695) the level's `block` when the set names one (nothing ranks or checks by it). A level with an unlimited budget has no row. `release.yml` attaches it as `BS3D-<version>-ceilings.json` to every release. **It is written even when a gate failed** so it can be looked at; the exit code refuses. Hashes are the game's `LevelIdentity` ([Formats](level-formats.md)).

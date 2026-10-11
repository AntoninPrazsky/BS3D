# Special balls: shot and mass

Part of [The game: a session](../game-session.md).

Gravity changes what a **shot** is, Heavy the **simulation**, and the Wildcard is the kind the player **shoots** (the Cutter: [Power-ups](powerups.md)). Kinds a level places: [Special balls](special-balls.md).

## Gravity (#332)

An ordinary matchable ball that **pulls on shots in flight** (not debris, not the cluster). ⚠ **The hard part is the preview**: `ShotPlacement` exists so ghost and attach cannot disagree, and a curve breaks the straight-line half. So `TryFindFirstHitCurved` integrates **the simulation's own arithmetic at its own step** off **the same snapshot of wells** (`GravityWells`, refreshed once a frame, so two readings cannot drift by the cluster's sway). The field is **bounded**: the kernel reaches zero, value and slope, at `RANGE`, so the flight is exactly straight outside every well. The beam follows the flight. What reaches the landing is the level's business: `Gravity.json` hangs wells on stalks and the generator refuses a **buried** well. `AimReachability` deliberately does not know about wells (necessary, no longer sufficient); `SagProbe` says such a level is finishable. ⚠ A boundary nearer than one integration step advanced the flight clock by nothing and the solver looped for ever.

## Heavy (#333)

An ordinary matchable ball with `BallsConstraintsBuilder.HEAVY_MASS_RATIO` times the mass. The mechanic is the simulation alone (one line in `BuildBallsStructure`: a different `BodyInertia`); no handler, match, release or scoring code knows it, so `SagProbe` repeats no step and the feedback is free (a branch that droops explains itself).

- ⚠ **The ratio is measured and the measurement is the design**: a `BallSocket` between very different masses jitters, and the cliff is the **stretch** (1.044 on a normal cluster, 1.027 at x12, **1.402 at x100, a lattice torn open**; figures on the constant).
- ⚠ **The sag is a property of the LOAD, not the mass** (x12 at the lowest cell of a dense cluster moves it 0.02 units): a deeper droop **hangs more off the ball**. ⚠ A single capture cannot measure it (the cluster sways): compare means over undisturbed hangs.
- **A deliberate sag moves neither the threshold nor the death line** (a branch under the line has lost whether the weight was designed or not); the probe learns **attribution** (`SagProbe.HeavyBaseline`: rehang with ordinary weights). Tearing is deliberately not built. `LevelGen` refuses a heavy ball with an empty lattice under it.

## Wildcard (#330)

**The only kind the player SHOOTS**: it never appears in a layout, touching the magazine, muzzle and HUD. It **cycles through the colours** in queue, bore and flight and **collapses to one colour on landing**.

- **It becomes the colour completing the largest group** (`BallsMap.TryChooseWildcardColour`); ties go to the colour the landing touches most balls of, then the lower `BallType`, so identical-looking landings never differ. **Beside nothing matchable it keeps the colour it was showing.** The cycle walks **only colours still hanging** (from `RecountBallTypes`), or it would park an unmatchable ball. The collapse is at the top of the landing (`ProcessContact`, before `PutBallAt`), because everything below reads the shot's `BallType`. Log: `[shot] wildcard landed as Type2, group of 29` / `... beside nothing matchable, kept Type1`.
- ⚠ **No ball in the lattice is ever a wildcard**: a hanging one is not matchable and nothing triggers it, so the level could be neither cleared nor lost. Held at `BallsMap.PutBallAt` (clamps a kind `BallKinds.InCluster` refuses) and `BallKinds.Next`.
- **The cycle is the game's, not the ball's** (`WildcardCycle`, one per session): a wildcard shows in five places and per-ball phases would cycle out of step (#175: two testers read a colour off the wrong ball). It runs at `WildcardCycle.CROSSING_SECONDS` and **never settles**, which tells it from a transmute. ⚠ A single screenshot says almost nothing: compare two frames a fifth of a crossing apart.
- **How the player gets one is a difficulty rule**: `wildcardEvery` in the set entry, one in every N balls **dealt**, counted rather than diced (visible three balls ahead and plannable; counting deals puts one in the first queue). `wildcard=<n>` overrides it. **Scoring is untouched**; `ScoreSim` does not model wildcards.

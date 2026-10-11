# LevelGen's probes: sag, shortest clear, arrival, cuts, drops

Part of [Formats and tools](../formats-and-tools.md).

The static gates read the layout as authored ([what the tool refuses](levelgen-gates.md)). These play it. Only the shortest-clear gate runs on every invocation.

## The sag gate: `--sag`, `--sagfile=` (`SagProbe`, #301, #302)

**Every static gate is true of a level that cannot be finished, because none knows what the remainder weighs: the gate that says the links exist cannot say there are enough of them to carry the weight.** `SagProbe` stands up the game's own simulation (`PhysicsWorld`, the kinematic glass at `CeilingPlate.CentreYAbove`, the island's funnel floor, `BuildBallsStructure`, hung where `ClusterHang.FitWorldOffset` hangs it; `SolveDescription(8, 1)`, 1/120 s steps, the glass descending), plays each level five ways and reads the death line off the live bodies through the game's own `ClusterLineWatch` (`ClusterHang` and `ClusterLineWatch` are shared so the two cannot drift). No graphics device; opt-in (whole levels in physics). `--sag=Pylon,Orrery` narrows it and traces shot by shot; `--sagfile=<path>[,<path>]` (#333) hangs files outside the set (no budget, a glass at rest).

- **The flight is not simulated.** A real `Cannon` is swept with `ShotPlacement.TryFindFirstHit` (`…Curved` for gravity wells, #332) and the landing mirrors `BallContactEventHandler` step for step, **including its order** (the body is *put* in its cell before its constraints, #265).
- **⚠ Every step of a landing that lives in the contact handler must be repeated in the probe, or it measures a different game.** Repeated so far: `ColourTransparentGroup` between attach and group check (and in `WouldMatch`, with an exact undo); `LoadedColour` over matchable balls only; ending on `GetRemovableBallsCount` (a rock level would report `OutOfShots`); bombs collected before the release and detonated after; infection ticking per **resolved shot** in the shot loop. **A trigger keyed to a call, not a call site, needs no line here**: ice thaws inside `ReleaseSameTypeCluster` (#329), an orphaned bomb detonates inside `ResolveDisconnected` (#396). Reach for that shape.
- **Heavy balls** (#333): such a level is hung a second time with every heavy ball at ordinary mass (`SagProbe.HeavyBaseline`): if the baseline sags too the layout is at fault, if only the loaded pass does too much hangs off the heavy ball. Attribution, never tolerance, and not an authored figure (it would go stale).
- **The player model is where every wrong verdict came from** (each found by refutation against known-finishable levels): aim is held to `AIM_BAND_LEVELS` (the gun stands underneath: "the column has to be eaten from the bottom"); a candidate counts only if the sweep returns that very ball; the cannon's orbit centre must be `(0, 5, 0)` as in both executables (zero gave a NaN aim: every level "survived"); a bounce is a spent shot; it aims at **empty cells**, matching first. ⚠ Bepu sleeps an unfired cluster in pieces and `ReleaseSameTypeCluster` cuts constraints *before* waking, so `WakeCluster` wakes every ball.

**Reading it.**

- `sagged n of 5 (k at rest)`. **`SAG_RUNS_TO_REPORT` (4)** is the one threshold fitted to play (nine reported-unfinishable levels read 4–5, three known-finishable 0, 2, 3). ⚠ **Bepu's threaded solve is not bit-deterministic**: seeds fix sequences, not counts, so a borderline level reads 2, 3, 3, 3, 2 across identical sweeps; repeat a reading at the edge (a 0 or a 5 is not). **It refuses nothing**: a ranking of where to look first.
- **`clear margin`** (#414): shots the dearest *clearing* order left unspent (`OutOfShots` runs excluded), **THIN** under `CLEAR_MARGIN_TO_REPORT` (6): a level can survive every order and still run the player out. ⚠ **Raise the budget and the ceiling step TOGETHER**: the glass descends every `CeilingStep` shots, so more shots at the same step buy the cluster another 0.60 descent (Causeway: 52 shots at step 8 became 60 at 10).
- **Anchor load**, printed beside the group count (balls per surviving anchor at the worst single shot): a figure to design against, **not a gate** (it does not separate the reported levels).

## The shortest clear: `ClearProbe` (#458, #474)

Plays the level **on the lattice** (a move is a landing: a reachable pocket, a colour, the glass body it colours, the group it completes; then what that group held up falls) until no removable ball is left (`CheckLevelCleared`'s condition), and reports the fewest moves, in milliseconds.

- **Sound lower bound**: the top level is never orphaned, so its balls must be *matched*, and one shot matches one group of one colour: **no level empties in fewer shots than there are colours on its anchor course**.
- ⚠ **The refusal is not the shot count**: many shipped levels clear in two or three matches by design (a weight on four ropes falls when two rope tops go). It is **under `MINIMUM_CLEAR_SHOTS` (4) while matching over `CHEAP_CLEAR_MATCHED_PERCENT` (50) of the removable field**, multiplied out, not read from the printed percentage (Lean is 50.09 %, printed 50). The cure is a colouring, never a moved cell.
- **Exhaustive to `PROVEN_SHOTS` (3)**, so a refusal is proved; the found line is **replayed through `BallsMap` itself** and a disagreement prints as a fault in the tool.
- **Not played**: blasts, zaps, acid shafts, thaws, infections (they live in `BallsConstraintsBuilder` over `PhysicsBall`, and a second copy here is refused) and setup shots. It can only **over-state** the shots needed, so a refusal is real; bomb and zap levels are the ones it can miss.
- `--clear` adds a **beam** line (`BEAM_WIDTH` 6, an upper bound); `--clearfile=` takes files outside the set.

## Shot arrival: `--arrival`, `--arrivalfile=` (`ArrivalProbe`, #514)

A report, not a gate. `AimReachability` (can the barrel *point* there) and an open-space flood both pass everything: a shot **travels in a straight line and rests against the first ball it touches**. The probe casts a ray from the trunnions to the landing cell's centre, blocked by any standing ball within `BLOCK_CLEARANCE` (0.98) of the line (⚠ a gap one cell wide is **closed**: neighbours are 1 apart and the ball is 1 across), from `STATIONS` (16) positions 22.5° apart round the orbit outwards from the opening stance, so the answer is "from how far round"; it is re-asked after every cut.

## What one cut lets go: `--cuts` (`CutProbe`, #692)

For every level granting a Cut (from `LevelSet.CUT_FROM_BLOCK`): how many balls one strike lets go on the starting cluster, by the game's own rule (the struck cell leaves the `BallsMap`; `GetCellsDisconnectedFromCeiling` says what no longer reaches the top; a bomb is left out), and how many strikes **clear the level outright**. The shipped rule is `CutReach.IsProtected` (at most the lower half of the cluster's height may be cut): no strike clears a level ("The anchor cut" in `docs/game-session.md`).

## How often the drop cinematic fires: `--drops[=levels]` (`DropProbe`, #719)

Plays every shipped level to a clear with two modelled players (**greedy** and **casual**) and counts the trigger under several dial settings. The trigger is `Prazsky.BS3D.DropTrigger`, shared with `GameplayScreen.TryBeginDropCinematic`; the first row is the game's own setting. The figure is cinematics before the clearing shot (which always has one, #424). ⚠ It does not play physics, the ceiling's descent, a blast or a zap: after changing a dial also count the `[cinematic]` lines of real played levels.

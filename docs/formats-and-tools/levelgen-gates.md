# LevelGen: what a design is and what the tool refuses

Part of [Formats and tools](../formats-and-tools.md).

A design is one `Design` object: a silhouette (a predicate on the centred polar or taxicab radius, or on the **raw lattice indices**, #130), a colouring, a scene, a dome, a music theme, a **kinding** (#325) and the set entry's rules. The field is `FIELD_LEVELS` deep unless `Design.FieldLevels` says otherwise; the emitter refuses an odd layout offset (the loader would move the design). `LevelGates.Validate` prints a line per gate per level, each naming its refusal. Command line: [LevelGen](levelgen.md).

## The static gates

- **Nothing floats.** Read back through `Level.Load` and `BallsMap`, `GetCellsDisconnectedFromCeiling` must be empty.
- **No colour's best shot may take the cluster** (`ONE_SHOT_PERCENT`, 90). Balls hang off the field's *top* level, so a same-colour group spanning it is the last anchor for everything below; the drop test releases a group on a throwaway map and counts the orphans. Horizontal colour bands are the obvious trap. ⚠ **It tries EVERY group of a colour, not the largest** (#98): how much comes down is not monotonic in group size, and testing only the largest let `Column` ship as a one-shot level. A sound gate that samples one case of several is worth no more than no gate on the case it misses.
- **No ball stands alone** (`FindLonelyBalls`). Cells on one level touch only their four **orthogonal** neighbours, so a one-cell colour band running diagonally is a string of balls that do not touch. `RepairLonelyBalls` recolours the handful a rounded-off rim leaves to the neighbouring colour giving the largest group; a whole lonely rim is a design fault.
- **Specials** (`FindStrandedSpecials`), each asked ahead of the guard that skips non-matchable kinds (where its gap was found):
  - a colourless special (glass, bomb) with **no empty neighbour** is unreachable and the level cannot be finished (`specials a shot can reach`); stated as a property, "removable but not matchable", so the next such kind is gated the day it exists.
  - **glass on the anchor course** is an anchor that dissolves with no warning (glass-only: a bomb is a trap the player can see). **A rock there** (#343) hangs off a socket nothing can cut. ⚠ A bomb in blast radius would take it down, and a bomb also detonates when it loses its last path to the glass (#396); it is refused anyway, because the gate reads the level *as it hangs* and a removal that depends on the rest collapsing in a certain order is no guarantee.
  - **frozen** (#329) is opened by a group cleared beside it, so it needs a **matchable neighbour** (`ice a group can ever reach`); **infection on the anchor course** (#331) hardens to stone at once with no player agency (`infection off the anchor course`). ⚠ The census of kinds that trigger this walk once missed the acid, so a level of acid and colour alone skipped the gate: anything not matchable belongs in it.
  - **a heavy ball with an empty lattice under it** (#333), tested one course down (how much droop is enough is the sag probe's call): `heavy balls with a load`. **A buried gravity well** (no open space within `GravityWells.RANGE`) bends nothing (#332).
- **Glass landings** (#344, #362): colouring runs through the whole connected glass **body**, so one landing can colour a pane. **`GlassLandingDrop` plays the landing** in the handler's order for every pocket beside glass and every colour, and the one-shot gate takes the larger of it and the layout drop test. **ALONE** refuses a glass body whose every landing pays exactly one (asked of the **landing**, not the pane).
- **Shortest clear** (#458, #474): refused when `ClearProbe` finds a clear under `MINIMUM_CLEAR_SHOTS` (4) matching over `CHEAP_CLEAR_MATCHED_PERCENT` (50) of the removable field ([The probes](levelgen-probes.md)).
- **Mirror image** (#664): `Design.MirrorOutline` and `MirrorColours` make symmetry about the gun's orbit axis (X only) a gate (`LevelGates.MirrorFaults`).
- **A reveal** (#743): `Design.Payoff` names the payoff cells with the design's own part function and `LevelGates.RevealFaults` refuses unless (1) with the body taken away **none of the payoff falls**, (2) **no body ball wears a payoff colour and no glass touches the payoff** (a pane takes the colour of the shot landed beside it, so one beside the payoff joins it on one shot, the body whole: #736's Rocket) and (3) **no payoff ball can be touched by a shot at the start** (no empty cell beside one is arrived at from any of `ArrivalProbe`'s sixteen stations); `Design.PayoffInSight` makes (3) a report. ⚠ Rule 3 is not the open-space flood, which slips between courses where no ball can pass.
- **From the stance** (`Design.ClearFromStance`): `ClearProbe.FromOpeningStance` plays only landings a straight shot from the opening stance reaches. ⚠ It does **not** test that a level needs the walk (a match takes the whole group, so a front shot takes a shell's hidden back with it); it guarantees a sheet is playable from the stand.
- **Printed, not gated**: standing colour groups and shots per group (what a budget is priced against; a design under 1 with no cascade cannot be finished), ceiling anchors, lateral margin.

`Validate` cannot say whether a level *looks* like its picture (the `screenshot` skill does), and gun reach is `aimcheck`/`[aimcheck]`, which needs a graphics device.

## Kinding (#325)

`Design.Kind` (polar) and `Design.BlockKind` (raw lattice) mirror `Colour`/`BlockColour`; the colour is still written for a rock or glass (the cell is not nullable). Null means `Normal`. What had to learn kinds:

- **`RepairLonelyBalls` skips them** (silent data loss otherwise): a rock has no colour group, which reads as the worst lonely ball, and the repair is `PutBallAt` whose `kind` defaults to Normal, so every special would be "repaired" into an ordinary ball.
- **`Validate`'s colour census is over matchable balls only** (a rock entered `counts` but never `largestGroup`: a `KeyNotFoundException`); **`CountGroups`** and **`FindLonelyBalls`** skip them.
- **Two denominators**: a *shot's* worth (balls at par, one-shot percentage) is priced off `GetRemovableBallsCount` (a rock never comes down); the anchor load off the whole cluster. The sag probe learned all of it too.

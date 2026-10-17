# Levels and the campaign

Part of [The game: a session](../game-session.md).

## The set and its files

- **The order lives in the set, never in the directory listing** (a filesystem order would silently promote whatever was dropped in). `Game/Levels/Levels.json` is a `LevelSet` (`"format": "bs3d-levels"`, `Prazsky.BS3D/Levels/LevelSet.cs`): entries in play order, as objects so they can carry rules, paths relative to `LevelSet.Directory`. `LevelSet.Load` validates and throws. Levels are plain `Content` copied beside the exe, not MGCB assets: editable without a rebuild.
- `InstallLevel` probes `Level.IsLevelFile`: a full level's scene and sky are applied; a plain map (`sx`/`sz`/`l`/`b`) leaves the scene the player picked.
- **An entry's rules** (optional): `shots` (budget; null unlimited), `minStars` (the star total it unlocks at), `ceilingStep` (shots between glass descents), `wildcardEvery` ([Special balls: shot and mass](special-balls-shot-and-mass.md)). `LoadLevelSet` logs every entry's rules at startup. `shots` and `ceilingStep` are **not independent** and `LevelSet` does not cross-check them ([A level from start to end](level-flow.md) has the sum).
- **Nothing about a missing or broken level is fatal**: a null set or a parse failure falls back to `BuildFallbackMap`, a stepped square pyramid apex down in four colours (`DEFAULT_BALL_TYPES`, fixed seed; half a unit narrower a side per level, because levels are offset by 0.5 and a whole-cell step zig-zags the flank).

## The campaign: 140 levels in fourteen blocks of ten

The `block` field gives the order: the Meadow, Gallery, Coil, Tower, Silhouettes, Reveal, Quarry, Grid, Nebula, Big Top, Eruption, Spectrum, Arcade, Mirage (the owner's ordering: gentle to epic, with a lighter breather after a hard chapter). A block is one scene, dome, music theme, ball material and statable style. `Tools/LevelGen` writes the set (`CampaignSet.BLOCKS` is a table of sizes, not a constant, because a size that can differ is the thing that was learned); see "The campaign is fourteen blocks" in `docs/formats-and-tools.md`.

- **Colossus** closes the Quarry and is its hardest level (#849): a giant holding the glass up on both arms, the arch between its legs and a crystal buried in its chest on a cord up through its head, in six colours with the glass every four shots.
- **One** (Cannonball), the fourth level of the Meadow and the first pyramid after the three flat sheets (Pennant, Rainbow, Zigzag), is generated, its colours on its four walls (the two facing walls share one: the gun starts on +Z).
- **The Coil swings** (slender links; deliberately not tall): its 12-14-deep layouts sit in **17-18-level** fields (eight of the ten at 18), the empty levels being the swing's clearance.
- **The Mirage is the first block whose subject is a rule**: five levels of glass (met first in the Silhouettes, #736), five of rock, one kind a level (the bomb joins from the third rock level, `Seam`, #879); the campaign-complete moment is a clear of its last entry, `Cairn`.
- Swap, Brake and Cut arrive in blocks 3, 4 and 5 ([Power-ups](powerups.md)).

## Authoring rules (`Tools/LevelGen` gates the first three)

- **The anchor layer must not be one colour**: a same-colour group spanning the top level is the last anchor for everything beneath, so one lucky ball drops the cluster (colour bands do this). Check *the largest same-colour group plus everything it anchors* as a fraction of the cluster: the pack runs 5-45 %, ~90 % is a firework.
- **The layout must not reach the field's wall**: a flank ball on the wall has no lateral neighbour, so a shot into its pocket sticks nowhere and bounces, costing a ball and the streak. `LevelGen.LateralMargin` makes every field two cells wider than its shape needs.
- **No ball may stand alone**: cells on one level touch only their four orthogonal neighbours, so a one-cell diagonal band touches nothing, and a lone ball needs two landed balls to make a group.
- **The magazine draws evenly among live colours** (`RandomBallType`), so the colour count *is* the difficulty. **Large standing groups are primed**: a group of `MINIMUM_CLUSTER_SIZE` does nothing until a ball attaches, then all of it goes (why big-plate levels clear in a few shots).

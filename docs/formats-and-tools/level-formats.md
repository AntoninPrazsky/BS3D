# Map, level and level-set formats

Part of [Formats and tools](../formats-and-tools.md).

Serialization is **System.Text.Json**; STJ has no multidimensional arrays, so `Balls3DArrayJsonConverter` reads and writes the nested `[x][z][level]` shape of the legacy map files. Types carry `[JsonPropertyName]` only. Loaders validate and throw except where noted. Player files: [Player files](player-files.md).

A scene's tunable look is a `SceneConfig` POCO per `SceneKind` in `Prazsky.Core/Render/Config/`, **fixed in code and nowhere else** since format 2: a level names its scene and never carries a config, so a change to a scene's defaults reaches every level of it with no file to regenerate. `SceneRenderer` reads it once, at load; there is no runtime re-apply ([The map editor](map-editor.md)).

## The map

`BallPositionTypes`: field dimensions `sx`/`sz`/`l` stored **separately** from the layout `b`, the nested `[x][z][level]` array of cells `{x, y, z, t, k}`. A legacy map without `sx`/`sz`/`l` still loads (`BallsMap` adds the extra bottom levels).

- **`t`** is the colour (`BallType`); **`k`** the `BallKind`, written **only when not `Normal`** (`WhenWritingDefault`), so older maps round-trip byte for byte.
- **A colour or kind this build does not define is refused** with the cell named (`InvalidDataException`, when the map is built; `Level.Load` does not look at colours): it used to hang an undrawn ball that could never match, an unclearable level.
- ⚠ A kind adds no format bump, so **an older build plays a level with kinds using ordinary balls**: a different level, with nothing on screen to say so. A kind is not a fourteenth colour and must never become one ("Special ball kinds" in `docs/game-session.md`).

## The level (`"format": "bs3d-level"`)

A level bundles a map with the **name** of its scene and the sky dome, and nothing derivable. `Level.IsLevelFile` is the cheap probe that tells it from a plain map (both `.json`). Nulls are omitted on write.

| Field | Meaning |
|---|---|
| `format`, `version` | `"bs3d-level"`, `Level.CurrentVersion` (2) |
| `name`, `author` | display name, author |
| `sky` | sky dome number (default 1) |
| `scene` | the scene's name |
| `music` | a music family or one recording's stem |
| `balls` | the ball material |
| `weather` | `"clear"`, `"scattered"`, `"broken"`, `"overcast"`, `"storm"`; absent or unknown means the scene's own |
| `map` | the ball map |
| `crates` | `{x, y, z, w, h, d}` boxes in world units (centre from the field's floor on its axis, full sizes) a shot banks off ("Crates" in `docs/game-session.md`) |
| `softness` | `{"hz", "damping"}`, the lattice spring |

**Not stored**: ceiling, physics statics and cannon and camera placement are derived from the map size at load.

- **`scene`** is a plain name, the `scene=` command line's own keys (`SceneCatalog.TryParse`/`ParseKey`; `"neon"` is the neon city), read by `SceneNameJsonConverter`. **Lenient**: an unknown name is a null scene and the consumer keeps its backdrop. A **version-1** file (`"scene"` a whole `SceneConfig` on a `"kind"` discriminator) still loads: only `kind` and `Neon` are read.
- **Readers and writers.** The Testbed and editor probe a file and route a level through the scene and sky switch, a plain map through the layout-only path; a broken file logs and leaves the running program untouched. The editor's F4 carries `music`, `author`, `crates`, `softness` and `weather` back untouched.
- **`sky` is inert for the four sky-replacing scenes** (space, dream, cavern, Moon; `SceneRenderer.ReplacesSky`): no dome is drawn and the scene's own `*LightingConfig` overrides the rig. It only pins `_skyDomeNumber`, what B/NumPad1 cycle *from*.
- **`music`** is a **family** in `Game/Music` (`"pulse"`, `"summit"`, `"nocturne"`, `"mural"`, `"ember"`, …) or **one recording** by file stem (`"ember-punk-03"`), which pins it; `"dechovka"` aliases `mural`, `"bohemia"` `summit`. Absent hands the choice to the set's rotation, where a level's *position* picks the piece. **Every level in `Game/Levels` names its theme** (a positional rotation would silently rescore the campaign when the order changes). A string, not an enum, because the music lives in the game executable: resolved by `GameMusic.SetTheme`, case- and whitespace-insensitively. ⚠ **An unknown spelling falls back silently, not to Pulse but to whatever the level's position gives**, so it reads as if the field were absent; LevelGen echoes each level's theme off the *loaded* file ("The rotation" in `docs/game-feedback.md`).
- **`balls`** is one of ten `BallStyle` names, lowercase (`BallStyles.ToName`): `beach`, `bubble`, `marble`, `wool`, `metal`, `ice`, `gem`, `plasma`, `lava`, `porcelain`. Absent is the beach ball, an unknown spelling reads as absent, the writer **omits the default**, and nothing in the simulation or score reads it ("Ball rendering" in `docs/rendering.md`). No bump (an older build draws vinyl).
- **`softness`** (`SoftnessSpec`) is the spring every socket **between two balls** gets once the cluster is built and on every ball landing after (`LatticeSoftness`; the Game's `ReleaseCluster`, the contact handler's `LatticeSpring`); the glass's anchors keep the builder's critically damped spring. Absent is the stiff spring (a dense lattice hangs as one rigid body); `hz` alone is critically damped (`damping` 1). **`Level.Load` refuses a spring the solver cannot hold**: frequency `SoftnessSpec.MIN_FIGURE` to `MAX_FREQUENCY` (half the simulation step rate), damping `MIN_FIGURE` to `MAX_DAMPING`. It is per **level** (a strand is springs in series and sags far more than a block), and the sag gate and Testbed hang a level at its own spring (`softness=` pins another). No bump (an older build hangs it stiff: a different level, with its own board).

## The level set (`"format": "bs3d-levels"`)

`LevelSet` (`Levels.json`, `CurrentVersion` 1) lists level files **in play order**; each `LevelSetEntry`'s `file` is relative to the set's directory and may name a level or a plain map.

| Entry field | Meaning |
|---|---|
| `file` | the level file (the key a save uses) |
| `name` | display name; the file name when absent |
| `block` | the chapter the entry belongs to |
| `shots` | ball budget; null is unlimited |
| `minStars` | the campaign **star total** at which it unlocks |
| `ceilingStep` | shots between two descents of the glass; null holds still |
| `wildcardEvery` | one in every N dealt balls is a wildcard (a free match); null is none |

- The rules sit on the entry because the set is the only place that can annotate a **plain map**, and because they are difficulty, not picture. **Null means "no rule", not zero**, so older sets keep playing as they did. `Load` rejects a rule present but meaningless: a zero budget, a zero ceiling step, a negative star gate, a wildcard cadence of zero. `wildcardEvery` adds no bump (an older build ignores it and plays a *harder* level).
- **Blocks**: a run of **consecutive** entries with the same `block` name; null everywhere means no blocks. **`LevelSet.Load` refuses a name that opens a second run later in the set.** `HasBlocks`, `BlockName`, `BlockRange`, `BlockNumber` and `BlockCount` walk the run on every call, not from a table built in `Load` (a set is built in memory as often as it is loaded).

### `LevelIdentity`: which online board a clear belongs to (#549)

An entry's `file` plus a hash: SHA-256 over the level file's bytes **with every carriage return dropped and nothing else normalized**, then one rules line `\nshots=30;ceilingStep=5;wildcardEvery=-` (an absent rule is `-`), cut to `HashLength` (16) hex characters. **That layout is the contract: change it and every board starts again.**

- A regenerated level or a retuned `shots`, `ceilingStep` or `wildcardEvery` changes the hash (a new board); a renamed `name`, re-chaptered `block` or moved `minStars` does not. Scoring rules are `ScoreKeeper.RulesVersion`, sent beside the hash and bumped in the same commit as any rate or rule change.
- ⚠ CRs are dropped because a checkout from before the repository went LF kept CRLF level files and hashed 51 of 120 levels differently: every submission for them would have been refused as unknown, silently.
- The game prints the identity as the last word of the `[levels] Loaded` line (`board One.json#113e5b5cdc39b7a9`); [ScoreSim](score-sim.md)'s ceiling table uses the same function.

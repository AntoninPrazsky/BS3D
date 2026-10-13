# The level generator (Tools/LevelGen): command line, structure and the campaign

Part of [Formats and tools](../formats-and-tools.md).

`Tools/LevelGen` (plain `net10.0`, in `Game.sln`) **writes the campaign's levels and `Levels.json` and refuses a level that is not playable**; it exits non-zero when any level fails and **goes in front of a commit**. **Every level of the campaign is a design** it writes, scene, dome, music, material and the set entry's rules included. See [what it refuses](levelgen-gates.md), [the probes](levelgen-probes.md) and [design lessons](levelgen-design-lessons.md).

```
dotnet run --project Tools\LevelGen\LevelGen.csproj [output directory]
    [--sag[=<name,...>]] [--sagfile=<file,...>] [--clear] [--clearfile=<file,...>]
    [--arrival] [--arrivalfile=<file,...>] [--cuts] [--drops[=levels]]
    [--softness=<Hz>[:<damping>]] [--wind=<acceleration>]
```

- With no directory it walks up from its bin directory to the first `Game\Levels`. A plain run writes the **whole** set. `--sagfile=`, `--clearfile=`, `--arrivalfile=` probe level files the set has never heard of and generate nothing; `--arrival`, `--cuts`, `--drops` are read-only; `--sag` is opt-in (it plays whole levels in physics). `--softness=` and `--wind=` (#95, the game's gusting wind) set the sag probe's conditions.
- **An unknown option, or a second output directory, exits 2 before anything is written** (#574): flags were once read by looking for themselves, so `--sagg` ran a full regeneration and `-sag` became the output directory. The Testbed and Game print `[args] Ignored '…'` instead.
- **Prototypes** (#604) are emitted every run into `Game\Levels\Prototypes` through the same checks but into no set (play with `BS3D.exe levelfile=Levels\Prototypes\Butterfly.json`); the published download leaves the folder out.

## Source layout (#386, #597)

One `partial class Program`: `Program.cs`, `Cli.cs` (`Main`, flag refusal, the **play-order** arrays, runners), `Design.cs`, `LevelEmitter.cs` (`Emit`, `HollowOut`, `RepairLonelyBalls`), `LevelGates.cs` (`Validate` and every gate), `CampaignSet.cs` (`BLOCKS`, `MUSIC_*`, `BALLS_*`, `WriteLevelSet`, `MinStarsAt`), the probes, and `Designs/BlockNN_Name.cs` (numbered by the order blocks were *written*; a helper two blocks use goes in `Designs/Shared.cs`). A new block is a new file plus a row in the tables. ⚠ **A static initialiser must not read a static field declared in another file of the partial class** (the initialisation order of the parts is unspecified).

## The set: fourteen blocks of ten

`CampaignSet.BLOCKS` is a table and `Cli.Main` hands `WriteLevelSet` the design arrays in play order: **the two must agree**. A block is one scene, one dome, one music family, one ball material and one statable style (below: scene with its sky-dome number, where "inert" means the number changes no pixel; music family; ball material; style); its name is written into the *set*, derived from the entry's **position**, while a theme is written into the level *file*.

1. **The Meadow**: meadow (1), `bloom`, bubble balls; three flat sheets, then solids of revolution.
2. **The Gallery**: savanna (14), `mural`, wool balls; flat drawn walls from a bitmap.
3. **The Coil**: desert (6), `ember`, marble balls; slender links: the cluster springs.
4. **The Tower**: mountains (8), `summit`, ice balls; deeper than the camera frames.
5. **The Silhouettes**: aurora (13, inert), `puppet`, wool balls; black figures as solids, each hung its own way, and the campaign's first glass (#736).
6. **The Reveal**: cavern, `nocturne`, marble balls; a body with a payoff inside.
7. **The Quarry**: Moon, `lunar`, metal balls; chunky blocks, then one idea a level the gun can see (#742); Colossus, a giant holding the glass up with a crystal buried in its chest, closes it (#849).
8. **The Grid**: grid (13, inert), `pulse`, ice balls; named mathematical constructions.
9. **The Nebula**: space, `nebula`, plasma balls; tall open levels.
10. **The Big Top**: big top (13, inert), `bigtop`, beach balls; softer springs (7–13 Hz).
11. **The Eruption**: volcano (9), `magma`, lava balls; the glow is the load.
12. **The Spectrum**: city (11), `skyline`, porcelain balls; one hue family a level.
13. **The Arcade**: neon city (16), `neon`, gem balls; pixel art on hollow solids.
14. **The Mirage**: dream (10, inert), `mirage`, porcelain balls; glass and rock kinds.

- **Order**: after the Quarry it is the owner's (2026-10-05): a hard chapter (by LevelGen's own figures the Quarry, Arcade and Eruption) is followed by a lighter or different one. The Mirage stays last and keeps the campaign's last word, which rides the set's last entry. Swap, Brake and Cut are granted from `LevelSet.SWAP_FROM_BLOCK`, `BRAKE_FROM_BLOCK`, `CUT_FROM_BLOCK` (3, 4, 5).
- **Music**: each chapter has its own family of about ten recordings (#486), one per level opening ("The music (Game)" in `docs/game-feedback.md`; briefs in the sidecars in `Research/AI-Music`). A level that names no family, or one with no recordings, falls back to the set's positional rotation and plays whatever its place gives.
- **Materials** (`BALLS_*`, each constant's doc carries its reasoning) belong to the block like the music; metal goes flat on a dome with nothing to mirror, plasma washes out on a bright one, and the bubble is dearer than vinyl, so it sits on the smallest clusters.
- **A broken block is refused by the logic tests, not by the tool** (its gates are per level). `WriteLevelSet` groups its printout by block and reads scene, dome, theme and ball style back off the **level files the game will load**, printing `MIXED SCENES`, `MIXED`, `MIXED THEMES` or `MIXED BALL STYLES` where they disagree; `ChapterTests` fails on the same disagreement, and on a level naming no music family or one with no recordings in `Game/Music`.
- **The unlock ramp** is `MinStarsAt(position)`: the opener free, the second entry 1 star, then 2 × (position − 1). A function of *position*, so a moved design carries its new place's gate and a reorder rewrites `Levels.json` only (`PlayerProgress` is keyed by file). It holds at any length: a player who two-stars every level ahead holds 2*i* against a gate of 2(*i* − 1), and the most anyone can hold at entry *i* is 4*i*, so the last gate stays under half (276 against 556 at the 140th entry).

# The session and the host

Part of [The game: a session](../game-session.md).

The Game is a thin executable over the shared libraries, not a fork of the Testbed: the island and glass drain in any shared scene, a hanging cluster in a **real Bepu simulation** ([Physics in the game](physics-in-the-game.md)), the procedural cannon. The cluster is a level off disk ([Levels and the campaign](levels-and-campaign.md)); a clear earns 1-4 stars ([Scoring, stars and progress](scoring-and-stars.md)); a level can be lost ([A level from start to end](level-flow.md)).

## Host and session

- `Game/BS3DGame.cs` is the **host**: frame, device and content, the shared setting every screen stands in, post-processing, the screen stack. `Game/Screens/GameplayScreen.cs` is the **session**: gun, shot, simulation, level (#65).
- Every shader comes from the compiled copy `Prazsky.Shaders` puts beside the exe; the Game builds only `Fonts/segoeui.spritefont` and the logo. The menu TTFs (Anton, Inter) are `EmbeddedResource`s. The Game's content must never come out of the Testbed's content directory: the product must not depend on the instrument.
- The session is partial across `GameplayScreen.*.cs` in `Game/Screens/` (`.Rules`, `.Session`, `.Input`, `.Camera`, `.Physics`, `.Draw`, `.Ceiling`, `.Powerups`, `.Swap`, `.Motion`, `.Wormhole`, `.Note`, `.Debug`). Each partial's doc names the extraction it stages for and the invariant that survives there; read it before working in one ("Both halves are partial across several files now" in `docs/game-shell.md`).
- **State moves out into owned objects.** `CeilingDescent` holds the descent (one `Reset` call in `FitFieldToMap`); `ClusterRipple` the landing's wave; and **one attempt at a level is one `LevelRun`** (entry, `ScoreKeeper`, clock, frozen shots/seconds, power-up charges, wildcard cadence, line grace...), which `BuildLevel` replaces whole before `InstallLevel` fills in what the file decides, so a per-level value cannot be missed by a reset list. The phase and its ending data stay on the screen (`EnterPhase(Playing)` resets them), as do the generator, the cinematics' resets and everything reused across levels.

## What a test can change: `SessionTestOptions`

The command line's testing levers reach the session as one immutable record built by `SessionTestOptions.From(LaunchOptions)`: `streak=`, `wildcard=`, `powerups=`, `lasers`, `lineloss=`, `detonate=`, `tutorial`, `balls=`, `levelfile=`, `seed=`, `wind=`, `logshots`, `logtilt`, `pad`, `cutshot=` and `swapshot=`. The host keeps the one instance as `TestOptions` because the front end's preview reads `BallStyleOverride` and `StartupScript` is told `StartupLevelFile`. The `detonate=` schedule is data with its cursor in `_detonateNext`; the screen lives for the whole program, so a schedule runs once per launch.

## The seeded generator

Everything the screen draws at random (the magazine's deal, the transmute's replacements, the cinematics' rolls) goes through one `Random`. `SeedSession`, in `InstallLevel` before the refill, rebuilds it from `seed=<n>` or a fresh roll and prints `[session] seed N (pinned)` or `(rolled; replay it with seed=)`.

⚠ **A seed pins the draws, not the landings**: the deal is drawn among the colours still hanging, so two runs deal the same queue only while their shots land in the same cells, and a landing depends on the aim and on a simulation stepped against real time. The scene's arrangement stays `sceneseed=`'s; the host's effects keep their own generators.

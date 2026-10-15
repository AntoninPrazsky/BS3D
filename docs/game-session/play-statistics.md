# Play statistics: what players do with each level

Part of [The game: a session](../game-session.md).

The game counts, per level, how often it is **started, retried, cleared, lost (out of balls, or at the line), quit and skipped**, the **shots and seconds** spent on it, and **how many starts a first clear took** (#709). The point is the question the level work keeps guessing at: *where does the game get stuck?* ScoreSim says how a simulated player rates; these say what real ones do. Counters only: no event log, no device, no time of day, no quality tier.

## What is counted, and where

- **The rules are the library's** (`Prazsky.BS3D/Levels/PlayStats.cs`): `PlayStatsRecorder` takes four events and returns the change each makes; `PlayStats` is the file. **One row per board** (`LevelIdentity` and `ScoreKeeper.RulesVersion`), so a regenerated or retuned level counts afresh, as it starts a fresh board. The built-in fallback (no identity) is counted nowhere.
- **The game's seam** is `Game/PlayStatsSession.cs` (`Game.Stats`), called from the gameplay screen:
  - **start**: `BuildLevel`, right after `InstallLevel` has said which board it is;
  - **ending**: `ShowResultScreen`, the funnel the save's record and the boards' submission share, with the same frozen `EndShots`/`EndSeconds`; a **first clear** is a clear of a level the save had no clear of, asked before the record writes it;
  - **quit** (abandoned): `TearDown`, so every way an attempt can be torn down before its page counts once: the pause's Restart, another level or New Game over a session kept for Continue, and the game closing (`UnloadContent` counts it **before** the online client stops, so the change still reaches its outbox). A level decided but left before its page (a Restart during the line's flight) counts as a quit;
  - **skip**: `SkipLevel`, on the board of the attempt the skip was offered over.
- **A retry** is a start of the board the last attempt was on, while that attempt did not clear it (a loss, a Restart, the level left and picked again). A replay after a clear is a start, not a retry. Held in memory only: the first start after a launch is never one.
- **One attempt is open at a time**; a second close counts nothing, which is what lets `TearDown` say "quit" without asking whether the attempt ended first.
- ⚠ **The counts observe and never decide.** Nothing reads them back. The skip stays a per-chapter budget exactly as #347 ruled, not something earned by losing.

## The file: `Stats.json`

Beside `Progress.json` in the user data folder, **its own file and never part of the save**, so a bug in the counts cannot cost a player their progress. Through `AtomicFile` with a backup, and an unreadable one is kept aside (`KeepUnreadable`) before it is answered with an empty one, like every player file. **Written off the frame**: an event serializes the totals where they change and a task of its own writes the latest text (`PlayStatsSession.BackgroundWrite`), so a level's start or end costs the frame no disk flush; the exit waits for the last write at most `PlayStatsSession.DisposeWait`. Each event prints one `[stats]` line with the board's totals.

**The report without a server**: `dotnet run --project Tools\ScoreSim\ScoreSim.csproj -- --stats [path]` prints a file's counts per level in play order (the player's own `%LOCALAPPDATA%\BS3D\Stats.json` without a path), every rate beside its counts, and the boards the set does not name as shipped after them ([The score simulator](../formats-and-tools/score-sim.md)).

## Sent, anonymously, only with online scores on

- **The owner's ruling (2026-10-15): sent only for a player who has online scores on**, with no opt-in of its own; the nickname is the consent (#763). With them off (or never decided) nothing of the counts leaves the machine: `OnlineSession.SubmitStats` hands a change to the client only while it is `Enabled`.
- **Anonymous**: `POST /v1/stats` carries a random `batchId` (the service counts a batch once by it) and rows of counts per board (`LevelPlayCounts`, field for field), and nothing else: **no player id, no token, no nickname, no version, no clock**, and the request goes without its `Authorization` header. The service keeps running totals per board and the batch ids, with no address, player or time ([BS3D-API](https://github.com/AntoninPrazsky/BS3D-API), `play_stats`). So "Remove scores" has nothing of the player's to take there, and the privacy sentence on Settings and About says so (`OnlineScores.PrivacySentence`).
- **Batched, through the outbox's worker** (#546): each change is merged into one **open batch** in `Outbox.json` (`statsOpen`). It is frozen under a fresh id into `statsSending` and sent **at the worker's start** (what an earlier session left) and then **at most once every `OnlineScores.StatsInterval` (10 minutes)**, at a drain; at most `StatsMaxLevels` rows a batch. **One frozen batch at a time**, sent unchanged until the service answers for it, while new counts gather in the open one; so the outbox holds at most two.
- **The clears go first and the counts never stop one**: the counts are sent only after every clear in the outbox is answered for. A wake for the counts alone puts them on disk and sends nothing until they are due, so a level's start makes no request and does not retry a clear stuck behind a server that is away.
- **Answers are read as a clear's are**: 2xx with the service's JSON delivered; a 4xx with its reason refused for good (dropped, logged); anything else kept. ⚠ **A service without the endpoint answers a bare 404, which is no answer (#691)**: the batch stays and costs one request an interval, never a clear. An older build reads past the two fields of the outbox and drops them at its next write (the outbox stays version 1 so it never keeps a whole file of clears aside for them).

## Testing it

- `actions=<seconds>:<verb>,...` ([Controls](controls-and-testing.md)) presses a level's buttons on the clock: `retry`, `restart`, `skip`, `next`, `menu`, `level<n>`; `lineloss=` stages a loss at the line and `quitconfirm=<s>:quit` closes the game mid-level. Always with `userdata=<dir>`.
- Against a **local** BS3D-API (`dotnet run --project src/BS3D.Api --urls http://127.0.0.1:<port>` and `"server": "http://127.0.0.1:<port>"` in the run's `Settings.json`); never against the public service. `admin stats` on its database prints what arrived.

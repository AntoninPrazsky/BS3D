# Player files: the save, the settings, the online identity

Part of [Formats and tools](../formats-and-tools.md).

Every file a player's run writes lives in one per-user directory and goes through `Prazsky.Core.Tools.AtomicFile`. Level and map files: [Map, level and level-set formats](level-formats.md).

## Where they live

- **`%LOCALAPPDATA%\BS3D\`**: `Progress.json`, `Settings.json`, `Online.json`, `Outbox.json`, `Notes/`. `Game/UserData.cs` is the one place that answers where. Per **user**, not roaming. `Levels.json` stays with the build.
- GamePi answers `~/.local/share/BS3D` (`$XDG_DATA_HOME` when set) asked with `SpecialFolderOption.DoNotVerify` (#798): .NET answers an empty string by default when `~/.local/share` does not exist yet.
- **`userdata=<dir>`** (#546) moves every file for one run: a scripted run must pass it or it writes the owner's real save ("Online scores" in `docs/game-session.md`).
- ⚠ The save lived in the build output until #353 and `dotnet clean` took the campaign. `BS3DGame.AdoptProgressFromBuildOutput` adopts an old-location save once (only when the new home holds neither save nor backup), byte for byte, and leaves the old file standing: it was the only copy.

## The write and the read

- **`AtomicFile.WriteText`** builds `<name>.tmp` in the same directory (`File.Replace` cannot cross a volume), flushes it to disk and swaps it in with `File.Replace`, which demotes the previous file to `<name>.bak` (`PlayerProgress.BackupSuffix`): a torn write leaves a short but syntactically fine file. `Level.Save`, `LevelSet.Save` and `BallsMap.SerializeAsJson` use it with `backupSuffix: null`.
- **`Load` tries the backup before giving up** and comes back bound to the real path. **`PlayerProgress.Outcome`** is `Fresh`, `Loaded`, `RecoveredFromBackup` or `Discarded` (a file was there, none usable); `BS3DGame.ReportProgressLoad` prints the last three. `Save` throws and the game wraps it (`BS3DGame.SaveProgress`).
- ⚠ **`AtomicFile.KeepUnreadable`**: every player-file loader (`PlayerProgress`, `GameSettings`, `OnlineIdentity`, the online outbox) calls it on a file that exists and does not read, **before anything writes**: a copy `<name>.unreadable-<utc>`, never touched or read back by the game (recovery is by hand), and one log line (`[progress]`, `[settings]`, `[online]`). Without it a save a *newer* build wrote was gone after two saves (the first demotes it to `.bak`, the second overwrites that); the identity's token, the only proof the scores are the player's, is the file that matters most.
- The mix rows (`master`, `sfx`, `music`, `ambience`, `rumble`) are clamped to 0..1 on load.

## `Progress.json` (`"bs3d-progress"`)

`PlayerProgress`, `CurrentVersion` 1, keyed by the entry's `file` (it survives a renamed display name and a regenerated set).

- **`levels`**: per file `{score, stars}` bests. An entry the set no longer lists goes unread by the picker, but `TotalStars`/`TotalScore` sum every record, so stars on a retired level stay the player's. The star total is what `minStars` gates against.
- **`skipped`** (#347): levels skipped past, null until the first (a skip is not a clear). **`lessons`** (#189): tutorial lessons completed (`"aim"`, `"fire"`, … from the game's `Tutorial` class), null until the first; the save's, not the settings', because it records what the player *did*; `Reset` clears it with the stars. An older build ignores both (it teaches again: the safe direction).
- **`Load` is lenient**, unlike every other loader: a missing, unreadable or malformed file is a fresh empty progress.

## `Settings.json` (`"bs3d-settings"`)

`GameSettings`: the four volumes, exposure (a raw multiplier read onto the nearest Brightness rung, #711), sky dome, fullscreen, 10-bit colour (`tenBitColour`, #859), FPS counter and limit, aberration, film grain, the quality tier and whether the game may lower it itself (#390), plus **`online`** (three states, #763: absent until decided, then `true` or `false`; **nothing is sent unless `true`**), **`nicknameDismissed`** (times the first-launch nickname question was closed with Esc; absent while zero) and **`server`**, which no row shows: it points a build at a local score service and is the only way a local build submits at all. Anything that is not a settings file comes back as the defaults. What it excludes (the adaptive probe's verdict, **Unlock all**, a dev-build row a release does not draw, #854): `docs/game-shell/settings.md`.

## `Online.json`, `Outbox.json` (#546) and `Notes/` (#813)

- **`Online.json`** (`"bs3d-online"`, `OnlineIdentity`): a random `playerId`, a random 32-byte base64url `token` and the player's `name`, written with a backup; created at the first opt-in, deleted (with `Outbox.json`) once the service has forgotten the player (#548). **Its own file, not a settings row**: the settings file is rewritten by every click and restored by hand after scripted runs, and a lost token is a player who can no longer reach their scores.
- **`Outbox.json`** (`"bs3d-outbox"`, `ScoreSubmission`): undelivered clears, oldest first, each the exact request but for its `name`, restamped with the current nickname at every send (#572); written before anything is sent, emptied as the service answers. Both are lenient on read ("Online scores" in `docs/game-session.md`).
- **`Notes/`**: Send a Note's outbox, one `<id>.json` per undelivered note (`"bs3d-note"`, `"version": 1`: id, when written, text, game version, context, whether a picture goes with it) and, with a picture, `<id>.jpg`. The picture is written first, whole, under a temporary name; **the note's file, written through `AtomicFile`, is what says a note exists**. A note leaves once the service has answered for it; at most `OnlineNotes.OutboxCapacity` are kept (oldest dropped); an unreadable note is dropped when the worker reaches it. "Remove scores" leaves the folder alone ("Send a Note" in `docs/game-shell.md`).

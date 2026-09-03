using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// What the player has done with a <see cref="LevelSet"/>, remembered across sessions (#92): each level's
    /// best score and best star rating, keyed by the entry's own <see cref="LevelSetEntry.File"/>. The sum of
    /// the best stars is <see cref="TotalStars"/>, which is the currency level unlocks are gated on (#111).
    /// <para>
    /// It is a JSON file, and <b>where</b> it goes is the caller's to say — <see cref="Load"/> and
    /// <see cref="Save"/> only ever touch the path they were handed. That used to be the level set's own
    /// directory, on the argument that "progress through a set belongs with the set it measures", and #353
    /// overruled it: the set's directory is the build output, so a <c>dotnet clean</c>, a deleted <c>bin</c>
    /// or a fresh clone took the save with it and there was no second copy of it anywhere. <b>A save must
    /// outlive the build output</b>, so the game now hands this a path under the player's own profile.
    /// The bests are <b>best-per-level</b>, not a running sum of every play: replaying a level can only raise
    /// its entry, so the totals reward getting better rather than grinding the same level over.
    /// </para>
    /// <para>
    /// Unlike <see cref="LevelSet.Load"/>, <see cref="Load"/> is <b>lenient</b>: a missing, unreadable or
    /// malformed file comes back as a fresh, empty progress rather than an exception. A save file is the one
    /// piece of level data whose absence is the normal case (a first run), and a corrupt one must cost the
    /// player their stars, never the game.
    /// </para>
    /// <para>
    /// <b>That leniency is also what made the loss silent, so it is no longer silent</b> (#353): a wiped save
    /// and a first run produce the identical empty object, and nothing in the game could tell them apart.
    /// <see cref="Outcome"/> now says which of the two happened — and whether the file that answered was the
    /// backup rather than the save — for the caller to log. The write is atomic besides
    /// (<see cref="Save"/>), so the window where a lost machine can leave a half-written save is closed
    /// rather than merely reported.
    /// </para>
    /// </summary>
    public sealed class PlayerProgress
    {
        public const string FormatMarker = "bs3d-progress";
        public const int CurrentVersion = 1;

        /// <summary>The save's own file name, wherever the caller decides to keep it.</summary>
        public const string DefaultFileName = "Progress.json";

        /// <summary>
        /// What the last good save is kept as while a new one is written (#353) — see
        /// <see cref="AtomicFile.WriteText"/>, which demotes the old file to this in the same operation that
        /// puts the new one in its place. Public because <see cref="Load"/> reads it and the game names it in
        /// the line it logs when a save had to be recovered.
        /// </summary>
        public const string BackupSuffix = ".bak";

        /// <summary>File-type marker; always <see cref="FormatMarker"/> in a valid progress file.</summary>
        [JsonPropertyName("format")]
        public string Format { get; set; } = FormatMarker;

        [JsonPropertyName("version")]
        public int Version { get; set; } = CurrentVersion;

        /// <summary>
        /// Each played level's bests, keyed by the set entry's <see cref="LevelSetEntry.File"/> — the one
        /// identifier that survives a display name being retuned. An entry a set no longer lists simply goes
        /// unread, so regenerating the set never invalidates the save.
        /// </summary>
        [JsonPropertyName("levels")]
        public Dictionary<string, LevelBest> Levels { get; set; } = new();

        /// <summary>
        /// The levels the player has explicitly <b>skipped</b> past (#347), keyed like <see cref="Levels"/> by
        /// the set entry's own file. A skip is the campaign's relief valve: since the campaign opens one level
        /// at a time, a level the player cannot finish is a wall across the whole set rather than something
        /// they can route around on stars, and this is the one bounded way past it.
        /// <para>
        /// <b>A skip is not a clear and is never counted as one.</b> It earns no score and no stars, so it adds
        /// nothing to <see cref="TotalStars"/> or <see cref="TotalScore"/> and buys no unlock of its own; all it
        /// does is let the frontier move on. A skipped level stays playable for ever, and clearing it later
        /// records an ordinary best beside this — the two lists are deliberately separate rather than a flag on
        /// <see cref="LevelBest"/>, which is a record of <i>bests</i> and has none to hold for a level nobody
        /// finished.
        /// </para>
        /// <para>
        /// <b>Null until the first skip</b>, so a save with none in it round-trips exactly as it did before this
        /// existed — the same decision, for the same reason, as the map format's optional <c>"k"</c>. An older
        /// build reading a save with skips ignores the key and simply finds those levels unfinished, which is
        /// the safe direction: it under-reports progress rather than inventing it.
        /// </para>
        /// </summary>
        [JsonPropertyName("skipped")]
        public List<string> Skipped { get; set; }

        /// <summary>Where this progress was loaded from and where <see cref="Save"/> writes it back.</summary>
        [JsonIgnore]
        public string Path { get; private set; }

        /// <summary>
        /// Which of the four things <see cref="Load"/> actually did (#353). The caller logs it: leniency means
        /// three of these four return an object that looks exactly alike, and only this says whether the empty
        /// one in hand is a first run or a campaign that was just thrown away.
        /// </summary>
        [JsonIgnore]
        public ProgressLoad Outcome { get; private set; }

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Reads the progress at <paramref name="path"/>, or returns a fresh empty one bound to that path when
        /// there is nothing readable there — no file on a first run, a wrong marker, malformed JSON. Lenient
        /// where the set's loader throws, because an absent save is the normal case rather than an error.
        /// <para>
        /// <b>The backup is tried before it gives up</b> (#353). <see cref="Save"/> keeps the previous save as
        /// <see cref="BackupSuffix"/>, so the one failure this is really guarding against — the machine lost
        /// mid-write, which leaves a short but syntactically fine file — costs the player at most the single
        /// clear that was being written. Whichever file answered, the object comes back bound to
        /// <paramref name="path"/>: the next save must go back to the real one, not overwrite the backup.
        /// </para>
        /// </summary>
        public static PlayerProgress Load(string path)
        {
            string backup = path + BackupSuffix;

            //Asked BEFORE anything is read, because it is the whole difference between "a first run" and "the
            //campaign is gone": once the reads have failed, an empty object is all that is left to look at
            bool anythingWasThere = Exists(path) || Exists(backup);

            PlayerProgress progress = TryRead(path);

            if (progress != null)
            {
                progress.Outcome = ProgressLoad.Loaded;
                return progress;
            }

            progress = TryRead(backup);

            if (progress != null)
            {
                //Bound back to the real save: the backup is where the last good copy is kept, never where the
                //next one is written
                progress.Path = path;
                progress.Outcome = ProgressLoad.RecoveredFromBackup;

                return progress;
            }

            return new PlayerProgress
            {
                Path = path,
                Outcome = anythingWasThere ? ProgressLoad.Discarded : ProgressLoad.Fresh,
            };
        }

        /// <summary>Whether there is a file at <paramref name="path"/>, an unreadable one included.</summary>
        private static bool Exists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
                or NotSupportedException)
            {
                //A path this cannot even be asked about is one nothing was loaded from either
                return false;
            }
        }

        /// <summary>
        /// One readable save from <paramref name="path"/>, or null for anything at all that is not one — no
        /// file, a wrong marker, a version from a later build, malformed JSON, a directory in the way. Null
        /// rather than an exception because both of <see cref="Load"/>'s attempts are allowed to fail.
        /// </summary>
        private static PlayerProgress TryRead(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    PlayerProgress progress = JsonSerializer.Deserialize<PlayerProgress>(stream, Options);

                    if (progress?.Format == FormatMarker && progress.Version <= CurrentVersion)
                    {
                        progress.Levels ??= new Dictionary<string, LevelBest>();
                        progress.Path = path;

                        return progress;
                    }
                }
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException)
            {
                //Whatever was there is not progress
            }

            return null;
        }

        /// <summary>
        /// Writes the progress back where it was loaded from. Throws on an unwritable path, exactly as
        /// <see cref="LevelSet.Save"/> does — whether a failed save is worth more than a log line is the
        /// caller's call, not this file's.
        /// <para>
        /// <b>Never straight over the save</b> (#353). It used to be one <see cref="File.WriteAllText(string,
        /// string)"/>, which opens, truncates and then writes: lose the machine in that window and what is on
        /// disk is a short but syntactically fine file, <see cref="Load"/> takes it for a first run, and the
        /// next clear writes that emptiness out for real. This desktop hard-resets under GPU load, so the
        /// window was not hypothetical. <see cref="AtomicFile.WriteText"/> carries the whole discipline and
        /// the settings file shares it.
        /// </para>
        /// </summary>
        public void Save() =>
            AtomicFile.WriteText(Path, JsonSerializer.Serialize(this, Options), BackupSuffix);

        /// <summary>All the stars collected across the campaign — the currency unlocks are gated on.</summary>
        [JsonIgnore]
        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (LevelBest best in Levels.Values) total += best.Stars;

                return total;
            }
        }

        /// <summary>
        /// The career score: the best scores summed over every level cleared. Best-per-level rather than
        /// every play summed — see the class doc for why.
        /// </summary>
        [JsonIgnore]
        public int TotalScore
        {
            get
            {
                int total = 0;
                foreach (LevelBest best in Levels.Values) total += best.Score;

                return total;
            }
        }

        /// <summary>The best stars earned on one level, or 0 for a level never cleared.</summary>
        public int StarsFor(string levelFile) =>
            levelFile != null && Levels.TryGetValue(levelFile, out LevelBest best) ? best.Stars : 0;

        /// <summary>The best score reached on one level, or 0 for a level never cleared.</summary>
        public int ScoreFor(string levelFile) =>
            levelFile != null && Levels.TryGetValue(levelFile, out LevelBest best) ? best.Score : 0;

        /// <summary>
        /// Records one cleared level. Each best only ever rises — a worse replay changes nothing, which is
        /// what lets a player retry freely.
        /// </summary>
        /// <returns>Whether anything improved, which is what a result screen's "new best" wants to know.</returns>
        public bool Record(string levelFile, int score, int stars)
        {
            if (string.IsNullOrEmpty(levelFile)) return false;

            if (!Levels.TryGetValue(levelFile, out LevelBest best))
            {
                Levels[levelFile] = new LevelBest { Score = score, Stars = stars };
                return true;
            }

            bool improved = false;

            if (score > best.Score) { best.Score = score; improved = true; }
            if (stars > best.Stars) { best.Stars = stars; improved = true; }

            return improved;
        }

        /// <summary>Whether the player skipped past this level rather than clearing it (#347).</summary>
        public bool WasSkipped(string levelFile) =>
            levelFile != null && Skipped != null && Skipped.Contains(levelFile);

        /// <summary>
        /// Records a skip. Idempotent, and it deliberately does <b>not</b> refuse a level that is already
        /// cleared: whether a skip is allowed at all is a campaign rule (the budget, and the level being one
        /// the player is actually stuck on), and answering it twice in two places is how the two answers drift
        /// apart. This file's job is to remember.
        /// </summary>
        /// <returns>Whether anything changed, so a caller knows whether it needs to save.</returns>
        public bool Skip(string levelFile)
        {
            if (string.IsNullOrEmpty(levelFile) || WasSkipped(levelFile)) return false;

            Skipped ??= new List<string>();
            Skipped.Add(levelFile);

            return true;
        }

        /// <summary>
        /// Back to a fresh start: every best gone, <b>and every skip with them</b> — a skip is progress through
        /// the campaign, so a reset that left them standing would hand the player a campaign whose walls were
        /// already spent. The settings row that offers this saves after it.
        /// </summary>
        public void Reset()
        {
            Levels.Clear();
            Skipped = null;
        }
    }

    /// <summary>
    /// What <see cref="PlayerProgress.Load"/> found (#353). The loader is lenient by design, so three of
    /// these four hand back an object that is indistinguishable from the others — this is the only thing that
    /// says which one it is, and the distinction that matters is <see cref="Fresh"/> against
    /// <see cref="Discarded"/>: a player with no campaign yet, and a player whose campaign has just been
    /// thrown away, otherwise look identical from inside the game.
    /// </summary>
    public enum ProgressLoad
    {
        /// <summary>Nothing was there at all — the normal first run.</summary>
        Fresh,

        /// <summary>The save read cleanly.</summary>
        Loaded,

        /// <summary>
        /// The save did not read but its backup did, so what is in hand is the state before the last write.
        /// The one clear that was being written when the machine was lost is the whole cost.
        /// </summary>
        RecoveredFromBackup,

        /// <summary>
        /// <b>A file was there and none of it could be used.</b> The player is being handed an empty campaign
        /// they did not start, and the next clear will write it out for real — the one outcome worth saying
        /// out loud.
        /// </summary>
        Discarded,
    }

    /// <summary>One level's bests: the highest score and the most stars any single clear of it earned.</summary>
    public sealed class LevelBest
    {
        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("stars")]
        public int Stars { get; set; }
    }
}

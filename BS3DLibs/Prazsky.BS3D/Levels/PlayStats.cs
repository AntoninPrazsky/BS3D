using Prazsky.BS3D.Scoring;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// What players do with a level, counted (#709): how often it is started, retried, cleared, lost (out of balls, or the
    /// cluster at the line), left mid-level and skipped, the shots and the seconds spent on it, and how many starts the
    /// first clear took. One row per board (<see cref="LevelIdentity"/> and <see cref="ScoreKeeper.RulesVersion"/>), so a
    /// regenerated or retuned level starts its counts afresh as it starts a fresh board.
    /// <para>
    /// The same shape serves three uses: a row of the player's own totals (<see cref="PlayStats"/>), the change one event
    /// makes (<see cref="PlayStatsRecorder"/> returns one), and a row of the batch, naming no player, that the game sends to
    /// the score service's <c>POST /v1/stats</c>, field for field but <see cref="StartsToFirstClear"/>, which stays on the
    /// machine. <b>Counters only</b>: nothing here says who, when, on which machine or in what order.
    /// </para>
    /// </summary>
    public sealed class LevelPlayCounts
    {
        [JsonPropertyName("file")]
        public string File { get; set; }

        [JsonPropertyName("hash")]
        public string Hash { get; set; }

        /// <summary><see cref="ScoreKeeper.RulesVersion"/>: the third part of a board's key.</summary>
        [JsonPropertyName("rules")]
        public int Rules { get; set; }

        /// <summary>Attempts started: every build of the level, a Retry and a Restart included.</summary>
        [JsonPropertyName("starts")]
        public int Starts { get; set; }

        /// <summary>
        /// Starts that went straight back to the same level after an attempt at it that did not clear it (a loss, a
        /// Restart, or the level left and picked again). A replay after a clear is a start and not a retry.
        /// </summary>
        [JsonPropertyName("retries")]
        public int Retries { get; set; }

        [JsonPropertyName("clears")]
        public int Clears { get; set; }

        /// <summary>Losses because the budget ran out.</summary>
        [JsonPropertyName("lostOutOfBalls")]
        public int LostOutOfBalls { get; set; }

        /// <summary>Losses because the cluster reached the line.</summary>
        [JsonPropertyName("lostLine")]
        public int LostLine { get; set; }

        /// <summary>
        /// Attempts left before their result page: a Restart, another level started over a session kept for Continue,
        /// or the game closed mid-level.
        /// </summary>
        [JsonPropertyName("abandons")]
        public int Abandons { get; set; }

        /// <summary>The chapter's skip spent on the level (#347).</summary>
        [JsonPropertyName("skips")]
        public int Skips { get; set; }

        /// <summary>Clears of a level the save had never cleared.</summary>
        [JsonPropertyName("firstClears")]
        public int FirstClears { get; set; }

        /// <summary>
        /// The starts counted on this board up to and including each first clear, summed: divided by
        /// <see cref="FirstClears"/>, the tries a first clear took. <b>The player's own file only</b>: the game zeroes it in
        /// what it sends, since the starts it counts include those made before the player turned online scores on, and a
        /// zero is not written at all, so a batch never carries the field.
        /// </summary>
        [JsonPropertyName("startsToFirstClear")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int StartsToFirstClear { get; set; }

        /// <summary>Shots fired over every attempt, ended or left.</summary>
        [JsonPropertyName("shots")]
        public int Shots { get; set; }

        /// <summary>Seconds of play over every attempt, the way <c>LevelRun.Seconds</c> counts them (no pause, no unfocused window).</summary>
        [JsonPropertyName("seconds")]
        public double Seconds { get; set; }

        /// <summary>An empty row for one board.</summary>
        public static LevelPlayCounts For(LevelIdentity level, int rules) => new() { File = level.File, Hash = level.Hash, Rules = rules };

        /// <summary>Whether this row is for the board of <paramref name="other"/>.</summary>
        public bool SameBoard(LevelPlayCounts other) =>
            other != null && File == other.File && Hash == other.Hash && Rules == other.Rules;

        /// <summary>Adds every counter of <paramref name="other"/> to this row's (the board is not compared).</summary>
        public void Add(LevelPlayCounts other)
        {
            Starts += other.Starts;
            Retries += other.Retries;
            Clears += other.Clears;
            LostOutOfBalls += other.LostOutOfBalls;
            LostLine += other.LostLine;
            Abandons += other.Abandons;
            Skips += other.Skips;
            FirstClears += other.FirstClears;
            StartsToFirstClear += other.StartsToFirstClear;
            Shots += other.Shots;
            Seconds += other.Seconds;
        }

        /// <summary>A copy of this row, board and counters.</summary>
        public LevelPlayCounts Copy()
        {
            LevelPlayCounts copy = new() { File = File, Hash = Hash, Rules = Rules };
            copy.Add(this);
            return copy;
        }
    }

    /// <summary>
    /// The player's own play counts (#709), <c>Stats.json</c> beside <c>Progress.json</c>: every board's
    /// <see cref="LevelPlayCounts"/> added up since the file began. <b>Its own file, never a part of the save</b>, so a bug
    /// in the counts can never cost a player their progress; written through <see cref="AtomicFile"/> with a backup, and
    /// an unreadable one kept aside (<see cref="AtomicFile.KeepUnreadable"/>) before it is answered with an empty one, as
    /// every player file is.
    /// <para>
    /// <b>The counts observe and never decide</b>: nothing in the game reads them back. The skip stays a per-chapter
    /// budget exactly as #347 ruled, not something earned by losing. They are for the owner's level tuning
    /// (<c>Tools/ScoreSim --stats</c> prints a file's), and, while the player has online scores on, the changes are sent
    /// in batches that name no player (the game's <c>OnlineScores</c>); this file itself never leaves the machine.
    /// </para>
    /// </summary>
    public sealed class PlayStats
    {
        public const string FormatMarker = "bs3d-playstats";
        public const int CurrentVersion = 1;
        public const string DefaultFileName = "Stats.json";
        public const string BackupSuffix = ".bak";

        [JsonPropertyName("format")]
        public string Format { get; set; } = FormatMarker;

        [JsonPropertyName("version")]
        public int Version { get; set; } = CurrentVersion;

        /// <summary>One row per board played, in the order each was first counted.</summary>
        [JsonPropertyName("levels")]
        public List<LevelPlayCounts> Levels { get; set; } = new();

        /// <summary>Where this was loaded from, and where it is written.</summary>
        [JsonIgnore]
        public string Path { get; private set; }

        /// <summary>What <see cref="Load"/> found, for the caller's log line (#353's reason: an empty object hides a loss).</summary>
        [JsonIgnore]
        public ProgressLoad Outcome { get; private set; }

        /// <summary>Where a copy of a file that would not read was kept, or null.</summary>
        [JsonIgnore]
        public string KeptUnreadable { get; private set; }

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
        };

        /// <summary>
        /// The counts at <paramref name="path"/>, or its backup, or empty ones bound to the path — lenient, as the save is:
        /// a missing file is the normal first run and an unreadable one costs the counts, never the game.
        /// </summary>
        public static PlayStats Load(string path)
        {
            string backup = path + BackupSuffix;
            bool anythingWasThere = System.IO.File.Exists(path) || System.IO.File.Exists(backup);

            PlayStats stats = TryRead(path);
            if (stats != null)
            {
                stats.Path = path;
                stats.Outcome = ProgressLoad.Loaded;
                return stats;
            }

            string kept = AtomicFile.KeepUnreadable(path);
            stats = TryRead(backup);
            if (stats != null)
            {
                stats.Path = path;
                stats.Outcome = ProgressLoad.RecoveredFromBackup;
                stats.KeptUnreadable = kept;
                return stats;
            }

            return new PlayStats
            {
                Path = path,
                Outcome = anythingWasThere ? ProgressLoad.Discarded : ProgressLoad.Fresh,
                KeptUnreadable = kept ?? AtomicFile.KeepUnreadable(backup),
            };
        }

        /// <summary>A readable file at <paramref name="path"/>, or null for anything that is not one.</summary>
        public static PlayStats TryRead(string path)
        {
            try
            {
                using FileStream stream = System.IO.File.OpenRead(path);
                PlayStats stats = JsonSerializer.Deserialize<PlayStats>(stream, Options);
                if (stats?.Format != FormatMarker || stats.Version > CurrentVersion) return null;

                stats.Levels ??= new List<LevelPlayCounts>();
                stats.Levels.RemoveAll(level => level == null || string.IsNullOrEmpty(level.File) || string.IsNullOrEmpty(level.Hash));
                return stats;
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException)
            {
                return null;
            }
        }

        /// <summary>The file's text, made where the counts are changed, so the write can go elsewhere (the game writes it off the frame).</summary>
        public string Serialize() => JsonSerializer.Serialize(this, Options);

        /// <summary>Writes the file where it was loaded from, atomically, keeping the last one as the backup. Throws as the filesystem does.</summary>
        public void Save() => AtomicFile.WriteText(Path, Serialize(), BackupSuffix);

        /// <summary>The row of one board, made (and kept) when there is none yet.</summary>
        public LevelPlayCounts For(LevelPlayCounts board)
        {
            foreach (LevelPlayCounts level in Levels)
                if (level.SameBoard(board)) return level;

            LevelPlayCounts row = new() { File = board.File, Hash = board.Hash, Rules = board.Rules };
            Levels.Add(row);
            return row;
        }

        /// <summary>The row of one board, or null when it has never been counted.</summary>
        public LevelPlayCounts Find(string file, string hash, int rules)
        {
            foreach (LevelPlayCounts level in Levels)
                if (level.File == file && level.Hash == hash && level.Rules == rules) return level;

            return null;
        }
    }

    /// <summary>How a counted attempt ended, as <see cref="PlayStatsRecorder.Ended"/> takes it.</summary>
    public enum PlayEnding
    {
        Cleared,
        OutOfBalls,
        ClusterReachedLine,
    }

    /// <summary>
    /// The rules of the counting (#709), apart from any screen: what one attempt adds to <see cref="PlayStats"/>. The game
    /// tells it four events — an attempt started, ended on its result page, left before it, and a skip — and each call
    /// adds its change to the totals and returns that change (null when it counts nothing), which the game writes and,
    /// with online scores on, hands to the score client. Nothing here reads the counts back to decide anything.
    /// <para>
    /// <b>One attempt is open at a time</b>: a start opens it, its ending or its abandonment closes it, and a second close
    /// counts nothing, so the gameplay screen can say "abandoned" from every place an attempt can be torn down without
    /// asking whether it ended first. A level on no board (the built-in fallback, whose identity is null) is not counted.
    /// </para>
    /// <para>
    /// <b>A retry</b> is a start of the board the last attempt was on, when that attempt did not clear it — a loss, a
    /// Restart, or the level left and picked again. Held in memory only: the first start after a launch is never one.
    /// </para>
    /// </summary>
    public sealed class PlayStatsRecorder
    {
        /// <summary>The player's totals, which every event is added to.</summary>
        public PlayStats Totals { get; }

        private LevelPlayCounts _open;
        private LevelPlayCounts _last;
        private bool _lastUncleared;

        public PlayStatsRecorder(PlayStats totals) => Totals = totals ?? throw new ArgumentNullException(nameof(totals));

        /// <summary>Whether an attempt is open: started, and neither ended nor left yet.</summary>
        public bool AttemptOpen => _open != null;

        /// <summary>The board of the attempt open now, or null.</summary>
        public LevelPlayCounts OpenBoard => _open;

        /// <summary>
        /// An attempt at <paramref name="level"/> starts. An attempt still open is the caller's to close first
        /// (<see cref="Abandoned"/>); one left open here is dropped uncounted rather than counted as two.
        /// </summary>
        public LevelPlayCounts Started(LevelIdentity level, int rules = ScoreKeeper.RulesVersion)
        {
            if (level == null)
            {
                _open = null;
                return null;
            }

            LevelPlayCounts board = LevelPlayCounts.For(level, rules);
            bool retry = _lastUncleared && board.SameBoard(_last);

            _open = board;
            _last = board;
            _lastUncleared = true;

            LevelPlayCounts change = LevelPlayCounts.For(level, rules);
            change.Starts = 1;
            change.Retries = retry ? 1 : 0;
            return Apply(change);
        }

        /// <summary>
        /// The open attempt reached its result page. <paramref name="firstClear"/>: a clear of a level the save had never
        /// cleared, which also adds the starts this board has counted so far, the tries that first clear took.
        /// </summary>
        public LevelPlayCounts Ended(PlayEnding ending, int shots, double seconds, bool firstClear)
        {
            if (_open == null) return null;

            LevelPlayCounts change = Close(shots, seconds);

            switch (ending)
            {
                case PlayEnding.Cleared:
                    change.Clears = 1;
                    _lastUncleared = false;

                    if (firstClear)
                    {
                        change.FirstClears = 1;
                        change.StartsToFirstClear = Totals.For(change).Starts;
                    }
                    break;

                case PlayEnding.OutOfBalls:
                    change.LostOutOfBalls = 1;
                    break;

                default:
                    change.LostLine = 1;
                    break;
            }

            return Apply(change);
        }

        /// <summary>The open attempt was left before its result page; nothing when none is open.</summary>
        public LevelPlayCounts Abandoned(int shots, double seconds)
        {
            if (_open == null) return null;

            LevelPlayCounts change = Close(shots, seconds);
            change.Abandons = 1;
            return Apply(change);
        }

        /// <summary>The chapter's skip was spent on <paramref name="level"/> (#347).</summary>
        public LevelPlayCounts Skipped(LevelIdentity level, int rules = ScoreKeeper.RulesVersion)
        {
            if (level == null) return null;

            LevelPlayCounts change = LevelPlayCounts.For(level, rules);
            change.Skips = 1;
            return Apply(change);
        }

        private LevelPlayCounts Close(int shots, double seconds)
        {
            LevelPlayCounts change = _open.Copy();
            change.Shots = Math.Max(0, shots);
            change.Seconds = double.IsFinite(seconds) ? Math.Max(0.0, Math.Round(seconds, 2)) : 0.0;
            _open = null;
            return change;
        }

        private LevelPlayCounts Apply(LevelPlayCounts change)
        {
            Totals.For(change).Add(change);
            return change;
        }
    }
}

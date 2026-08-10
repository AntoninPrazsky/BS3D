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
    /// It is a JSON file beside the level set — the repository's convention keeps level data next to the
    /// executable's own <c>Levels</c> directory, and progress through a set belongs with the set it measures.
    /// The bests are <b>best-per-level</b>, not a running sum of every play: replaying a level can only raise
    /// its entry, so the totals reward getting better rather than grinding the same level over.
    /// </para>
    /// <para>
    /// Unlike <see cref="LevelSet.Load"/>, <see cref="Load"/> is <b>lenient</b>: a missing, unreadable or
    /// malformed file comes back as a fresh, empty progress rather than an exception. A save file is the one
    /// piece of level data whose absence is the normal case (a first run), and a corrupt one must cost the
    /// player their stars, never the game.
    /// </para>
    /// </summary>
    public sealed class PlayerProgress
    {
        public const string FormatMarker = "bs3d-progress";
        public const int CurrentVersion = 1;

        /// <summary>What sits beside a level set's own file — see <see cref="LevelSet.DefaultFileName"/>.</summary>
        public const string DefaultFileName = "Progress.json";

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

        /// <summary>Where this progress was loaded from and where <see cref="Save"/> writes it back.</summary>
        [JsonIgnore]
        public string Path { get; private set; }

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Reads the progress at <paramref name="path"/>, or returns a fresh empty one bound to that path when
        /// there is nothing readable there — no file on a first run, a wrong marker, malformed JSON. Lenient
        /// where the set's loader throws, because an absent save is the normal case rather than an error.
        /// </summary>
        public static PlayerProgress Load(string path)
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
                //Fall through to the fresh instance below: whatever was there is not progress
            }

            return new PlayerProgress { Path = path };
        }

        /// <summary>
        /// Writes the progress back where it was loaded from. Throws on an unwritable path, exactly as
        /// <see cref="LevelSet.Save"/> does — whether a failed save is worth more than a log line is the
        /// caller's call, not this file's.
        /// </summary>
        public void Save() => File.WriteAllText(Path, JsonSerializer.Serialize(this, Options));

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

        /// <summary>Back to a fresh start: every best gone. The settings row that offers this saves after it.</summary>
        public void Reset() => Levels.Clear();
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// The order the levels are played in: which one is first, which one is second. A <see cref="Level"/> (or
    /// a plain map file) says what a level <i>is</i>; this says how they are strung together, and it is the
    /// only place that order lives — a game reads the set, not the directory listing, because the filesystem
    /// has no order worth relying on and a directory would silently promote whatever was dropped into it.
    /// <para>
    /// A set is a JSON object marked by <c>"format": "bs3d-levels"</c>, the same shape of marker
    /// <see cref="Level"/> uses, and its entries name files <b>relative to the set's own directory</b> — so a
    /// set and its levels move together and nothing depends on the working directory. Entries are deliberately
    /// objects rather than bare strings: a level needs a display name now, and par, unlock rules or a preview
    /// later, and adding a field to an object does not break a file that predates it.
    /// </para>
    /// </summary>
    public sealed class LevelSet
    {
        public const string FormatMarker = "bs3d-levels";
        public const int CurrentVersion = 1;

        /// <summary>What a game looks for in its <c>Levels</c> directory when nothing else is specified.</summary>
        public const string DefaultFileName = "Levels.json";

        /// <summary>File-type marker; always <see cref="FormatMarker"/> in a valid level-set file.</summary>
        [JsonPropertyName("format")]
        public string Format { get; set; } = FormatMarker;

        [JsonPropertyName("version")]
        public int Version { get; set; } = CurrentVersion;

        /// <summary>Display name of the whole set — a campaign, an episode, a pack (optional).</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>The levels, <b>in play order</b>. The first entry is the first level.</summary>
        [JsonPropertyName("levels")]
        public List<LevelSetEntry> Levels { get; set; } = new();

        /// <summary>
        /// The directory the set was loaded from, which is what <see cref="ResolvePath"/> makes entry paths
        /// relative to. Not serialized: it is a property of where the file was found, not of the file.
        /// </summary>
        [JsonIgnore]
        public string Directory { get; private set; }

        public int Count => Levels?.Count ?? 0;

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Loads and validates a level set. Throws rather than returning a broken set, so a caller can log and
        /// leave its current state untouched — the same contract <see cref="Level.Load"/> has.
        /// </summary>
        public static LevelSet Load(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                LevelSet set = JsonSerializer.Deserialize<LevelSet>(stream, Options)
                    ?? throw new InvalidDataException($"'{path}' does not contain a level set");

                if (set.Format != FormatMarker)
                    throw new InvalidDataException($"'{path}' is not a level set (format marker '{set.Format}')");
                if (set.Version > CurrentVersion)
                    throw new InvalidDataException($"'{path}' is a version {set.Version} level set; this build reads up to {CurrentVersion}");
                if (set.Count == 0)
                    throw new InvalidDataException($"'{path}' lists no levels");

                for (int i = 0; i < set.Levels.Count; i++)
                {
                    LevelSetEntry entry = set.Levels[i];

                    if (string.IsNullOrWhiteSpace(entry.File))
                        throw new InvalidDataException($"'{path}': level {i + 1} names no file");

                    //A rule that is present has to mean something. Left to reach the game, a zero budget is a
                    //level that is over before it starts and a zero ceiling step is a division by zero or an
                    //infinite descent — both of which would surface as an unplayable level rather than as the
                    //bad file they are. Absent is the way to say "no rule"; zero is not a way to say anything.
                    if (entry.Shots is <= 0)
                        throw new InvalidDataException(
                            $"'{path}': level {i + 1} ('{entry.File}') grants {entry.Shots} shots; omit \"shots\" for unlimited");

                    if (entry.MinScore is < 0)
                        throw new InvalidDataException(
                            $"'{path}': level {i + 1} ('{entry.File}') has a negative minimum score ({entry.MinScore})");

                    if (entry.CeilingStep is <= 0)
                        throw new InvalidDataException(
                            $"'{path}': level {i + 1} ('{entry.File}') steps the ceiling every {entry.CeilingStep} shots; omit \"ceilingStep\" to hold it still");
                }

                //Captured here rather than at each use: the entries are relative to the set, and the set is
                //the only thing that knows where it came from
                set.Directory = Path.GetDirectoryName(Path.GetFullPath(path));

                return set;
            }
        }

        public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Options));

        /// <summary>The full path of one entry's level file, resolved against <see cref="Directory"/>.</summary>
        public string ResolvePath(int index) => Path.Combine(Directory ?? string.Empty, Levels[index].File);

        /// <summary>
        /// What to call the level at <paramref name="index"/>: its own name if it carries one, otherwise the
        /// file's name without the extension, so an unnamed entry still says something useful in a log or a HUD.
        /// </summary>
        public string DisplayName(int index) =>
            string.IsNullOrWhiteSpace(Levels[index].Name)
                ? Path.GetFileNameWithoutExtension(Levels[index].File)
                : Levels[index].Name;

        /// <summary>
        /// One level's rules as a line for a log — what the file actually says, with each absent rule spelled
        /// out as the thing it means rather than left blank. It lives here so the vocabulary ("unlimited
        /// shots", "ceiling holds") is written once instead of at every place that reports a level.
        /// <para>
        /// It reports only what was <b>authored</b>. Whether an authored budget and an authored ceiling step
        /// agree with each other cannot be told from here — see <see cref="LevelSetEntry.CeilingStep"/>.
        /// </para>
        /// </summary>
        public string DescribeRules(int index)
        {
            LevelSetEntry entry = Levels[index];

            string shots = entry.Shots.HasValue ? $"{entry.Shots.Value} shots" : "unlimited shots";
            string minScore = entry.MinScore.GetValueOrDefault() > 0 ? $"min score {entry.MinScore.Value}" : "no score gate";
            string ceiling = entry.CeilingStep.HasValue ? $"ceiling every {entry.CeilingStep.Value}" : "ceiling holds";

            return $"{shots}, {minScore}, {ceiling}";
        }

        /// <summary>
        /// Cheap probe, mirroring <see cref="Level.IsLevelFile"/>: true when the file is a JSON object carrying
        /// the level-set marker. A level, a plain map or anything unreadable returns false.
        /// </summary>
        public static bool IsLevelSetFile(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (JsonDocument doc = JsonDocument.Parse(stream))
                {
                    return doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("format", out JsonElement format)
                        && format.ValueKind == JsonValueKind.String
                        && format.GetString() == FormatMarker;
                }
            }
            //Best-effort, exactly as Level.IsLevelFile is: a directory, an ACL-denied path, an invalid path or
            //malformed JSON all mean "not a level set" rather than an exception out of a probe
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// One level in a <see cref="LevelSet"/>: the file it lives in, what to call it, and the rules it is
    /// played under.
    /// <para>
    /// The rules live here rather than in the level file because this is the only place that can annotate a
    /// <b>plain map file</b> — a hand-drawn <c>.json</c> map has nowhere to put metadata of its own, and
    /// putting the rules in the <see cref="Level"/> format would force every such map to be converted first.
    /// They are all <b>nullable</b>, and null means "no rule" rather than zero: a set authored before rules
    /// existed has to keep playing exactly as it did, and a sentinel number cannot say the difference between
    /// "unlimited" and "none". What each null falls back to is documented at the point it is <i>read</i>, not
    /// here — the game decides what an absent rule means, and this only records that it is absent.
    /// </para>
    /// </summary>
    public sealed class LevelSetEntry
    {
        /// <summary>
        /// The level's file, relative to the set's own directory. Either a full <see cref="Level"/> file or a
        /// plain map file — both are <c>.json</c> and the loader probes, so a set can mix them.
        /// </summary>
        [JsonPropertyName("file")]
        public string File { get; set; }

        /// <summary>Display name (optional; the file name stands in when it is missing).</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// How many balls the level grants. <b>Null means unlimited</b> — the sandbox the game has today, and
        /// what every set authored before rules existed keeps. Running out with the field uncleared is one of
        /// the two ways to lose; the other is <see cref="CeilingStep"/>.
        /// </summary>
        [JsonPropertyName("shots")]
        public int? Shots { get; set; }

        /// <summary>
        /// The score needed to unlock the <b>next</b> level. Null (or zero) means clearing the field is enough,
        /// which is what every level starts as — the gate exists so difficulty can be raised level by level as
        /// the campaign grows, not so it is on everywhere from the start.
        /// </summary>
        [JsonPropertyName("minScore")]
        public int? MinScore { get; set; }

        /// <summary>
        /// Shots between two descents of the glass ceiling. <b>Null means the ceiling holds still.</b>
        /// <para>
        /// This and <see cref="Shots"/> are <b>not independent</b>: the descent is driven by shots and the
        /// death line is fixed, so the geometry already implies a shot limit of its own. Authoring both
        /// carelessly leaves one of them dead — either the budget runs out with the ceiling still high, or the
        /// ceiling arrives with shots to spare. Nothing here can check that, because the implied limit depends
        /// on the ceiling's travel and the death line, which are the game's geometry and not the set's data;
        /// the cross-check belongs with whoever moves the ceiling.
        /// </para>
        /// </summary>
        [JsonPropertyName("ceilingStep")]
        public int? CeilingStep { get; set; }
    }
}

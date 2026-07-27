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
                    if (string.IsNullOrWhiteSpace(set.Levels[i].File))
                        throw new InvalidDataException($"'{path}': level {i + 1} names no file");

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

    /// <summary>One level in a <see cref="LevelSet"/>: the file it lives in, and what to call it.</summary>
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
    }
}

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

        //Ignored, or STJ serializes the read-only property and every saved set carries a "Count" that the
        //loader then has to shrug off — which is exactly what the generated Levels.json used to do
        [JsonIgnore]
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

                    if (entry.MinStars is < 0)
                        throw new InvalidDataException(
                            $"'{path}': level {i + 1} ('{entry.File}') unlocks at a negative star count ({entry.MinStars})");

                    if (entry.CeilingStep is <= 0)
                        throw new InvalidDataException(
                            $"'{path}': level {i + 1} ('{entry.File}') steps the ceiling every {entry.CeilingStep} shots; omit \"ceilingStep\" to hold it still");

                    //A block is a STRETCH of the campaign, so the same name may not open a second run later on:
                    //that is either a typo or a milestone that fires twice, and both are better refused at the
                    //file than explained in play. Checked against the entry two back and beyond rather than by
                    //collecting names, because what is illegal is specifically a name RETURNING after it stopped.
                    if (!string.IsNullOrWhiteSpace(entry.Block) && i > 0 && set.Levels[i - 1].Block != entry.Block)
                        for (int earlier = 0; earlier < i - 1; earlier++)
                            if (set.Levels[earlier].Block == entry.Block)
                                throw new InvalidDataException(
                                    $"'{path}': level {i + 1} ('{entry.File}') reopens block '{entry.Block}', "
                                    + $"which already ran at level {earlier + 1}; a block must be consecutive");
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

        #region Blocks (#184)

        //A block is a run of consecutive entries sharing LevelSetEntry.Block, and these five queries are the
        //whole of reading one. They WALK the run on every call rather than caching a table, for one reason worth
        //stating: a set is built in memory as often as it is loaded (LevelGen writes one it never loads back
        //through Load), so anything computed in Load would be absent exactly where a tool needs it and present
        //where the game does. A block is five entries in the shipped set and these are called at a level's end
        //and when the picker is built, never per frame.

        /// <summary>
        /// Whether this set is chaptered at all — whether any entry names a block. A set that does not is played
        /// exactly as it was before #184, with no milestone anywhere.
        /// </summary>
        [JsonIgnore]
        public bool HasBlocks
        {
            get
            {
                for (int i = 0; i < Count; i++)
                    if (!string.IsNullOrWhiteSpace(Levels[i].Block)) return true;

                return false;
            }
        }

        /// <summary>The block <paramref name="index"/> belongs to, or null when it is in none.</summary>
        public string BlockName(int index) =>
            string.IsNullOrWhiteSpace(Levels[index].Block) ? null : Levels[index].Block;

        /// <summary>
        /// The bounds of the block <paramref name="index"/> sits in, both inclusive. An entry naming no block is
        /// its own run of one, which is what makes every caller's arithmetic hold without a special case.
        /// </summary>
        public void BlockRange(int index, out int first, out int last)
        {
            first = index;
            while (first > 0 && SameBlock(first - 1, index)) first--;

            last = index;
            while (last + 1 < Count && SameBlock(last + 1, index)) last++;
        }

        /// <summary>Which block <paramref name="index"/> is in, counting from 1, and how many there are.</summary>
        public int BlockNumber(int index)
        {
            int number = 1;

            for (int i = 1; i <= index; i++)
                if (!SameBlock(i, i - 1)) number++;

            return number;
        }

        /// <summary>
        /// Whether two entries are in the <b>same</b> block. Both must NAME one and name the same one — an unnamed
        /// entry is in no block, so it is never in the same one as anything, not even as the next unnamed entry
        /// beside it.
        /// <para>
        /// That last clause is the whole of this method, and it is here because the obvious spelling was wrong. A
        /// plain <c>Levels[i].Block == Levels[j].Block</c> makes <c>null == null</c> true, so every entry of a set
        /// naming no blocks at all merged into <b>one</b> run: <see cref="BlockRange"/> handed back the whole
        /// campaign for level 1 of a two-level set, and <see cref="BlockCount"/> said 1 where the comment on it
        /// says a set naming none has as many blocks as levels. Nothing live read it — both callers in the game
        /// are gated on <see cref="HasBlocks"/> — which is exactly why it would have sat there: the contract was
        /// wrong while the game was right by luck.
        /// </para>
        /// </summary>
        private bool SameBlock(int a, int b) =>
            !string.IsNullOrWhiteSpace(Levels[a].Block) && Levels[a].Block == Levels[b].Block;

        /// <summary>How many blocks the set is in — runs of entries, so a set naming none has as many as levels.</summary>
        [JsonIgnore]
        public int BlockCount => Count == 0 ? 0 : BlockNumber(Count - 1);

        #endregion

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
            string ceiling = entry.CeilingStep.HasValue ? $"ceiling every {entry.CeilingStep.Value}" : "ceiling holds";

            //The unlock gate is deliberately not in this line: these are the rules a level is PLAYED under,
            //and the gate is over by the time one is — the picker presents it on the locked entry itself
            return $"{shots}, {ceiling}";
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
        /// Which <b>block</b> — chapter, if you like — this level belongs to (#184). A run of
        /// <i>consecutive</i> entries carrying the same name is one block; null on every entry, which is what
        /// every set authored before this said, means the set has no blocks and nothing celebrates one.
        /// <para>
        /// <b>A name on the entry rather than a size on the set</b>, and there were three candidates. A
        /// <c>blockSize</c> would have been cheapest, but it can only make blocks that are all the same length
        /// and it gives the player's milestone nothing to be called — "BLOCK 3 COMPLETE" is a number, where
        /// "THE TOWER COMPLETE" is a place. Grouping by the levels' own <c>scene</c> would need no new field at
        /// all, but a scene is a property of the level <i>file</i> and the grouping is a property of the
        /// <i>order</i>, so two sets sharing a level could not chapter it differently — and it would name a
        /// block after its backdrop, which is a coincidence of this campaign rather than a rule. The name here
        /// says what the grouping is <b>for</b>, sits with the other per-entry rules for the reason this type's
        /// own remarks give, and needs no arithmetic to read.
        /// </para>
        /// <para>
        /// <b>Consecutive is enforced</b> by <see cref="LevelSet.Load"/>: the same name may not open a second
        /// run later in the set. A block is a stretch of the campaign, so a name appearing in two places is
        /// either a typo or a milestone that would fire twice, and both are worth refusing at the file rather
        /// than explaining in play.
        /// </para>
        /// </summary>
        [JsonPropertyName("block")]
        public string Block { get; set; }

        /// <summary>
        /// How many balls the level grants. <b>Null means unlimited</b> — the sandbox the game has today, and
        /// what every set authored before rules existed keeps. Running out with the field uncleared is one of
        /// the two ways to lose; the other is <see cref="CeilingStep"/>.
        /// </summary>
        [JsonPropertyName("shots")]
        public int? Shots { get; set; }

        /// <summary>
        /// The <b>total stars</b> the campaign must have collected before <i>this</i> level unlocks
        /// (see <c>PlayerProgress.TotalStars</c>). Null (or zero) means the level is open from the start.
        /// <para>
        /// It replaced a per-level <c>minScore</c> gate that no shipped set ever authored: a raw score is
        /// meaningless to a player as a target, while stars are the very thing the result screen headlines —
        /// and gating on the campaign's <i>total</i> makes every level played contribute, instead of one
        /// number on one level deciding everything (#111). On the entry it locks rather than on the entry
        /// before it, so the picker can read each level's own gate straight off it.
        /// </para>
        /// </summary>
        [JsonPropertyName("minStars")]
        public int? MinStars { get; set; }

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

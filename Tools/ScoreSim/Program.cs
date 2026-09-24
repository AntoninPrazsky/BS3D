using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BS3D.Tools.ScoreSim
{
    /// <summary>
    /// Plays every shipped level several ways through the <b>real</b> <see cref="ScoreKeeper"/> and
    /// <see cref="StarRating"/>, and refuses a scoring rule that rates them wrongly.
    /// <para>
    /// It exists because #173 found the rating <b>ordered backwards</b> — the score rose with the number of
    /// shots taken, on all thirteen levels, so the play style four of them are designed around was the one the
    /// stars punished. Nothing in the repository would have caught that, and nothing would catch it coming
    /// back: the levels have a validator (<c>Tools/LevelGen</c>) and the scoring had nothing. This is that.
    /// </para>
    /// <para>
    /// It models none of the scoring. It calls it — so it cannot drift from the rules the way a spreadsheet
    /// of them would, and a change to <see cref="ScoreKeeper"/> is felt here immediately.
    /// </para>
    /// <para>
    /// <c>dotnet run --project Tools\ScoreSim\ScoreSim.csproj [levels directory] [--ceilings out.json]</c>. With no
    /// directory it walks up from its own bin directory to the first <c>Game\Levels</c> it finds, exactly as
    /// LevelGen does and for the same reason: the tool is run from a bin path whose depth depends on the
    /// configuration.
    /// </para>
    /// <para>
    /// <b>It also states the online boards' ceilings (#549)</b>: every level's <see cref="ScoreKeeper.ScoreCeiling"/>
    /// beside its runs, the check that no run reaches it, and with <c>--ceilings</c> the table the score service
    /// whitelists levels and refuses scores against — keyed by the same <see cref="LevelIdentity"/> the game's
    /// client computes, so the two cannot disagree about which board a clear belongs to.
    /// </para>
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Shot counts a clear is simulated at. The point of the spread is the <b>ordering</b> between them,
        /// not any single figure: a rating that is doing its job puts the efficient clears above the patient
        /// one, and it was doing precisely the opposite before #173.
        /// </summary>
        private static readonly int[] ShotCounts = { 4, 8, 16 };

        /// <summary>One shot in this many misses, in the sloppy run that sets the bottom of the band.</summary>
        private const int SloppyMissEvery = 3;

        private static int Main(string[] args)
        {
            string directory = null;
            string ceilingsPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--ceilings")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("--ceilings needs the path of the table to write.");
                        return 1;
                    }

                    ceilingsPath = args[++i];
                }
                else directory = args[i];
            }

            try
            {
                directory ??= FindLevelsDirectory();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                return 1;
            }

            Console.WriteLine($"Reading {directory}");

            List<Playable> levels = ReadLevels(directory);
            if (levels.Count == 0)
            {
                Console.WriteLine("No levels with a shot budget were found — nothing to rate.");
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine($"Rules v{ScoreKeeper.RulesVersion}   " +
                $"Thresholds: {StarRating.TWO_STAR_FLOOR_MULTIPLE:F2} / " +
                $"{StarRating.THREE_STAR_FLOOR_MULTIPLE:F2} / {StarRating.FOUR_STAR_FLOOR_MULTIPLE:F2}   " +
                $"multiplier +{ScoreKeeper.MultiplierStep} to x{ScoreKeeper.MaxMultiplier}   " +
                $"unused shot = {ScoreKeeper.UnusedShotWorthInShots} average shots");
            Console.WriteLine();
            Console.WriteLine("level        balls shots |   4 shots      8         16      budget-1  |  sloppy  | ceiling best/ceil min");

            bool ok = true;

            foreach (Playable level in levels) ok &= Report(level);

            Console.WriteLine();
            Console.WriteLine(ok
                ? "All levels rate the right way round, and every run is under its ceiling."
                : "At least one level rates WRONGLY or reaches its ceiling — see above.");

            if (ceilingsPath != null)
            {
                //Written even when a gate failed, so the table can be looked at — the exit code is what refuses it
                WriteCeilings(levels, ceilingsPath);
                Console.WriteLine($"Wrote {levels.Count} ceilings (rules v{ScoreKeeper.RulesVersion}) to {Path.GetFullPath(ceilingsPath)}");
            }

            return ok ? 0 : 1;
        }

        /// <summary>
        /// One level, played several ways. Returns whether the rating held up, and says why when it did not.
        /// </summary>
        private static bool Report(Playable level)
        {
            double[] fast = new double[ShotCounts.Length];
            int[] fastStars = new int[ShotCounts.Length];
            int bestScore = 0;

            for (int i = 0; i < ShotCounts.Length; i++)
            {
                //A clear cannot take fewer shots than it has balls to place, and a level with a small budget
                //cannot be simulated at more shots than it grants.
                int shots = Math.Min(ShotCounts[i], level.Shots - 1);

                int score = Clean(level, Math.Max(1, shots));
                fast[i] = Multiple(score, level.Balls);
                fastStars[i] = StarRating.Rate(score, level.Balls);
                bestScore = Math.Max(bestScore, score);
            }

            int patientScore = Clean(level, level.Shots - 1);
            double patient = Multiple(patientScore, level.Balls);
            int patientStars = StarRating.Rate(patientScore, level.Balls);

            int sloppyScore = Sloppy(level);
            double sloppy = Multiple(sloppyScore, level.Balls);
            int sloppyStars = StarRating.Rate(sloppyScore, level.Balls);

            //The best any of the runs above scored, against the ceiling the online boards refuse a score over
            bestScore = Math.Max(bestScore, Math.Max(patientScore, sloppyScore));

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-12} {1,5} {2,5} | {3,6:F2}({4}) {5,6:F2}({6}) {7,6:F2}({8}) {9,6:F2}({10}) | {11,5:F2}({12}) | {13,7} {14,6:F2} {15,4}",
                level.Name, level.Balls, level.Shots,
                fast[0], fastStars[0], fast[1], fastStars[1], fast[2], fastStars[2],
                patient, patientStars, sloppy, sloppyStars,
                level.Ceiling, bestScore / (double)level.Ceiling, level.MinShots));

            bool ok = true;

            //THE CEILING (#549): the service refuses a score over it, so a score this simulator reaches with the
            //real rules must be under it — or the bound has a term missing and real players would be refused
            if (bestScore >= level.Ceiling)
            {
                Console.WriteLine($"    OVER CEILING: a simulated clear scores {bestScore}, " +
                    $"at or over the ceiling of {level.Ceiling} the online boards would refuse it at");
                ok = false;
            }

            //THE ORDERING, and it is the whole reason this tool exists. Spending the entire budget must not
            //out-rate clearing in a handful of shots — that inversion is what #173 found, and it was invisible
            //because every individual number looked reasonable on its own.
            foreach (double multiple in fast)
                if (multiple <= patient)
                {
                    Console.WriteLine($"    INVERTED: an efficient clear rates {multiple:F2}, " +
                        $"no better than spending the whole budget at {patient:F2}");
                    ok = false;
                }

            //The top of the scale has to be reachable by the play the levels are designed for...
            int best = 0;
            foreach (int stars in fastStars) best = Math.Max(best, stars);

            if (best < StarRating.MAX)
            {
                Console.WriteLine($"    UNREACHABLE: the best clean clear earns {best} of {StarRating.MAX} stars");
                ok = false;
            }

            //...and it has to mean something, so the worst realistic clear must not reach it.
            if (sloppyStars >= StarRating.MAX)
            {
                Console.WriteLine($"    UNEARNED: a clear that misses one shot in {SloppyMissEvery} " +
                    $"still earns {sloppyStars} stars");
                ok = false;
            }

            return ok;
        }

        /// <summary>A clear in exactly <paramref name="shots"/> landing shots, never missing.</summary>
        private static int Clean(Playable level, int shots)
        {
            ScoreKeeper keeper = new(level.Shots, null, level.Balls);

            int perShot = Math.Max(1, level.Balls / shots);
            int placed = 0;

            for (int i = 0; i < shots; i++)
            {
                int balls = i == shots - 1 ? level.Balls - placed : Math.Min(perShot, level.Balls - placed);
                if (balls <= 0) break;

                placed += balls;
                keeper.Landed(balls, 0);
                keeper.Shot();
            }

            keeper.AwardCompletionBonus();
            return keeper.Score;
        }

        /// <summary>
        /// The bottom of the band: the whole budget spent, missing one shot in <see cref="SloppyMissEvery"/>
        /// so the streak keeps resetting. The thresholds have to be set against this as much as against
        /// perfect play — a top rating the worst realistic clear also earns is not rating anything.
        /// </summary>
        private static int Sloppy(Playable level)
        {
            ScoreKeeper keeper = new(level.Shots, null, level.Balls);

            int landing = Math.Max(1, level.Shots * (SloppyMissEvery - 1) / SloppyMissEvery);
            int perShot = Math.Max(1, level.Balls / landing);
            int placed = 0;

            for (int i = 0; i < level.Shots && placed < level.Balls; i++)
            {
                if (i % SloppyMissEvery == SloppyMissEvery - 1)
                {
                    keeper.Missed();
                    keeper.Shot();
                    continue;
                }

                int balls = Math.Min(perShot, level.Balls - placed);
                placed += balls;
                keeper.Landed(balls, 0);
                keeper.Shot();
            }

            keeper.AwardCompletionBonus();
            return keeper.Score;
        }

        /// <summary>
        /// The score as a multiple of the level's floor — the same yardstick <see cref="StarRating"/> rates on,
        /// restated here so a row can be read against the thresholds printed above it.
        /// </summary>
        private static double Multiple(int score, int balls) =>
            score / (double)(ScoreKeeper.MatchedBallPoints * balls);

        /// <summary>The shipped set, with the ball count read from each level's own map.</summary>
        private static List<Playable> ReadLevels(string directory)
        {
            List<Playable> playable = new();

            LevelSet set = LevelSet.Load(Path.Combine(directory, LevelSet.DefaultFileName));
            if (set == null)
            {
                Console.WriteLine("The level set could not be read.");
                return playable;
            }

            foreach (LevelSetEntry entry in set.Levels)
            {
                //An unlimited budget has no efficiency to rate and no completion bonus to earn, so there is
                //nothing here to check — it is not a failure, it is a level the rating does not apply to.
                if (entry.Shots is not > 0) continue;

                string path = Path.Combine(directory, entry.File);
                Level level = Level.Load(path);

                if (level?.Map == null)
                {
                    Console.WriteLine($"  {entry.File}: could not be read, skipped");
                    continue;
                }

                BallsMap map = new(level.Map);
                int balls = map.GetBallsCount();
                if (balls <= 0) continue;

                playable.Add(new Playable(entry.Name ?? entry.File, balls, entry.Shots.Value,
                    LevelIdentity.Of(entry, File.ReadAllBytes(path)), ScoreKeeper.ScoreCeiling(balls, entry.Shots.Value),
                    MinimumShots(map)));
            }

            return playable;
        }

        /// <summary>
        /// The table the score service loads (#549): one row per level with a budget, keyed by the
        /// <see cref="LevelIdentity"/> the game's client computes. A level with an unlimited budget has no ceiling
        /// (any number of shots can stick and fall) and is left out, so the service refuses boards for it — which
        /// is the honest answer for a sandbox. Every row carries the rules version as well as the header, so a row
        /// copied out of the file on its own still says which rules it bounds.
        /// <para>
        /// <b>A shot floor, not a time floor.</b> #549 asked for a <c>minSeconds</c> a human cannot beat, as the
        /// level's minimum shot count times a shot's flight and settle. The shot count half is here as
        /// <c>minShots</c> (<see cref="MinimumShots"/>). The seconds half is not: nothing in the game spaces shots
        /// out (<c>Shoot</c> has no interval between them, and several shots fly at once), so the only time floor
        /// that holds for every player is one flight, a tenth of a second — a floor no forged duration a cheat
        /// would bother to send falls under. A real one needs a rule the game does not have, and is the owner's
        /// call.
        /// </para>
        /// </summary>
        private static void WriteCeilings(List<Playable> levels, string path)
        {
            List<CeilingRow> rows = new();
            foreach (Playable level in levels)
                rows.Add(new CeilingRow(level.Identity.File, level.Name, level.Identity.Hash, ScoreKeeper.RulesVersion,
                    level.Shots, level.Ceiling, level.MinShots));

            CeilingTable table = new(CeilingTable.FormatMarker, CeilingTable.CurrentVersion, ScoreKeeper.RulesVersion,
                LevelIdentity.HashLength, rows);

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllText(path, JsonSerializer.Serialize(table, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        /// <summary>
        /// The game's <c>Game\Levels</c>, found by walking up from wherever this was built — by landmark
        /// rather than by counting <c>..</c>, since the bin path's depth depends on the configuration.
        /// </summary>
        private static string FindLevelsDirectory()
        {
            DirectoryInfo directory = new(AppContext.BaseDirectory);

            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Game", "Levels");
                if (Directory.Exists(candidate)) return candidate;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "No Game\\Levels directory was found above this tool — pass one as the first argument.");
        }

        /// <summary>
        /// The fewest shots any clear of <paramref name="map"/> can take (#549) — what the score service refuses a
        /// clear claiming fewer than. It is <c>Tools/LevelGen</c>'s floor (#458, <c>ClearProbe.AnchorColourFloor</c>):
        /// a ball on the field's top course is anchored to the glass and can never be orphaned, so it leaves only in
        /// a matched group, and one shot matches one group of one colour — so a level cannot be emptied in fewer
        /// shots than there are colours standing on its anchor course.
        /// <para>
        /// <b>Stricter than LevelGen's, because this one refuses players.</b> LevelGen drops its floor only for a
        /// special on the top course itself, which is right for a design gate. Here a special <i>anywhere</i> drops
        /// it to one: a blast deep in the cluster can take anchor-course balls by geometry, a zap empties a colour
        /// off the whole field, and an infection repaints colours — any of them can leave the top course in fewer
        /// shots than its colours, and a floor that is wrong for one real clear refuses a real player. So the
        /// bound is taken only on a level made of ordinary balls, where nothing but a match can move the top.
        /// </para>
        /// </summary>
        private static int MinimumShots(BallsMap map)
        {
            StaticBall[,,] balls = map.GetStaticBallsArray();

            foreach (StaticBall ball in balls)
                if (ball != null && ball.Kind != BallKind.Normal) return 1;

            int top = map.Levels - 1, mask = 0, colours = 0;

            for (int x = 0; x < balls.GetLength(0); x++)
                for (int z = 0; z < balls.GetLength(1); z++)
                {
                    StaticBall ball = balls[x, z, top];
                    if (ball == null) continue;

                    int bit = 1 << (int)ball.Type;
                    if ((mask & bit) != 0) continue;

                    mask |= bit;
                    colours++;
                }

            return Math.Max(1, colours);
        }

        private readonly record struct Playable(string Name, int Balls, int Shots, LevelIdentity Identity, int Ceiling,
            int MinShots);

        /// <summary>
        /// The ceiling table's file (#549), marked like every other file this project writes (<c>bs3d-levels</c>,
        /// <c>bs3d-level</c>) so a reader can refuse something that is not one.
        /// </summary>
        private sealed record CeilingTable(
            [property: JsonPropertyName("format")] string Format,
            [property: JsonPropertyName("version")] int Version,
            [property: JsonPropertyName("rulesVersion")] int RulesVersion,
            [property: JsonPropertyName("hashLength")] int HashLength,
            [property: JsonPropertyName("levels")] List<CeilingRow> Levels)
        {
            public const string FormatMarker = "bs3d-ceilings";
            public const int CurrentVersion = 1;
        }

        private sealed record CeilingRow(
            [property: JsonPropertyName("file")] string File,
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("hash")] string Hash,
            [property: JsonPropertyName("rulesVersion")] int RulesVersion,
            [property: JsonPropertyName("shots")] int Shots,
            [property: JsonPropertyName("ceiling")] int Ceiling,
            [property: JsonPropertyName("minShots")] int MinShots);
    }
}

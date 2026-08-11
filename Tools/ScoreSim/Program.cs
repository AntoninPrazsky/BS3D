using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

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
    /// <c>dotnet run --project Tools\ScoreSim\ScoreSim.csproj [levels directory]</c>. With no argument it
    /// walks up from its own bin directory to the first <c>Game\Levels</c> it finds, exactly as LevelGen does
    /// and for the same reason: the tool is run from a bin path whose depth depends on the configuration.
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
            string directory;

            try
            {
                directory = args.Length > 0 ? args[0] : FindLevelsDirectory();
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
            Console.WriteLine($"Thresholds: {StarRating.TWO_STAR_FLOOR_MULTIPLE:F2} / " +
                $"{StarRating.THREE_STAR_FLOOR_MULTIPLE:F2} / {StarRating.FOUR_STAR_FLOOR_MULTIPLE:F2}   " +
                $"multiplier +{ScoreKeeper.MultiplierStep} to x{ScoreKeeper.MaxMultiplier}   " +
                $"unused shot = {ScoreKeeper.UnusedShotWorthInShots} average shots");
            Console.WriteLine();
            Console.WriteLine("level        balls shots |   4 shots      8         16      budget-1  |  sloppy");

            bool ok = true;

            foreach (Playable level in levels) ok &= Report(level);

            Console.WriteLine();
            Console.WriteLine(ok
                ? "All levels rate the right way round."
                : "At least one level rates WRONGLY — see above.");

            return ok ? 0 : 1;
        }

        /// <summary>
        /// One level, played several ways. Returns whether the rating held up, and says why when it did not.
        /// </summary>
        private static bool Report(Playable level)
        {
            double[] fast = new double[ShotCounts.Length];
            int[] fastStars = new int[ShotCounts.Length];

            for (int i = 0; i < ShotCounts.Length; i++)
            {
                //A clear cannot take fewer shots than it has balls to place, and a level with a small budget
                //cannot be simulated at more shots than it grants.
                int shots = Math.Min(ShotCounts[i], level.Shots - 1);

                int score = Clean(level, Math.Max(1, shots));
                fast[i] = Multiple(score, level.Balls);
                fastStars[i] = StarRating.Rate(score, level.Balls);
            }

            int patientScore = Clean(level, level.Shots - 1);
            double patient = Multiple(patientScore, level.Balls);
            int patientStars = StarRating.Rate(patientScore, level.Balls);

            int sloppyScore = Sloppy(level);
            double sloppy = Multiple(sloppyScore, level.Balls);
            int sloppyStars = StarRating.Rate(sloppyScore, level.Balls);

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-12} {1,5} {2,5} | {3,6:F2}({4}) {5,6:F2}({6}) {7,6:F2}({8}) {9,6:F2}({10}) | {11,5:F2}({12})",
                level.Name, level.Balls, level.Shots,
                fast[0], fastStars[0], fast[1], fastStars[1], fast[2], fastStars[2],
                patient, patientStars, sloppy, sloppyStars));

            bool ok = true;

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

                int balls = new BallsMap(level.Map).GetBallsCount();
                if (balls <= 0) continue;

                playable.Add(new Playable(entry.Name ?? entry.File, balls, entry.Shots.Value));
            }

            return playable;
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

        private readonly record struct Playable(string Name, int Balls, int Shots);
    }
}

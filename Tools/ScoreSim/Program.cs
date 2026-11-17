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
    /// <para>
    /// <b>And with <c>--stats [Stats.json]</c> it prints a save's play counts instead (#709)</b> — no run is simulated: the
    /// starts, retries, clears, losses by cause, quits and skips the game counted per level, in play order, with each rate's
    /// counts beside it, so the owner's playtests and the machines' saves inform the level tuning with no server involved.
    /// Without a path it reads the player's own (<c>%LOCALAPPDATA%\BS3D\Stats.json</c>). A counted board the shipped set no
    /// longer names (a regenerated level's older version, a <c>levelfile=</c> run) is listed after the set.
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

        /// <summary>
        /// The time a fast clean clear takes, in pars (#1045): half of one. Every level's best clean clear at that pace must
        /// earn four stars (UNREACHABLE) and outrank the patient run (INVERTED).
        /// </summary>
        private const float FastPars = 0.5f;

        /// <summary>
        /// The time a slow clean clear takes, in pars (#1045): twice par, where the time bonus is gone
        /// (<see cref="ScoreKeeper.TimeBonusZeroAtPars"/>). The same shots at that pace must score less than at
        /// <see cref="FastPars"/> (TIME INVERTED: a faster clear scores more) and must not earn four stars (SLOW FOUR STARS:
        /// four stars need a fast clear, the owner's ruling of 2026-11-02).
        /// </summary>
        private const float SlowPars = 2f;

        /// <summary>
        /// The seconds the patient and the sloppy runs take (#1045): <see cref="RunBaseSeconds"/> and
        /// <see cref="RunSecondsAShot"/> for every shot, the par rule's own pace (<see cref="ParRule"/>), so the runs that
        /// spend the whole budget are timed like any player who fires that many shots.
        /// </summary>
        private const float RunBaseSeconds = ParRule.BASE_SECONDS;

        /// <inheritdoc cref="RunBaseSeconds"/>
        private const float RunSecondsAShot = ParRule.SECONDS_A_SHOT;

        /// <summary>
        /// The least time a clear may claim, as a share of its par (#1045), written into the ceiling table as
        /// <c>minSeconds</c> for the score service to refuse a faster claim. A time in the score makes a forged short duration
        /// worth points, and this is the coarse half of the check (the ceiling at the claimed time is the fine one). 0.15:
        /// the fastest a real clear has come to, one player's gap between two clears after v0.6.0, held a clear and its
        /// result page inside 0.57 par, so a par of 20 s floors a clear at 3 s.
        /// </summary>
        private const double MinSecondsShareOfPar = 0.15;

        /// <summary>
        /// The most stars a level the campaign's unlock gates may climb by (#1045), checked as GATE TOO STEEP: an entry may
        /// ask for at most this many stars for every entry before it (the second asks for one whatever this says). Under
        /// rules v2 four stars are hard by design, and the #1045 model rates a newcomer's clear 2.2 stars on average (two
        /// stars on 77 % of the levels): the old ramp of two a level left a newcomer 0.2 a level to spare, so a short run of
        /// one-star clears walled the campaign. One and a half leaves 0.7. LevelGen's <c>MinStarsAt</c> writes the ramp.
        /// </summary>
        private const double GateStarsPerLevel = 1.5;

        private static int Main(string[] args)
        {
            string directory = null;
            string ceilingsPath = null;
            double ceilingScale = 1.0;
            string statsPath = null;

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
                else if (args[i] == "--ceiling-scale")
                {
                    //The lever that proves the ceiling gate fires (BestPractices.md §10): every ceiling is
                    //multiplied by this before the check and by nothing else — the table, when one is written,
                    //carries the real figures. At 0.25 it must refuse every level; a run that does not has lost
                    //the gate.
                    if (i + 1 >= args.Length
                        || !double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out ceilingScale)
                        || ceilingScale <= 0)
                    {
                        Console.WriteLine("--ceiling-scale needs a positive number.");
                        return 1;
                    }

                    i++;
                }
                else if (args[i] == "--stats")
                {
                    //A path when one follows, else the player's own file (#709)
                    statsPath = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                        ? args[++i]
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BS3D", PlayStats.DefaultFileName);
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

            if (statsPath != null) return ReportStats(levels, statsPath);

            if (levels.Count == 0)
            {
                Console.WriteLine("No levels with a shot budget were found — nothing to rate.");
                return 1;
            }

            Console.WriteLine();
            //Invariant, like every row under it: this line printed "1,80 / 4,00" on a Czech machine
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Rules v{0}   Thresholds: {1:F2} / {2:F2} / {3:F2}   multiplier +{4} to x{5}   unused shot = {6} average shot(s)"
                + "   time bonus {7} floors, gone at {8} par",
                ScoreKeeper.RulesVersion,
                StarRating.TWO_STAR_FLOOR_MULTIPLE, StarRating.THREE_STAR_FLOOR_MULTIPLE, StarRating.FOUR_STAR_FLOOR_MULTIPLE,
                ScoreKeeper.MultiplierStep, ScoreKeeper.MaxMultiplier, ScoreKeeper.UnusedShotWorthInShots,
                ScoreKeeper.TimeBonusWorthInFloors, ScoreKeeper.TimeBonusZeroAtPars));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Clean clears in 4/8/16 shots at {0} par; the best of them again at {1} par (slow); budget-1 clean and sloppy at {2} s + {3} s a shot",
                FastPars, SlowPars, RunBaseSeconds, RunSecondsAShot));

            if (ceilingScale != 1.0)
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "Ceilings scaled by {0:F2} for the check ONLY — a test of the gate, not a rule; a table written now is unscaled",
                    ceilingScale));
            Console.WriteLine();
            Console.WriteLine("level        balls shots  par |   4 shots      8         16      slow     | budget-1    sloppy  | ceiling best/ceil min");

            bool ok = true;

            foreach (Playable level in levels) ok &= Report(level, ceilingScale);

            ok &= CheckGates();

            Console.WriteLine();
            Console.WriteLine(ok
                ? "All levels rate the right way round, every run is under its ceiling, and no unlock gate is too steep."
                : "At least one level rates WRONGLY, reaches its ceiling or is gated too steeply — see above.");

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
        private static bool Report(Playable level, double ceilingScale)
        {
            double[] fast = new double[ShotCounts.Length];
            int[] fastStars = new int[ShotCounts.Length];
            double[] slow = new double[ShotCounts.Length];
            int slowStars = 0, slowBest = 0;

            //Every run's score beside the most its own time allows, for the ceiling at the claimed time (#1045)
            List<(int Score, float Seconds)> runs = new();

            float fastSeconds = level.Par.HasValue ? FastPars * level.Par.Value : 0f;
            float slowSeconds = level.Par.HasValue ? SlowPars * level.Par.Value : 0f;

            for (int i = 0; i < ShotCounts.Length; i++)
            {
                //A clear cannot take fewer shots than it has balls to place, and a level with a small budget
                //cannot be simulated at more shots than it grants.
                int shots = Math.Max(1, Math.Min(ShotCounts[i], level.Shots - 1));

                int score = Clean(level, shots, fastSeconds);
                fast[i] = Multiple(score, level.Balls);
                fastStars[i] = StarRating.Rate(score, level.Balls);
                runs.Add((score, fastSeconds));

                //The same clear, slowly (#1045): the time bonus is all that differs
                int slowScore = Clean(level, shots, slowSeconds);
                slow[i] = Multiple(slowScore, level.Balls);
                slowStars = Math.Max(slowStars, StarRating.Rate(slowScore, level.Balls));
                slowBest = Math.Max(slowBest, slowScore);
                runs.Add((slowScore, slowSeconds));
            }

            int patientShots = level.Shots - 1;
            float patientSeconds = RunBaseSeconds + RunSecondsAShot * patientShots;
            int patientScore = Clean(level, patientShots, patientSeconds);
            double patient = Multiple(patientScore, level.Balls);
            int patientStars = StarRating.Rate(patientScore, level.Balls);
            runs.Add((patientScore, patientSeconds));

            float sloppySeconds = RunBaseSeconds + RunSecondsAShot * level.Shots;
            int sloppyScore = Sloppy(level, sloppySeconds);
            double sloppy = Multiple(sloppyScore, level.Balls);
            int sloppyStars = StarRating.Rate(sloppyScore, level.Balls);
            runs.Add((sloppyScore, sloppySeconds));

            //The best any of the runs above scored, against the ceiling the online boards refuse a score over
            int bestScore = 0;
            foreach ((int score, float _) in runs) bestScore = Math.Max(bestScore, score);

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-12} {1,5} {2,5} {3,4} | {4,6:F2}({5}) {6,6:F2}({7}) {8,6:F2}({9}) {10,6:F2}({11}) | {12,6:F2}({13}) {14,5:F2}({15}) | {16,7} {17,6:F2} {18,4}",
                level.Name, level.Balls, level.Shots, level.Par?.ToString(CultureInfo.InvariantCulture) ?? "-",
                fast[0], fastStars[0], fast[1], fastStars[1], fast[2], fastStars[2],
                Multiple(slowBest, level.Balls), slowStars,
                patient, patientStars, sloppy, sloppyStars,
                level.Ceiling, bestScore / (double)level.Ceiling, level.MinShots));

            bool ok = true;

            //THE CEILING (#549): the service refuses a score over it, so a score this simulator reaches with the
            //real rules must be under it — or the bound has a term missing and real players would be refused. And since
            //#1045 under the ceiling at each run's own time, which is what the service checks a clear against: a ceiling
            //that held only for the whole time bonus would let the service refuse a fast clear it should take.
            foreach ((int score, float seconds) in runs)
            {
                int ceiling = ScoreKeeper.ScoreCeilingAt(level.Balls, level.Shots, level.Par, seconds);
                if (score < ceiling * ceilingScale) continue;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    OVER CEILING: a simulated clear of {0:0.#} s scores {1}, at or over the ceiling of {2} the online boards would refuse it at",
                    seconds, score, ceiling * ceilingScale));
                ok = false;
                break;
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

            //AND IN TIME (#1045, the owner's ruling of 2026-11-02): the faster of two clears in the same shots scores more.
            //A level with no par has no time bonus, and fails here: its four stars would be out of reach of any clear
            for (int i = 0; i < ShotCounts.Length; i++)
                if (fast[i] <= slow[i])
                {
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "    TIME INVERTED: a clean clear at {0} par rates {1:F2}, no better than the same clear at {2} par ({3:F2}){4}",
                        FastPars, fast[i], SlowPars, slow[i], level.Par.HasValue ? "" : " - the level has no par"));
                    ok = false;
                    break;
                }

            //The top of the scale has to be reachable by the play the levels are designed for...
            int best = 0;
            foreach (int stars in fastStars) best = Math.Max(best, stars);

            if (best < StarRating.MAX)
            {
                Console.WriteLine($"    UNREACHABLE: the best clean clear at {FastPars} par earns {best} of {StarRating.MAX} stars");
                ok = false;
            }

            //...and it has to mean something, so the worst realistic clear must not reach it...
            if (sloppyStars >= StarRating.MAX)
            {
                Console.WriteLine($"    UNEARNED: a clear that misses one shot in {SloppyMissEvery} " +
                    $"still earns {sloppyStars} stars");
                ok = false;
            }

            //...nor a slow one (#1045): four stars need a fast clear, so a clean clear at twice par earns three at most
            if (slowStars >= StarRating.MAX)
            {
                Console.WriteLine($"    SLOW FOUR STARS: a clean clear at {SlowPars} par still earns {slowStars} stars");
                ok = false;
            }

            //THE STAR FLOORS (#1024): the score service rates a clear by them, so each must sit exactly where the rating
            //turns and the three must rise, or the service refuses real clears on a boundary
            int[] floors = StarFloors(level.Balls);
            for (int stars = 2; stars <= StarRating.MAX; stars++)
            {
                int floor = floors[stars - 2];
                if (StarRating.Rate(floor - 1, level.Balls) < stars && StarRating.Rate(floor, level.Balls) >= stars
                    && (stars == 2 || floor > floors[stars - 3])) continue;

                Console.WriteLine($"    STAR FLOORS: {stars} stars from {floor} is not where the rating turns");
                ok = false;
            }

            return ok;
        }

        /// <summary>A clear in exactly <paramref name="shots"/> landing shots, never missing, after <paramref name="seconds"/> of play.</summary>
        private static int Clean(Playable level, int shots, float seconds)
        {
            ScoreKeeper keeper = new(level.Shots, null, level.Balls, level.Par);

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
            keeper.AwardTimeBonus(seconds);
            return keeper.Score;
        }

        /// <summary>
        /// The bottom of the band: the whole budget spent, missing one shot in <see cref="SloppyMissEvery"/>
        /// so the streak keeps resetting, after <paramref name="seconds"/> of play. The thresholds have to be set against
        /// this as much as against perfect play — a top rating the worst realistic clear also earns is not rating anything.
        /// </summary>
        private static int Sloppy(Playable level, float seconds)
        {
            ScoreKeeper keeper = new(level.Shots, null, level.Balls, level.Par);

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
            keeper.AwardTimeBonus(seconds);
            return keeper.Score;
        }

        /// <summary>
        /// THE UNLOCK GATES (#1045): no entry of the set may ask for more than <see cref="GateStarsPerLevel"/> stars for every
        /// entry before it — see that constant for why — beyond the one star the second entry asks for whatever the ramp says.
        /// Every entry, budgeted or not: a gate locks a sandbox as surely as a level.
        /// </summary>
        private static bool CheckGates()
        {
            bool ok = true;

            foreach ((int position, string name, int? minStars) in _gates)
            {
                if (minStars is not int gate) continue;

                double allowed = Math.Max(1, GateStarsPerLevel * position);
                if (gate <= allowed) continue;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    GATE TOO STEEP: {0} (entry {1}) unlocks at {2} stars, over {3} a level for the {4} before it ({5:0.#})",
                    name, position + 1, gate, GateStarsPerLevel, position, allowed));
                ok = false;
            }

            return ok;
        }

        /// <summary>Every entry of the set in play order, with its unlock gate, for <see cref="CheckGates"/>.</summary>
        private static readonly List<(int Position, string Name, int? MinStars)> _gates = new();

        /// <summary>
        /// The score as a multiple of the level's floor — the same yardstick <see cref="StarRating"/> rates on,
        /// restated here so a row can be read against the thresholds printed above it.
        /// </summary>
        private static double Multiple(int score, int balls) =>
            score / (double)(ScoreKeeper.MatchedBallPoints * balls);

        /// <summary>
        /// A save's play counts (#709), one row per level in play order and then the boards the set does not name. Rates are
        /// shares of their own counts, printed beside them, so two attempts are not read as a trend: a loss rate is losses
        /// over the attempts that reached a result page, a retry rate retries over starts.
        /// </summary>
        private static int ReportStats(List<Playable> levels, string path)
        {
            PlayStats stats = PlayStats.TryRead(path);
            if (stats == null)
            {
                Console.WriteLine($"No play counts at {Path.GetFullPath(path)} (missing, or not a {PlayStats.FormatMarker} file).");
                return 1;
            }

            Console.WriteLine($"Play counts of {Path.GetFullPath(path)}: {stats.Levels.Count} board(s), rules v{ScoreKeeper.RulesVersion} in this build");
            Console.WriteLine();
            Console.WriteLine("  # level            starts  retries      clears  lost  balls  line   loss rate  quits  skips  tries/1st  shots/att  s/att");

            HashSet<LevelPlayCounts> listed = new();
            int place = 0;
            foreach (Playable level in levels)
            {
                place++;
                LevelPlayCounts row = stats.Find(level.Identity.File, level.Identity.Hash, ScoreKeeper.RulesVersion);
                if (row == null) continue;

                listed.Add(row);
                Console.WriteLine(StatsRow(place.ToString(CultureInfo.InvariantCulture), level.Name, row));
            }

            List<LevelPlayCounts> others = stats.Levels.FindAll(row => !listed.Contains(row));
            if (others.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Boards the set does not name as shipped (an older version of a level, another rules version, a levelfile= run):");
                foreach (LevelPlayCounts row in others)
                    Console.WriteLine(StatsRow("-", $"{row.File}#{row.Hash[..Math.Min(6, row.Hash.Length)]} r{row.Rules}", row));
            }

            return 0;
        }

        private static string StatsRow(string place, string name, LevelPlayCounts row)
        {
            int lost = row.LostOutOfBalls + row.LostLine;
            int ended = row.Clears + lost;
            int attempts = ended + row.Abandons;

            return string.Format(CultureInfo.InvariantCulture,
                "{0,3} {1,-16} {2,6}  {3,7}  {4,10}  {5,4}  {6,5}  {7,4}  {8,10}  {9,5}  {10,5}  {11,9}  {12,9}  {13,5}",
                place, name.Length > 16 ? name[..16] : name, row.Starts, Rate(row.Retries, row.Starts), row.Clears, lost,
                row.LostOutOfBalls, row.LostLine, Rate(lost, ended), row.Abandons, row.Skips,
                row.FirstClears > 0 ? (row.StartsToFirstClear / (double)row.FirstClears).ToString("0.0", CultureInfo.InvariantCulture) : "-",
                attempts > 0 ? (row.Shots / (double)attempts).ToString("0.0", CultureInfo.InvariantCulture) : "-",
                attempts > 0 ? (row.Seconds / attempts).ToString("0", CultureInfo.InvariantCulture) : "-");
        }

        /// <summary>A count with its share of a whole: "3 (60%)", or the count alone when there is no whole.</summary>
        private static string Rate(int count, int of) =>
            of > 0 ? string.Format(CultureInfo.InvariantCulture, "{0} ({1:0}%)", count, 100.0 * count / of) : count.ToString(CultureInfo.InvariantCulture);

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

            _gates.Clear();
            for (int i = 0; i < set.Levels.Count; i++)
                _gates.Add((i, set.DisplayName(i), set.Levels[i].MinStars));

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
                    LevelIdentity.Of(entry, File.ReadAllBytes(path)), ScoreKeeper.ScoreCeiling(balls, entry.Shots.Value, entry.Par),
                    MinimumShots(map), entry.Block, entry.Par));
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
        /// <b>A shot floor and, since #1045, a time floor and the time bonus.</b> #549 asked for a <c>minSeconds</c> a human
        /// cannot beat; the shot count half is <c>minShots</c> (<see cref="MinimumShots"/>), and the seconds half waited for
        /// a rule that made a short time worth anything, which the time bonus is. So a row on a level with a par carries
        /// <c>par</c>, <c>minSeconds</c> (<see cref="MinSecondsShareOfPar"/> of it: the service refuses a clear claiming less)
        /// and <c>timeBonus</c>, the most the time bonus can pay (<see cref="ScoreKeeper.TimeBonusMax"/>, included in
        /// <c>ceiling</c>). The service checks a clear against the ceiling <b>at its claimed time</b>: <c>ceiling −
        /// timeBonus + timeBonus × max(0, 1 − seconds / (timeBonusZeroAtPars × par))</c>, which is
        /// <see cref="ScoreKeeper.ScoreCeilingAt"/>; the game counts whole seconds begun, so the exact claimed time never
        /// refuses a real clear. <c>timeBonusZeroAtPars</c> is in the header. A row without a par has none of the three.
        /// </para>
        /// <para>
        /// <b>And the star floors (#1024)</b>: the least score for 2, 3 and 4 stars, <see cref="StarRating.LeastScoreFor"/>
        /// over the same ball count the game rates a clear by (its <c>InitialBallCount</c> is the map's
        /// <c>GetBallsCount()</c>, as here). The service refuses a clear whose stars its score does not earn
        /// (AntoninPrazsky/BS3D-API#16), by the floors of any table that names the board: a retune of
        /// <see cref="StarRating"/> moves them under an unchanged board key, and a release already out keeps rating by the
        /// old ones, so a star retune needs no <see cref="ScoreKeeper.RulesVersion"/> bump. They come last in a row, so the
        /// rest of the table reads as it always did.
        /// </para>
        /// <para>
        /// <b>And the chapter, for the service's admin page (#695).</b> The rows come in the set's play order, and each
        /// names its level's <c>block</c> when the set names one, so the page can list the boards chapter by chapter
        /// rather than by name. Nothing on either side checks or ranks by it, and it is not in the identity: a
        /// re-chaptered level keeps its board. A set without blocks writes no such field, so the table it writes is the
        /// one it always wrote.
        /// </para>
        /// </summary>
        private static void WriteCeilings(List<Playable> levels, string path)
        {
            List<CeilingRow> rows = new();
            foreach (Playable level in levels)
            {
                ScoreKeeper keeper = new(level.Shots, null, level.Balls, level.Par);
                rows.Add(new CeilingRow(level.Identity.File, level.Name, level.Identity.Hash, ScoreKeeper.RulesVersion,
                    level.Shots, level.Ceiling, level.MinShots, level.Block, StarFloors(level.Balls),
                    level.Par, level.Par.HasValue ? Math.Round(MinSecondsShareOfPar * level.Par.Value, 2) : null,
                    level.Par.HasValue ? keeper.TimeBonusMax : null));
            }

            CeilingTable table = new(CeilingTable.FormatMarker, CeilingTable.CurrentVersion, ScoreKeeper.RulesVersion,
                LevelIdentity.HashLength, ScoreKeeper.TimeBonusZeroAtPars, rows);

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

        /// <summary>The least score for 2, 3 and 4 stars on a level of <paramref name="balls"/> balls (#1024).</summary>
        private static int[] StarFloors(int balls)
        {
            int[] floors = new int[StarRating.MAX - 1];
            for (int stars = 2; stars <= StarRating.MAX; stars++) floors[stars - 2] = StarRating.LeastScoreFor(stars, balls);
            return floors;
        }

        private readonly record struct Playable(string Name, int Balls, int Shots, LevelIdentity Identity, int Ceiling,
            int MinShots, string Block, int? Par);

        /// <summary>
        /// The ceiling table's file (#549), marked like every other file this project writes (<c>bs3d-levels</c>,
        /// <c>bs3d-level</c>) so a reader can refuse something that is not one. Still version 1 with the time fields
        /// (#1045): a field a reader does not know is one it ignores, and the <c>ceiling</c> it does know is still a bound
        /// above every clear (it holds the whole time bonus), only a looser one than the ceiling at the claimed time.
        /// </summary>
        private sealed record CeilingTable(
            [property: JsonPropertyName("format")] string Format,
            [property: JsonPropertyName("version")] int Version,
            [property: JsonPropertyName("rulesVersion")] int RulesVersion,
            [property: JsonPropertyName("hashLength")] int HashLength,
            [property: JsonPropertyName("timeBonusZeroAtPars")] int TimeBonusZeroAtPars,
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
            [property: JsonPropertyName("minShots")] int MinShots,
            [property: JsonPropertyName("block"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string Block,
            [property: JsonPropertyName("starFloors")] int[] StarFloors,
            [property: JsonPropertyName("par"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Par,
            [property: JsonPropertyName("minSeconds"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? MinSeconds,
            [property: JsonPropertyName("timeBonus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TimeBonus);
    }
}

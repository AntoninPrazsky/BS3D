using Prazsky.BS3D;
using Prazsky.BS3D.Levels;
using System;
using System.Collections.Generic;
using System.IO;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>How often the drop cinematic fires</b> (#719, the second pass on #615's dial), measured: every level of the shipped
    /// set played to a clear by a modelled greedy player and a modelled casual one (<see cref="ClearProbe.PlayDrops"/>, on the
    /// lattice and through the real match and orphan rule), six deals each, and every candidate setting of the trigger's dials
    /// (<see cref="DropTrigger"/>) run over the <b>same plays</b> — the player does not change with the camera, so one set of
    /// releases answers every setting and the comparison between them is exact.
    /// <para>
    /// The figure is cinematics <b>before the clearing shot</b>, as #615 stated it, because the clearing shot always has one
    /// (#424) and no dial moves it. A report and not a gate: whether "about one a level" is the right amount is the owner's
    /// ear and eye, and what this answers is how far a dial moves it. The first row is the game's own dials, read from
    /// <see cref="DropTrigger"/> and never typed here; the second is #615's old rule, 12 balls and ×1.25, which the report
    /// prints so that a harness that reads it back at 2.0–2.1 a level is seen to be the one that measured it.
    /// </para>
    /// <para>
    /// ⚠ <b>The lattice plays neither the physics, the ceiling's descent, a blast nor a zap</b>, and a spent shot (a colour
    /// with nowhere to complete a group) is not a setup shot, so the counts differ somewhat from the game's: they say how the
    /// settings compare, and the game's own <c>[cinematic]</c> lines say what one level did.
    /// </para>
    /// </summary>
    internal static class DropProbe
    {
        private const int DEALS = 6;
        private const int SHOT_CAP = 1500;

        /// <summary>The settings run side by side: the game's own, the rule #615 replaced, then the candidates for #719.</summary>
        private static readonly (string Name, float Share, float Factor)[] Rules =
        {
            ("game's own", DropTrigger.MIN_SHARE_OF_LEVEL, DropTrigger.MustBeatBestBy),
            ("#615 before", 0f, 1.25f),
            ("share 6 %", 0.06f, DropTrigger.MustBeatBestBy),
            ("share 7 %", 0.07f, DropTrigger.MustBeatBestBy),
            ("share 8 %", 0.08f, DropTrigger.MustBeatBestBy),
            ("share 9 %", 0.09f, DropTrigger.MustBeatBestBy),
            ("share 10 %", 0.10f, DropTrigger.MustBeatBestBy),
            ("x2.5", DropTrigger.MIN_SHARE_OF_LEVEL, 2.5f),
            ("x3", DropTrigger.MIN_SHARE_OF_LEVEL, 3f),
            ("8 % and x2.5", 0.08f, 2.5f),
            ("10 % and x2.5", 0.10f, 2.5f),
            ("8 % and x3", 0.08f, 3f),
        };

        private static readonly ClearProbe.DropPlayer[] Players = { ClearProbe.DropPlayer.Greedy, ClearProbe.DropPlayer.Casual };

        public static bool Run(string levelsDirectory, bool perLevel)
        {
            LevelSet set = LevelSet.Load(Path.Combine(levelsDirectory, "Levels.json"));

            Console.WriteLine("=== the drop cinematic (#719): cinematics a level before the clearing shot, by the trigger's dials ===");
            Console.WriteLine($"    {set.Count} levels, each played to a clear by a greedy and a casual player, {DEALS} deals each, on the lattice;"
                + " the clearing shot always has one and is not counted");

            //[rule][player]: one mean per level that was cleared, and over every cleared play how many had none
            List<double>[,] means = new List<double>[Rules.Length, Players.Length];
            int[,] noneAtAll = new int[Rules.Length, Players.Length];
            int[] plays = new int[Players.Length];
            int[] uncleared = new int[Players.Length];
            int[] levelsWithoutAClear = new int[Players.Length];
            double[] shotsToClear = new double[Players.Length];

            for (int r = 0; r < Rules.Length; r++)
                for (int p = 0; p < Players.Length; p++)
                    means[r, p] = new List<double>();

            if (perLevel)
                Console.WriteLine($"    {"#",3}  {"level",-14} {"balls",5}  {"greedy",6} {"casual",6}   (the game's own dials; shots to clear: greedy / casual)");

            for (int index = 0; index < set.Count; index++)
            {
                LevelSetEntry entry = set.Levels[index];
                Level level = Level.Load(Path.Combine(levelsDirectory, entry.File));

                double[] shown = new double[Players.Length];
                double[] shots = new double[Players.Length];
                int balls = 0;

                for (int p = 0; p < Players.Length; p++)
                {
                    double[] fires = new double[Rules.Length];
                    int cleared = 0;
                    double shotSum = 0;

                    for (int deal = 0; deal < DEALS; deal++)
                    {
                        ClearProbe.DropRun run = ClearProbe.PlayDrops(level.Map, Players[p], 7919 * index + deal, SHOT_CAP);
                        balls = run.InitialBalls;

                        if (!run.Cleared || run.Releases.Length == 0)
                        {
                            uncleared[p]++;
                            continue;
                        }

                        cleared++;
                        plays[p]++;
                        shotSum += run.Shots;

                        for (int r = 0; r < Rules.Length; r++)
                        {
                            int count = Fires(run, Rules[r].Share, Rules[r].Factor);
                            fires[r] += count;
                            if (count == 0) noneAtAll[r, p]++;
                        }
                    }

                    if (cleared == 0)
                    {
                        levelsWithoutAClear[p]++;
                        continue;
                    }

                    for (int r = 0; r < Rules.Length; r++) means[r, p].Add(fires[r] / cleared);

                    shown[p] = fires[0] / cleared;
                    shots[p] = shotSum / cleared;
                    shotsToClear[p] += shotSum;
                }

                if (perLevel)
                    Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "    {0,3}  {1,-14} {2,5}  {3,6:F2} {4,6:F2}   {5,5:F0} / {6:F0}",
                        index + 1, Trim(set.DisplayName(index), 14), balls, shown[0], shown[1], shots[0], shots[1]));
            }

            Console.WriteLine();
            Console.WriteLine($"    {"dials",-16} {"floor",-7} {"over the best",-13} | {"greedy",-30} | {"casual",-30}");
            Console.WriteLine($"    {"",-16} {"",-7} {"",-13} | {"a level",8} {"most",6} {"none",8}   {"vs own",6} | {"a level",8} {"most",6} {"none",8}   {"vs own",6}");

            for (int r = 0; r < Rules.Length; r++)
            {
                string line = $"    {Rules[r].Name,-16} {(Rules[r].Share * 100).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " %",-7} {"x" + Rules[r].Factor.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),-13} |";

                for (int p = 0; p < Players.Length; p++)
                {
                    double mean = Mean(means[r, p]);
                    double own = Mean(means[0, p]);
                    double none = plays[p] == 0 ? 0 : 100.0 * noneAtAll[r, p] / plays[p];
                    string versus = r == 0 ? "" : (100.0 * (mean - own) / own).ToString("+0;-0", System.Globalization.CultureInfo.InvariantCulture) + " %";

                    line += string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        " {0,8:F2} {1,6:F1} {2,7:F1}%   {3,6} |", mean, Max(means[r, p]), none, versus);
                }

                Console.WriteLine(line);
            }

            Console.WriteLine();
            for (int p = 0; p < Players.Length; p++)
                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "    {0}: {1} levels cleared in the model, {2} never, {3} of the plays gave up; {4:F0} shots to clear on average."
                    + " 'none' = plays with no cinematic before the clearing shot.",
                    Players[p], means[0, p].Count, levelsWithoutAClear[p], uncleared[p], plays[p] == 0 ? 0 : shotsToClear[p] / plays[p]));

            return true;
        }

        /// <summary>
        /// Cinematics before the clearing shot in one play under one setting. The record is raised by every release, the ones
        /// refused included, exactly as <c>GameplayScreen.TryBeginDropCinematic</c> does.
        /// </summary>
        private static int Fires(ClearProbe.DropRun run, float share, float factor)
        {
            int best = 0, fires = 0;

            for (int i = 0; i < run.Releases.Length; i++)
            {
                int total = run.Releases[i];
                bool clears = run.Cleared && i == run.Releases.Length - 1;

                bool worth = DropTrigger.IsWorthWatching(total, clears, run.InitialBalls, best, share, factor);
                if (total > best) best = total;

                if (worth && !clears) fires++;
            }

            return fires;
        }

        private static double Mean(List<double> values)
        {
            if (values.Count == 0) return 0;

            double sum = 0;
            foreach (double value in values) sum += value;
            return sum / values.Count;
        }

        private static double Max(List<double> values)
        {
            double max = 0;
            foreach (double value in values) max = Math.Max(max, value);
            return max;
        }

        private static string Trim(string text, int width) => text.Length <= width ? text : text[..width];
    }
}

using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>What one cut lets go</b> (#692), on the starting cluster of every shipped level that grants a Cut. A read-only
    /// report and not a gate: the owner's playtest was that a cutter "seems to do nothing, or at most disconnects a
    /// single ball", and <c>docs/game-session.md</c> had predicted exactly that and left the measurement undone.
    /// <para>
    /// <b>The rule is the game's own, not a model of it.</b> A cutter destroys the one ball it strikes and lets go of
    /// whatever hung by that ball alone (<c>BallsConstraintsBuilder.CutBall</c> → <c>ResolveDisconnected</c>); here
    /// that is the struck cell taken out of the <see cref="BallsMap"/> and
    /// <see cref="BallsMap.GetCellsDisconnectedFromCeiling"/> asked what no longer reaches the top level, which is the
    /// same touching-neighbour walk the release runs. A bomb is left out: a cutter that strikes one detonates it, and
    /// that is a blast's measurement, not a cut's.
    /// </para>
    /// <para>
    /// <b>Two answers per level, because a cutter cannot reach every ball.</b> It flies in a straight line and strikes
    /// the first ball it touches, so a ball buried in the cluster is out of reach until what covers it is gone. The
    /// second column counts only <i>exposed</i> balls: one with an empty cell beside it inside the field, or standing
    /// on the field's side walls. That is an approximation of "a straight shot can strike it" (<see cref="ArrivalProbe"/>
    /// asks the exact question of landings, not of strikes), and it errs towards counting a ball as reachable.
    /// </para>
    /// </summary>
    internal static class CutProbe
    {
        /// <summary>A cut that frees at least this many is worth more than a plain shot: a match takes three.</summary>
        private const int PAYS = 3;

        /// <summary>And a cut that frees this many is a real collapse, the tool doing what its name promises.</summary>
        private const int COLLAPSE = 10;

        /// <summary>The report over the shipped set: every entry from the block that first grants a Cut on.</summary>
        public static bool Run(string levelsDirectory)
        {
            string setPath = Path.Combine(levelsDirectory, "Levels.json");
            LevelSet set = LevelSet.Load(setPath);

            Console.WriteLine("=== the cut (#692): balls one cut lets go (struck + orphaned), on each level's starting cluster ===");
            Console.WriteLine($"    from block {LevelSet.CUT_FROM_BLOCK} on; 'exposed' = an empty cell beside it in the field, or on a side wall");
            Console.WriteLine("    'ring' = the alternative #692 names: the struck ball AND every ball touching it go, then what hung by them");
            Console.WriteLine($"    {"#",3}  {"level",-14} {"block",-14} {"balls",5}  {"best",4}  {"best exp",8}  {">=" + PAYS,5}  {">=" + PAYS + " exp",7}  {">=" + COLLAPSE,5}  {"ring exp",8}  {"ring med",8}");

            int levelsMeasured = 0, payNowhere = 0, payNowhereExposed = 0, collapseSomewhere = 0;
            List<int> bestExposed = new(), ringBest = new(), ringMedians = new();

            for (int index = 0; index < set.Count; index++)
            {
                if (set.CutChargesAt(index) <= 0) continue;

                LevelSetEntry entry = set.Levels[index];
                Level level = Level.Load(Path.Combine(levelsDirectory, entry.File));
                BallsMap map = new(level.Map);

                Measure(map, out int balls, out int best, out int bestOfExposed, out int pays, out int paysExposed, out int collapses);
                MeasureRing(map, out int ringBestExposed, out int ringMedianExposed);
                ringBest.Add(ringBestExposed);
                ringMedians.Add(ringMedianExposed);

                levelsMeasured++;
                if (best < PAYS) payNowhere++;
                if (bestOfExposed < PAYS) payNowhereExposed++;
                if (best >= COLLAPSE) collapseSomewhere++;
                bestExposed.Add(bestOfExposed);

                Console.WriteLine($"    {index + 1,3}  {Trim(set.DisplayName(index), 14),-14} {Trim(set.BlockName(index), 14),-14} {balls,5}  {best,4}  {bestOfExposed,8}"
                    + $"  {pays,5}  {paysExposed,7}  {collapses,5}  {ringBestExposed,8}  {ringMedianExposed,8}");
            }

            bestExposed.Sort();
            int median = bestExposed.Count > 0 ? bestExposed[bestExposed.Count / 2] : 0;
            ringBest.Sort();
            ringMedians.Sort();

            Console.WriteLine($"  {levelsMeasured} level(s) grant a Cut. On {payNowhere} no cut anywhere frees {PAYS} or more;"
                + $" on {payNowhereExposed} no EXPOSED cut does. {collapseSomewhere} have a cut freeing {COLLAPSE} or more."
                + $" The median level's best exposed cut frees {median}.");
            Console.WriteLine($"  A ring cut instead: the median level's best exposed ring frees {Median(ringBest)}, and its typical"
                + $" (median) exposed ring frees {Median(ringMedians)}; the least any level's best ring frees is {(ringBest.Count > 0 ? ringBest[0] : 0)}.");

            RunStorey(set, levelsDirectory);

            return true;
        }

        //The storey cut's guard, measured (#692): the top storeys a cut may not strike, from none to three
        private static readonly int[] TOP_EXCLUDED = { 0, 1, 2, 3 };

        /// <summary>
        /// <b>The owner's rule for the Cut (#692, 2026-10-03): the whole storey at the struck ball</b> — every ball of the
        /// struck ball's level connected to it through that level (a flood over same-level neighbours, so a separate clump
        /// on the same level stays), then whatever no longer reaches the ceiling. The storey's balls count as destroyed (his
        /// answer of 2026-10-04); here only how many go matters. Measured over the exposed balls of every level that grants a
        /// Cut, because the issue's hazard is that a storey cut can pay too much: one strike under the anchor rows frees
        /// nearly everything under it. So per level the best strike, the median one, and how many strikes CLEAR the level
        /// outright (no removable ball left) - and the same with the top one, two or three storeys of the cluster struck
        /// off the list, the guard the issue names first.
        /// </summary>
        private static void RunStorey(LevelSet set, string levelsDirectory)
        {
            Console.WriteLine();
            Console.WriteLine("=== the storey cut (#692): the struck ball's whole connected storey goes, then what hung by it; exposed strikes only ===");
            Console.Write($"    {"#",3}  {"level",-14} {"balls",5}");
            foreach (int n in TOP_EXCLUDED) Console.Write($"  {"top" + n + " best",9} {"med",4} {"clears",6}");
            Console.WriteLine();

            int[] levelsClearable = new int[TOP_EXCLUDED.Length];
            List<int>[] bests = new List<int>[TOP_EXCLUDED.Length];
            List<int>[] medians = new List<int>[TOP_EXCLUDED.Length];
            for (int i = 0; i < TOP_EXCLUDED.Length; i++) { bests[i] = new List<int>(); medians[i] = new List<int>(); }
            int levels = 0;

            for (int index = 0; index < set.Count; index++)
            {
                if (set.CutChargesAt(index) <= 0) continue;

                Level level = Level.Load(Path.Combine(levelsDirectory, set.Levels[index].File));
                BallsMap map = new(level.Map);
                levels++;

                Console.Write($"    {index + 1,3}  {Trim(set.DisplayName(index), 14),-14} {map.GetBallsCount(),5}");
                for (int i = 0; i < TOP_EXCLUDED.Length; i++)
                {
                    MeasureStorey(map, TOP_EXCLUDED[i], out int best, out int median, out int clears);
                    bests[i].Add(best);
                    medians[i].Add(median);
                    if (clears > 0) levelsClearable[i]++;
                    Console.Write($"  {best,9} {median,4} {clears,6}");
                }
                Console.WriteLine();
            }

            Console.WriteLine();
            for (int i = 0; i < TOP_EXCLUDED.Length; i++)
            {
                bests[i].Sort();
                medians[i].Sort();
                Console.WriteLine($"  top {TOP_EXCLUDED[i]} storey(s) uncuttable: one strike clears {levelsClearable[i]} of {levels} level(s) outright;"
                    + $" the median level's best strike frees {Median(bests[i])}, its typical (median) strike {Median(medians[i])}");
            }
        }

        /// <summary>
        /// Every exposed ball of the cluster struck in turn under the storey rule, the top <paramref name="topExcluded"/>
        /// storeys of the cluster left alone: the most one strike frees, the median strike, and how many strikes leave no
        /// removable ball. The map is put back after each.
        /// </summary>
        private static void MeasureStorey(BallsMap map, int topExcluded, out int best, out int median, out int clears)
        {
            StaticBall[,,] cells = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();
            List<int> freedAll = new();
            List<(XZLevel At, StaticBall Ball)> taken = new();
            Queue<XZLevel> walk = new();
            bool[,,] seen = new bool[size.X, size.Z, size.Level];
            clears = 0;

            //The cluster's top storey: the highest level holding a ball
            int top = -1;
            for (int l = size.Level - 1; l >= 0 && top < 0; l--)
                for (int x = 0; x < size.X && top < 0; x++)
                    for (int z = 0; z < size.Z && top < 0; z++)
                        if (cells[x, z, l] != null) top = l;

            for (int l = 0; l < size.Level; l++)
            {
                if (l > top - topExcluded) continue;

                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = cells[x, z, l];
                        if (ball == null || ball.Kind == BallKind.Bomb) continue;

                        XZLevel at = new(x, z, l);
                        if (!IsExposed(cells, size, at)) continue;

                        //The storey: a flood over same-level neighbours from the struck ball
                        taken.Clear();
                        Array.Clear(seen);
                        walk.Enqueue(at);
                        seen[x, z, l] = true;
                        while (walk.Count > 0)
                        {
                            XZLevel cell = walk.Dequeue();
                            taken.Add((cell, cells[cell.X, cell.Z, cell.Level]));
                            foreach (XZLevel n in BallsMap.GetNeighboringCells(cell, size))
                            {
                                if (n.Level != l || seen[n.X, n.Z, n.Level] || cells[n.X, n.Z, n.Level] == null) continue;
                                seen[n.X, n.Z, n.Level] = true;
                                walk.Enqueue(n);
                            }
                        }

                        foreach ((XZLevel cell, StaticBall _) in taken) cells[cell.X, cell.Z, cell.Level] = null;
                        List<XZLevel> fallen = map.GetCellsDisconnectedFromCeiling();
                        freedAll.Add(taken.Count + fallen.Count);

                        //Cleared outright: with the storey and what fell gone, nothing removable is left
                        List<(XZLevel At, StaticBall Ball)> fell = new(fallen.Count);
                        foreach (XZLevel cell in fallen) { fell.Add((cell, cells[cell.X, cell.Z, cell.Level])); cells[cell.X, cell.Z, cell.Level] = null; }
                        if (map.GetRemovableBallsCount() == 0) clears++;
                        foreach ((XZLevel cell, StaticBall b) in fell) cells[cell.X, cell.Z, cell.Level] = b;

                        foreach ((XZLevel cell, StaticBall b) in taken) cells[cell.X, cell.Z, cell.Level] = b;
                    }
            }

            freedAll.Sort();
            best = freedAll.Count > 0 ? freedAll[^1] : 0;
            median = Median(freedAll);
        }

        /// <summary>
        /// Every ball of the cluster cut in turn: <paramref name="best"/> is the most one cut lets go, of any ball and of an
        /// exposed one, and the counts are how many cells free at least <see cref="PAYS"/> (all, exposed) and at least
        /// <see cref="COLLAPSE"/>. The map is put back after each cut, so every cut is asked of the intact cluster.
        /// </summary>
        private static void Measure(BallsMap map, out int balls, out int best, out int bestExposed, out int pays,
            out int paysExposed, out int collapses)
        {
            StaticBall[,,] cells = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();

            balls = best = bestExposed = pays = paysExposed = collapses = 0;

            for (int l = 0; l < size.Level; l++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = cells[x, z, l];
                        if (ball == null) continue;

                        balls++;
                        if (ball.Kind == BallKind.Bomb) continue;

                        bool exposed = IsExposed(cells, size, new XZLevel(x, z, l));

                        map.RemoveBallAt((byte)x, (byte)z, (byte)l);
                        int freed = 1 + map.GetCellsDisconnectedFromCeiling().Count;
                        cells[x, z, l] = ball;

                        best = Math.Max(best, freed);
                        if (freed >= PAYS) pays++;
                        if (freed >= COLLAPSE) collapses++;

                        if (!exposed) continue;

                        bestExposed = Math.Max(bestExposed, freed);
                        if (freed >= PAYS) paysExposed++;
                    }
        }

        /// <summary>
        /// The alternative rule #692 names, measured so the choice is made on numbers: the struck ball and every ball
        /// touching it go (a radius of one lattice step, up to thirteen balls), then whatever hung by them alone. Over the
        /// exposed balls only, the ones a cutter can strike: the best ring on the level and the median one, which is what
        /// a cut fired without studying the cluster tends to get.
        /// </summary>
        private static void MeasureRing(BallsMap map, out int best, out int median)
        {
            StaticBall[,,] cells = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();
            List<int> freedAll = new();
            List<(XZLevel At, StaticBall Ball)> taken = new(BallsMap.MAX_NEIGHBORS + 1);

            for (int l = 0; l < size.Level; l++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = cells[x, z, l];
                        if (ball == null || ball.Kind == BallKind.Bomb) continue;

                        XZLevel at = new(x, z, l);
                        if (!IsExposed(cells, size, at)) continue;

                        taken.Clear();
                        taken.Add((at, ball));
                        foreach (XZLevel n in BallsMap.GetNeighboringCells(at, size))
                            if (cells[n.X, n.Z, n.Level] != null) taken.Add((n, cells[n.X, n.Z, n.Level]));

                        foreach ((XZLevel cell, StaticBall _) in taken) cells[cell.X, cell.Z, cell.Level] = null;
                        freedAll.Add(taken.Count + map.GetCellsDisconnectedFromCeiling().Count);
                        foreach ((XZLevel cell, StaticBall b) in taken) cells[cell.X, cell.Z, cell.Level] = b;
                    }

            freedAll.Sort();
            best = freedAll.Count > 0 ? freedAll[^1] : 0;
            median = Median(freedAll);
        }

        private static int Median(List<int> sorted) => sorted.Count > 0 ? sorted[sorted.Count / 2] : 0;

        /// <summary>
        /// Whether a straight shot could plausibly strike the ball: an empty cell beside it inside the field, or the ball on
        /// one of the field's four side walls, which open onto the space round the cluster.
        /// </summary>
        private static bool IsExposed(StaticBall[,,] cells, XZLevel size, XZLevel at)
        {
            if (at.X == 0 || at.Z == 0 || at.X == size.X - 1 || at.Z == size.Z - 1) return true;

            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(at, size))
                if (cells[neighbour.X, neighbour.Z, neighbour.Level] == null) return true;

            return false;
        }

        private static string Trim(string text, int width) =>
            text == null ? string.Empty : text.Length <= width ? text : text[..(width - 1)] + "~";
    }
}

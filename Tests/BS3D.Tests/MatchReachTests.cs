using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// #845: <see cref="MatchReach"/>, "has a shot of this colour anywhere to score", which the Swap chip's hint asks of the
    /// round in the bore and the round behind it. Its one promise is the direction it errs in: <b>"none" is certain</b>, so
    /// the chip never points at the Swap while the bore has a match to be had. Both answers are seen here on fields built
    /// for them, and the "somewhere" answer is held against the match rule's own walk over every shipped level.
    /// </summary>
    public class MatchReachTests
    {
        private const int GROUP = 3;
        private const BallType A = BallType.Type1;
        private const BallType B = BallType.Type2;

        private readonly ITestOutputHelper _output;

        public MatchReachTests(ITestOutputHelper output) => _output = output;

        private static BallsMap Field() => new(7, 7, 7);

        private static void Put(BallsMap map, int x, int z, int level, BallType type, BallKind kind = BallKind.Normal) =>
            map.PutBallAt((byte)x, (byte)z, (byte)level, type, kind);

        [Fact]
        public void An_empty_field_has_nowhere_to_score()
        {
            Assert.False(new MatchReach().HasScoringLanding(Field(), A, GROUP));
        }

        [Fact]
        public void A_pair_in_the_open_is_somewhere_to_score()
        {
            BallsMap map = Field();
            Put(map, 3, 3, 6, A);
            Put(map, 4, 3, 6, A);

            Assert.True(new MatchReach().HasScoringLanding(map, A, GROUP));

            //And only for its own colour: a B lands beside two A's and completes nothing
            Assert.False(new MatchReach().HasScoringLanding(map, B, GROUP));
        }

        [Fact]
        public void A_lone_ball_or_two_apart_is_nowhere_to_score()
        {
            BallsMap map = Field();
            Put(map, 1, 1, 6, A);
            Assert.False(new MatchReach().HasScoringLanding(map, A, GROUP));

            //Two singles with no cell touching both: a shot beside either makes a pair, never three
            Put(map, 5, 5, 6, A);
            Assert.False(new MatchReach().HasScoringLanding(map, A, GROUP));
        }

        [Fact]
        public void A_gap_between_two_singles_is_somewhere_to_score()
        {
            //A _ A on one level: the gap touches both, so a shot into it is the third
            BallsMap map = Field();
            Put(map, 2, 3, 6, A);
            Put(map, 4, 3, 6, A);

            Assert.True(new MatchReach().HasScoringLanding(map, A, GROUP));
        }

        /// <summary>
        /// A pocket no shot can fly into does not count: a pair of A's walled inside a block of B's, beside an empty cell
        /// whose every neighbour is a ball, has nowhere to score, while the B's on the block's face do.
        /// </summary>
        [Fact]
        public void A_walled_in_pocket_is_not_somewhere()
        {
            BallsMap map = Field();
            for (int level = 1; level <= 5; level++)
                for (int x = 1; x <= 5; x++)
                    for (int z = 1; z <= 5; z++)
                        Put(map, x, z, level, B);

            //The pocket at the block's heart, and two A's beside it, which touch nothing else but B's
            XZLevel pocket = new(3, 3, 3);
            map.RemoveBallAt(3, 3, 3);

            Span<XZLevel> around = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];
            int count = BallsMap.FillNeighboringCells(pocket, map.GetStaticBallsArraySize(), around);
            Put(map, around[0].X, around[0].Z, around[0].Level, A);
            Put(map, around[count - 1].X, around[count - 1].Z, around[count - 1].Level, A);

            Assert.False(new MatchReach().HasScoringLanding(map, A, GROUP));
            Assert.True(new MatchReach().HasScoringLanding(map, B, GROUP));
        }

        [Fact]
        public void A_special_a_landing_sets_off_is_somewhere_to_score_and_a_rock_is_not()
        {
            BallsMap map = Field();
            Put(map, 3, 3, 6, A);
            Put(map, 4, 3, 6, B, BallKind.Rock);
            Assert.False(new MatchReach().HasScoringLanding(map, A, GROUP));

            //A bomb goes off beside any landing, so any colour has somewhere to score beside it
            Put(map, 1, 1, 6, B, BallKind.Bomb);
            Assert.True(new MatchReach().HasScoringLanding(map, A, GROUP));

            //Glass takes the shot's colour and may complete a group through itself
            BallsMap glass = Field();
            Put(glass, 3, 3, 6, B, BallKind.Transparent);
            Assert.True(new MatchReach().HasScoringLanding(glass, A, GROUP));
        }

        [Fact]
        public void One_instance_answers_many_fields_and_the_revision_moves_on_every_write()
        {
            MatchReach reach = new();
            BallsMap map = Field();
            int revision = map.Revision;

            Put(map, 3, 3, 6, A);
            Assert.NotEqual(revision, revision = map.Revision);
            Assert.False(reach.HasScoringLanding(map, A, GROUP));

            Put(map, 4, 3, 6, A);
            Assert.NotEqual(revision, revision = map.Revision);
            Assert.True(reach.HasScoringLanding(map, A, GROUP));

            map.RemoveBallAt(4, 3, 6);
            Assert.NotEqual(revision, revision = map.Revision);
            Assert.False(reach.HasScoringLanding(map, A, GROUP));

            map.Clear();
            Assert.NotEqual(revision, map.Revision);

            //Hundreds of asks: the walks' marks wrap past a byte and must still never mistake a cell for walked
            for (int i = 0; i < 300; i++) Assert.False(reach.HasScoringLanding(map, A, GROUP));

            //And a larger field after a smaller one
            BallsMap large = new(12, 12, 12);
            Put(large, 6, 6, 11, B);
            Put(large, 7, 6, 11, B);
            Assert.True(reach.HasScoringLanding(large, B, GROUP));
        }

        /// <summary>
        /// Every shipped level, every colour on it: <see cref="MatchReach"/> answers exactly what the game's own rule says,
        /// asked the slow way. The oracle floods the open space here from the walls and the floor, and for every open cell
        /// beside a ball either finds a special a landing sets off, or places the ball for real and asks the match rule's own
        /// walk (<see cref="BallsMap.GetConnectedSameTypeCells"/>) how big its group is. Both answers are counted and printed:
        /// "nowhere" really happens on the shipped levels (the next test is one).
        /// </summary>
        [Fact]
        public void It_agrees_with_the_match_rule_on_every_shipped_level()
        {
            MatchReach reach = new();
            int somewhere = 0, nowhere = 0, warmAsks = 0;
            double surveys = 0.0, slowestSurvey = 0.0, warm = 0.0;
            List<(BallType Type, bool Answer)> answers = new();

            foreach (string file in Shipped.LevelFiles())
            {
                BallsMap map = new(Level.Load(file).Map);

                HashSet<BallType> colours = new();
                foreach (StaticBall ball in map.GetStaticBallsArray())
                    if (ball != null && BallKinds.Matchable(ball.Kind)) colours.Add(ball.Type);

                //Every colour asked first, on the field as loaded: the first ask pays for the survey, the rest ride on it
                answers.Clear();
                bool first = true;
                foreach (BallType type in colours)
                {
                    long started = System.Diagnostics.Stopwatch.GetTimestamp();
                    bool answer = reach.HasScoringLanding(map, type, GROUP);
                    double ms = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;

                    if (first)
                    {
                        surveys += ms;
                        slowestSurvey = Math.Max(slowestSurvey, ms);
                        first = false;
                    }
                    else
                    {
                        warm += ms;
                        warmAsks++;
                    }

                    answers.Add((type, answer));
                }

                //Then the oracle, which places and removes balls and so moves the field's revision
                bool[,,] open = OpenSpace(map);
                foreach ((BallType type, bool answer) in answers)
                {
                    if (answer) somewhere++;
                    else
                    {
                        nowhere++;
                        _output.WriteLine($"{System.IO.Path.GetFileName(file)}: {type} has nowhere to score");
                    }

                    Assert.True(answer == AnyCellScores(map, open, type),
                        $"{System.IO.Path.GetFileName(file)}: MatchReach says {type} {(answer ? "can" : "cannot")} score, the match rule disagrees");
                }
            }

            int levels = Shipped.LevelFiles().Count;
            _output.WriteLine($"colours with somewhere to score: {somewhere}, nowhere: {nowhere}");
            _output.WriteLine($"the first ask on a field (the survey): {surveys / levels:F3} ms on average, {slowestSurvey:F3} ms at most;"
                + $" an ask on a surveyed field: {warm / Math.Max(1, warmAsks) * 1000.0:F1} us on average");
            Assert.True(somewhere > 0);
        }

        /// <summary>
        /// The certain "nowhere" on real data (BestPractices.md §10: the branch seen to fire): Chest, a chapter-six level, is a
        /// hollow box whose white lines the inside of a sealed hold. A white shot placed in the hold would complete a group of
        /// dozens, but no shot can fly in until the box is opened, so a white round in the bore has nowhere to score, which is
        /// the moment the Swap's hint is for.
        /// </summary>
        [Fact]
        public void The_chests_white_is_sealed_in_its_hold()
        {
            BallsMap map = new(Level.Load(Shipped.Level("Chest.json")).Map);

            Assert.False(new MatchReach().HasScoringLanding(map, BallType.Type4, GROUP));
            Assert.True(AnyCellScores(map, null, BallType.Type4), "the hold no longer completes a white group: the test lost its subject");
        }

        //Every empty cell joined to the walls or the floor through empty cells, flooded here as the oracle's own
        private static bool[,,] OpenSpace(BallsMap map)
        {
            StaticBall[,,] cells = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();
            bool[,,] open = new bool[size.X, size.Z, size.Level];
            Queue<XZLevel> queue = new();

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                        if ((x == 0 || z == 0 || x == size.X - 1 || z == size.Z - 1 || level == 0) && cells[x, z, level] == null)
                        {
                            open[x, z, level] = true;
                            queue.Enqueue(new XZLevel(x, z, level));
                        }

            while (queue.Count > 0)
                foreach (XZLevel next in BallsMap.GetNeighboringCells(queue.Dequeue(), size))
                    if (!open[next.X, next.Z, next.Level] && cells[next.X, next.Z, next.Level] == null)
                    {
                        open[next.X, next.Z, next.Level] = true;
                        queue.Enqueue(next);
                    }

            return open;
        }

        //Every empty cell beside a ball (only the open ones, unless open is null), the ball really placed and the match
        //rule's walk asked, then taken away again
        private static bool AnyCellScores(BallsMap map, bool[,,] open, BallType type)
        {
            StaticBall[,,] cells = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();
            Span<XZLevel> around = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        if (cells[x, z, level] != null || (open != null && !open[x, z, level])) continue;

                        XZLevel cell = new(x, z, level);
                        int count = BallsMap.FillNeighboringCells(cell, size, around);
                        bool beside = false;
                        for (int i = 0; i < count; i++)
                        {
                            StaticBall ball = cells[around[i].X, around[i].Z, around[i].Level];
                            if (ball == null) continue;
                            beside = true;
                            if (!BallKinds.Matchable(ball.Kind) && !MatchReach.IsInert(ball.Kind)) return true;
                        }

                        if (!beside) continue;

                        map.PutBallAt((byte)x, (byte)z, (byte)level, type);
                        int group = map.GetConnectedSameTypeCells(cell).Count;
                        map.RemoveBallAt((byte)x, (byte)z, (byte)level);

                        if (group >= GROUP) return true;
                    }

            return false;
        }
    }
}

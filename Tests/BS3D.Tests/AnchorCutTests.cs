using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The anchor cut (#213's third step): what a <see cref="BallKind.Cutter"/> is, what it does to a hanging structure
    /// (<see cref="BallsConstraintsBuilder.CutBall"/>), and which levels come with one (<see cref="LevelSet.CutChargesAt"/>).
    /// The contact handler's branch that calls <c>CutBall</c> on a real strike needs the whole Game's simulation and was
    /// checked in the running game (the log line and the score); what can be held without a device is here.
    /// </summary>
    public class AnchorCutTests
    {
        private static LevelSet ShippedSet() =>
            LevelSet.Load(Path.Combine(Shipped.LevelsDirectory, LevelSet.DefaultFileName));

        /// <summary>
        /// A single chain of <paramref name="length"/> balls hanging from the top level (the HIGHEST level index: level is
        /// the vertical axis, and the field's top is where the glass holds), each one the only support of the balls below
        /// it — the shape a cut is for (a rope). Returns the cells top to bottom.
        /// </summary>
        private static List<XZLevel> Chain(BallsMap map, int length)
        {
            XZLevel size = map.GetStaticBallsArraySize();
            int top = size.Level - 1;
            var cells = new List<XZLevel> { new XZLevel(3, 3, top) };
            map.PutBallAt(3, 3, (byte)top, BallType.Type4);

            while (cells.Count < length)
            {
                XZLevel last = cells[^1];
                bool found = false;

                foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(last, size))
                {
                    if (neighbour.Level != last.Level - 1) continue;

                    map.PutBallAt((byte)neighbour.X, (byte)neighbour.Z, (byte)neighbour.Level, BallType.Type4);
                    cells.Add(neighbour);
                    found = true;
                    break;
                }

                Assert.True(found, "the chain could not be extended downwards");
            }

            return cells;
        }

        [Fact]
        public void CuttingTheSecondBallOfARopeBringsDownEverythingBelowIt()
        {
            using HungLevel hung = new(new BallsMap(7, 7, 10));
            List<XZLevel> rope = Chain(hung.Map, 5);

            //Rebuilt: the chain was written into the map after the hang, so hang it again from the chain
            using HungLevel chain = new(RebuildFrom(hung.Map, rope));

            var released = new List<PhysicsBall>();
            BallsReleased result = BallsConstraintsBuilder.CutBall(rope[1], chain.Balls, chain.Map, chain.World.Simulation, released);

            Assert.Equal(0, result.Matched);
            Assert.Equal(3, result.Orphaned);
            Assert.Equal(1, result.Destroyed);

            //Every one of them is loose and drawn falling: the ball struck and the three that hung on it
            Assert.Equal(4, released.Count);

            //The top ball, held by the glass, is the only thing left in the lattice
            Assert.NotNull(chain.Balls[rope[0].X, rope[0].Z, rope[0].Level]);
            for (int i = 1; i < rope.Count; i++)
            {
                Assert.Null(chain.Balls[rope[i].X, rope[i].Z, rope[i].Level]);
                Assert.Null(chain.Map.GetStaticBallsArray()[rope[i].X, rope[i].Z, rope[i].Level]);
            }

            ClusterInvariants.Verify(chain.Balls, chain.Map, chain.World.Simulation, chain.Ceiling.Handle);
        }

        [Fact]
        public void CuttingTheEndOfARopeDestroysOnlyThatBall()
        {
            using HungLevel hung = new(new BallsMap(7, 7, 10));
            List<XZLevel> rope = Chain(hung.Map, 5);
            using HungLevel chain = new(RebuildFrom(hung.Map, rope));

            var released = new List<PhysicsBall>();
            BallsReleased result = BallsConstraintsBuilder.CutBall(rope[^1], chain.Balls, chain.Map, chain.World.Simulation, released);

            Assert.Equal(0, result.Orphaned);
            Assert.Equal(1, result.Destroyed);
            Assert.Single(released);

            ClusterInvariants.Verify(chain.Balls, chain.Map, chain.World.Simulation, chain.Ceiling.Handle);
        }

        [Fact]
        public void ACutOfAnEmptyCellCutsNothing()
        {
            using HungLevel hung = new(new BallsMap(7, 7, 10));
            List<XZLevel> rope = Chain(hung.Map, 3);
            using HungLevel chain = new(RebuildFrom(hung.Map, rope));

            var released = new List<PhysicsBall>();
            BallsReleased result = BallsConstraintsBuilder.CutBall(new XZLevel(0, 0, 1), chain.Balls, chain.Map,
                chain.World.Simulation, released);

            Assert.Equal(0, result.Destroyed);
            Assert.Equal(0, result.Orphaned);
            Assert.Empty(released);
        }

        /// <summary>A rock in the rope is cut like any ball — it is very often the anchor a level hangs from — and what hung on it falls.</summary>
        [Fact]
        public void ARockCanBeCutAndItsLoadFalls()
        {
            using HungLevel hung = new(new BallsMap(7, 7, 10));
            List<XZLevel> rope = Chain(hung.Map, 5);

            var map = new BallsMap(hung.Map.StageSizeX, hung.Map.StageSizeZ, hung.Map.Levels);
            for (int i = 0; i < rope.Count; i++)
                map.PutBallAt((byte)rope[i].X, (byte)rope[i].Z, (byte)rope[i].Level, BallType.Type4, i == 1 ? BallKind.Rock : BallKind.Normal);

            using HungLevel chain = new(map);

            var released = new List<PhysicsBall>();
            BallsReleased result = BallsConstraintsBuilder.CutBall(rope[1], chain.Balls, chain.Map, chain.World.Simulation, released);

            Assert.Equal(1, result.Destroyed);
            Assert.Equal(3, result.Orphaned);
            Assert.Equal(4, released.Count);
            ClusterInvariants.Verify(chain.Balls, chain.Map, chain.World.Simulation, chain.Ceiling.Handle);
        }

        /// <summary>A bomb the cutter strikes goes off (it has been reached) and is not destroyed quietly: the blast takes its neighbours too.</summary>
        [Fact]
        public void ABombTheCutterStrikesGoesOff()
        {
            using HungLevel hung = new(new BallsMap(7, 7, 10));
            List<XZLevel> rope = Chain(hung.Map, 5);

            var map = new BallsMap(hung.Map.StageSizeX, hung.Map.StageSizeZ, hung.Map.Levels);
            for (int i = 0; i < rope.Count; i++)
                map.PutBallAt((byte)rope[i].X, (byte)rope[i].Z, (byte)rope[i].Level, BallType.Type4, i == 2 ? BallKind.Bomb : BallKind.Normal);

            using HungLevel chain = new(map);

            var released = new List<PhysicsBall>();
            var detonations = new List<Detonation>();
            BallsReleased result = BallsConstraintsBuilder.CutBall(rope[2], chain.Balls, chain.Map, chain.World.Simulation, released, detonations);

            //A blast of two units takes the whole short rope below the glass ball it could not reach: more than the one ball
            Assert.True(result.Destroyed > 1, "the bomb destroyed only " + result.Destroyed);
            Assert.NotEmpty(detonations);
            ClusterInvariants.Verify(chain.Balls, chain.Map, chain.World.Simulation, chain.Ceiling.Handle);
        }

        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(0, 0, 99)]
        [InlineData(99, 0, 0)]
        public void ACutOutsideTheFieldCutsNothing(int x, int z, int level)
        {
            using HungLevel hung = new(new BallsMap(7, 7, 10));
            List<XZLevel> rope = Chain(hung.Map, 3);
            using HungLevel chain = new(RebuildFrom(hung.Map, rope));

            var released = new List<PhysicsBall>();
            BallsReleased result = BallsConstraintsBuilder.CutBall(new XZLevel(x, z, level), chain.Balls, chain.Map,
                chain.World.Simulation, released);

            Assert.Equal(0, result.Destroyed);
            Assert.Empty(released);
        }

        /// <summary>
        /// A field whose top level is full (every ball held by the glass) with <paramref name="rows"/> rows of three hanging
        /// under it on the next level down, each a storey of its own, and a rope of two hanging under the first row's middle.
        /// Returns the rows' middle cells and the rope's cells.
        /// </summary>
        private static BallsMap Slabs(int[] rows, out List<XZLevel> middles, out List<XZLevel> rope)
        {
            var map = new BallsMap(9, 9, 10);
            XZLevel size = map.GetStaticBallsArraySize();
            int top = size.Level - 1, under = top - 1;

            for (int x = 0; x < size.X; x++)
                for (int z = 0; z < size.Z; z++)
                    map.PutBallAt((byte)x, (byte)z, (byte)top, BallType.Type1);

            middles = new List<XZLevel>();
            foreach (int x in rows)
            {
                for (int z = 3; z <= 5; z++) map.PutBallAt((byte)x, (byte)z, (byte)under, BallType.Type4);
                middles.Add(new XZLevel(x, 4, under));
            }

            rope = new List<XZLevel>();
            XZLevel last = middles[0];
            while (rope.Count < 2)
            {
                foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(last, size))
                {
                    if (neighbour.Level != last.Level - 1) continue;
                    map.PutBallAt((byte)neighbour.X, (byte)neighbour.Z, (byte)neighbour.Level, BallType.Type4);
                    rope.Add(neighbour);
                    last = neighbour;
                    break;
                }
            }

            return map;
        }

        /// <summary>The owner's rule (#692): the struck ball's whole storey goes, counted as destroyed, and what hung on it alone falls.</summary>
        [Fact]
        public void ACutTakesTheStruckBallsWholeStoreyAndWhatHungOnIt()
        {
            using HungLevel hung = new(Slabs(new[] { 4 }, out List<XZLevel> middles, out List<XZLevel> rope));

            var released = new List<PhysicsBall>();
            XZLevel end = new(middles[0].X, 3, middles[0].Level);
            BallsReleased result = BallsConstraintsBuilder.CutBall(end, hung.Balls, hung.Map, hung.World.Simulation, released);

            Assert.Equal(3, result.Destroyed);
            Assert.Equal(rope.Count, result.Orphaned);
            Assert.Equal(3 + rope.Count, released.Count);
            for (int z = 3; z <= 5; z++) Assert.Null(hung.Map.GetStaticBallsArray()[middles[0].X, z, middles[0].Level]);

            ClusterInvariants.Verify(hung.Balls, hung.Map, hung.World.Simulation, hung.Ceiling.Handle);
        }

        /// <summary>"Only the attached balls" (#692): a clump on the same level that the struck one does not touch through it stays.</summary>
        [Fact]
        public void ASeparateClumpOnTheSameLevelStays()
        {
            using HungLevel hung = new(Slabs(new[] { 2, 6 }, out List<XZLevel> middles, out _));

            var released = new List<PhysicsBall>();
            BallsReleased result = BallsConstraintsBuilder.CutBall(middles[1], hung.Balls, hung.Map, hung.World.Simulation, released);

            Assert.Equal(3, result.Destroyed);
            Assert.Equal(0, result.Orphaned);
            for (int z = 3; z <= 5; z++) Assert.NotNull(hung.Map.GetStaticBallsArray()[middles[0].X, z, middles[0].Level]);

            ClusterInvariants.Verify(hung.Balls, hung.Map, hung.World.Simulation, hung.Ceiling.Handle);
        }

        /// <summary>
        /// The owner's guard (#692, 2026-10-07): a cut may take at most the lower half of the cluster, from its lowest ball
        /// to its highest - the storey struck and every one under it. It replaced the four storeys under the glass.
        /// </summary>
        [Theory]
        [InlineData(8, 4)]
        [InlineData(12, 6)]
        [InlineData(5, 2)]
        [InlineData(4, 2)]
        [InlineData(1, 0)]
        public void AtMostTheLowerHalfOfTheClusterMayBeCut(int storeys, int cuttable)
        {
            var map = new BallsMap(7, 7, 12);
            List<XZLevel> rope = Chain(map, storeys);   //top to bottom

            for (int i = 0; i < rope.Count; i++)
            {
                int fromTheBottom = rope.Count - 1 - i;  //0 for the lowest ball
                bool protectedHere = BallsConstraintsBuilder.IsCutProtected(rope[i], map);
                Assert.Equal(fromTheBottom >= cuttable, protectedHere);
            }
        }

        /// <summary>
        /// The preview the gun lights before the shot (#692, <see cref="CutReach"/>) is what the cut then takes: the storey
        /// and every ball left reaching nothing, counted against <see cref="BallsConstraintsBuilder.CutBall"/> on the same
        /// cluster hung for real, at every cell a cutter may strike.
        /// </summary>
        [Theory]
        [InlineData(11)]
        [InlineData(23)]
        [InlineData(47)]
        public void ThePreviewIsWhatTheCutTakes(int seed)
        {
            List<XZLevel> cluster = RandomCluster(seed);
            BallsMap probe = RebuildFrom(new BallsMap(7, 7, 9), cluster);
            var reach = new CutReach();
            int withFall = 0, withoutFall = 0;

            //Every cell a cutter may strike, compared where something falls (that is the half a preview can get wrong) and a
            //couple where nothing does, each against a cluster hung afresh
            foreach (XZLevel cell in cluster)
            {
                if (CutReach.IsProtected(cell, probe)) continue;

                reach.Measure(probe, cell);
                bool falls = reach.Cells.Count > reach.StoreyCount;
                if (falls ? withFall >= 6 : withoutFall >= 2) continue;
                if (falls) withFall++; else withoutFall++;

                using HungLevel hung = new(RebuildFrom(probe, cluster));
                var released = new List<PhysicsBall>();
                BallsReleased result = BallsConstraintsBuilder.CutBall(cell, hung.Balls, hung.Map, hung.World.Simulation, released);

                Assert.Equal(result.Destroyed + result.Orphaned, reach.Cells.Count);
                Assert.Equal(result.Destroyed, reach.StoreyCount);
            }

            Assert.True(withFall > 0, "no cut the cluster allows lets anything fall, so the comparison proves nothing");
        }

        /// <summary>
        /// A cluster that hangs together: the top level full, and below it each cell kept now and then if a ball it touches
        /// on the level above is there - a seeded scatter, so the cuts compared run through ropes, ledges and clumps.
        /// </summary>
        private static List<XZLevel> RandomCluster(int seed)
        {
            var random = new System.Random(seed);
            var map = new BallsMap(7, 7, 9);
            XZLevel size = map.GetStaticBallsArraySize();
            var cells = new List<XZLevel>();
            StaticBall[,,] balls = map.GetStaticBallsArray();

            for (int level = size.Level - 1; level >= 1; level--)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        bool hangs = level == size.Level - 1;
                        if (!hangs)
                            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(new XZLevel(x, z, level), size))
                                if (neighbour.Level == level + 1 && balls[neighbour.X, neighbour.Z, neighbour.Level] != null) hangs = true;

                        if (!hangs || (level < size.Level - 1 && random.NextDouble() > 0.55)) continue;

                        map.PutBallAt((byte)x, (byte)z, (byte)level, BallType.Type1);
                        cells.Add(new XZLevel(x, z, level));
                    }

            return cells;
        }

        /// <summary>A fresh map holding only <paramref name="cells"/>: <see cref="HungLevel"/> centres and hangs the map it is given once.</summary>
        private static BallsMap RebuildFrom(BallsMap source, List<XZLevel> cells)
        {
            var map = new BallsMap(source.StageSizeX, source.StageSizeZ, source.Levels);
            foreach (XZLevel cell in cells) map.PutBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level, BallType.Type4);
            return map;
        }

        [Fact]
        public void ACutterNeverHangsInTheLattice()
        {
            Assert.False(BallKinds.InCluster(BallKind.Cutter));
            Assert.False(BallKinds.InCluster(BallKind.Wildcard));

            //The map's one placement door turns it into an ordinary ball rather than store it
            var map = new BallsMap(4, 4, 4);
            StaticBall placed = map.PutBallAt(1, 1, 1, BallType.Type4, BallKind.Cutter);
            Assert.Equal(BallKind.Normal, placed.Kind);

            //And an editor cycling kinds steps over it
            BallKind kind = BallKind.Normal;
            for (int i = 0; i < 40; i++)
            {
                kind = BallKinds.Next(kind);
                Assert.True(BallKinds.InCluster(kind), $"the cycle reached {kind}");
            }
        }

        [Theory]
        [InlineData("cutter")]
        [InlineData("cut")]
        [InlineData("Blade")]
        public void TheCutterSpellsItselfLenientlyOnACommandLine(string spelling)
        {
            Assert.True(BallKinds.TryParse(spelling, out BallKind kind));
            Assert.Equal(BallKind.Cutter, kind);
        }

        [Fact]
        public void TheShippedFourthChapterOnwardHasACutAndNothingBeforeIt()
        {
            LevelSet set = ShippedSet();

            //The Tower opens the fourth chapter at index 30 (The Meadow, the Gallery and the Coil are ten each)
            set.BlockRange(30, out int firstOfFourth, out _);
            Assert.Equal(30, firstOfFourth);

            for (int index = 0; index < 30; index++) Assert.Equal(0, set.CutChargesAt(index));
            for (int index = 30; index < set.Count; index++) Assert.Equal(1, set.CutChargesAt(index));

            Assert.Equal(0, set.CutChargesAt(-1));
            Assert.Equal(0, set.CutChargesAt(set.Count));
            Assert.Equal(0, new LevelSet().CutChargesAt(0));
        }

        [Fact]
        public void ASetWithNoChaptersGrantsNoCut()
        {
            LevelSet set = new();
            for (int i = 0; i < 45; i++) set.Levels.Add(new LevelSetEntry { File = $"l{i}.json", Name = $"L{i}" });

            for (int index = 0; index < set.Count; index++) Assert.Equal(0, set.CutChargesAt(index));
        }
    }
}

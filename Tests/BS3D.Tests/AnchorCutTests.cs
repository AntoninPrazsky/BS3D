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

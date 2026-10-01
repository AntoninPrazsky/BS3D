using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #257's buckshot: no colour takes it and it holds the level open, it falls when its support is cut, and a clump
    /// hanging from the glass by rocks and buckshot alone is the one that can never come down.
    /// </summary>
    public class BuckshotTests
    {
        [Fact]
        public void BuckshotIsNotMatchableButHoldsTheLevelOpen()
        {
            Assert.False(BallKinds.Matchable(BallKind.Buckshot));
            Assert.True(BallKinds.Removable(BallKind.Buckshot));
            Assert.True(BallKinds.InCluster(BallKind.Buckshot));

            //An editor's cycle reaches it: past the heavy ball, over the cutter (a gun-side kind)
            Assert.Equal(BallKind.Buckshot, BallKinds.Next(BallKind.Heavy));
        }

        /// <summary>A clump hung from an ordinary ball comes down when that ball goes, and counts until it does.</summary>
        [Fact]
        public void AClumpFallsWhenItsSupportIsCut()
        {
            BallsMap map = new(3, 3, 2);
            map.PutBallAt(1, 1, 1);
            XZLevel under = Below(map, new XZLevel(1, 1, 1));
            map.PutBallAt((byte)under.X, (byte)under.Z, (byte)under.Level, kind: BallKind.Buckshot);

            Assert.Equal(2, map.GetRemovableBallsCount());
            Assert.Empty(map.GetCellsDisconnectedFromCeiling());

            map.RemoveBallAt(1, 1, 1);

            Assert.Contains(under, map.GetCellsDisconnectedFromCeiling());
        }

        /// <summary>
        /// The gate's question (<see cref="BallsMap.GetUncuttableFromCeiling"/>): a clump under an ordinary ball can be
        /// cut down; under a rock, or on the glass itself, it never can.
        /// </summary>
        [Fact]
        public void AClumpHangingByRocksAloneCanNeverComeDown()
        {
            //Under an ordinary ball: cuttable
            BallsMap open = new(3, 3, 2);
            open.PutBallAt(1, 1, 1);
            XZLevel under = Below(open, new XZLevel(1, 1, 1));
            open.PutBallAt((byte)under.X, (byte)under.Z, (byte)under.Level, kind: BallKind.Buckshot);
            Assert.False(open.GetUncuttableFromCeiling()[under.X, under.Z, under.Level]);

            //Under a rock: stuck
            BallsMap walled = new(3, 3, 2);
            walled.PutBallAt(1, 1, 1, kind: BallKind.Rock);
            walled.PutBallAt((byte)under.X, (byte)under.Z, (byte)under.Level, kind: BallKind.Buckshot);
            Assert.True(walled.GetUncuttableFromCeiling()[under.X, under.Z, under.Level]);

            //Under a rock that also hangs beside an ordinary ball: still stuck - the clump's own path to the glass is the rock
            walled.PutBallAt(0, 0, 1);
            Assert.True(walled.GetUncuttableFromCeiling()[under.X, under.Z, under.Level]);

            //On the glass itself: stuck
            BallsMap anchored = new(3, 3, 2);
            anchored.PutBallAt(1, 1, 1, kind: BallKind.Buckshot);
            Assert.True(anchored.GetUncuttableFromCeiling()[1, 1, 1]);
        }

        //A lattice neighbour of a cell on the level below it
        private static XZLevel Below(BallsMap map, XZLevel cell)
        {
            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, new XZLevel(map.StageSizeX, map.StageSizeZ, map.Levels)))
                if (neighbour.Level < cell.Level) return neighbour;

            throw new System.InvalidOperationException("no cell below");
        }
    }
}

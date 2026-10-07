using Prazsky.BS3D.Levels;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// Which levels come with a Swap (#213): one from the third chapter on since #705, none before it. A rule of the campaign's
    /// shape (<see cref="LevelSet.SwapChargesAt"/>) rather than a field of each level file, so what is checked here is
    /// the shape: the shipped set's own boundary, and that a set with no chapters, or an index outside the set, grants
    /// nothing. The price the rule pays for the swap being worth having is that it is one a level - see the doc
    /// on <see cref="LevelSet.SWAP_FROM_BLOCK"/>.
    /// </summary>
    public class SwapGrantTests
    {
        private static LevelSet ShippedSet() =>
            LevelSet.Load(Path.Combine(Shipped.LevelsDirectory, LevelSet.DefaultFileName));

        [Fact]
        public void TheShippedFirstTwoChaptersHaveNoSwapAndEveryLevelAfterThemHasOne()
        {
            LevelSet set = ShippedSet();

            //The Meadow and the Gallery are ten levels each; the Coil opens the third chapter (#705: the second teaches the
            //score and nothing beside it)
            set.BlockRange(10, out _, out int lastOfSecond);
            Assert.Equal(19, lastOfSecond);

            for (int index = 0; index <= lastOfSecond; index++) Assert.Equal(0, set.SwapChargesAt(index));
            for (int index = lastOfSecond + 1; index < set.Count; index++) Assert.Equal(1, set.SwapChargesAt(index));
        }

        [Fact]
        public void AnIndexOutsideTheSetGrantsNothing()
        {
            LevelSet set = ShippedSet();

            Assert.Equal(0, set.SwapChargesAt(-1));
            Assert.Equal(0, set.SwapChargesAt(set.Count));
            Assert.Equal(0, new LevelSet().SwapChargesAt(0));
        }

        [Fact]
        public void ASetWithNoChaptersGrantsNothing()
        {
            //"The second chapter" means nothing where no entry names a block, so a set of loose levels is played as it
            //always was
            LevelSet set = new();
            for (int i = 0; i < 25; i++) set.Levels.Add(new LevelSetEntry { File = $"l{i}.json", Name = $"L{i}" });

            for (int index = 0; index < set.Count; index++) Assert.Equal(0, set.SwapChargesAt(index));
        }

        [Fact]
        public void AChapteredSetOfThreeBlocksGrantsFromTheThird()
        {
            LevelSet set = new();
            for (int i = 0; i < 2; i++) set.Levels.Add(new LevelSetEntry { File = $"a{i}.json", Name = $"A{i}", Block = "A" });
            for (int i = 0; i < 2; i++) set.Levels.Add(new LevelSetEntry { File = $"b{i}.json", Name = $"B{i}", Block = "B" });
            for (int i = 0; i < 2; i++) set.Levels.Add(new LevelSetEntry { File = $"c{i}.json", Name = $"C{i}", Block = "C" });

            Assert.Equal(new[] { 0, 0, 0, 0, 1, 1 }, new[]
            {
                set.SwapChargesAt(0), set.SwapChargesAt(1), set.SwapChargesAt(2),
                set.SwapChargesAt(3), set.SwapChargesAt(4), set.SwapChargesAt(5),
            });
        }
    }
}

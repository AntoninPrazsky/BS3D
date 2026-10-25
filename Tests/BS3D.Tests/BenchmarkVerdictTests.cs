using BS3D;
using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The benchmark's recommendation (#988): one step, read from the numbers.</summary>
    public class BenchmarkVerdictTests
    {
        private const float BUDGET = 1000f / 75f;

        private static FrameStats.Summary Scene(float p99, float overShare) =>
            new(1000, p99 * 0.8f, p99 * 0.8f, p99 * 0.9f, p99, 1000f / p99, p99 * 1.2f, overShare, BUDGET);

        [Fact]
        public void ATierThatDropsFramesInAnySceneStepsDown()
        {
            var results = new[] { Scene(6f, 0f), Scene(15f, 0.2f), Scene(7f, 0f) };
            Assert.Equal(QualityLevel.Medium, BenchmarkVerdict.Recommend(QualityLevel.High, results, 3, out int by));
            Assert.Equal(1, by);
            //Low has nowhere lower to go
            Assert.Equal(QualityLevel.Low, BenchmarkVerdict.Recommend(QualityLevel.Low, results, 3, out _));
        }

        [Fact]
        public void RoomEverywhereStepsUpOnce()
        {
            var results = new[] { Scene(3f, 0f), Scene(5f, 0f), Scene(7.5f, 0f) };
            Assert.Equal(QualityLevel.Ultra, BenchmarkVerdict.Recommend(QualityLevel.High, results, 3, out _));
            //Ultra has nowhere higher
            Assert.Equal(QualityLevel.Ultra, BenchmarkVerdict.Recommend(QualityLevel.Ultra, results, 3, out _));
        }

        [Fact]
        public void HoldingTheRefreshWithoutRoomKeepsTheTier()
        {
            //One scene's 99th percentile past 0.6 of the budget: no room for the tier above, and no dropped frames
            var results = new[] { Scene(3f, 0f), Scene(11f, 0.01f) };
            Assert.Equal(QualityLevel.High, BenchmarkVerdict.Recommend(QualityLevel.High, results, 2, out int by));
            Assert.Equal(-1, by);
            Assert.Equal(QualityLevel.Potato, BenchmarkVerdict.Recommend(QualityLevel.Potato, results, 2, out _));
            Assert.Equal(QualityLevel.High, BenchmarkVerdict.Recommend(QualityLevel.High, results, 0, out _));
        }
    }
}

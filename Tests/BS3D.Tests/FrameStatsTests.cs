using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The benchmark's arithmetic (#988), on frame times whose answers are known.</summary>
    public class FrameStatsTests
    {
        [Fact]
        public void AnEvenRunReadsAsItself()
        {
            var stats = new FrameStats();
            for (int i = 0; i < 200; i++) stats.Add(10f);
            FrameStats.Summary s = stats.Summarize(13.33f);
            Assert.Equal(200, s.Frames);
            Assert.Equal(10f, s.Mean, 3);
            Assert.Equal(10f, s.Median, 3);
            Assert.Equal(10f, s.P99, 3);
            Assert.Equal(100f, s.OnePercentLowFps, 2);
            Assert.Equal(0f, s.OverBudgetShare);
            Assert.Equal(100f, s.MedianFps, 2);
        }

        /// <summary>Two hitches in two hundred frames: the median does not see them, the 1 % low and the worst do.</summary>
        [Fact]
        public void HitchesShowInTheTailNotTheMiddle()
        {
            var stats = new FrameStats();
            for (int i = 0; i < 198; i++) stats.Add(10f);
            stats.Add(40f);
            stats.Add(50f);
            FrameStats.Summary s = stats.Summarize(13.33f);
            Assert.Equal(10f, s.Median, 3);
            Assert.Equal(50f, s.Worst, 3);
            //The slowest hundredth is the two hitches: (40 + 50) / 2 = 45 ms, 22.2 FPS
            Assert.Equal(1000f / 45f, s.OnePercentLowFps, 2);
            Assert.Equal(2f / 200f, s.OverBudgetShare, 5);
            Assert.True(s.P99 > 10f && s.P99 < 50f, $"p99 {s.P99}");
        }

        [Fact]
        public void PercentilesInterpolate()
        {
            float[] sorted = { 1f, 2f, 3f, 4f, 5f };
            Assert.Equal(3f, FrameStats.Percentile(sorted, 0.5f), 4);
            Assert.Equal(4.6f, FrameStats.Percentile(sorted, 0.9f), 4);
            Assert.Equal(5f, FrameStats.Percentile(sorted, 1f), 4);
            Assert.Equal(default, new FrameStats().Summarize(13f));
        }
    }
}

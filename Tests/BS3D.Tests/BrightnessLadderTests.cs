using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What the Game's Brightness row promises (#711): 100 % is the authored look and sits in the middle of the
    /// ladder, a click goes up and wraps, a value off the ladder walks onto it, and a settings file written by the row's
    /// old raw-multiplier ladder opens on the nearest new rung instead of resetting.
    /// </summary>
    public class BrightnessLadderTests
    {
        [Fact]
        public void TheDefaultIsTheMiddleRungAndIsOneHundredPercent()
        {
            var rungs = BrightnessLadder.Rungs;

            Assert.Equal(7, rungs.Length);
            Assert.Equal(BrightnessLadder.DEFAULT_PERCENT, rungs[rungs.Length / 2]);
            Assert.Equal(100, BrightnessLadder.PercentOf(PostProcessPipeline.DEFAULT_EXPOSURE));
            Assert.Equal(PostProcessPipeline.DEFAULT_EXPOSURE, BrightnessLadder.ExposureOf(100), 5);
        }

        [Fact]
        public void EveryRungSurvivesTheRoundTripThroughAnExposure()
        {
            //The row stores the exposure and shows a percent read back from it, so a rung must come back as itself
            foreach (int rung in BrightnessLadder.Rungs)
                Assert.Equal(rung, BrightnessLadder.PercentOf(BrightnessLadder.ExposureOf(rung)));
        }

        [Fact]
        public void ClickingWalksUpEveryRungAndWrapsToTheBottom()
        {
            int percent = 100;
            int[] seen = new int[BrightnessLadder.Rungs.Length];

            for (int i = 0; i < seen.Length; i++)
            {
                percent = BrightnessLadder.NextAbove(percent);
                seen[i] = percent;
            }

            //From the default: the three brighter rungs, the wrap to the bottom, the dim side, and back on the default
            Assert.Equal(new[] { 110, 120, 130, 70, 80, 90, 100 }, seen);
        }

        [Fact]
        public void AValueOffTheLadderWalksOntoTheNextRungUp()
        {
            //exposure=1.0 on the command line is a raw multiplier and 91 % of the authored look
            int percent = BrightnessLadder.PercentOf(1.0f);

            Assert.Equal(91, percent);
            Assert.Equal(100, BrightnessLadder.NextAbove(percent));
            Assert.Equal(70, BrightnessLadder.NextAbove(140));
            Assert.Equal(70, BrightnessLadder.NextAbove(130));
        }

        [Theory]
        [InlineData(0.7f, 70)]   //the old ladder's rungs: 64, 82, 100, 118 and 136 % of the authored look
        [InlineData(0.9f, 80)]
        [InlineData(1.1f, 100)]
        [InlineData(1.3f, 120)]
        [InlineData(1.5f, 130)]
        public void AnOldSavedExposureOpensOnTheNearestNewRung(float stored, int expectedPercent)
        {
            Assert.Equal(expectedPercent, BrightnessLadder.PercentOf(BrightnessLadder.NearestExposure(stored)));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        public void AnUnusableSavedExposureIsTheAuthoredLook(float stored)
        {
            Assert.Equal(PostProcessPipeline.DEFAULT_EXPOSURE, BrightnessLadder.NearestExposure(stored));
        }
    }
}

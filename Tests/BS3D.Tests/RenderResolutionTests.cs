using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What the Game's Resolution row promises (#801): every size it offers has the display's own aspect ratio and even
    /// sides, native comes first, nothing goes under the floor, and a click steps down and wraps.
    /// </summary>
    public class RenderResolutionTests
    {
        [Theory]
        [InlineData(1920, 1080, new[] { 1080, 900, 720, 540, 360 })]
        [InlineData(3840, 2160, new[] { 2160, 1800, 1440, 1080, 720 })]
        [InlineData(2560, 1440, new[] { 1440, 1206, 954, 720, 486 })]
        [InlineData(3840, 1600, new[] { 1600, 1330, 1070, 800, 530 })]
        [InlineData(1920, 1200, new[] { 1200, 1000, 800, 600, 400 })]
        public void TheLadderIsTheFractionsAtTheDisplaysExactAspect(int width, int height, int[] expected)
        {
            Assert.Equal(expected, RenderResolution.Heights(width, height));
        }

        [Theory]
        [InlineData(1920, 1080)]
        [InlineData(3840, 2160)]
        [InlineData(2560, 1440)]
        [InlineData(3840, 1600)]
        [InlineData(3440, 1440)]
        [InlineData(1920, 1200)]
        public void EveryRungKeepsTheAspectExactlyWithEvenSides(int width, int height)
        {
            foreach (int rung in RenderResolution.Heights(width, height))
            {
                int rungWidth = RenderResolution.WidthAt(width, height, rung);

                Assert.Equal(0, rung % 2);
                Assert.Equal(0, rungWidth % 2);
                //Cross-multiplied, so "exactly" means exactly
                Assert.Equal((long)width * rung, (long)rungWidth * height);
            }
        }

        [Fact]
        public void APanelWithNoUsableUnitStaysWithinAFifthOfAPercent()
        {
            //1366x768 does not reduce: the fraction's own height is taken, and only the rounding of its width is off
            int[] heights = RenderResolution.Heights(1366, 768);

            Assert.Equal(new[] { 768, 640, 512, 384 }, heights);

            foreach (int rung in heights)
            {
                float ratio = RenderResolution.WidthAt(1366, 768, rung) / (float)rung;
                Assert.InRange(ratio / (1366f / 768f), 0.998f, 1.002f);
            }
        }

        [Fact]
        public void NothingGoesUnderTheFloorAndNativeIsAlwaysFirst()
        {
            //Exact 16:9 multiples of 18 near 600 and 480 - 1056x594 and 864x486 - then the floor
            Assert.Equal(new[] { 720, 594, 486, 360 }, RenderResolution.Heights(1280, 720));
            Assert.Equal(new[] { 480, 402 }, RenderResolution.Heights(640, 480));

            foreach (int rung in RenderResolution.Heights(1280, 720)[1..])
                Assert.True(rung >= RenderResolution.MIN_HEIGHT);
        }

        [Fact]
        public void AClickStepsDownAndWrapsToNative()
        {
            int[] heights = RenderResolution.Heights(1920, 1080);

            Assert.Equal(900, RenderResolution.Next(heights, 1080));
            Assert.Equal(720, RenderResolution.Next(heights, 900));
            Assert.Equal(1080, RenderResolution.Next(heights, 360));
            //Off the ladder, the first rung below it
            Assert.Equal(720, RenderResolution.Next(heights, 800));
        }

        [Fact]
        public void AStoredHeightSnapsToTheNearestRungTheLowerOnATie()
        {
            int[] heights = RenderResolution.Heights(1920, 1080);

            Assert.Equal(720, RenderResolution.Nearest(heights, 720));
            Assert.Equal(720, RenderResolution.Nearest(heights, 700));
            //Equally far from 720 and 900: the lower, the cheaper frame
            Assert.Equal(720, RenderResolution.Nearest(heights, 810));
            Assert.Equal(1080, RenderResolution.Nearest(heights, 4000));
        }
    }
}

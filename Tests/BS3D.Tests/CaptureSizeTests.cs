using BS3D.Capture;
using Microsoft.Xna.Framework;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>A capture's frames of their own size (#1177, <c>capturesize=</c>): the spelling, the limits, the preview window and the scene's fit.</summary>
    public class CaptureSizeTests
    {
        [Theory]
        [InlineData("3840x2160", 3840, 2160)]
        [InlineData("7680X4320", 7680, 4320)]
        [InlineData("1920x1080", 1920, 1080)]
        [InlineData("2160x3840", 2160, 3840)]
        public void ASizeIsTwoWholeEvenNumbers(string text, int width, int height)
        {
            Assert.True(CaptureSize.TryParse(text, out Point size));
            Assert.Equal(new Point(width, height), size);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("3840")]
        [InlineData("3840x")]
        [InlineData("x2160")]
        [InlineData("3840x2160x2")]
        [InlineData("3840,2160")]
        [InlineData("-3840x2160")]
        [InlineData("3840 x 2160")]
        [InlineData("3840.0x2160")]
        //An odd side: the encoders store colour at half resolution and refuse it
        [InlineData("1921x1080")]
        [InlineData("1920x1081")]
        //Past what Direct3D 11 makes, and too small to be a film
        [InlineData("16386x2160")]
        [InlineData("32x32")]
        public void AnythingElseIsRefused(string text)
        {
            Assert.False(CaptureSize.TryParse(text, out _));
        }

        [Fact]
        public void TheLimitsThemselvesAreTaken()
        {
            Assert.True(CaptureSize.TryParse($"{CaptureSize.MAX_SIDE}x{CaptureSize.MIN_SIDE}", out _));
        }

        [Theory]
        //A 4K film in the default box: the box's own 16:9
        [InlineData(3840, 2160, 1280, 720)]
        //A 21:9 film is held by the box's width
        [InlineData(3840, 1600, 1280, 533)]
        //A portrait one by its height
        [InlineData(2160, 3840, 405, 720)]
        //A frame smaller than the box is shown at its own size, never enlarged
        [InlineData(640, 360, 640, 360)]
        public void ThePreviewKeepsTheFramesAspectInsideTheBox(int width, int height, int previewWidth, int previewHeight)
        {
            Assert.Equal(new Point(previewWidth, previewHeight), CaptureSize.Preview(new Point(width, height), CaptureSize.PREVIEW_BOX));
        }

        [Theory]
        //Ultra (a factor of 2) at 4K is the 8K scene the trailer wants; any desktop factor fits a 4K film
        [InlineData(3840, 2160, 2, true)]
        [InlineData(3840, 2160, 4, true)]
        //An 8K film takes 2 and no more
        [InlineData(7680, 4320, 2, true)]
        [InlineData(7680, 4320, 3, false)]
        //A tall one is held by its height the same way
        [InlineData(2160, 8192, 2, true)]
        [InlineData(2160, 8194, 2, false)]
        public void TheSceneFitsDirect3DsLargestTextureOrIsRefused(int width, int height, int factor, bool fits)
        {
            Assert.Equal(fits, CaptureSize.SceneFits(new Point(width, height), factor));
        }
    }
}

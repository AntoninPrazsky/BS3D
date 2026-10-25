using BS3D.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The benchmark's correctness probe (#988): a picture passes, a black and a flat frame are each caught.</summary>
    public class FrameProbeTests
    {
        private const int W = 320, H = 180;

        private static Color[] Frame(System.Func<int, int, Color> at)
        {
            var frame = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++) frame[x + y * W] = at(x, y);
            return frame;
        }

        [Fact]
        public void APictureIsOkAndTheTwoFailuresAreCaught()
        {
            //A sky over ground: a gradient top to bottom and a darker lower half
            Color[] picture = Frame((x, y) => y < H / 2 ? new Color(90, 140, 220) : new Color(60 + x % 40, 110, 40));
            Assert.Equal(FrameProbe.Verdict.Ok, FrameProbe.Check(picture, W, H, out float mean, out float spread));
            Assert.True(mean > 0.1f && spread > 0.05f, $"mean {mean} spread {spread}");

            Assert.Equal(FrameProbe.Verdict.Black, FrameProbe.Check(Frame((x, y) => Color.Black), W, H, out _, out _));
            Assert.Equal(FrameProbe.Verdict.Flat, FrameProbe.Check(Frame((x, y) => new Color(255, 0, 255)), W, H, out _, out _));
            Assert.Equal(FrameProbe.Verdict.Black, FrameProbe.Check(null, W, H, out _, out _));
        }
    }
}

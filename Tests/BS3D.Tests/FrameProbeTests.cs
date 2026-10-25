using BS3D.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The benchmark's correctness probe (#988): a picture passes, a black and a flat frame are each caught, and a NaN or an infinity in the HDR scene is counted.</summary>
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

        //A texel as the GPU writes an R16G16B16A16 float one: x in the low bits, w in the high, each a System.Half
        private static HalfVector4 Texel(float x, float y, float z, float w) => new()
        {
            PackedValue = Bits(x) | Bits(y) << 16 | Bits(z) << 32 | Bits(w) << 48,
        };

        private static ulong Bits(float v) => System.BitConverter.HalfToUInt16Bits((System.Half)v);

        [Fact]
        public void ANaNOrAnInfinityInTheHdrSceneIsCounted()
        {
            var hdr = new HalfVector4[64];
            for (int i = 0; i < hdr.Length; i++) hdr[i] = Texel(0.2f, 0.4f, 0.8f, 1f);
            hdr[5] = Texel(6.5f, 3f, 1f, 1f);
            Assert.Equal(0, FrameProbe.CountNonFinite(hdr, hdr.Length, out float brightest));
            Assert.Equal(6.5f, brightest, 2);

            hdr[10] = Texel(float.NaN, 0f, 0f, 1f);
            hdr[20] = Texel(0f, float.PositiveInfinity, 0f, 1f);
            Assert.Equal(2, FrameProbe.CountNonFinite(hdr, hdr.Length, out brightest));
            Assert.Equal(6.5f, brightest, 2);

            //Only the first count are read
            Assert.Equal(0, FrameProbe.CountNonFinite(hdr, 10, out _));
        }
    }
}

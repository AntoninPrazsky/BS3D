using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The benchmark's look at a finished frame (#988's correctness probe): a driver that draws the wrong picture is a surprise
    /// no frame time shows, and the cheap catch is the frame itself, read back once a segment (<see cref="BenchmarkPage"/>).
    /// A frame is <see cref="Verdict.Black"/> when there is next to no light in it at all (a pass that drew nothing, a target
    /// a driver would not make), <see cref="Verdict.Flat"/> when it is one colour from edge to edge (a clear colour and no
    /// scene), and <see cref="Verdict.Ok"/> otherwise. Pure: <c>FrameProbeTests</c>. Every scene the benchmark tours has a
    /// sky, ground and an island in every frame, so neither failure is a scene's own look.
    /// <para>
    /// And the HDR scene behind it (<see cref="CountNonFinite"/>): a NaN or an infinity in the scene target (a shader's
    /// division by zero, a driver's broken half floats) is <see cref="Verdict.NonFinite"/> even where the finished frame
    /// passed, because the tonemap clamps it to black or white in its own pixel and the glare spreads it into blocks
    /// only where it is bright enough.
    /// </para>
    /// </summary>
    internal static class FrameProbe
    {
        //Unread: the frame could not be read back at all, which says nothing about the picture (an odd back-buffer format, a
        //platform's readback) and is never reported to the player as a driver fault
        internal enum Verdict : byte { Ok, Black, Flat, NonFinite, Unread }

        //The mean luma (0 to 1) under which a frame is black, and the spread of luma under which it is one flat colour
        internal const float BLACK = 0.01f, FLAT = 0.004f;

        /// <summary>What the frame is, and its mean luma and the spread of it (a standard deviation), sampled on a sparse grid.</summary>
        internal static Verdict Check(Color[] frame, int width, int height, out float mean, out float spread)
        {
            mean = 0f;
            spread = 0f;
            if (frame == null || width <= 0 || height <= 0 || frame.Length < width * height) return Verdict.Unread;

            //A grid of about 64 x 36 samples is plenty to tell a picture from a black or a flat one
            int stepX = Math.Max(1, width / 64), stepY = Math.Max(1, height / 36);
            double sum = 0, sumSquares = 0;
            int n = 0;
            for (int y = stepY / 2; y < height; y += stepY)
                for (int x = stepX / 2; x < width; x += stepX)
                {
                    Color c = frame[x + y * width];
                    double luma = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
                    sum += luma;
                    sumSquares += luma * luma;
                    n++;
                }
            mean = (float)(sum / n);
            spread = (float)Math.Sqrt(Math.Max(sumSquares / n - mean * (double)mean, 0.0));
            return mean < BLACK ? Verdict.Black : spread < FLAT ? Verdict.Flat : Verdict.Ok;
        }

        /// <summary>
        /// How many of the first <paramref name="count"/> HDR samples carry a NaN or an infinity in any channel, and the
        /// brightest finite colour channel among them (for the record: a target clamped to 1 would never pass it).
        /// <para>
        /// ⚠ Read from the packed bits, never through <see cref="HalfVector4.ToVector4"/>: MonoGame's half conversion does not
        /// keep a NaN or an infinity (a NaN put in came back finite in <c>FrameProbeTests</c>), which is exactly what this
        /// looks for. A half is not finite when its five exponent bits are all set.
        /// </para>
        /// </summary>
        internal static int CountNonFinite(HalfVector4[] samples, int count, out float brightest)
        {
            brightest = 0f;
            int bad = 0;
            for (int i = 0; i < count && i < samples.Length; i++)
            {
                ulong packed = samples[i].PackedValue;
                bool finite = true;
                float colour = 0f;
                for (int channel = 0; channel < 4; channel++)
                {
                    ushort bits = (ushort)(packed >> (16 * channel));
                    if ((bits & HALF_EXPONENT) == HALF_EXPONENT)
                    {
                        finite = false;
                        break;
                    }
                    //x, y and z are the colour; w the alpha
                    if (channel < 3) colour = MathF.Max(colour, (float)BitConverter.UInt16BitsToHalf(bits));
                }
                if (finite) brightest = MathF.Max(brightest, colour);
                else bad++;
            }
            return bad;
        }

        //A half's exponent bits
        private const ushort HALF_EXPONENT = 0x7C00;
    }
}

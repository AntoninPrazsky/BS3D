using Microsoft.Xna.Framework;
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
    /// </summary>
    internal static class FrameProbe
    {
        internal enum Verdict : byte { Ok, Black, Flat }

        //The mean luma (0 to 1) under which a frame is black, and the spread of luma under which it is one flat colour
        internal const float BLACK = 0.01f, FLAT = 0.004f;

        /// <summary>What the frame is, and its mean luma and the spread of it (a standard deviation), sampled on a sparse grid.</summary>
        internal static Verdict Check(Color[] frame, int width, int height, out float mean, out float spread)
        {
            mean = 0f;
            spread = 0f;
            if (frame == null || width <= 0 || height <= 0 || frame.Length < width * height) return Verdict.Black;

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
    }
}

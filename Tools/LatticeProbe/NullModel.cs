using System;

namespace BS3D.Tools.LatticeProbe
{
    /// <summary>
    /// <b>What a score means: the 99.9th percentile of the same score over pure 1/f² noise</b> (#674). A natural image
    /// has power falling with frequency about as 1/f², and a lattice-free field of that kind still produces some peak
    /// over its neighbourhood by chance; the null is how large a chance peak gets. A tile is "above the null" when its
    /// score is over that figure, so a field with none of a lattice in it is flagged on about one tile in a thousand.
    /// <para>
    /// Simulated when the tool starts, from a fixed seed, through the very pipeline that scores a real tile
    /// (<see cref="TileDetector.Score"/>): white Gaussian noise, shaped in the frequency domain to amplitude 1/f. No number is
    /// carried over from another rig — the band, the window, the median's neighbourhood and the tile size are all part
    /// of it, and moving any of them moves the null.
    /// </para>
    /// </summary>
    internal static class NullModel
    {
        private const int SEED = 20260929;

        /// <summary>The null's p99.9 over <paramref name="tiles"/> synthetic tiles (linearly interpolated between the two ranks it falls between).</summary>
        public static double Percentile999(TileDetector detector, int tiles, out double median)
        {
            int n = detector.Size;
            var random = new Random(SEED);
            double[] re = new double[n * n], im = new double[n * n];
            float[] tile = new float[n * n];
            double[] scores = new double[tiles];

            for (int t = 0; t < tiles; t++)
            {
                for (int i = 0; i < re.Length; i++)
                {
                    re[i] = Gaussian(random);
                    im[i] = 0.0;
                }

                Fft.Transform2D(re, im, n, inverse: false);

                for (int ky = 0; ky < n; ky++)
                    for (int kx = 0; kx < n; kx++)
                    {
                        double fx = kx < n / 2 ? kx : kx - n, fy = ky < n / 2 ? ky : ky - n;
                        double f = Math.Sqrt(fx * fx + fy * fy);
                        double gain = f < 1.0 ? 0.0 : 1.0 / f;
                        re[ky * n + kx] *= gain;
                        im[ky * n + kx] *= gain;
                    }

                Fft.Transform2D(re, im, n, inverse: true);

                double square = 0;
                for (int i = 0; i < re.Length; i++) square += re[i] * re[i];
                double scale = 25.0 / Math.Sqrt(square / re.Length);

                for (int i = 0; i < tile.Length; i++) tile[i] = (float)(128.0 + re[i] * scale);

                scores[t] = detector.Score(tile).Score;
            }

            Array.Sort(scores);
            median = scores[tiles / 2];

            double rank = 0.999 * (tiles - 1);
            int lower = (int)Math.Floor(rank);
            int upper = Math.Min(lower + 1, tiles - 1);
            return scores[lower] + (scores[upper] - scores[lower]) * (rank - lower);
        }

        private static double Gaussian(Random random)
        {
            double a = 1.0 - random.NextDouble(), b = random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(a)) * Math.Cos(2.0 * Math.PI * b);
        }
    }
}

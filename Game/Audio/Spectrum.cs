using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The one FFT: <c>Tools/MusicBake</c> measures where a piece's energy sits with it, and the About page's
    /// visualizer (<c>ProceduralJukebox</c>) draws bars with it. The tool compiles this file as source,
    /// so the two cannot disagree about what a band is.
    /// </summary>
    internal static class Spectrum
    {
        /// <summary>In-place radix-2 FFT over a power-of-two length. Allocation-free, so it can run every frame.</summary>
        public static void Fft(double[] re, double[] im)
        {
            int n = re.Length;

            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;

                for (; (j & bit) != 0; bit >>= 1) j ^= bit;

                j ^= bit;

                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = -2 * Math.PI / len;
                double stepRe = Math.Cos(angle), stepIm = Math.Sin(angle);

                for (int i = 0; i < n; i += len)
                {
                    double wRe = 1, wIm = 0;

                    for (int j = 0; j < len / 2; j++)
                    {
                        double uRe = re[i + j], uIm = im[i + j];
                        double vRe = re[i + j + len / 2] * wRe - im[i + j + len / 2] * wIm;
                        double vIm = re[i + j + len / 2] * wIm + im[i + j + len / 2] * wRe;

                        re[i + j] = uRe + vRe;
                        im[i + j] = uIm + vIm;
                        re[i + j + len / 2] = uRe - vRe;
                        im[i + j + len / 2] = uIm - vIm;

                        double nextRe = wRe * stepRe - wIm * stepIm;
                        wIm = wRe * stepIm + wIm * stepRe;
                        wRe = nextRe;
                    }
                }
            }
        }
    }
}

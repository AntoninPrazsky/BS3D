using System;

namespace BS3D.Tools.LatticeProbe
{
    /// <summary>
    /// A plain in-place radix-2 FFT, one and two dimensional, in double precision. The probe needs nothing more than
    /// a 256 x 256 transform per tile, so there is no library behind it: what a lattice score means depends on
    /// exactly this arithmetic, and a dependency that changed its scaling or its precision would move the null
    /// with it.
    /// </summary>
    internal static class Fft
    {
        /// <summary>Transforms <paramref name="re"/> and <paramref name="im"/> in place. Their length must be a power of two.</summary>
        public static void Transform(double[] re, double[] im, bool inverse)
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

            for (int length = 2; length <= n; length <<= 1)
            {
                double angle = 2.0 * Math.PI / length * (inverse ? 1.0 : -1.0);
                double stepRe = Math.Cos(angle), stepIm = Math.Sin(angle);

                for (int start = 0; start < n; start += length)
                {
                    double wr = 1.0, wi = 0.0;

                    for (int k = 0; k < length / 2; k++)
                    {
                        int u = start + k, v = start + k + length / 2;
                        double xr = re[v] * wr - im[v] * wi;
                        double xi = re[v] * wi + im[v] * wr;

                        re[v] = re[u] - xr;
                        im[v] = im[u] - xi;
                        re[u] += xr;
                        im[u] += xi;

                        double next = wr * stepRe - wi * stepIm;
                        wi = wr * stepIm + wi * stepRe;
                        wr = next;
                    }
                }
            }

            if (inverse)
                for (int i = 0; i < n; i++)
                {
                    re[i] /= n;
                    im[i] /= n;
                }
        }

        /// <summary>The two-dimensional transform of an <paramref name="n"/> x <paramref name="n"/> row-major array pair, rows first.</summary>
        public static void Transform2D(double[] re, double[] im, int n, bool inverse)
        {
            double[] rowRe = new double[n], rowIm = new double[n];

            for (int y = 0; y < n; y++)
            {
                Array.Copy(re, y * n, rowRe, 0, n);
                Array.Copy(im, y * n, rowIm, 0, n);
                Transform(rowRe, rowIm, inverse);
                Array.Copy(rowRe, 0, re, y * n, n);
                Array.Copy(rowIm, 0, im, y * n, n);
            }

            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    rowRe[y] = re[y * n + x];
                    rowIm[y] = im[y * n + x];
                }

                Transform(rowRe, rowIm, inverse);

                for (int y = 0; y < n; y++)
                {
                    re[y * n + x] = rowRe[y];
                    im[y * n + x] = rowIm[y];
                }
            }
        }
    }
}

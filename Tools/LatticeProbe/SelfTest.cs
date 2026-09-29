using System;
using System.Collections.Generic;
using System.Linq;

namespace BS3D.Tools.LatticeProbe
{
    /// <summary>
    /// <b>The probe checked against fields whose answer is known</b> (#674; BestPractices §10: a check counts only once its
    /// failing branch has been seen to fire on data). Synthetic 1600 x 900 star fields — one star per ten-pixel cell on
    /// 40 % of the cells, at a range of jitters — plus the two kinds of tile the detector must turn away:
    /// <list type="bullet">
    /// <item>a Poisson field, which has no lattice and must be flagged on next to no tile;</item>
    /// <item>a jittered lattice at 0.32 and 0.6 of a cell (0.32 is the margin-capped layout the game's stars had until #370),
    /// which must be flagged on most tiles and at the lattice's period or a harmonic of it;</item>
    /// <item>full-cell jitter, printed but not asserted: the jitter's own spectrum has a zero at the lattice frequency, so
    /// this spectral test cannot see it — the row is there so nobody reads a clean result as a clearance;</item>
    /// <item>a smooth gradient quantised to 8 bits, whose staircase is perfectly periodic and must be refused as flat, and
    /// a horizon (a hard edge across a textured field), which must be refused as an edge.</item>
    /// </list>
    /// </summary>
    internal static class SelfTest
    {
        private const int WIDTH = 1600, HEIGHT = 900, CELL = 10;

        /// <summary>Runs every case, prints the table, and returns whether all of the asserted ones held.</summary>
        public static bool Run(TileDetector detector, double nullScore, int stride)
        {
            Console.WriteLine("self-test: synthetic fields of known answer (10 px cell, 40 % of the cells lit, 2.5-level noise floor)");
            Console.WriteLine($"{"field",-30} {"scored",6} {"flat",5} {"edge",5} {"above",6} {"share",7} {"best",8} {"period",7} {"peaks",5}  expected");

            bool ok = true;

            ok &= Case("Poisson (no lattice)", Field(null, 1), detector, nullScore, stride,
                r => r.AboveShare <= 0.05, "at most 5 % of tiles above the null");

            ok &= Case("lattice, jitter 0.32", Field(0.32, 2), detector, nullScore, stride,
                r => r.AboveShare >= 0.80 && PeriodIsLattice(r), ">= 80 % above, at the period or a harmonic");

            ok &= Case("lattice, jitter 0.60", Field(0.60, 3), detector, nullScore, stride,
                r => r.AboveShare >= 0.50 && PeriodIsLattice(r), ">= 50 % above, at the period or a harmonic");

            ok &= Case("lattice, jitter 1.00 (blind spot)", Field(1.0, 4), detector, nullScore, stride,
                r => true, "printed, not asserted: full-cell jitter has no spectral peak");

            ok &= Case("facade of windows (designed)", Facade(), detector, nullScore, stride,
                r => r.AboveShare >= 0.80 && r.Edge <= r.Tiles / 5, ">= 80 % above and not mistaken for an edge");

            ok &= Case("band-limited texture (ring)", Ring(), detector, nullScore, stride,
                r => true, "printed, not asserted: one scale, no lattice - read the PEAKS column");

            ok &= Case("smooth gradient, 8-bit steps", Gradient(), detector, nullScore, stride,
                r => r.Scored == 0 && r.Flat > 0, "every tile refused as flat");

            ok &= Case("horizon over a textured field", Horizon(), detector, nullScore, stride,
                r => r.Above == 0, "no tile above the null (the edge tiles refused)");

            Console.WriteLine(ok ? "self-test: PASSED" : "self-test: FAILED");
            return ok;
        }

        private static bool Case(string name, LumaImage image, TileDetector detector, double nullScore, int stride,
            Func<ImageReport, bool> assertion, string expected)
        {
            ImageReport r = ImageScan.Scan(name, image, detector, nullScore, stride, crosshairHalf: 0);
            bool held = assertion(r);

            string period = r.Flagged.Count > 0 ? MedianPeriod(r).ToString("F2") : "-";
            Console.WriteLine($"{name,-30} {r.Scored,6} {r.Flat,5} {r.Edge,5} {r.Above,6} {r.AboveShare,6:P0} {(r.Best is ScoredTile b ? b.Score.Score : 0),8:F1} {period,7} {(r.Best is ScoredTile p ? p.Score.Peaks : 0),5}  {(held ? "ok" : "FAILED")}: {expected}");
            return held;
        }

        private static double MedianPeriod(ImageReport r)
        {
            double[] periods = r.Flagged.Select(t => t.Score.PeriodPixels).OrderBy(p => p).ToArray();
            return periods[periods.Length / 2];
        }

        //The lattice's fundamental (10 px), its diagonal (10 / sqrt 2) and the second harmonic (5) all belong to it
        private static bool PeriodIsLattice(ImageReport r)
        {
            if (r.Flagged.Count == 0) return false;

            double period = MedianPeriod(r);
            return new[] { CELL, CELL / Math.Sqrt(2.0), CELL / 2.0 }.Any(p => Math.Abs(period - p) < 0.6);
        }

        //Stars on a jittered lattice (jitter is a share of the cell, 1 = anywhere in it), or at random when null
        private static LumaImage Field(double? jitter, int seed)
        {
            var random = new Random(seed);
            float[] luma = Background(random);

            int cellsX = WIDTH / CELL, cellsY = HEIGHT / CELL;
            var stars = new List<(double X, double Y, double Brightness)>();

            for (int cy = 0; cy < cellsY; cy++)
                for (int cx = 0; cx < cellsX; cx++)
                {
                    if (random.NextDouble() >= 0.4) continue;

                    double brightness = 60 + random.NextDouble() * 195;

                    if (jitter is double j)
                        stars.Add(((cx + 0.5 + (random.NextDouble() - 0.5) * j) * CELL,
                                   (cy + 0.5 + (random.NextDouble() - 0.5) * j) * CELL, brightness));
                    else
                        stars.Add((random.NextDouble() * WIDTH, random.NextDouble() * HEIGHT, brightness));
                }

            foreach (var (x, y, brightness) in stars) Splat(luma, x, y, brightness);

            Quantise(luma);
            return new LumaImage(WIDTH, HEIGHT, luma);
        }

        //A wall of lit and dark windows on a fixed pitch: the designed lattice the probe must call one (the Grid scene and the
        //neon city's windows are its real-data counterparts), with rows of strong horizontal edges it must not take for a horizon
        private static LumaImage Facade()
        {
            var random = new Random(7);
            float[] luma = Background(random);

            for (int y = 0; y < HEIGHT; y++)
                for (int x = 0; x < WIDTH; x++)
                {
                    int px = x % 14, py = y % 20;
                    if (px < 3 || px >= 11 || py < 4 || py >= 16) continue;

                    int windowX = x / 14, windowY = y / 20;
                    var window = new Random(windowX * 7919 + windowY * 104729);
                    luma[y * WIDTH + x] = window.NextDouble() < 0.6 ? 150f + (float)(window.NextDouble() * 80) : 70f;
                }

            Quantise(luma);
            return new LumaImage(WIDTH, HEIGHT, luma);
        }

        //A Gaussian random field with a RING spectrum: a hundred and fifty plane waves of wavelength 12 px (+-12 %) in random
        //directions and phases. One characteristic scale and no lattice - what a cellular or a single-octave noise looks like
        //to the spectrum - so it shows what a texture does to a peak-over-neighbourhood score: its speckle can stand over a
        //neighbourhood that is mostly out of the ring. The PEAKS column is what tells it from a lattice's few discrete ones.
        private static LumaImage Ring()
        {
            const int W = 768, H = 384;
            var random = new Random(11);
            const int WAVES = 150;
            double[] kx = new double[WAVES], ky = new double[WAVES], phase = new double[WAVES];

            for (int i = 0; i < WAVES; i++)
            {
                double wavelength = 12.0 * (0.88 + 0.24 * random.NextDouble());
                double angle = random.NextDouble() * 2.0 * Math.PI;
                kx[i] = 2.0 * Math.PI / wavelength * Math.Cos(angle);
                ky[i] = 2.0 * Math.PI / wavelength * Math.Sin(angle);
                phase[i] = random.NextDouble() * 2.0 * Math.PI;
            }

            float[] luma = new float[W * H];
            double gain = 22.0 / Math.Sqrt(WAVES / 2.0);

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    double sum = 0;
                    for (int i = 0; i < WAVES; i++) sum += Math.Cos(kx[i] * x + ky[i] * y + phase[i]);
                    luma[y * W + x] = (float)(120.0 + gain * sum);
                }

            Quantise(luma);
            return new LumaImage(W, H, luma);
        }

        private static LumaImage Gradient()
        {
            float[] luma = new float[WIDTH * HEIGHT];

            for (int y = 0; y < HEIGHT; y++)
                for (int x = 0; x < WIDTH; x++) luma[y * WIDTH + x] = 30f + 60f * y / HEIGHT + 8f * x / WIDTH;

            Quantise(luma);
            return new LumaImage(WIDTH, HEIGHT, luma);
        }

        //A textured field over a bright sky with a hard, slightly tilted horizon: the line every outdoor capture has
        private static LumaImage Horizon()
        {
            var random = new Random(9);
            float[] luma = Background(random);

            for (int y = 0; y < HEIGHT; y++)
                for (int x = 0; x < WIDTH; x++)
                {
                    double horizon = 420 + x * 0.01;
                    if (y < horizon) luma[y * WIDTH + x] = 190f + 25f * y / HEIGHT + (float)(random.NextDouble() * 2.5);
                }

            for (int i = 0; i < 4000; i++) Splat(luma, random.NextDouble() * WIDTH, 450 + random.NextDouble() * 400, 40 + random.NextDouble() * 60);

            Quantise(luma);
            return new LumaImage(WIDTH, HEIGHT, luma);
        }

        private static float[] Background(Random random)
        {
            float[] luma = new float[WIDTH * HEIGHT];

            for (int y = 0; y < HEIGHT; y++)
                for (int x = 0; x < WIDTH; x++)
                    luma[y * WIDTH + x] = 40f + 20f * y / HEIGHT + (float)(Noise(random) * 2.5);

            return luma;
        }

        private static double Noise(Random random)
        {
            double a = 1.0 - random.NextDouble(), b = random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(a)) * Math.Cos(2.0 * Math.PI * b);
        }

        private static void Splat(float[] luma, double x, double y, double brightness)
        {
            const double SIGMA = 0.8;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);

            for (int dy = -3; dy <= 4; dy++)
                for (int dx = -3; dx <= 4; dx++)
                {
                    int px = x0 + dx, py = y0 + dy;
                    if (px < 0 || py < 0 || px >= WIDTH || py >= HEIGHT) continue;

                    double ex = px + 0.5 - x, ey = py + 0.5 - y;
                    luma[py * WIDTH + px] += (float)(brightness * Math.Exp(-(ex * ex + ey * ey) / (2 * SIGMA * SIGMA)));
                }
        }

        private static void Quantise(float[] luma)
        {
            for (int i = 0; i < luma.Length; i++) luma[i] = (float)Math.Round(Math.Clamp(luma[i], 0f, 255f));
        }
    }
}

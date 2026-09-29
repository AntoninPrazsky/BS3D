using System;
using System.Collections.Generic;

namespace BS3D.Tools.LatticeProbe
{
    /// <summary>One scored tile of an image: where its top-left corner is, and its <see cref="TileScore"/>.</summary>
    internal readonly record struct ScoredTile(int X, int Y, TileScore Score);

    /// <summary>What a scan of one image found: how many tiles it looked at and turned away, and how many of the rest are over the null.</summary>
    internal sealed class ImageReport
    {
        public string Name { get; init; }
        public int Tiles { get; set; }
        public int Scored { get; set; }
        public int Flat { get; set; }
        public int Edge { get; set; }
        public int Masked { get; set; }
        public int Above { get; set; }
        public ScoredTile? Best { get; set; }
        public List<ScoredTile> Flagged { get; } = new();

        /// <summary>The share of the scored tiles that are over the null, 0-1 (0 when none was scored).</summary>
        public double AboveShare => Scored == 0 ? 0.0 : Above / (double)Scored;
    }

    /// <summary>
    /// Runs a <see cref="TileDetector"/> over an image in overlapping tiles and tallies them against the null. The
    /// crosshair is masked because it is the one periodic-looking thing every Testbed capture carries on purpose.
    /// </summary>
    internal static class ImageScan
    {
        public static ImageReport Scan(string name, LumaImage image, TileDetector detector, double nullScore, int stride, int crosshairHalf)
        {
            int n = detector.Size;
            var report = new ImageReport { Name = name };
            float[] tile = new float[n * n];

            int centreX = image.Width / 2, centreY = image.Height / 2;

            for (int y0 = 0; y0 + n <= image.Height; y0 += stride)
                for (int x0 = 0; x0 + n <= image.Width; x0 += stride)
                {
                    report.Tiles++;

                    bool underCrosshair = crosshairHalf > 0
                        && x0 < centreX + crosshairHalf && x0 + n > centreX - crosshairHalf
                        && y0 < centreY + crosshairHalf && y0 + n > centreY - crosshairHalf;

                    if (underCrosshair)
                    {
                        report.Masked++;
                        continue;
                    }

                    for (int y = 0; y < n; y++)
                        Array.Copy(image.Luma, (y0 + y) * image.Width + x0, tile, y * n, n);

                    if (!detector.TryScore(tile, out TileScore score, out string rejected))
                    {
                        if (rejected == "edge") report.Edge++;
                        else report.Flat++;
                        continue;
                    }

                    report.Scored++;
                    var scored = new ScoredTile(x0, y0, score);

                    if (report.Best is not ScoredTile best || score.Score > best.Score.Score) report.Best = scored;

                    if (score.Score > nullScore)
                    {
                        report.Above++;
                        report.Flagged.Add(scored);
                    }
                }

            return report;
        }
    }
}

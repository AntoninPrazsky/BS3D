using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BS3D.Tools.LatticeProbe
{
    /// <summary>
    /// <b>Does this image show a lattice?</b> (#674) A measuring instrument for the complaint that came back nine times in
    /// four months — procedural "randomness" (water, clouds, grass, stars, craters, flowers) that a player's eye reads as a
    /// repeating grid from certain angles and that the author of the shader, a person or a model, cannot see: the
    /// eye completes a pattern, a sample judged "random" by the one who wrote it is not. The probe scores tiles of a
    /// screenshot by the height of their strongest spectral peak over the spectrum round it and reports how many stand
    /// above what pure 1/f² noise gives by chance. See <see cref="TileDetector"/> for the method and, as much, for what it
    /// cannot see, and <see cref="SelfTest"/> for the fields it is checked against.
    /// <para>
    /// <b>An instrument, not a gate</b> — it always exits 0 on a scan — until it has been seen to fire on a real defect
    /// in a shipped scene; only <c>--selftest</c> exits non-zero (3), because that one has a known answer. The captures come
    /// from <c>Tools/lattice-sweep.ps1</c>: the Testbed at pinned vantages, <c>nopost</c> (the film grain would hide
    /// exactly what is being looked for) and the overlay off.
    /// </para>
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            int tile = 256, stride = 128, nullTiles = 200, crosshair = 48;
            bool selfTest = false, listFlagged = false;
            var inputs = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--selftest": selfTest = true; break;
                    case "--flagged": listFlagged = true; break;
                    case "--tile": tile = Int(args, ref i); break;
                    case "--stride": stride = Int(args, ref i); break;
                    case "--null-tiles": nullTiles = Int(args, ref i); break;
                    case "--crosshair": crosshair = Int(args, ref i); break;
                    case "--help": case "-h": Usage(); return 0;
                    default: inputs.Add(args[i]); break;
                }
            }

            if (!selfTest && inputs.Count == 0)
            {
                Usage();
                return 0;
            }

            var detector = new TileDetector(tile);
            double nullScore = NullModel.Percentile999(detector, nullTiles, out double nullMedian);
            Console.WriteLine($"null: p99.9 = {nullScore:F1} over {nullTiles} synthetic 1/f^2 tiles of {tile} px (median {nullMedian:F1}); band {TileDetector.MIN_PERIOD}-{TileDetector.MAX_PERIOD} px, stride {stride}");

            if (selfTest) return SelfTest.Run(detector, nullScore, stride) ? 0 : 3;

            var files = new List<string>();
            foreach (string input in inputs)
            {
                if (Directory.Exists(input)) files.AddRange(Directory.GetFiles(input, "*.png").OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
                else if (File.Exists(input)) files.Add(input);
                else Console.Error.WriteLine($"not found: {input}");
            }

            Console.WriteLine($"{"image",-44} {"tiles",5} {"scored",6} {"flat",5} {"edge",5} {"mask",5} {"above",6} {"share",6} {"best",9} {"period",7} {"angle",6}  at");

            foreach (string file in files)
            {
                LumaImage image;
                try { image = PngReader.Load(file); }
                catch (Exception e) when (e is InvalidDataException or NotSupportedException)
                {
                    Console.Error.WriteLine(e.Message);
                    continue;
                }

                ImageReport r = ImageScan.Scan(Path.GetFileNameWithoutExtension(file), image, detector, nullScore, stride, crosshair);
                string name = r.Name.Length > 44 ? r.Name[..44] : r.Name;

                if (r.Best is ScoredTile best)
                    Console.WriteLine($"{name,-44} {r.Tiles,5} {r.Scored,6} {r.Flat,5} {r.Edge,5} {r.Masked,5} {r.Above,6} {r.AboveShare,6:P0} {best.Score.Score,9:F1} {best.Score.PeriodPixels,7:F2} {best.Score.AngleDegrees,6:F0}  ({best.X},{best.Y})");
                else
                    Console.WriteLine($"{name,-44} {r.Tiles,5} {r.Scored,6} {r.Flat,5} {r.Edge,5} {r.Masked,5} {r.Above,6} {r.AboveShare,6:P0} {"-",9} {"-",7} {"-",6}");

                if (listFlagged)
                    foreach (ScoredTile t in r.Flagged.OrderByDescending(t => t.Score.Score).Take(8))
                        Console.WriteLine($"    flagged ({t.X},{t.Y}) score {t.Score.Score:F1} period {t.Score.PeriodPixels:F2} px at {t.Score.AngleDegrees:F0} deg");
            }

            return 0;
        }

        private static int Int(string[] args, ref int i)
        {
            if (i + 1 >= args.Length || !int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                throw new ArgumentException($"{args[i]} needs a whole number");

            i++;
            return value;
        }

        private static void Usage()
        {
            Console.WriteLine("LatticeProbe <image.png | folder>... [--tile 256] [--stride 128] [--crosshair 48] [--null-tiles 200] [--flagged]");
            Console.WriteLine("LatticeProbe --selftest        synthetic fields of known answer; exit 3 if one fails");
            Console.WriteLine();
            Console.WriteLine("Scores tiles of a screenshot by the strongest spectral peak over its neighbourhood (periods 3-23 px) and counts those over the");
            Console.WriteLine("99.9th percentile of pure 1/f^2 noise. A column of zeros is not a clearance: a recurrence longer than a tile, or items jittered by a whole");
            Console.WriteLine("cell, are invisible to it (docs/formats-and-tools.md, \"The lattice probe\"). Captures: Tools/lattice-sweep.ps1.");
        }
    }
}

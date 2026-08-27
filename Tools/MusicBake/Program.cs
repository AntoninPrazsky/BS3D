using BS3D.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace BS3D.Tools.MusicBake
{
    /// <summary>
    /// Renders the game's compositions to .wav and measures them.
    /// <para>
    /// It exists because music is the one part of this game that cannot be judged from a screenshot and cannot
    /// comfortably be judged inside the game either: hearing a piece there means playing a level of the right
    /// chapter for two minutes. This writes every piece to a file that opens in any player, and prints the
    /// numbers the pieces are held to beside it — the loudness a switch between two of them must not jump on,
    /// the low band #264 measured a piece out of the set on, the treble band #201 filed a complaint about, and
    /// the two-second envelope that says WHEN a piece arrives, which is the other half of that complaint.
    /// </para>
    /// </summary>
    internal static class Program
    {
        private const int SAMPLE_RATE = 44100;

        private static int Main(string[] args)
        {
            string outDir = "MusicBake";
            bool wav = true;
            string only = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (Is(arg, "--out") && i + 1 < args.Length) outDir = args[++i];
                else if (Is(arg, "--theme") && i + 1 < args.Length) only = args[++i];
                else if (Is(arg, "--no-wav")) wav = false;
                else
                {
                    Console.WriteLine("usage: MusicBake [--out <dir>] [--theme <name>] [--no-wav]");
                    return 2;
                }
            }

            if (wav) Directory.CreateDirectory(outDir);

            List<string> names = new();

            foreach (MusicTheme theme in Enum.GetValues<MusicTheme>())
                if (only == null || Is(only, theme.ToString())) names.Add(theme.ToString());

            if (only == null || Is(only, "menu")) names.Add("Menu");

            Console.WriteLine("piece            secs  bake  entry   peak    rms   bal  mono | <100 100-200 200-500  500-2k   2k-6k    6k+ |  head   tail");

            foreach (string name in names)
            {
                bool menu = Is(name, "Menu");

                Stopwatch clock = Stopwatch.StartNew();

                float[] mix = menu
                    ? ProceduralMusic.RenderMenu()
                    : ProceduralMusic.Render(Enum.Parse<MusicTheme>(name));

                clock.Stop();

                //Where a LEVEL comes in on the piece (#201). The front end's loop has no entry of its own —
                //it is a lobby, and it plays from the top.
                double entry = menu
                    ? -1
                    : ProceduralMusic.EntryOffset(Enum.Parse<MusicTheme>(name)) / 4.0 / SAMPLE_RATE;

                Report(name, mix, clock.Elapsed.TotalMilliseconds, entry);

                if (wav) WriteWav(Path.Combine(outDir, $"{name.ToLowerInvariant()}.wav"), mix);
            }

            if (wav) Console.WriteLine($"\nWritten to {Path.GetFullPath(outDir)}");

            return 0;
        }

        private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>One line of numbers per rendering, with the envelope under it.</summary>
        private static void Report(string name, float[] mix, double bakeMs, double entrySeconds)
        {
            int frames = mix.Length / 2;
            double seconds = frames / (double)SAMPLE_RATE;

            double peak = 0, sumL = 0, sumR = 0, sumMono = 0;

            for (int f = 0; f < frames; f++)
            {
                double l = mix[f * 2], r = mix[f * 2 + 1];

                peak = Math.Max(peak, Math.Max(Math.Abs(l), Math.Abs(r)));
                sumL += l * l;
                sumR += r * r;

                double mono = (l + r) * 0.5;
                sumMono += mono * mono;
            }

            double rmsL = Math.Sqrt(sumL / frames), rmsR = Math.Sqrt(sumR / frames);
            double rms = Math.Sqrt((sumL + sumR) / (2.0 * frames));
            double monoRms = Math.Sqrt(sumMono / frames);

            double[] bands = Bands(mix, frames);

            //Where one playing of the piece joins the next: both ends have to be at silence, or the seam is a
            //click. Half a second at each end, which is longer than any fade this file writes is steep.
            double head = WindowRms(mix, frames, 0, SAMPLE_RATE / 2);
            double tail = WindowRms(mix, frames, frames - SAMPLE_RATE / 2, SAMPLE_RATE / 2);

            Console.WriteLine($"{name,-14} {seconds,6:0.0} {bakeMs,5:0} {(entrySeconds < 0 ? "-" : entrySeconds.ToString("0.0")),6} {Db(peak),6:0.0} {Db(rms),6:0.0} "
                + $"{Db(rmsL) - Db(rmsR),5:+0.0;-0.0;0.0} {Db(monoRms) - Db(rms),5:+0.0;-0.0;0.0} | "
                + $"{bands[0],4:0.0} {bands[1],7:0.0} {bands[2],7:0.0} {bands[3],7:0.0} {bands[4],7:0.0} {bands[5],6:0.0} | "
                + $"{Db(head),5:0.0} {Db(tail),6:0.0}");

            Envelope(mix, frames);
        }

        /// <summary>
        /// The arrangement as a column of numbers: the RMS of every two seconds of the piece, in dB under its
        /// own loudest two seconds. A section 20 dB down is a hole, and a piece whose first ten readings are
        /// all far down does not start for twenty seconds — which is what #201 reported by ear.
        /// </summary>
        private static void Envelope(float[] mix, int frames)
        {
            int bucket = 2 * SAMPLE_RATE;
            int buckets = Math.Max(1, frames / bucket);

            double[] db = new double[buckets];
            double loudest = double.NegativeInfinity;

            for (int b = 0; b < buckets; b++)
            {
                db[b] = Db(WindowRms(mix, frames, b * bucket, bucket));
                loudest = Math.Max(loudest, db[b]);
            }

            Console.Write("               envelope, 2 s a reading, dB under the loudest:");

            for (int b = 0; b < buckets; b++)
            {
                if (b % 20 == 0) Console.Write("\n                 ");

                Console.Write($"{db[b] - loudest,4:0}");
            }

            Console.WriteLine();
        }

        private static double WindowRms(float[] mix, int frames, int from, int count)
        {
            from = Math.Max(0, from);
            count = Math.Min(count, frames - from);

            if (count <= 0) return 0;

            double sum = 0;

            for (int f = from; f < from + count; f++)
            {
                double l = mix[f * 2], r = mix[f * 2 + 1];
                sum += l * l + r * r;
            }

            return Math.Sqrt(sum / (2.0 * count));
        }

        /// <summary>
        /// Where the energy sits, as a percentage of the whole in six bands: under 100 Hz, 100–200, 200–500,
        /// 500–2k, 2k–6k and over 6k. Measured off the mono fold, since that is what a listener on one speaker
        /// hears and the balance #264 held a piece to.
        /// </summary>
        private static double[] Bands(float[] mix, int frames)
        {
            const int SIZE = 4096;
            const int HOP = 2048;

            double[] edges = { 100, 200, 500, 2000, 6000 };
            double[] power = new double[6];

            double[] window = new double[SIZE];
            for (int i = 0; i < SIZE; i++) window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / SIZE);

            double[] re = new double[SIZE], im = new double[SIZE];

            for (int at = 0; at + SIZE <= frames; at += HOP)
            {
                for (int i = 0; i < SIZE; i++)
                {
                    double mono = (mix[(at + i) * 2] + mix[(at + i) * 2 + 1]) * 0.5;

                    re[i] = mono * window[i];
                    im[i] = 0;
                }

                Fft(re, im);

                for (int bin = 1; bin < SIZE / 2; bin++)
                {
                    double hz = bin * (double)SAMPLE_RATE / SIZE;
                    double p = re[bin] * re[bin] + im[bin] * im[bin];

                    int band = 0;
                    while (band < edges.Length && hz >= edges[band]) band++;

                    power[band] += p;
                }
            }

            double total = 0;
            foreach (double p in power) total += p;

            double[] percent = new double[6];
            for (int i = 0; i < 6; i++) percent[i] = total > 0 ? 100.0 * power[i] / total : 0;

            return percent;
        }

        /// <summary>In-place radix-2 FFT. Small enough to keep here: this tool measures, it does not synthesize.</summary>
        private static void Fft(double[] re, double[] im)
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

        private static double Db(double linear) => linear <= 1e-9 ? -180 : 20 * Math.Log10(linear);

        /// <summary>The same 16-bit stereo PCM the game submits, in a .wav wrapper any player opens.</summary>
        private static void WriteWav(string path, float[] mix)
        {
            int frames = mix.Length / 2;
            int dataBytes = frames * 4;

            using FileStream file = File.Create(path);
            using BinaryWriter writer = new(file);

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);            //PCM
            writer.Write((short)2);            //stereo
            writer.Write(SAMPLE_RATE);
            writer.Write(SAMPLE_RATE * 4);     //bytes a second
            writer.Write((short)4);            //block align
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            for (int i = 0; i < mix.Length; i++)
                writer.Write((short)(Math.Clamp(mix[i], -1f, 1f) * short.MaxValue));
        }
    }
}

using BS3D.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace BS3D.Tools.MusicBake
{
    /// <summary>
    /// Renders the game's procedural compositions to .wav and measures them, and builds the game's music tracks
    /// out of the generated masters (#443).
    /// <para>
    /// It exists because music is the one part of this game that cannot be judged from a screenshot and cannot
    /// comfortably be judged inside the game either: hearing a piece there means playing a level of the right
    /// chapter for two minutes. This writes every piece to a file that opens in any player, and prints the
    /// numbers the pieces are held to beside it — the loudness a switch between two of them must not jump on,
    /// the low band #264 measured a piece out of the set on, the treble band #201 filed a complaint about, and
    /// the two-second envelope that says WHEN a piece arrives, which is the other half of that complaint.
    /// </para>
    /// <para>
    /// <b>--tracks</b> is the other half since #443: the music a level plays is a generated recording now, and
    /// the procedural pieces are the About page's. The masters in <c>Research/AI-Music</c> are 32-bit float and
    /// louder than anything the game's mix was tuned against, with peaks above full scale; this brings each one
    /// to the loudness the procedural piece in its slot measured, holds its peaks under full scale, and writes
    /// the 16-bit PCM <c>GameMusic</c> submits to <c>Game/Music</c> — printing the same table, so the old and
    /// the new sit in one set of numbers.
    /// </para>
    /// </summary>
    internal static class Program
    {
        private const int SAMPLE_RATE = 44100;

        /// <summary>
        /// Where a generated theme is brought to, as combined L+R RMS in dBFS: what the five procedural themes
        /// measure through this tool (−14.9 to −15.2). The game's mix — music under the effects, the fanfare over
        /// the music — was tuned against that level, so a track arriving at it changes nothing about the balance.
        /// </summary>
        private const double THEME_RMS_DB = -15.0;

        /// <summary>The front end's loop measured −19.5: a lobby under a dance floor, and the replacement stays one.</summary>
        private const double MENU_RMS_DB = -19.5;

        /// <summary>
        /// Where the peak shaper starts, −1 dBFS. Above it a sample is bent into the remaining headroom along a
        /// tanh knee, so nothing reaches full scale; below it nothing is touched. It is memoryless — the same
        /// curve on every sample — which is what keeps a loop's seam exactly as seamless as the master's.
        /// </summary>
        private const float KNEE = 0.891f;

        /// <summary>
        /// Master (in <c>Research/AI-Music</c>, without .wav) → the file the game plays (in <c>Game/Music</c>) and
        /// the loudness it is brought to. The game finds its files by name — a theme's own name, then its variants
        /// as <c>name-*.wav</c> in order — so adding a variant is a line here and nothing in the game.
        /// </summary>
        private static readonly (string Master, string Track, double RmsDb)[] TRACKS =
        {
            ("menu-loop-v2", "menu", MENU_RMS_DB),
            ("theme-pulse", "pulse", THEME_RMS_DB),
            ("theme-bohemia", "bohemia", THEME_RMS_DB),
            ("theme-nocturne", "nocturne", THEME_RMS_DB),
            ("theme-mural", "mural", THEME_RMS_DB),
            ("theme-ember", "ember", THEME_RMS_DB),
            ("theme-ember-punk-01", "ember-punk-01", THEME_RMS_DB),
            ("theme-ember-punk-02", "ember-punk-02", THEME_RMS_DB),
            ("theme-ember-punk-03", "ember-punk-03", THEME_RMS_DB),
            ("theme-ember-punk-04", "ember-punk-04", THEME_RMS_DB),
            ("theme-ember-punk-05", "ember-punk-05", THEME_RMS_DB),
        };

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
                else if (Is(arg, "--tracks")) return BuildTracks(wav);
                else
                {
                    Console.WriteLine("usage: MusicBake [--out <dir>] [--theme <name>] [--no-wav] | --tracks [--no-wav]");
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

                Report(name, mix, SAMPLE_RATE, clock.Elapsed.TotalMilliseconds, entry);

                if (wav) WriteWav(Path.Combine(outDir, $"{name.ToLowerInvariant()}.wav"), mix, SAMPLE_RATE);
            }

            if (wav) Console.WriteLine($"\nWritten to {Path.GetFullPath(outDir)}");

            return 0;
        }

        private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Builds <c>Game/Music</c> from the masters: each brought to its loudness, its peaks shaped under full
        /// scale, written as 16-bit PCM at the master's own rate. <paramref name="wav"/> false measures without
        /// writing. The table's "bake" column is the processing time and "entry" is empty — a generated loop is
        /// cut from its render's body, so a level opens on it wherever it opens.
        /// </summary>
        private static int BuildTracks(bool wav)
        {
            string repo = FindRepo();
            if (repo == null)
            {
                Console.WriteLine("MusicBake --tracks: run from inside the repository (no Game.sln with a docs folder above it)");
                return 1;
            }

            string masters = Path.Combine(repo, "Research", "AI-Music");
            string tracks = Path.Combine(repo, "Game", "Music");
            if (wav) Directory.CreateDirectory(tracks);

            Console.WriteLine("track            secs  bake  entry   peak    rms   bal  mono | <100 100-200 200-500  500-2k   2k-6k    6k+ |  head   tail");

            foreach ((string master, string track, double rmsDb) in TRACKS)
            {
                string path = Path.Combine(masters, master + ".wav");
                if (!File.Exists(path))
                {
                    Console.WriteLine($"MusicBake --tracks: missing master {path}");
                    return 1;
                }

                Stopwatch clock = Stopwatch.StartNew();

                (float[] mix, int rate) = ReadWav(path);

                double sum = 0;
                foreach (float s in mix) sum += (double)s * s;
                double gainDb = rmsDb - Db(Math.Sqrt(sum / mix.Length));
                float gain = (float)Math.Pow(10, gainDb / 20);

                int shaped = 0;
                for (int i = 0; i < mix.Length; i++)
                {
                    float x = mix[i] * gain;
                    float a = MathF.Abs(x);

                    if (a > KNEE)
                    {
                        x = MathF.CopySign(KNEE + (1f - KNEE) * MathF.Tanh((a - KNEE) / (1f - KNEE)), x);
                        shaped++;
                    }

                    mix[i] = x;
                }

                clock.Stop();

                Report(track, mix, rate, clock.Elapsed.TotalMilliseconds, -1);
                Console.WriteLine($"                 from {master}.wav at {rate} Hz: gain {gainDb:+0.0;-0.0} dB, "
                    + $"{100.0 * shaped / mix.Length:0.000} % of samples shaped above -1 dBFS");

                if (wav) WriteWav(Path.Combine(tracks, track + ".wav"), mix, rate);
            }

            if (wav) Console.WriteLine($"\nWritten to {tracks}");

            return 0;
        }

        //By landmark rather than by counting "..", as SemanticSearch does
        private static string FindRepo()
        {
            for (DirectoryInfo dir = new(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Game.sln")) && Directory.Exists(Path.Combine(dir.FullName, "docs")))
                    return dir.FullName;

            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Game.sln")) && Directory.Exists(Path.Combine(dir.FullName, "docs")))
                    return dir.FullName;

            return null;
        }

        /// <summary>
        /// A stereo .wav as interleaved floats: 32-bit IEEE float (what ace-synth's wav32 writes, format tag 3)
        /// or 16-bit PCM. The chunks are walked rather than a 44-byte header assumed, since a writer may put
        /// other chunks before the data.
        /// </summary>
        private static (float[] Mix, int Rate) ReadWav(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 12 || data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F')
                throw new InvalidDataException($"{path} is not a RIFF file");

            int tag = 0, channels = 0, rate = 0, bits = 0, dataAt = -1, dataLength = 0;

            for (int pos = 12; pos + 8 <= data.Length;)
            {
                int size = BitConverter.ToInt32(data, pos + 4);

                if (data[pos] == 'f' && data[pos + 1] == 'm' && data[pos + 2] == 't')
                {
                    tag = BitConverter.ToInt16(data, pos + 8);
                    channels = BitConverter.ToInt16(data, pos + 10);
                    rate = BitConverter.ToInt32(data, pos + 12);
                    bits = BitConverter.ToInt16(data, pos + 22);
                }
                else if (data[pos] == 'd' && data[pos + 1] == 'a' && data[pos + 2] == 't' && data[pos + 3] == 'a')
                {
                    dataAt = pos + 8;
                    dataLength = Math.Min(size, data.Length - dataAt);
                }

                pos += 8 + size + (size & 1);
            }

            if (dataAt < 0 || channels != 2)
                throw new InvalidDataException($"{path}: expected a stereo data chunk (channels {channels})");

            float[] mix;

            if (tag == 3 && bits == 32)
            {
                mix = new float[dataLength / 4];
                Buffer.BlockCopy(data, dataAt, mix, 0, mix.Length * 4);
            }
            else if (tag == 1 && bits == 16)
            {
                mix = new float[dataLength / 2];
                for (int i = 0; i < mix.Length; i++) mix[i] = BitConverter.ToInt16(data, dataAt + i * 2) / 32768f;
            }
            else throw new InvalidDataException($"{path}: format tag {tag} at {bits} bits is neither float32 nor PCM16");

            return (mix, rate);
        }

        /// <summary>One line of numbers per rendering, with the envelope under it.</summary>
        private static void Report(string name, float[] mix, int rate, double bakeMs, double entrySeconds)
        {
            int frames = mix.Length / 2;
            double seconds = frames / (double)rate;

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

            double[] bands = Bands(mix, frames, rate);

            //Where one playing of the piece joins the next: both ends have to be at silence, or the seam is a
            //click. Half a second at each end, which is longer than any fade this file writes is steep. A
            //generated loop is the other case — cut so its tail runs straight on into its head — so for a
            //track the two figures should instead be close to each other and to the rms.
            double head = WindowRms(mix, frames, 0, rate / 2);
            double tail = WindowRms(mix, frames, frames - rate / 2, rate / 2);

            Console.WriteLine($"{name,-14} {seconds,6:0.0} {bakeMs,5:0} {(entrySeconds < 0 ? "-" : entrySeconds.ToString("0.0")),6} {Db(peak),6:0.0} {Db(rms),6:0.0} "
                + $"{Db(rmsL) - Db(rmsR),5:+0.0;-0.0;0.0} {Db(monoRms) - Db(rms),5:+0.0;-0.0;0.0} | "
                + $"{bands[0],4:0.0} {bands[1],7:0.0} {bands[2],7:0.0} {bands[3],7:0.0} {bands[4],7:0.0} {bands[5],6:0.0} | "
                + $"{Db(head),5:0.0} {Db(tail),6:0.0}");

            Envelope(mix, frames, rate);
        }

        /// <summary>
        /// The arrangement as a column of numbers: the RMS of every two seconds of the piece, in dB under its
        /// own loudest two seconds. A section 20 dB down is a hole, and a piece whose first ten readings are
        /// all far down does not start for twenty seconds — which is what #201 reported by ear.
        /// </summary>
        private static void Envelope(float[] mix, int frames, int rate)
        {
            int bucket = 2 * rate;
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
        private static double[] Bands(float[] mix, int frames, int rate)
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

                Spectrum.Fft(re, im);

                for (int bin = 1; bin < SIZE / 2; bin++)
                {
                    double hz = bin * (double)rate / SIZE;
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


        private static double Db(double linear) => linear <= 1e-9 ? -180 : 20 * Math.Log10(linear);

        /// <summary>The same 16-bit stereo PCM the game submits, in a .wav wrapper any player opens.</summary>
        private static void WriteWav(string path, float[] mix, int rate)
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
            writer.Write(rate);
            writer.Write(rate * 4);            //bytes a second
            writer.Write((short)4);            //block align
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            for (int i = 0; i < mix.Length; i++)
                writer.Write((short)(Math.Clamp(mix[i], -1f, 1f) * short.MaxValue));
        }
    }
}

using BS3D.Audio;
using OggVorbisEncoder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
    /// it to <c>Game/Music</c> as the Ogg Vorbis <c>GameMusic</c> decodes (#444) — printing the same table, so
    /// the old and the new sit in one set of numbers. Every track it encodes it also decodes again, through the
    /// game's own <see cref="OggTrack"/>, and says what came back: the length a loop depends on to the frame,
    /// what the codec did at the loop's two edges, and how long the game's load of all of them takes.
    /// </para>
    /// </summary>
    internal static class Program
    {
        private const int SAMPLE_RATE = 44100;

        /// <summary>
        /// How far <see cref="ProceduralMusic.LIMITER_DRIVE"/>'s stored figure may sit from a fresh measurement
        /// before this tool calls it stale (#464). Not zero: two runs of the SAME deterministic render (#229)
        /// should in principle agree exactly, but the summed-square RMS in <c>ComputeDrive</c> is a double
        /// accumulated over several million adds in a different order than whatever produced the stored
        /// figure's own run, so float/double rounding alone earns a hair of slack. A real drift from an edited
        /// composition is orders of magnitude past this.
        /// </summary>
        private const float DRIVE_TOLERANCE = 0.0001f;

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
        /// The Vorbis quality the tracks are encoded at, on libvorbis's scale of −0.1 to 1 (oggenc's <c>-q</c> over
        /// ten). <c>--quality</c> overrides it for a trial run, up to <see cref="OGG_QUALITY_LIMIT"/>. The highest the
        /// encoder gets right, rather than a listening choice: under the limit OggVorbisEncoder matched libvorbis at
        /// the same setting in size, in signal-to-noise ratio, and within 0.4 dB of it in each of the six bands the
        /// table reports (#444, all 11 tracks at 0.5 and 0.59, worst track per band).
        /// </summary>
        private const float OGG_QUALITY = 0.59f;

        /// <summary>
        /// Where OggVorbisEncoder 1.2.2 goes wrong, refused rather than written. From quality 0.6 its stereo setup
        /// switches to the coupled high residue, and that table is a copy of the low one (its upstream PR #24, open
        /// as of 2026-09-16): at 0.8 the tracks came back with a median signal-to-noise ratio of 9 dB where
        /// libvorbis gives 32, the damage all under 2 kHz, where this music keeps nearly all of its energy.
        /// </summary>
        private const float OGG_QUALITY_LIMIT = 0.6f;

        /// <summary>How much of a track the encoder is handed at a time: a second of audio.</summary>
        private const int OGG_CHUNK_FRAMES = 48000;

        //The generated sound effects (#482): the game's effects are baked at 44.1 kHz (ProceduralAudio.SAMPLE_RATE), and
        //a render that is not is refused rather than resampled here. The file is written at a sane peak; the loudness
        //law is the game's own at load, so a chosen sound is stored as it was chosen.
        private const int SFX_RATE = 44100;
        private const float SFX_PEAK = 0.9f;

        /// <summary>
        /// How many frames at each end of a track are held to the rest of it in the loop-edge check, about 43 ms:
        /// the stretch either side of the wrap in which a codec that treats a file's edges worse than its body
        /// would show it.
        /// </summary>
        private const int EDGE_FRAMES = 2048;

        /// <summary>
        /// How much of a track the alignment check matches against its master: a second. A shorter stretch can
        /// lie — 8192 frames of Nocturne's opening, a sustained low note, matched best 726 frames out (a period of
        /// about 66 Hz) on a decode that was exact, as a second-long window then confirmed.
        /// </summary>
        private const int LAG_WINDOW_FRAMES = 48000;

        /// <summary>
        /// Master (in <c>Research/AI-Music</c>, without .wav) → the file the game plays (in <c>Game/Music</c>) and
        /// the loudness it is brought to. The game finds its files by name — a theme's own name, then its variants
        /// as <c>name-*.ogg</c> in order — so adding a variant is a line here and nothing in the game.
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
            bool write = true;
            bool tracks = false;
            float quality = OGG_QUALITY;
            string only = null;
            string sfxWav = null, sfxName = null;
            bool sfxMusic = false;
            int sfxRoot = -1;
            float sfxBpm = 0f;

            //Every argument is read before anything runs: "--tracks --no-write" used to start writing the moment
            //"--tracks" was reached, with the flag after it never seen
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (Is(arg, "--out") && i + 1 < args.Length) outDir = args[++i];
                else if (Is(arg, "--theme") && i + 1 < args.Length) only = args[++i];
                else if (Is(arg, "--no-write")) write = false;
                else if (Is(arg, "--tracks")) tracks = true;
                else if (Is(arg, "--sfx") && i + 2 < args.Length) { sfxWav = args[++i]; sfxName = args[++i]; }
                else if (Is(arg, "--music")) sfxMusic = true;
                else if (Is(arg, "--root") && i + 1 < args.Length && int.TryParse(args[++i], out sfxRoot)) continue;
                else if (Is(arg, "--bpm") && i + 1 < args.Length
                    && float.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out sfxBpm)) continue;
                else if (Is(arg, "--quality") && i + 1 < args.Length
                    && float.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out quality)
                    && quality >= -0.1f && quality < OGG_QUALITY_LIMIT) continue;
                else
                {
                    Console.WriteLine("usage: MusicBake [--out <dir>] [--theme <name>] [--no-write] | --tracks [--quality <-0.1..0.59>] [--no-write] | --sfx <wav> <name> [--music [--root <midi>] [--bpm <n>]] [--no-write]");
                    return 2;
                }
            }

            if (tracks) return BuildTracks(write, quality);
            if (sfxWav != null) return BuildSfx(sfxWav, sfxName, write, quality, sfxMusic, sfxRoot, sfxBpm);

            if (write) Directory.CreateDirectory(outDir);

            List<string> names = new();

            foreach (MusicTheme theme in Enum.GetValues<MusicTheme>())
                if (only == null || Is(only, theme.ToString())) names.Add(theme.ToString());

            if (only == null || Is(only, "menu")) names.Add("Menu");

            Console.WriteLine("piece            secs  bake  entry   peak    rms   bal  mono | <100 100-200 200-500  500-2k   2k-6k    6k+ |  head   tail");

            bool driveOk = true;

            foreach (string name in names)
            {
                bool menu = Is(name, "Menu");

                Stopwatch clock = Stopwatch.StartNew();

                float[] mix = menu
                    ? ProceduralMusic.RenderMenu(out float drive)
                    : ProceduralMusic.Render(Enum.Parse<MusicTheme>(name), out drive);

                clock.Stop();

                //Where a LEVEL comes in on the piece (#201). The front end's loop has no entry of its own —
                //it is a lobby, and it plays from the top.
                double entry = menu
                    ? -1
                    : ProceduralMusic.EntryOffset(Enum.Parse<MusicTheme>(name)) / 4.0 / SAMPLE_RATE;

                Report(name, mix, SAMPLE_RATE, clock.Elapsed.TotalMilliseconds, entry);

                //#464: a streamed render cannot compute this live, so it plays back a stored figure instead —
                //here is where that figure gets checked. Menu is baked too, at the table's last slot, for the
                //day someone streams it, even though nothing reads it yet (see BakeMenu's own remarks on why
                //not) — checked all the same, since a stored figure nobody checks is worse than none.
                {
                    int index = menu ? ProceduralMusic.LIMITER_DRIVE.Length - 1 : (int)Enum.Parse<MusicTheme>(name);
                    float stored = ProceduralMusic.LIMITER_DRIVE[index];
                    float driveError = MathF.Abs(drive - stored) / MathF.Max(drive, 1e-6f);

                    if (driveError > DRIVE_TOLERANCE)
                    {
                        Console.WriteLine($"  ⚠ drive: measured {drive:F6}, LIMITER_DRIVE[{index}] stores {stored:F6} ({driveError * 100:F2}% off) — re-bake it");
                        driveOk = false;
                    }
                }

                if (write) WriteWav(Path.Combine(outDir, $"{name.ToLowerInvariant()}.wav"), mix, SAMPLE_RATE);
            }

            if (!driveOk)
            {
                Console.WriteLine("\nLIMITER_DRIVE is stale against at least one piece above — a streamed render of it would use the wrong gain.");
                return 3;
            }

            if (write) Console.WriteLine($"\nWritten to {Path.GetFullPath(outDir)}");

            return 0;
        }

        /// <summary>
        /// One generated sound effect (#482) → <c>Game/Sfx/&lt;name&gt;.ogg</c>. The render is read as a master is,
        /// mixed to <b>mono</b> — the game's effects are placed by <c>Apply3D</c>, which takes a mono source — peak-normalised
        /// to <see cref="SFX_PEAK"/>, written through the same encoder as a track and decoded straight back through the
        /// game's decoder, as a track is, so the length is proved to survive. It is stereo on disk with both channels the
        /// one signal, because the encoder and <see cref="OggTrack.Decode(Stream, int)"/> are stereo-only and a second
        /// channel of a two-second sound costs nothing; the game reads one channel back (<c>ProceduralAudio.TryLoadSfx</c>).
        /// </summary>
        private static int BuildSfx(string wavPath, string name, bool write, float quality, bool music, int rootOverride, float bpmOverride)
        {
            string repo = FindRepo();
            if (repo == null)
            {
                Console.WriteLine("MusicBake --sfx: run from inside the repository (no Game.sln with a docs folder above it)");
                return 1;
            }
            if (!File.Exists(wavPath))
            {
                Console.WriteLine($"MusicBake --sfx: missing {wavPath}");
                return 1;
            }

            (float[] mix, int rate) = ReadWav(wavPath);
            if (rate != SFX_RATE)
            {
                Console.WriteLine($"MusicBake --sfx: {wavPath} is {rate} Hz and the game's effects are {SFX_RATE} Hz");
                return 1;
            }

            int frames = mix.Length / 2;
            float[] mono = new float[frames];
            float peak = 0f;
            for (int f = 0; f < frames; f++)
            {
                mono[f] = (mix[f * 2] + mix[f * 2 + 1]) * 0.5f;
                peak = Math.Max(peak, Math.Abs(music ? mix[f * 2] : mono[f]));
                if (music) peak = Math.Max(peak, Math.Abs(mix[f * 2 + 1]));
            }

            //A sound is mono for Apply3D; a piece of MUSIC (--music: the victory fanfare, #482) keeps its stereo image and
            //carries the key and tempo the star chime tunes to, measured here or given on the command line
            float[] dup = new float[frames * 2];
            if (music)
            {
                for (int i = 0; i < dup.Length; i++) dup[i] = peak > 1e-6f ? mix[i] * SFX_PEAK / peak : mix[i];
            }
            else
            {
                if (peak > 1e-6f) for (int f = 0; f < frames; f++) mono[f] *= SFX_PEAK / peak;
                for (int f = 0; f < frames; f++) dup[f * 2] = dup[f * 2 + 1] = mono[f];
            }

            double sum = 0;
            foreach (float s in mono) sum += (double)s * s;
            double rms = Math.Sqrt(sum / Math.Max(1, frames)) * (peak > 1e-6f ? SFX_PEAK / peak : 1f);

            List<(string Name, string Value)> tags = new();
            string shape = "";
            if (music)
            {
                (int root, float bpm, string keys) = EstimateKeyAndTempo(mono, rate);
                if (rootOverride >= 0) root = rootOverride;
                if (bpmOverride > 0f) bpm = bpmOverride;
                tags.Add(("ROOT", root.ToString(CultureInfo.InvariantCulture)));
                tags.Add(("BPM", bpm.ToString("F1", CultureInfo.InvariantCulture)));
                shape = $"  ROOT {root} ({NoteName(root)}) BPM {bpm:F1} [{keys}]";
            }

            //A serial the file's bytes can be reproduced from: a string's hash code changes per process in .NET
            int serial = 0x1000;
            foreach (char c in name) serial = unchecked(serial * 31 + c);
            byte[] ogg = EncodeOgg(dup, rate, quality, serial, title: name, tags);

            string dir = Path.Combine(repo, "Game", "Sfx");
            string path = Path.Combine(dir, name + ".ogg");
            if (write)
            {
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, ogg);
            }

            int back = OggTrack.Decode(new MemoryStream(ogg), rate).Length / 4;
            Console.WriteLine($"{name,-16} {frames / (double)rate,5:F2} s  peak {SFX_PEAK:F2}  rms {Db(rms),6:F1} dBFS  crest {SFX_PEAK / rms,5:F2}"
                + $"  ogg {ogg.Length / 1024.0,6:F1} KB  decoded {back} of {frames} frames"
                + (back == frames ? "" : "  LENGTH DIFFERS") + shape + (write ? "  -> " + path : "  (not written)"));
            return back == frames ? 0 : 1;
        }

        private static readonly string[] NOTE_NAMES = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        private static string NoteName(int midi) => NOTE_NAMES[((midi % 12) + 12) % 12] + (midi / 12 - 1);

        //Krumhansl-Kessler major key profile: how much each scale degree, from the tonic, belongs to a major key
        private static readonly double[] MAJOR_PROFILE = { 6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88 };

        /// <summary>
        /// The key and the tempo of a short piece, for the tags the fanfare's file carries (#482). The key: a chroma
        /// over the whole piece — every FFT bin between 80 Hz and 2 kHz folded onto its pitch class, weighted by its
        /// magnitude — correlated with the major profile at each of the twelve roots, the best taken, written as the
        /// MIDI note in the fourth octave (C4 = 60, where the baked victory sits). The tempo: the onset strength (the
        /// positive spectral flux per hop) autocorrelated over 70–180 BPM, the strongest lag taken. Both are estimates
        /// from a few seconds of brass — the printout names the runners-up, and <c>--root</c>/<c>--bpm</c> override them.
        /// </summary>
        private static (int Root, float Bpm, string Candidates) EstimateKeyAndTempo(float[] mono, int rate)
        {
            const int Window = 4096, Hop = 1024;
            int bins = Window / 2;
            double[] chroma = new double[12];
            double[] re = new double[Window], im = new double[Window], previous = new double[bins];
            List<double> onset = new();

            for (int at = 0; at + Window <= mono.Length; at += Hop)
            {
                for (int i = 0; i < Window; i++)
                {
                    double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Window - 1));
                    re[i] = mono[at + i] * w;
                    im[i] = 0;
                }
                Spectrum.Fft(re, im);

                double flux = 0;
                for (int k = 1; k < bins; k++)
                {
                    double magnitude = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                    double frequency = k * (double)rate / Window;
                    if (frequency >= 80 && frequency <= 2000)
                    {
                        int pitchClass = (((int)Math.Round(12 * Math.Log2(frequency / 440.0)) + 9) % 12 + 12) % 12;
                        chroma[pitchClass] += magnitude;
                    }
                    double rise = magnitude - previous[k];
                    if (rise > 0) flux += rise;
                    previous[k] = magnitude;
                }
                onset.Add(flux);
            }

            //The key: Pearson correlation of the chroma against the profile rotated to each root
            double chromaMean = chroma.Average(), profileMean = MAJOR_PROFILE.Average();
            var scores = new List<(int Root, double Score)>();
            for (int root = 0; root < 12; root++)
            {
                double dot = 0, cc = 0, pp = 0;
                for (int i = 0; i < 12; i++)
                {
                    double c = chroma[(root + i) % 12] - chromaMean, q = MAJOR_PROFILE[i] - profileMean;
                    dot += c * q; cc += c * c; pp += q * q;
                }
                scores.Add((root, cc > 0 ? dot / Math.Sqrt(cc * pp) : 0));
            }
            scores.Sort((a, b) => b.Score.CompareTo(a.Score));

            //The tempo: autocorrelation of the onset strength over the lags of 70–180 BPM
            double hopSeconds = Hop / (double)rate;
            double onsetMean = onset.Count > 0 ? onset.Average() : 0;
            int lagMin = (int)Math.Round(60.0 / 180.0 / hopSeconds), lagMax = (int)Math.Round(60.0 / 70.0 / hopSeconds);
            double bestScore = double.MinValue; int bestLag = lagMin;
            for (int lag = lagMin; lag <= Math.Min(lagMax, onset.Count - 2); lag++)
            {
                double s = 0; int n = 0;
                for (int i = lag; i < onset.Count; i++) { s += (onset[i] - onsetMean) * (onset[i - lag] - onsetMean); n++; }
                s = n > 0 ? s / n : 0;
                if (s > bestScore) { bestScore = s; bestLag = lag; }
            }
            float bpm = (float)(60.0 / (bestLag * hopSeconds));

            string candidates = string.Join(" ", scores.Take(3).Select(s => $"{NOTE_NAMES[s.Root]}:{s.Score:F2}"));
            return (60 + scores[0].Root, bpm, candidates);
        }

        private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Builds <c>Game/Music</c> from the masters: each brought to its loudness, its peaks shaped under full
        /// scale, encoded as Ogg Vorbis at <paramref name="quality"/> and the master's own rate, then decoded again
        /// through <see cref="OggTrack"/> and checked. <paramref name="write"/> false measures without writing. The
        /// table's "bake" column is the processing time and "entry" is empty — a generated loop is cut from its
        /// render's body, so a level opens on it wherever it opens. Exits 1 if any track decodes to a different
        /// length than its master, since that loop would open a gap at every repeat.
        /// </summary>
        private static int BuildTracks(bool write, float quality)
        {
            string repo = FindRepo();
            if (repo == null)
            {
                Console.WriteLine("MusicBake --tracks: run from inside the repository (no Game.sln with a docs folder above it)");
                return 1;
            }

            string masters = Path.Combine(repo, "Research", "AI-Music");
            string tracks = Path.Combine(repo, "Game", "Music");
            if (write) Directory.CreateDirectory(tracks);

            Console.WriteLine("track            secs  bake  entry   peak    rms   bal  mono | <100 100-200 200-500  500-2k   2k-6k    6k+ |  head   tail");

            List<byte[]> encoded = new();
            long wavBytes = 0, oggBytes = 0;
            double seconds = 0, decodeMs = 0;
            int trackRate = 0;
            bool framesHold = true;

            for (int t = 0; t < TRACKS.Length; t++)
            {
                (string master, string track, double rmsDb) = TRACKS[t];
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

                //The serial is fixed per track rather than random, so an unchanged master rebakes to the same bytes
                byte[] ogg = EncodeOgg(mix, rate, quality, serial: t + 1, title: track);

                Stopwatch decodeClock = Stopwatch.StartNew();
                byte[] decoded = OggTrack.Decode(new MemoryStream(ogg), rate);
                decodeClock.Stop();

                int frames = mix.Length / 2;

                (int clipped, double edgeRatio, double wrapGrowth, int headLag, int tailLag) = CompareDecoded(mix, decoded);

                //A length can come out right with the audio still shifted, so the two ends are placed as well
                bool fits = decoded.Length / 4 == frames && headLag == 0 && tailLag == 0;
                framesHold &= fits;

                double trackSeconds = frames / (double)rate;
                Console.WriteLine($"                 ogg q{quality.ToString("0.0#", CultureInfo.InvariantCulture)}: "
                    + $"{ogg.Length / 1024.0:0} KiB, {ogg.Length * 8 / trackSeconds / 1000:0} kbps, decoded in {decodeClock.Elapsed.TotalMilliseconds:0} ms "
                    + $"to {decoded.Length / 4} of {frames} frames, head {headLag:+0;-0;0} and tail {tailLag:+0;-0;0} frames out{(fits ? "" : " - DOES NOT FIT")}; "
                    + $"{clipped} samples clipped; coding error at the loop edges {edgeRatio:0.00}x the body's; "
                    + $"step across the wrap {wrapGrowth:+0.0000;-0.0000;0.0000} of full scale");

                if (write) File.WriteAllBytes(Path.Combine(tracks, track + ".ogg"), ogg);

                encoded.Add(ogg);
                wavBytes += 44 + frames * 4L;
                oggBytes += ogg.Length;
                seconds += trackSeconds;
                decodeMs += decodeClock.Elapsed.TotalMilliseconds;
                trackRate = rate;
            }

            //What GameMusic's constructor does: every track handed to the thread pool at once
            Stopwatch together = Stopwatch.StartNew();
            Task[] loads = new Task[encoded.Count];
            for (int i = 0; i < loads.Length; i++)
            {
                byte[] ogg = encoded[i];
                loads[i] = Task.Run(() => OggTrack.Decode(new MemoryStream(ogg), trackRate));
            }
            Task.WaitAll(loads);
            together.Stop();

            Console.WriteLine($"\n{encoded.Count} tracks, {seconds:0.0} s: {oggBytes / 1048576.0:0.0} MB of Ogg Vorbis against "
                + $"{wavBytes / 1048576.0:0.0} MB as 16-bit .wav ({100.0 * oggBytes / wavBytes:0} %), {oggBytes * 8 / seconds / 1000:0} kbps on average");
            Console.WriteLine($"Decoding took {decodeMs:0} ms one track after another, and {together.Elapsed.TotalMilliseconds:0} ms "
                + $"all at once on the thread pool as the game loads them ({Environment.ProcessorCount} logical processors)");

            if (!framesHold) Console.WriteLine("A track does not decode to its master's frames in place: its loop would jump or gap at every repeat");
            if (write) Console.WriteLine($"Written to {tracks}");

            return framesHold ? 0 : 1;
        }

        /// <summary>
        /// A track as Ogg Vorbis at <paramref name="quality"/>, in memory. The three header packets go first, flushed
        /// onto pages of their own so the audio starts on a fresh page as the Vorbis spec requires, then the audio a
        /// second at a time, then the end of stream, which stamps the last page with the frame count a decoder trims
        /// the final block to. Whether the result decodes to the master's frames, and in the right place, is
        /// <see cref="CompareDecoded"/>'s to say — the encoder's own start was a thousand frames out.
        /// </summary>
        private static byte[] EncodeOgg(float[] mix, int rate, float quality, int serial, string title, List<(string Name, string Value)> tags = null)
        {
            int frames = mix.Length / 2;

            float[][] planar = { new float[frames], new float[frames] };
            for (int f = 0; f < frames; f++)
            {
                planar[0][f] = mix[f * 2];
                planar[1][f] = mix[f * 2 + 1];
            }

            VorbisInfo info = VorbisInfo.InitVariableBitRate(2, rate, quality);
            OggStream stream = new(serial);

            Comments comments = new();
            comments.AddTag("TITLE", title);
            if (tags != null) foreach ((string tagName, string value) in tags) comments.AddTag(tagName, value);

            using MemoryStream output = new();

            stream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
            stream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(comments));
            stream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));
            FlushPages(stream, output, force: true);

            ProcessingState state = ProcessingState.Create(info);

            //OggVorbisEncoder 1.2.2 starts its buffer empty where libvorbis starts it half a long block in (its
            //pcm_current = centerW), then extrapolates backwards over that first half-block as libvorbis does over
            //its pre-roll: so the first half-block of the TRACK was overwritten and never decoded, and every track
            //came back exactly that much short at the head (1024 frames at 48 kHz, through NVorbis and libsndfile
            //alike). Half a block is written first, then, for the extrapolation to overwrite instead.
            int preroll = info.CodecSetup.BlockSizes[1] / 2;
            state.WriteData(new[] { new float[preroll], new float[preroll] }, preroll);
            Drain(state, stream, output);

            for (int at = 0; at < frames; at += OGG_CHUNK_FRAMES)
            {
                state.WriteData(planar, Math.Min(OGG_CHUNK_FRAMES, frames - at), at);
                Drain(state, stream, output);
            }

            //Separate from the loop on purpose: the library's own example calls it only when the read index lands
            //exactly on the end, which a track whose length is not a whole number of chunks never does
            state.WriteEndOfStream();
            Drain(state, stream, output);
            FlushPages(stream, output, force: true);

            return output.ToArray();
        }

        private static void Drain(ProcessingState state, OggStream stream, Stream output)
        {
            while (!stream.Finished && state.PacketOut(out OggPacket packet))
            {
                stream.PacketIn(packet);
                FlushPages(stream, output, force: false);
            }
        }

        private static void FlushPages(OggStream stream, Stream output, bool force)
        {
            while (stream.PageOut(out OggPage page, force))
            {
                output.Write(page.Header, 0, page.Header.Length);
                output.Write(page.Body, 0, page.Body.Length);
            }
        }

        /// <summary>
        /// The decoded track against the 16-bit samples the .wav would have held (the same clamp-and-scale):
        /// how many samples the codec pushed onto the rails that were not there, how much larger the coding error
        /// is in the <see cref="EDGE_FRAMES"/> at either end than across the rest (the loop's wrap is those two
        /// stretches back to back), how much bigger the step from the last frame to the first became, as a
        /// fraction of full scale, and how many frames the decoded audio sits away from the master's near each
        /// end. Measured over the shorter of the two if the lengths differ, so everything but the lags means
        /// little until those are zero.
        /// </summary>
        private static (int Clipped, double EdgeRatio, double WrapGrowth, int HeadLag, int TailLag) CompareDecoded(float[] mix, byte[] decoded)
        {
            int frames = Math.Min(mix.Length / 2, decoded.Length / 4);

            short Reference(int sample) => (short)(Math.Clamp(mix[sample], -1f, 1f) * short.MaxValue);
            short Decoded(int sample) => BitConverter.ToInt16(decoded, sample * 2);

            int clipped = 0;
            double headSum = 0, tailSum = 0, bodySum = 0;

            for (int s = 0; s < frames * 2; s++)
            {
                short r = Reference(s), d = Decoded(s);

                if (Math.Abs((int)d) >= short.MaxValue && Math.Abs((int)r) < short.MaxValue) clipped++;

                double e = (d - r) / (double)short.MaxValue;
                int frame = s / 2;

                if (frame < EDGE_FRAMES) headSum += e * e;
                else if (frame >= frames - EDGE_FRAMES) tailSum += e * e;
                else bodySum += e * e;
            }

            double edgeSamples = EDGE_FRAMES * 2.0;
            double bodyRms = Math.Sqrt(bodySum / Math.Max(1, frames * 2 - 2 * edgeSamples));
            double edgeRatio = bodyRms > 0 ? Math.Sqrt(Math.Max(headSum, tailSum) / edgeSamples) / bodyRms : 0;

            double wrapGrowth = double.NegativeInfinity;
            for (int c = 0; c < 2; c++)
            {
                int first = c, last = (frames - 1) * 2 + c;
                double decodedStep = Math.Abs(Decoded(last) - Decoded(first)) / (double)short.MaxValue;
                double referenceStep = Math.Abs(Reference(last) - Reference(first)) / (double)short.MaxValue;
                wrapGrowth = Math.Max(wrapGrowth, decodedStep - referenceStep);
            }

            int headLag = Lag(mix, decoded, 2 * EDGE_FRAMES);
            int tailLag = Lag(mix, decoded, decoded.Length / 4 - 2 * EDGE_FRAMES - LAG_WINDOW_FRAMES);

            return (clipped, edgeRatio, wrapGrowth, headLag, tailLag);
        }

        /// <summary>
        /// Where <see cref="LAG_WINDOW_FRAMES"/> of the decoded track starting at <paramref name="at"/> sit in the
        /// master: the shift, within a long Vorbis block either way, at which the mono folds differ least. Zero is
        /// the answer a sample-exact codec gives. Lag zero is tried first so every other shift can stop summing
        /// the moment it has lost, which is what makes a second-long window affordable.
        /// </summary>
        private static int Lag(float[] mix, byte[] decoded, int at)
        {
            const int REACH = 2048;

            if (at < 0 || (at + LAG_WINDOW_FRAMES) * 4 > decoded.Length) return 0;

            double[] mono = new double[LAG_WINDOW_FRAMES];
            for (int f = 0; f < mono.Length; f++)
                mono[f] = (BitConverter.ToInt16(decoded, (at + f) * 4) + BitConverter.ToInt16(decoded, (at + f) * 4 + 2)) / (2.0 * short.MaxValue);

            int bestLag = 0;
            double bestError = double.PositiveInfinity;

            for (int step = 0; step <= 2 * REACH + 1; step++)
            {
                //0, then -REACH..REACH skipping 0
                int lag = step == 0 ? 0 : step - 1 - REACH;
                if (step > 0 && lag == 0) continue;
                if (at + lag < 0 || (at + lag + mono.Length) * 2 > mix.Length) continue;

                double error = 0;

                for (int f = 0; f < mono.Length && error < bestError; f++)
                {
                    double r = (mix[(at + lag + f) * 2] + mix[(at + lag + f) * 2 + 1]) * 0.5;
                    error += (mono[f] - r) * (mono[f] - r);
                }

                if (error < bestError)
                {
                    bestError = error;
                    bestLag = lag;
                }
            }

            return bestLag;
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

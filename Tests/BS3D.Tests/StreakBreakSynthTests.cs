using BS3D.Audio;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// #845: the sound of a streak breaking keeps the owner's rules for this game's effects, and the shape the issue asks
    /// for: a short descending run, a lost ×5 longer than a lost ×3. The tests measure the arithmetic the game plays
    /// (<see cref="StreakBreakSynth"/>). Whether it is right is the owner's ear; what a measurement can refuse is a sound
    /// that carries nothing his monitor speakers play, hisses, does not fall, or does not grow with what was lost.
    /// <para>
    /// The band check is seen to refuse a planted fault (BestPractices.md §10): the same run transposed down into the bass,
    /// which is the very failure his speakers make inaudible (#726), fails the threshold the real one passes.
    /// </para>
    /// </summary>
    public class StreakBreakSynthTests
    {
        private const int SAMPLE_RATE = 44100;
        private const float BIN_HZ = 25f;
        private const float TOP_HZ = 12000f;

        //What the owner's speakers carry, and the share of the energy that has to sit there (AimStopSynthTests' own figures)
        private const float BAND_LOW_HZ = 300f;
        private const float BAND_HIGH_HZ = 4000f;
        private const float BAND_SHARE = 0.80f;

        private readonly ITestOutputHelper _output;

        public StreakBreakSynthTests(ITestOutputHelper output) => _output = output;

        public static TheoryData<int> Lost() => new() { 3, 5 };

        [Theory]
        [MemberData(nameof(Lost))]
        public void Render_is_the_same_every_time_and_finite_and_at_its_peak(int lost)
        {
            float[] first = StreakBreakSynth.Render(SAMPLE_RATE, lost);
            float[] second = StreakBreakSynth.Render(SAMPLE_RATE, lost);

            Assert.Equal((int)(SAMPLE_RATE * StreakBreakSynth.Duration(lost)), first.Length);
            Assert.Equal(first, second);

            float peak = 0f;
            foreach (float sample in first)
            {
                Assert.True(float.IsFinite(sample));
                peak = MathF.Max(peak, MathF.Abs(sample));
            }

            Assert.InRange(peak, StreakBreakSynth.PEAK - 0.001f, StreakBreakSynth.PEAK + 0.001f);
        }

        [Theory]
        [MemberData(nameof(Lost))]
        public void It_starts_and_ends_at_silence_so_it_never_clicks(int lost)
        {
            float[] signal = StreakBreakSynth.Render(SAMPLE_RATE, lost);

            for (int i = 0; i < 4; i++)
            {
                Assert.InRange(MathF.Abs(signal[i]), 0f, 0.01f);
                Assert.InRange(MathF.Abs(signal[signal.Length - 1 - i]), 0f, 0.01f);
            }
        }

        [Theory]
        [MemberData(nameof(Lost))]
        public void Its_identity_is_in_what_monitor_speakers_carry_and_it_has_no_hiss(int lost)
        {
            float[] signal = StreakBreakSynth.Render(SAMPLE_RATE, lost);

            float carried = BandShare(signal, BAND_LOW_HZ, BAND_HIGH_HZ);
            float hiss = BandShare(signal, 5000f, TOP_HZ);
            float low = BandShare(signal, BIN_HZ, BAND_LOW_HZ);

            _output.WriteLine($"x{lost}: 300-4000 Hz {carried:P1}, under 300 Hz {low:P1}, above 5 kHz {hiss:P2}");

            Assert.True(carried >= BAND_SHARE, $"only {carried:P1} of the energy is at 300-4000 Hz");
            Assert.True(hiss <= 0.02f, $"{hiss:P2} of the energy is above 5 kHz");
        }

        /// <summary>
        /// The planted fault: the same run two octaves and more down, in the bass a monitor's speakers do not play. The band
        /// check above must refuse it, or its passing the real sound says nothing.
        /// </summary>
        [Fact]
        public void A_bass_only_run_fails_the_band_check()
        {
            float[] bass = StreakBreakSynth.Render(SAMPLE_RATE, 5, pitchScale: 0.2f);

            float carried = BandShare(bass, BAND_LOW_HZ, BAND_HIGH_HZ);
            _output.WriteLine($"the run at a fifth of its pitch: 300-4000 Hz {carried:P1}");

            Assert.True(carried < BAND_SHARE, $"the band check passed a bass-only run ({carried:P1} at 300-4000 Hz)");
        }

        /// <summary>
        /// It falls: each note's fundamental, read off its own slice by autocorrelation and not off the synth's table, is
        /// below the one before it.
        /// </summary>
        [Theory]
        [MemberData(nameof(Lost))]
        public void Every_note_is_lower_than_the_one_before(int lost)
        {
            float[] signal = StreakBreakSynth.Render(SAMPLE_RATE, lost);
            int notes = StreakBreakSynth.NotesFor(lost);

            float previous = float.MaxValue;
            for (int n = 0; n < notes; n++)
            {
                float onset = StreakBreakSynth.LEAD_SECONDS + n * StreakBreakSynth.NOTE_SPACING;
                float pitch = Pitch(signal, (int)((onset + 0.012f) * SAMPLE_RATE), (int)((onset + 0.08f) * SAMPLE_RATE));
                _output.WriteLine($"x{lost} note {n + 1}: {pitch:F0} Hz");

                Assert.True(pitch < previous * 0.97f, $"note {n + 1} ({pitch:F0} Hz) does not fall from {previous:F0} Hz");
                previous = pitch;
            }
        }

        /// <summary>
        /// A lost ×5 is louder and longer than a lost ×3 (the issue's own words). Longer is read off the envelope (how long it
        /// stays within 30 dB of its own loudest 10 ms); louder is the play level, which the game sets in
        /// <c>ProceduralAudio.PlayStreakBreak</c> and this cannot reach, so it is the note count and the length that are held.
        /// </summary>
        [Fact]
        public void A_lost_cap_is_longer_and_has_more_notes_than_a_lost_three()
        {
            float[] three = StreakBreakSynth.Render(SAMPLE_RATE, 3);
            float[] five = StreakBreakSynth.Render(SAMPLE_RATE, 5);

            float heardThree = Heard(three), heardFive = Heard(five);
            _output.WriteLine($"heard for {heardThree:F2} s (x3) and {heardFive:F2} s (x5)");

            Assert.True(StreakBreakSynth.NotesFor(5) > StreakBreakSynth.NotesFor(3));
            Assert.True(heardFive > heardThree + 0.1f, $"a lost x5 is heard for {heardFive:F2} s, a lost x3 for {heardThree:F2} s");
        }

        //Seconds from the first to the last 10 ms frame within 30 dB of the loudest
        private static float Heard(float[] signal)
        {
            int frame = SAMPLE_RATE / 100;
            int frames = signal.Length / frame;
            float loudest = 0f;
            float[] rms = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                rms[f] = Rms(signal, f * frame, (f + 1) * frame);
                loudest = MathF.Max(loudest, rms[f]);
            }

            int first = -1, last = -1;
            for (int f = 0; f < frames; f++)
                if (rms[f] >= loudest * 0.0316f)
                {
                    if (first < 0) first = f;
                    last = f;
                }

            return (last - first + 1) * 0.01f;
        }

        //A fundamental read off the slice's autocorrelation between 250 and 1000 Hz. A periodic signal correlates as well at
        //two periods as at one, so the best lag alone halves the pitch now and then: the SHORTEST lag that is a local peak
        //within 15 % of the best is the period, the usual guard against the octave error
        private static float Pitch(float[] signal, int from, int to)
        {
            int shortest = SAMPLE_RATE / 1000, longest = SAMPLE_RATE / 250;
            double[] correlation = new double[longest + 2];
            double best = double.MinValue;

            for (int lag = shortest - 1; lag <= longest + 1; lag++)
            {
                double sum = 0.0;
                for (int i = from; i + lag < to; i++) sum += signal[i] * signal[i + lag];
                correlation[lag] = sum / (to - from - lag);
                if (lag >= shortest && lag <= longest) best = Math.Max(best, correlation[lag]);
            }

            for (int lag = shortest; lag <= longest; lag++)
                if (correlation[lag] >= 0.85 * best && correlation[lag] >= correlation[lag - 1] && correlation[lag] >= correlation[lag + 1])
                    return (float)SAMPLE_RATE / lag;

            return 0f;
        }

        private static float Rms(float[] signal, int from, int to)
        {
            double sum = 0.0;
            for (int i = from; i < to; i++) sum += signal[i] * signal[i];
            return (float)Math.Sqrt(sum / (to - from));
        }

        private static float BandShare(float[] signal, float lowHz, float highHz) =>
            BandEnergy(signal, lowHz, highHz) / BandEnergy(signal, BIN_HZ, TOP_HZ);

        //The energy between two frequencies, summed over bins BIN_HZ apart, each a Goertzel's single-frequency DFT
        private static float BandEnergy(float[] signal, float lowHz, float highHz)
        {
            double energy = 0.0;
            for (float hz = MathF.Max(BIN_HZ, MathF.Ceiling(lowHz / BIN_HZ) * BIN_HZ); hz <= highHz; hz += BIN_HZ)
                energy += BinEnergy(signal, hz);
            return (float)energy;
        }

        private static double BinEnergy(float[] signal, float hz)
        {
            double w = 2.0 * Math.PI * hz / SAMPLE_RATE;
            double coefficient = 2.0 * Math.Cos(w);
            double s1 = 0.0, s2 = 0.0;

            for (int i = 0; i < signal.Length; i++)
            {
                //A Hann window, so the buffer's own edges do not leak into the neighbouring bins
                double window = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (signal.Length - 1));
                double s0 = signal[i] * window + coefficient * s1 - s2;
                s2 = s1;
                s1 = s0;
            }

            return s1 * s1 + s2 * s2 - coefficient * s1 * s2;
        }
    }
}

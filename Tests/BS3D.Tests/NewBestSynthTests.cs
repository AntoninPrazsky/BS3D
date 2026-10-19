using BS3D.Audio;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// #926: the sound of a new best keeps the owner's rules for this game's effects, and the shape the page gives it: a quick rising glitter
    /// of bells landing on a chord that rings. The tests measure the arithmetic the game plays (<see cref="NewBestSynth"/>). Whether it is
    /// right is the owner's ear; what a measurement can refuse is a sound that carries nothing his monitor speakers play, hisses, does not
    /// rise, or does not ring.
    /// <para>
    /// The band check is seen to refuse a planted fault (BestPractices.md §10): the same glitter transposed down into the bass, which is the
    /// very failure his speakers make inaudible (#726), fails the threshold the real one passes.
    /// </para>
    /// </summary>
    public class NewBestSynthTests
    {
        private const int SAMPLE_RATE = 44100;
        private const float BIN_HZ = 25f;
        private const float TOP_HZ = 12000f;

        //What the owner's speakers carry, and the share of the energy that has to sit there (AimStopSynthTests' own figures)
        private const float BAND_LOW_HZ = 300f;
        private const float BAND_HIGH_HZ = 4000f;
        private const float BAND_SHARE = 0.80f;

        private readonly ITestOutputHelper _output;

        public NewBestSynthTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void Render_is_the_same_every_time_and_finite_and_at_its_peak()
        {
            float[] first = NewBestSynth.Render(SAMPLE_RATE);
            float[] second = NewBestSynth.Render(SAMPLE_RATE);

            Assert.Equal((int)(SAMPLE_RATE * NewBestSynth.Duration), first.Length);
            Assert.Equal(first, second);

            float peak = 0f;
            foreach (float sample in first)
            {
                Assert.True(float.IsFinite(sample));
                peak = MathF.Max(peak, MathF.Abs(sample));
            }

            Assert.InRange(peak, NewBestSynth.PEAK - 0.001f, NewBestSynth.PEAK + 0.001f);
        }

        [Fact]
        public void It_starts_and_ends_at_silence_so_it_never_clicks()
        {
            float[] signal = NewBestSynth.Render(SAMPLE_RATE);

            for (int i = 0; i < 4; i++)
            {
                Assert.InRange(MathF.Abs(signal[i]), 0f, 0.01f);
                Assert.InRange(MathF.Abs(signal[signal.Length - 1 - i]), 0f, 0.01f);
            }
        }

        [Fact]
        public void Its_identity_is_in_what_monitor_speakers_carry_and_it_has_no_hiss()
        {
            float[] signal = NewBestSynth.Render(SAMPLE_RATE);

            float carried = BandShare(signal, BAND_LOW_HZ, BAND_HIGH_HZ);
            float hiss = BandShare(signal, 5000f, TOP_HZ);
            float low = BandShare(signal, BIN_HZ, BAND_LOW_HZ);

            _output.WriteLine($"300-4000 Hz {carried:P1}, under 300 Hz {low:P1}, above 5 kHz {hiss:P2}");

            Assert.True(carried >= BAND_SHARE, $"only {carried:P1} of the energy is at 300-4000 Hz");
            Assert.True(hiss <= 0.02f, $"{hiss:P2} of the energy is above 5 kHz");
        }

        /// <summary>
        /// The planted fault: the same sound a fifth of its pitch, in the bass a monitor's speakers do not play. The band check above must
        /// refuse it, or its passing the real sound says nothing.
        /// </summary>
        [Fact]
        public void A_bass_only_glitter_fails_the_band_check()
        {
            float[] bass = NewBestSynth.Render(SAMPLE_RATE, pitchScale: 0.2f);

            float carried = BandShare(bass, BAND_LOW_HZ, BAND_HIGH_HZ);
            _output.WriteLine($"the sound at a fifth of its pitch: 300-4000 Hz {carried:P1}");

            Assert.True(carried < BAND_SHARE, $"the band check passed a bass-only sound ({carried:P1} at 300-4000 Hz)");
        }

        /// <summary>
        /// It rises: each note of the run is the strongest of the run's six pitches in its own first twenty milliseconds, read off the signal
        /// and not off the synth's order of notes, and each is higher than the one before.
        /// </summary>
        [Fact]
        public void Every_note_of_the_run_is_higher_than_the_one_before()
        {
            float[] signal = NewBestSynth.Render(SAMPLE_RATE);

            int previous = -1;
            for (int n = 0; n < NewBestSynth.RUN_HZ.Length - 1; n++)
            {
                int from = (int)((n * NewBestSynth.NOTE_SPACING + 0.004f) * SAMPLE_RATE);
                int to = (int)((n * NewBestSynth.NOTE_SPACING + 0.028f) * SAMPLE_RATE);

                int strongest = 0;
                double most = double.MinValue;
                for (int candidate = 0; candidate < NewBestSynth.RUN_HZ.Length; candidate++)
                {
                    double energy = BinEnergy(signal, from, to, NewBestSynth.RUN_HZ[candidate]);
                    if (energy > most)
                    {
                        most = energy;
                        strongest = candidate;
                    }
                }

                _output.WriteLine($"note {n + 1}: strongest of the run is {NewBestSynth.RUN_HZ[strongest]:F0} Hz");
                Assert.True(strongest > previous, $"note {n + 1} reads as pitch {strongest}, not above {previous}");
                previous = strongest;
            }
        }

        /// <summary>
        /// And it rings: after the run the chord is still sounding half a second on, within 20 dB of the loudest tenth of a second, and the
        /// chord's own root (A5) is there to be heard in that stretch.
        /// </summary>
        [Fact]
        public void The_chord_rings_after_the_run_and_carries_its_root()
        {
            float[] signal = NewBestSynth.Render(SAMPLE_RATE);

            float loudest = 0f;
            int window = SAMPLE_RATE / 10;
            for (int from = 0; from + window <= signal.Length; from += window / 2) loudest = MathF.Max(loudest, Rms(signal, from, from + window));

            int ringFrom = (int)((NewBestSynth.ChordAt + 0.4f) * SAMPLE_RATE);
            float ring = Rms(signal, ringFrom, ringFrom + window);

            double root = BinEnergy(signal, ringFrom, ringFrom + window, 880f);
            double neighbour = BinEnergy(signal, ringFrom, ringFrom + window, 1000f);

            _output.WriteLine($"half a second into the ring: {20f * MathF.Log10(ring / loudest):F1} dB under the loudest tenth; A5 against 1 kHz: {10.0 * Math.Log10(root / neighbour):F1} dB");

            Assert.True(ring >= 0.1f * loudest, $"the chord has died by {NewBestSynth.ChordAt + 0.4f:F1} s ({ring / loudest:P0} of the loudest)");
            Assert.True(root > 10.0 * neighbour, "the chord's root is not what is ringing");
        }

        private static float Rms(float[] signal, int from, int to)
        {
            double sum = 0.0;
            for (int i = from; i < to; i++) sum += signal[i] * signal[i];
            return (float)Math.Sqrt(sum / (to - from));
        }

        //The share of the whole spectrum's energy between two frequencies, summed over bins BIN_HZ apart
        private static float BandShare(float[] signal, float lowHz, float highHz)
        {
            float total = BandEnergy(signal, BIN_HZ, TOP_HZ);
            return BandEnergy(signal, lowHz, highHz) / total;

            float BandEnergy(float[] s, float low, float high)
            {
                double energy = 0.0;
                for (float hz = MathF.Max(BIN_HZ, MathF.Ceiling(low / BIN_HZ) * BIN_HZ); hz <= high; hz += BIN_HZ)
                    energy += NewBestSynthTests.BinEnergy(s, 0, s.Length, hz);
                return (float)energy;
            }
        }

        //One frequency's energy over a slice, a Goertzel's single-frequency DFT under a Hann window
        private static double BinEnergy(float[] signal, int from, int to, float hz)
        {
            double w = 2.0 * Math.PI * hz / SAMPLE_RATE;
            double coefficient = 2.0 * Math.Cos(w);
            double s1 = 0.0, s2 = 0.0;

            for (int i = from; i < to; i++)
            {
                double window = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * (i - from) / (to - from - 1));
                double s0 = signal[i] * window + coefficient * s1 - s2;
                s2 = s1;
                s1 = s0;
            }

            return s1 * s1 + s2 * s2 - coefficient * s1 * s2;
        }
    }
}

using BS3D.Audio;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// #811: the sound of the barrel meeting its stop promises what the owner's own constraints on this game's effects ask, and
    /// since his verdict of 2026-10-07 the shape he asked for - "e-e", two beats, the second lower - and the tests measure the
    /// very arithmetic the game plays (<see cref="AimStopSynth"/>). A sound cannot be judged here - the verdict is his ear's -
    /// but a sound that carries nothing above 300 Hz, or hisses, or is the refused shot's low "no" again, or is one tone
    /// rather than two, can be refused before it is ever played.
    /// </summary>
    public class AimStopSynthTests
    {
        private const int SAMPLE_RATE = 44100;

        //The spectrum is read every 25 Hz, which resolves the beats' partials (hundreds of Hz apart) with room to spare
        private const float BIN_HZ = 25f;
        private const float TOP_HZ = 12000f;

        private readonly ITestOutputHelper _output;

        public AimStopSynthTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void Render_is_the_same_every_time_and_finite_and_at_its_peak()
        {
            float[] first = AimStopSynth.Render(SAMPLE_RATE);
            float[] second = AimStopSynth.Render(SAMPLE_RATE);

            Assert.Equal((int)(SAMPLE_RATE * AimStopSynth.DURATION), first.Length);
            Assert.Equal(first, second);

            float peak = 0f;
            foreach (float sample in first)
            {
                Assert.True(float.IsFinite(sample));
                peak = MathF.Max(peak, MathF.Abs(sample));
            }

            Assert.InRange(peak, AimStopSynth.PEAK - 0.001f, AimStopSynth.PEAK + 0.001f);
        }

        [Fact]
        public void It_starts_and_ends_at_silence_so_it_never_clicks()
        {
            float[] signal = AimStopSynth.Render(SAMPLE_RATE);

            for (int i = 0; i < 4; i++)
            {
                Assert.InRange(MathF.Abs(signal[i]), 0f, 0.01f);
                Assert.InRange(MathF.Abs(signal[signal.Length - 1 - i]), 0f, 0.01f);
            }
        }

        [Fact]
        public void Its_identity_is_in_what_monitor_speakers_carry_and_it_has_no_hiss()
        {
            float[] signal = AimStopSynth.Render(SAMPLE_RATE);

            float total = BandEnergy(signal, 0, signal.Length, BIN_HZ, TOP_HZ);
            float carried = BandEnergy(signal, 0, signal.Length, 300f, 4000f) / total;
            float hiss = BandEnergy(signal, 0, signal.Length, 5000f, TOP_HZ) / total;

            _output.WriteLine($"300-4000 Hz {carried:P1}, above 5 kHz {hiss:P2}");

            //The owner hears the game on his monitor's own speakers, where bass-only sounds are inaudible
            Assert.True(carried >= 0.80f, $"only {carried:P1} of the energy is at 300-4000 Hz");

            //And the standing rule on this game's effects: no hiss
            Assert.True(hiss <= 0.02f, $"{hiss:P2} of the energy is above 5 kHz");
        }

        [Fact]
        public void It_is_not_the_refused_shots_low_no()
        {
            float[] signal = AimStopSynth.Render(SAMPLE_RATE);

            float total = BandEnergy(signal, 0, signal.Length, BIN_HZ, TOP_HZ);
            float low = BandEnergy(signal, 0, signal.Length, BIN_HZ, 300f) / total;

            _output.WriteLine($"below 300 Hz {low:P1} (the refusal has 93 % between 60 and 300 Hz)");

            //The refusal is 220 -> 165 Hz, nearly all of it under 300 Hz (docs/game-feedback.md); the pair is learned by
            //how different the two are
            Assert.True(low <= 0.15f, $"{low:P1} of the energy is under 300 Hz, where the refusal lives");
        }

        /// <summary>
        /// "Uh-uh": the second beat is lower than the first (#811, the owner's "e-e"), as the refusal's second note is - read as
        /// each beat's fundamental, the strongest autocorrelation between 250 and 700 Hz.
        /// </summary>
        [Fact]
        public void The_second_beat_is_lower_than_the_first()
        {
            float[] signal = AimStopSynth.Render(SAMPLE_RATE);

            float first = Pitch(signal, (int)(0.01f * SAMPLE_RATE), (int)(0.09f * SAMPLE_RATE));
            float second = Pitch(signal, (int)((AimStopSynth.SECOND_BEAT_SECONDS + 0.01f) * SAMPLE_RATE),
                (int)((AimStopSynth.SECOND_BEAT_SECONDS + 0.11f) * SAMPLE_RATE));

            _output.WriteLine($"the beats' fundamentals: {first:F0} Hz, then {second:F0} Hz");

            Assert.InRange(first, 380f, 460f);
            Assert.True(second < first * 0.95f, $"the second beat is not lower: {first:F0} Hz then {second:F0} Hz");
        }

        /// <summary>
        /// Two beats with a gap between them, not one long tone, and silence after the second. Read off the envelope alone,
        /// in 10 ms frames, and not off the synth's own constants, so a change that moved the beats together fails here: a
        /// strong beat, then at least 30 ms under a tenth of the loudest frame, then a second strong beat, then silence.
        /// </summary>
        [Fact]
        public void It_is_two_beats_and_not_one()
        {
            float[] signal = AimStopSynth.Render(SAMPLE_RATE);

            int frame = SAMPLE_RATE / 100;
            int frames = signal.Length / frame;
            float[] rms = new float[frames];
            float loudest = 0f;
            for (int f = 0; f < frames; f++)
            {
                rms[f] = Rms(signal, f * frame, (f + 1) * frame);
                loudest = MathF.Max(loudest, rms[f]);
            }

            _output.WriteLine("RMS every 10 ms from 0: " + string.Join(" ", Array.ConvertAll(rms, r => r.ToString("F3"))));

            //The first beat, the quiet after it, the second beat, and the quiet at the end, in that order
            int at = 0;
            while (at < frames && rms[at] < 0.5f * loudest) at++;
            Assert.True(at < frames, "there is no beat at all");

            while (at < frames && rms[at] >= 0.1f * loudest) at++;
            int quiet = 0;
            while (at < frames && rms[at] < 0.1f * loudest) { quiet++; at++; }
            Assert.True(quiet >= 3, $"the beats run into one another: {quiet * 10} ms between them");

            Assert.True(at < frames && rms[at] >= 0.1f * loudest, "there is no second beat");
            float second = 0f;
            while (at < frames && rms[at] >= 0.1f * loudest) second = MathF.Max(second, rms[at++]);
            Assert.True(second >= 0.5f * loudest, "the second beat is weak");

            Assert.True(at < frames, "it does not end in silence");
            for (; at < frames; at++) Assert.True(rms[at] < 0.1f * loudest, "a third sound follows the second beat");
        }

        //A fundamental read off the slice's autocorrelation: the lag between 250 and 700 Hz that matches best
        private static float Pitch(float[] signal, int from, int to)
        {
            int shortest = SAMPLE_RATE / 700, longest = SAMPLE_RATE / 250;
            double best = double.MinValue;
            int bestLag = shortest;

            for (int lag = shortest; lag <= longest; lag++)
            {
                double sum = 0.0;
                for (int i = from; i + lag < to; i++) sum += signal[i] * signal[i + lag];
                sum /= to - from - lag;

                if (sum > best)
                {
                    best = sum;
                    bestLag = lag;
                }
            }

            return (float)SAMPLE_RATE / bestLag;
        }

        private static float Rms(float[] signal, int from, int to)
        {
            double sum = 0.0;
            for (int i = from; i < to; i++) sum += signal[i] * signal[i];
            return (float)Math.Sqrt(sum / (to - from));
        }

        //The energy between two frequencies, summed over bins BIN_HZ apart, each one a Goertzel's single-frequency DFT
        private static float BandEnergy(float[] signal, int from, int to, float lowHz, float highHz)
        {
            double energy = 0.0;
            for (float hz = MathF.Max(BIN_HZ, MathF.Ceiling(lowHz / BIN_HZ) * BIN_HZ); hz <= highHz; hz += BIN_HZ)
                energy += BinEnergy(signal, from, to, hz);
            return (float)energy;
        }

        private static double BinEnergy(float[] signal, int from, int to, float hz)
        {
            double w = 2.0 * Math.PI * hz / SAMPLE_RATE;
            double coefficient = 2.0 * Math.Cos(w);
            double s1 = 0.0, s2 = 0.0;

            for (int i = from; i < to; i++)
            {
                //A Hann window, so the slice's own edges do not leak into the neighbouring bins
                double window = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * (i - from) / (to - from - 1));
                double s0 = signal[i] * window + coefficient * s1 - s2;
                s2 = s1;
                s1 = s0;
            }

            return s1 * s1 + s2 * s2 - coefficient * s1 * s2;
        }
    }
}

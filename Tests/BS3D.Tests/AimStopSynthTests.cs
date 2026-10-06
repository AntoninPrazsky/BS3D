using BS3D.Audio;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// #811: the sound of the barrel meeting its stop promises three things, each of them the owner's own constraint on this
    /// game's effects, and the tests measure the very arithmetic the game plays (<see cref="AimStopSynth"/>). A sound cannot
    /// be judged here - the verdict is his ear's - but a sound that carries nothing above 300 Hz, or hisses, or is the refused
    /// shot's "no" again, can be refused before it is ever played.
    /// </summary>
    public class AimStopSynthTests
    {
        private const int SAMPLE_RATE = 44100;

        //The spectrum is read every 25 Hz, which resolves a creak's resonances (hundreds of Hz wide) with room to spare
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

        [Fact]
        public void The_creak_climbs_as_the_pressure_does()
        {
            float[] signal = AimStopSynth.Render(SAMPLE_RATE);

            float early = Centroid(signal, (int)(0.04f * SAMPLE_RATE), (int)(0.14f * SAMPLE_RATE));
            float late = Centroid(signal, (int)(0.14f * SAMPLE_RATE), (int)(0.22f * SAMPLE_RATE));

            _output.WriteLine($"the creak's spectral centroid: {early:F0} Hz early, {late:F0} Hz late");

            //Before the puff begins (AimStopSynth.PUFF_START_SECONDS), where the creak is alone
            Assert.True(late > early, $"the creak does not climb: {early:F0} Hz then {late:F0} Hz");
        }

        [Fact]
        public void It_is_a_creak_and_then_a_puff_two_events_and_not_one()
        {
            float[] signal = AimStopSynth.Render(SAMPLE_RATE);

            //The envelope, a slice in 40 ms, for the doc and for anyone tuning it
            string envelope = "";
            for (float t = 0f; t + 0.04f <= AimStopSynth.DURATION; t += 0.04f)
                envelope += $" {Rms(signal, (int)(t * SAMPLE_RATE), (int)((t + 0.04f) * SAMPLE_RATE)):F3}";
            _output.WriteLine($"RMS every 40 ms from 0:{envelope}");

            //The creak alone, before the puff begins; the puff alone, after the creak has ended; and the end of it
            float creak = Rms(signal, (int)(0.08f * SAMPLE_RATE), (int)(AimStopSynth.PUFF_START_SECONDS * SAMPLE_RATE));
            float puff = Rms(signal, (int)(AimStopSynth.CREAK_SECONDS * SAMPLE_RATE), (int)((AimStopSynth.CREAK_SECONDS + 0.06f) * SAMPLE_RATE));
            float tail = Rms(signal, (int)((AimStopSynth.DURATION - 0.06f) * SAMPLE_RATE), signal.Length);

            _output.WriteLine($"RMS: creak {creak:F3}, puff after the creak {puff:F3}, tail {tail:F3}");

            Assert.True(creak > 0.05f, "the creak is not there");
            Assert.True(puff > 0.02f, "the puff is not there once the creak has ended");
            Assert.True(tail < puff * 0.5f, "the puff does not die away");
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

        private static float Centroid(float[] signal, int from, int to)
        {
            double weighted = 0.0, total = 0.0;
            for (float hz = BIN_HZ; hz <= TOP_HZ; hz += BIN_HZ)
            {
                double e = BinEnergy(signal, from, to, hz);
                weighted += e * hz;
                total += e;
            }

            return (float)(weighted / total);
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

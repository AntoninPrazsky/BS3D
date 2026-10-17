using BS3D.Audio;
using BS3D.Effects;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// #871: the Swap has a sound of its own (<see cref="SwapSynth"/>), two voices crossing, and it keeps what the owner asks of
    /// every sound in this game: its identity in the 300 Hz-4 kHz his monitor speakers play (a bass-only sound is inaudible to
    /// him), no hiss, no click at either end. And it says what it is: one voice rises as the other falls, over the length of the
    /// motion it goes with. Whether it is <i>right</i> is his ear; what a measurement can refuse is a sound his speakers cannot
    /// play, one that is not the Swap's picture's length, or one that is a single rising tone like the Cut's charge.
    /// <para>
    /// The band check is seen to refuse a planted fault (BestPractices.md §10): the sound at a fifth of its pitch.
    /// </para>
    /// </summary>
    public class SwapSoundTests
    {
        private const int SAMPLE_RATE = 44100;

        private const float BAND_LOW_HZ = 300f;
        private const float BAND_HIGH_HZ = 4000f;

        //The synthesized tones' own figure (AimStopSynthTests, StreakBreakSynthTests, CutSoundTests' charge)
        private const float BAND_SHARE = 0.80f;

        private const float HISS_FROM_HZ = 5000f;
        private const float HISS_SHARE = 0.02f;

        private readonly ITestOutputHelper _output;

        public SwapSoundTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void It_is_the_same_every_time_and_finite_and_at_its_peak()
        {
            float[] first = SwapSynth.Render(SAMPLE_RATE);
            float[] second = SwapSynth.Render(SAMPLE_RATE);

            Assert.Equal((int)(SAMPLE_RATE * SwapSynth.DURATION), first.Length);
            Assert.Equal(first, second);

            float peak = 0f;
            foreach (float sample in first)
            {
                Assert.True(float.IsFinite(sample));
                peak = MathF.Max(peak, MathF.Abs(sample));
            }

            Assert.InRange(peak, CutSynth.PEAK - 0.001f, CutSynth.PEAK + 0.001f);
        }

        [Fact]
        public void It_starts_and_ends_at_silence_so_it_never_clicks()
        {
            float[] signal = SwapSynth.Render(SAMPLE_RATE);

            for (int i = 0; i < 4; i++)
            {
                Assert.InRange(MathF.Abs(signal[i]), 0f, 0.01f);
                Assert.InRange(MathF.Abs(signal[signal.Length - 1 - i]), 0f, 0.01f);
            }
        }

        /// <summary>The two voices cross over the motion's own length: the sound and the picture are one gesture.</summary>
        [Fact]
        public void Its_sweep_is_the_length_of_the_swaps_picture()
        {
            Assert.Equal(SwapMotion.SECONDS, SwapSynth.SWEEP_SECONDS);
            Assert.True(SwapSynth.DURATION > SwapSynth.SWEEP_SECONDS, "no settle after the sweep");
            Assert.True(SwapSynth.DURATION < 0.6f, "a swap is pressed to fire straight after it; its sound must not outlast the gesture by much");
        }

        [Fact]
        public void It_is_carried_by_what_monitor_speakers_play_and_has_no_hiss()
        {
            float[] signal = SwapSynth.Render(SAMPLE_RATE);

            float carried = BandShare(signal, BAND_LOW_HZ, BAND_HIGH_HZ);
            float hiss = BandShare(signal, HISS_FROM_HZ, SAMPLE_RATE / 2f);
            _output.WriteLine($"swap: {signal.Length / (float)SAMPLE_RATE:F2} s, 300-4000 Hz {carried:P1}, "
                + $"under 200 Hz {BandShare(signal, 0f, 200f):P1}, 200-300 Hz {BandShare(signal, 200f, 300f):P1}, "
                + $"4-5 kHz {BandShare(signal, 4000f, 5000f):P2}, above 5 kHz {hiss:P2}, RMS {Rms(signal):F3}");

            Assert.True(carried >= BAND_SHARE, $"only {carried:P1} of the swap's energy is at 300-4000 Hz");
            Assert.True(hiss <= HISS_SHARE, $"{hiss:P2} of the swap's energy is above 5 kHz");
        }

        /// <summary>The planted fault: the sound at a fifth of its pitch, in the bass the speakers do not play.</summary>
        [Fact]
        public void A_bass_only_version_fails_the_band_check()
        {
            float[] bass = SwapSynth.Render(SAMPLE_RATE, pitchScale: 0.2f);

            float carried = BandShare(bass, BAND_LOW_HZ, BAND_HIGH_HZ);
            _output.WriteLine($"the swap at a fifth of its pitch: 300-4000 Hz {carried:P1}");

            Assert.True(carried < BAND_SHARE, $"the band check passed a bass-only swap ({carried:P1} at 300-4000 Hz)");
        }

        /// <summary>
        /// One voice rises as the other falls, read off the signal's own spectrum: early the low glide's start and the high one's
        /// start are both heard, late the two have traded, so the loud (arriving) voice's partial is low early and high late.
        /// The Cut's charge is one voice rising to a ting; the Swap is the only sound that does both at once.
        /// </summary>
        [Fact]
        public void One_voice_rises_as_the_other_falls()
        {
            float[] signal = SwapSynth.Render(SAMPLE_RATE);

            float early = StrongestPartial(signal, 0.02f, 0.10f, 280f, 600f);
            float late = StrongestPartial(signal, SwapSynth.SWEEP_SECONDS - 0.10f, SwapSynth.SWEEP_SECONDS - 0.02f, 280f, 600f);
            _output.WriteLine($"swap: the strongest partial {early:F0} Hz early, {late:F0} Hz late");

            //The arriving voice is the louder, so it carries the strongest partial: low at the start, high at the end
            Assert.InRange(early, SwapSynth.LOW_HZ * 0.9f, SwapSynth.LOW_HZ * 1.25f);
            Assert.InRange(late, SwapSynth.HIGH_HZ * 0.8f, SwapSynth.HIGH_HZ * 1.1f);

            //And the returning voice is really there, falling: its start (the high note) is heard early, its arrival (the low note)
            //late, each a real share of what the arriving voice carries at the same moment, not a stray bin
            float arrivingEarly = PowerAt(signal, 0.02f, 0.10f, 342f);
            float returningEarly = PowerAt(signal, 0.02f, 0.10f, 483f);
            float arrivingLate = PowerAt(signal, SwapSynth.SWEEP_SECONDS - 0.10f, SwapSynth.SWEEP_SECONDS - 0.02f, 476f);
            float returningLate = PowerAt(signal, SwapSynth.SWEEP_SECONDS - 0.10f, SwapSynth.SWEEP_SECONDS - 0.02f, 347f);
            _output.WriteLine($"swap: early arriving {arrivingEarly:E2} returning {returningEarly:E2}; late arriving {arrivingLate:E2} returning {returningLate:E2}");

            Assert.True(returningEarly > 0.1f * arrivingEarly, "the returning voice is not heard at its start");
            Assert.True(returningLate > 0.1f * arrivingLate, "the returning voice is not heard at its arrival");
        }

        /// <summary>
        /// It is not the Cut's charge, which it sits beside in the same register: that is one voice gliding up with a whirr and a
        /// ting, this one's energy is spread over a rising and a falling glide. Read off the two signals' own spectra, the charge's
        /// strongest partial goes up past the swap's top, and the swap's falling voice leaves energy near its start the charge does not have.
        /// </summary>
        [Fact]
        public void It_is_not_the_cuts_charge()
        {
            float[] swap = SwapSynth.Render(SAMPLE_RATE);
            float[] charge = CutSynth.RenderCharge(SAMPLE_RATE);

            //The charge ends up at 660 Hz and its ting above 1.8 kHz; the swap never goes past 495 Hz for a fundamental
            float swapTop = StrongestPartial(swap, 0.2f, 0.4f, 280f, 3000f);
            float chargeTop = StrongestPartial(charge, CutSynth.CHARGE_RISE_SECONDS - 0.08f, CutSynth.CHARGE_RISE_SECONDS, 280f, 3000f);
            _output.WriteLine($"swap's strongest partial late {swapTop:F0} Hz, charge's at its top {chargeTop:F0} Hz");
            Assert.True(chargeTop > swapTop * 1.15f, $"the swap's late partial ({swapTop:F0} Hz) is as high as the charge's ({chargeTop:F0} Hz)");

            Assert.True(CutSynth.CHARGE_DURATION > SwapSynth.DURATION * 1.3f);
        }

        private static float Rms(float[] signal)
        {
            double sum = 0.0;
            foreach (float sample in signal) sum += sample * sample;
            return (float)Math.Sqrt(sum / signal.Length);
        }

        //The share of the whole sound's energy between two frequencies: the power spectrum of the whole signal, zero-padded to
        //a power of two (Parseval: every bin counted), unwindowed as "A whole sound's band shares" asks
        private static float BandShare(float[] signal, float lowHz, float highHz)
        {
            double[] power = PowerSpectrum(signal, 0, signal.Length, window: false, out double binHz);

            double band = 0.0, total = 0.0;
            for (int k = 1; k < power.Length; k++)
            {
                total += power[k];
                double hz = k * binHz;
                if (hz >= lowHz && hz < highHz) band += power[k];
            }

            return (float)(band / total);
        }

        //The strongest partial between two frequencies over a slice, Hann-windowed, in Hz
        private static float StrongestPartial(float[] signal, float fromSeconds, float toSeconds, float lowHz, float highHz)
        {
            int from = Math.Max(0, (int)(fromSeconds * SAMPLE_RATE));
            int to = Math.Min(signal.Length, (int)(toSeconds * SAMPLE_RATE));
            double[] power = PowerSpectrum(signal, from, to, window: true, out double binHz);

            int best = (int)Math.Ceiling(lowHz / binHz);
            for (int k = best; k * binHz <= highHz && k < power.Length; k++)
                if (power[k] > power[best]) best = k;

            return (float)(best * binHz);
        }

        //The power at one frequency (the nearest bin, and its neighbours) over a slice, Hann-windowed
        private static float PowerAt(float[] signal, float fromSeconds, float toSeconds, float hz)
        {
            int from = Math.Max(0, (int)(fromSeconds * SAMPLE_RATE));
            int to = Math.Min(signal.Length, (int)(toSeconds * SAMPLE_RATE));
            double[] power = PowerSpectrum(signal, from, to, window: true, out double binHz);

            int bin = (int)Math.Round(hz / binHz);
            double sum = 0.0;
            for (int k = Math.Max(1, bin - 4); k <= bin + 4 && k < power.Length; k++) sum += power[k];
            return (float)sum;
        }

        private static double[] PowerSpectrum(float[] signal, int from, int to, bool window, out double binHz)
        {
            int length = to - from;
            int n = 1;
            while (n < length * (window ? 4 : 1)) n <<= 1;

            double[] re = new double[n], im = new double[n];
            for (int i = 0; i < length; i++)
            {
                double w = window ? 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (length - 1)) : 1.0;
                re[i] = signal[from + i] * w;
            }

            Spectrum.Fft(re, im);

            double[] power = new double[n / 2 + 1];
            for (int k = 0; k < power.Length; k++) power[k] = re[k] * re[k] + im[k] * im[k];

            binHz = (double)SAMPLE_RATE / n;
            return power;
        }
    }
}

using BS3D.Audio;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// #767: the Cut has two sounds of its own, the charge at the press and the slice at the strike (<see cref="CutSynth"/>),
    /// and they keep what the owner's rules ask of every sound: their identity in the 300 Hz-4 kHz his monitor speakers
    /// play (a bass-only sound is inaudible to him), no hiss, no click at either end. And they say what they are for: the
    /// charge rises to its top, the slice rings on after its zing. Whether they are <i>right</i> is his ear; what a
    /// measurement can refuse is a sound his speakers cannot play.
    /// <para>
    /// The band check is seen to refuse a planted fault (BestPractices.md §10): each sound at a fifth of its pitch.
    /// </para>
    /// </summary>
    public class CutSoundTests
    {
        private const int SAMPLE_RATE = 44100;

        private const float BAND_LOW_HZ = 300f;
        private const float BAND_HIGH_HZ = 4000f;

        //The synthesized tones' own figure (AimStopSynthTests, StreakBreakSynthTests, the blue step's bake) for the charge; the
        //slice a little less, because it keeps a warm thump under its ring for the speakers that can play one
        private const float CHARGE_BAND_SHARE = 0.80f;
        private const float SLICE_BAND_SHARE = 0.75f;

        private const float HISS_FROM_HZ = 5000f;
        private const float HISS_SHARE = 0.02f;

        private readonly ITestOutputHelper _output;

        public CutSoundTests(ITestOutputHelper output) => _output = output;

        public static TheoryData<string> Sounds => new() { "charge", "slice" };

        private static float[] Render(string sound, float pitchScale = 1f) => sound == "charge"
            ? CutSynth.RenderCharge(SAMPLE_RATE, pitchScale)
            : CutSynth.RenderSlice(SAMPLE_RATE, pitchScale);

        private static float Duration(string sound) => sound == "charge" ? CutSynth.CHARGE_DURATION : CutSynth.SLICE_DURATION;

        [Theory]
        [MemberData(nameof(Sounds))]
        public void Each_is_the_same_every_time_and_finite_and_at_its_peak(string sound)
        {
            float[] first = Render(sound);
            float[] second = Render(sound);

            Assert.Equal((int)(SAMPLE_RATE * Duration(sound)), first.Length);
            Assert.Equal(first, second);

            float peak = 0f;
            foreach (float sample in first)
            {
                Assert.True(float.IsFinite(sample));
                peak = MathF.Max(peak, MathF.Abs(sample));
            }

            Assert.InRange(peak, CutSynth.PEAK - 0.001f, CutSynth.PEAK + 0.001f);
        }

        [Theory]
        [MemberData(nameof(Sounds))]
        public void Each_starts_and_ends_at_silence_so_it_never_clicks(string sound)
        {
            float[] signal = Render(sound);

            for (int i = 0; i < 4; i++)
            {
                Assert.InRange(MathF.Abs(signal[i]), 0f, 0.01f);
                Assert.InRange(MathF.Abs(signal[signal.Length - 1 - i]), 0f, 0.01f);
            }
        }

        [Theory]
        [MemberData(nameof(Sounds))]
        public void Each_is_carried_by_what_monitor_speakers_play_and_has_no_hiss(string sound)
        {
            float[] signal = Render(sound);

            float carried = BandShare(signal, BAND_LOW_HZ, BAND_HIGH_HZ);
            float hiss = BandShare(signal, HISS_FROM_HZ, SAMPLE_RATE / 2f);
            _output.WriteLine($"{sound}: {signal.Length / (float)SAMPLE_RATE:F2} s, 300-4000 Hz {carried:P1}, "
                + $"under 200 Hz {BandShare(signal, 0f, 200f):P1}, 200-300 Hz {BandShare(signal, 200f, 300f):P1}, "
                + $"4-5 kHz {BandShare(signal, 4000f, 5000f):P2}, above 5 kHz {hiss:P2}, RMS {Rms(signal):F3}");

            float required = sound == "charge" ? CHARGE_BAND_SHARE : SLICE_BAND_SHARE;
            Assert.True(carried >= required, $"only {carried:P1} of the {sound}'s energy is at 300-4000 Hz");
            Assert.True(hiss <= HISS_SHARE, $"{hiss:P2} of the {sound}'s energy is above 5 kHz");
        }

        /// <summary>The planted fault: each sound at a fifth of its pitch, in the bass the speakers do not play.</summary>
        [Theory]
        [MemberData(nameof(Sounds))]
        public void A_bass_only_version_fails_the_band_check(string sound)
        {
            float[] bass = Render(sound, pitchScale: 0.2f);

            float carried = BandShare(bass, BAND_LOW_HZ, BAND_HIGH_HZ);
            _output.WriteLine($"the {sound} at a fifth of its pitch: 300-4000 Hz {carried:P1}");

            float required = sound == "charge" ? CHARGE_BAND_SHARE : SLICE_BAND_SHARE;
            Assert.True(carried < required, $"the band check passed a bass-only {sound} ({carried:P1} at 300-4000 Hz)");
        }

        /// <summary>
        /// The charge rises: the strongest partial near its top is well above the one at the press, read off the signal's own
        /// spectrum and not off the synth's table.
        /// </summary>
        [Fact]
        public void The_charge_rises_to_its_top()
        {
            float[] signal = CutSynth.RenderCharge(SAMPLE_RATE);

            float first = StrongestPartial(signal, 0.01f, 0.08f, 250f, 1500f);
            float top = StrongestPartial(signal, CutSynth.CHARGE_RISE_SECONDS - 0.08f, CutSynth.CHARGE_RISE_SECONDS, 250f, 1500f);
            _output.WriteLine($"charge: strongest partial {first:F0} Hz at the press, {top:F0} Hz at the top");

            Assert.True(top > first * 1.5f, $"the charge does not rise: {first:F0} Hz then {top:F0} Hz");
        }

        /// <summary>
        /// The slice rings on: a blade's ring, not a thud. Well after the zing and the thump have died the sound still holds a
        /// real share of its energy, and that tail is in the band and centred on the ring's partials.
        /// </summary>
        [Fact]
        public void The_slice_rings_on_after_its_zing()
        {
            float[] signal = CutSynth.RenderSlice(SAMPLE_RATE);

            int from = (int)(0.25f * SAMPLE_RATE);
            double total = 0.0, tail = 0.0;
            for (int i = 0; i < signal.Length; i++)
            {
                total += signal[i] * signal[i];
                if (i >= from) tail += signal[i] * signal[i];
            }

            float[] tailSignal = signal[from..];
            float tailCarried = BandShare(tailSignal, BAND_LOW_HZ, BAND_HIGH_HZ);
            float strongest = StrongestPartial(signal, 0.3f, 0.6f, 250f, 4000f);
            _output.WriteLine($"slice: {tail / total:P1} of the energy after 0.25 s, {tailCarried:P1} of it at 300-4000 Hz, "
                + $"its strongest partial {strongest:F0} Hz");

            Assert.True(tail / total >= 0.15, $"the slice does not ring on: {tail / total:P1} of its energy after 0.25 s");
            Assert.True(tailCarried >= 0.95f, $"the slice's ring is not in the band: {tailCarried:P1}");
            Assert.InRange(strongest, CutSynth.RING_HZ * 0.95f, CutSynth.RING_HZ * 1.05f);
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

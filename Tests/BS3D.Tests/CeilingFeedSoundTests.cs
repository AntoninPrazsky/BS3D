using BS3D.Audio;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// #726: the blue ceiling step (a tall level's feed, and the Brake's lift) has a sound of its own, and it keeps what the
    /// issue asked of it. The owner heard the red step and not the blue one, which was the red step's recording turned down:
    /// 86.7 % of that recording's energy is under 200 Hz, where his monitor's built-in speakers play almost nothing. So the
    /// new sound's identity has to sit at 300 Hz-4 kHz, it must not hiss, it rises (a reward, where the red step falls and
    /// settles), and it is not the red step. Measured on the shipped recording, decoded by the game's own
    /// <see cref="OggTrack"/> the way <c>ProceduralAudio.TryLoadSfx</c> loads it, and on the bake that stands behind a missing
    /// file (<see cref="CeilingFeedSynth"/>). Whether it is <i>right</i> is the owner's ear; what a measurement can refuse is a
    /// sound his speakers cannot play.
    /// <para>
    /// The band check is seen to refuse two planted faults (BestPractices.md §10): the bake transposed down into the bass,
    /// and the shipped red step itself, the very sound that went unheard.
    /// </para>
    /// </summary>
    public class CeilingFeedSoundTests
    {
        private const int SAMPLE_RATE = 44100;

        //What the owner's speakers carry, and the share of the energy that has to sit there: the synthesized tones' own
        //figure (AimStopSynthTests, StreakBreakSynthTests) for the bake; for the recording a little less, because the issue
        //asks for a warm low body to be kept underneath it for the speakers that can play one
        private const float BAND_LOW_HZ = 300f;
        private const float BAND_HIGH_HZ = 4000f;
        private const float BAKE_BAND_SHARE = 0.80f;
        private const float RECORDING_BAND_SHARE = 0.75f;

        //No hiss: the standing rule on this game's effects
        private const float HISS_FROM_HZ = 5000f;
        private const float HISS_SHARE = 0.02f;

        private const string FEED = "ceiling-feed";
        private const string RED_STEP = "ceiling-step";

        private readonly ITestOutputHelper _output;

        public CeilingFeedSoundTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void The_bake_is_the_same_every_time_and_finite_and_at_its_peak()
        {
            float[] first = CeilingFeedSynth.Render(SAMPLE_RATE);
            float[] second = CeilingFeedSynth.Render(SAMPLE_RATE);

            Assert.Equal((int)(SAMPLE_RATE * CeilingFeedSynth.DURATION), first.Length);
            Assert.Equal(first, second);

            float peak = 0f;
            foreach (float sample in first)
            {
                Assert.True(float.IsFinite(sample));
                peak = MathF.Max(peak, MathF.Abs(sample));
            }

            Assert.InRange(peak, CeilingFeedSynth.PEAK - 0.001f, CeilingFeedSynth.PEAK + 0.001f);
        }

        [Fact]
        public void The_bake_starts_and_ends_at_silence_so_it_never_clicks()
        {
            float[] signal = CeilingFeedSynth.Render(SAMPLE_RATE);

            for (int i = 0; i < 4; i++)
            {
                Assert.InRange(MathF.Abs(signal[i]), 0f, 0.01f);
                Assert.InRange(MathF.Abs(signal[signal.Length - 1 - i]), 0f, 0.01f);
            }
        }

        [Fact]
        public void The_bake_is_carried_by_what_monitor_speakers_play_and_has_no_hiss()
        {
            float[] signal = CeilingFeedSynth.Render(SAMPLE_RATE);

            float carried = BandShare(signal, BAND_LOW_HZ, BAND_HIGH_HZ);
            float hiss = BandShare(signal, HISS_FROM_HZ, SAMPLE_RATE / 2f);
            _output.WriteLine($"bake: 300-4000 Hz {carried:P1}, under 200 Hz {BandShare(signal, 0f, 200f):P1}, above 5 kHz {hiss:P2}");

            Assert.True(carried >= BAKE_BAND_SHARE, $"only {carried:P1} of the bake's energy is at 300-4000 Hz");
            Assert.True(hiss <= HISS_SHARE, $"{hiss:P2} of the bake's energy is above 5 kHz");
        }

        /// <summary>The planted fault: the same bake at a fifth of its pitch, in the bass the speakers do not play.</summary>
        [Fact]
        public void A_bass_only_bake_fails_the_band_check()
        {
            float[] bass = CeilingFeedSynth.Render(SAMPLE_RATE, pitchScale: 0.2f);

            float carried = BandShare(bass, BAND_LOW_HZ, BAND_HIGH_HZ);
            _output.WriteLine($"the bake at a fifth of its pitch: 300-4000 Hz {carried:P1}");

            Assert.True(carried < BAKE_BAND_SHARE, $"the band check passed a bass-only bake ({carried:P1} at 300-4000 Hz)");
        }

        /// <summary>
        /// It rises: the strongest partial once the second note rings is higher than the first note's, read off the signal's
        /// own spectrum and not off the synth's table.
        /// </summary>
        [Fact]
        public void The_bake_rises()
        {
            float[] signal = CeilingFeedSynth.Render(SAMPLE_RATE);

            float first = StrongestPartial(signal, 0.01f, 0.10f);
            float later = StrongestPartial(signal, CeilingFeedSynth.SECOND_NOTE_SECONDS + 0.12f, CeilingFeedSynth.SECOND_NOTE_SECONDS + 0.32f);
            _output.WriteLine($"bake: strongest partial {first:F0} Hz, then {later:F0} Hz");

            Assert.True(later > first * 1.2f, $"the bake does not rise: {first:F0} Hz then {later:F0} Hz");
        }

        /// <summary>
        /// The recording the game plays (<c>Game/Sfx/ceiling-feed.ogg</c>), decoded as the game decodes it: its identity in the
        /// band the owner's speakers play, a body under it allowed, no hiss.
        /// </summary>
        [Fact]
        public void The_shipped_recording_is_carried_by_what_monitor_speakers_play_and_has_no_hiss()
        {
            float[] signal = LoadAsTheGameDoes(FEED);

            float carried = BandShare(signal, BAND_LOW_HZ, BAND_HIGH_HZ);
            float hiss = BandShare(signal, HISS_FROM_HZ, SAMPLE_RATE / 2f);
            _output.WriteLine($"{FEED}.ogg: {signal.Length / (float)SAMPLE_RATE:F2} s, 300-4000 Hz {carried:P1}, "
                + $"under 200 Hz {BandShare(signal, 0f, 200f):P1}, above 5 kHz {hiss:P2}");

            Assert.True(carried >= RECORDING_BAND_SHARE, $"only {carried:P1} of {FEED}.ogg's energy is at 300-4000 Hz");
            Assert.True(hiss <= HISS_SHARE, $"{hiss:P2} of {FEED}.ogg's energy is above 5 kHz");
        }

        /// <summary>
        /// The second planted fault, and a real one: the red step's recording, which is what the blue step played until #726
        /// and what the owner did not hear in it. The band check must refuse it, or its passing the feed says nothing.
        /// </summary>
        [Fact]
        public void The_red_steps_recording_fails_the_band_check()
        {
            float[] red = LoadAsTheGameDoes(RED_STEP);

            float carried = BandShare(red, BAND_LOW_HZ, BAND_HIGH_HZ);
            _output.WriteLine($"{RED_STEP}.ogg: 300-4000 Hz {carried:P1}, under 200 Hz {BandShare(red, 0f, 200f):P1}");

            Assert.True(carried < RECORDING_BAND_SHARE, $"the band check passed the red step's recording ({carried:P1} at 300-4000 Hz)");
        }

        /// <summary>
        /// The blue step is not the red step: its energy under 200 Hz, where the red step's drone and settle live, is a small
        /// fraction of the red's, and it rises where the red step settles — read as the strongest partial early in the sound
        /// against later in it, after its loudest moment.
        /// </summary>
        [Fact]
        public void The_shipped_recording_rises_and_is_not_the_red_step()
        {
            float[] feed = LoadAsTheGameDoes(FEED);
            float[] red = LoadAsTheGameDoes(RED_STEP);

            float feedLow = BandShare(feed, 0f, 200f);
            float redLow = BandShare(red, 0f, 200f);
            _output.WriteLine($"under 200 Hz: {FEED} {feedLow:P1}, {RED_STEP} {redLow:P1}");
            Assert.True(feedLow < 0.25f * redLow, $"{FEED}.ogg is as bass-led as the red step ({feedLow:P1} under 200 Hz against {redLow:P1})");

            //Early: the sound's first tenth of a second; later: the fifth of a second after it
            int onset = Onset(feed);
            float onsetSeconds = onset / (float)SAMPLE_RATE;
            float first = StrongestPartial(feed, onsetSeconds, onsetSeconds + 0.10f);
            float later = StrongestPartial(feed, onsetSeconds + 0.15f, onsetSeconds + 0.35f);
            _output.WriteLine($"{FEED}.ogg from {onsetSeconds:F2} s: strongest partial {first:F0} Hz, then {later:F0} Hz");

            Assert.True(later > first * 1.2f, $"{FEED}.ogg does not rise: {first:F0} Hz then {later:F0} Hz");
        }

        //The file as ProceduralAudio.TryLoadSfx reads it: OggTrack's stereo PCM, one channel (MusicBake writes both the same),
        //then FromSfxOrBake's law for a tone, a peak of 0.9
        private static float[] LoadAsTheGameDoes(string name)
        {
            byte[] pcm = OggTrack.Decode(Shipped.Sfx(name), SAMPLE_RATE);
            float[] signal = new float[pcm.Length / 4];
            for (int i = 0; i < signal.Length; i++)
                signal[i] = (short)(pcm[i * 4] | (pcm[i * 4 + 1] << 8)) / 32768f;

            float peak = 0f;
            foreach (float sample in signal) peak = MathF.Max(peak, MathF.Abs(sample));
            Assert.True(peak > 1e-3f, $"{name}.ogg is silent");
            for (int i = 0; i < signal.Length; i++) signal[i] *= 0.9f / peak;

            return signal;
        }

        //Where the sound begins: the first sample within 20 dB of its peak
        private static int Onset(float[] signal)
        {
            float peak = 0f;
            foreach (float sample in signal) peak = MathF.Max(peak, MathF.Abs(sample));
            for (int i = 0; i < signal.Length; i++)
                if (MathF.Abs(signal[i]) >= 0.1f * peak) return i;
            return 0;
        }

        //The share of the whole sound's energy between two frequencies: the power spectrum of the whole signal, zero-padded to
        //a power of two (Parseval: every bin counted, none sampled past)
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

        //The strongest partial between 150 Hz and 2 kHz over a slice, Hann-windowed, in Hz
        private static float StrongestPartial(float[] signal, float fromSeconds, float toSeconds)
        {
            int from = Math.Max(0, (int)(fromSeconds * SAMPLE_RATE));
            int to = Math.Min(signal.Length, (int)(toSeconds * SAMPLE_RATE));
            double[] power = PowerSpectrum(signal, from, to, window: true, out double binHz);

            int best = 0;
            for (int k = (int)Math.Ceiling(150.0 / binHz); k * binHz <= 2000.0 && k < power.Length; k++)
                if (power[k] > power[best]) best = k;

            return (float)(best * binHz);
        }

        //|X(k)|² for k up to Nyquist, over signal[from..to) zero-padded to the next power of two (at least four times the
        //slice, so a short slice's partials are interpolated finely enough to tell a fifth apart), through the game's FFT
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

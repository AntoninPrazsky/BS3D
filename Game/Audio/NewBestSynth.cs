using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The sound of a new best (#926): a quick <b>rising glitter</b> of struck bells, six notes up an A major arpeggio, landing on a chord that
    /// rings, under the word "NEW BEST" striking onto the result page and throwing its sparkles. It plays on the frame the word lands
    /// (<c>ResultPage</c>), after the last earned star's own chime has begun, and is its own sound: the stars rise one note each in
    /// the same key, and this is the whole arpeggio at once, six notes in a third of a second.
    /// <para>
    /// <b>The owner's rules for the game's sounds, kept by construction</b> (<c>docs/game-feedback/sound-effects.md</c>): pleasant over credible
    /// and <b>bright</b> over big, so struck, rounded tones and no noise at all, which leaves nothing to hiss; its identity in 300 Hz-4 kHz,
    /// where his monitor's own speakers play (the lowest note is E5, 659 Hz, a warm A4 sounds under the chord, the highest partial is rolled
    /// off twice at <see cref="ROUNDING_CUTOFF_HZ"/>); no drive, peak-normalised.
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>The glitter</b> is E5 A5 C#6 E6 A6 C#7, 50 ms apart, each a soft mallet's tone (the fundamental, a quiet octave, the
    /// struck bar's own inharmonic 2.76 partial for the glassy edge) that dies in a tenth of a second, so the run is a flurry of
    /// separate glints and not a chord arriving.</description></item>
    /// <item><description><b>The chord</b> is A5, E6 and A6 with the A4 and E5 beneath, struck on the run's last note and left to ring for about a
    /// second: the major triad of the star chimes' own key (the stars are A5, C#6, E6 and A6), so the two sit together whenever they overlap.</description></item>
    /// </list>
    /// Pure arithmetic in a file of its own, as <see cref="StreakBreakSynth"/> is, so the tests compile the lines the game plays.
    /// </summary>
    internal static class NewBestSynth
    {
        /// <summary>The glitter, each note's fundamental in Hz, in the order it is struck.</summary>
        public static readonly float[] RUN_HZ = { 659.26f, 880f, 1108.73f, 1318.51f, 1760f, 2217.46f };

        /// <summary>The chord struck with the run's last note, each note's fundamental in Hz and its level against a run note's 1.</summary>
        public static readonly (float Hz, float Level)[] CHORD = { (440f, 0.55f), (659.26f, 0.5f), (880f, 0.8f), (1318.51f, 0.6f), (1760f, 0.4f) };

        /// <summary>Seconds from one note of the run to the next.</summary>
        public const float NOTE_SPACING = 0.05f;

        /// <summary>How long the chord is left to ring after it is struck, in seconds.</summary>
        public const float RING_SECONDS = 1.1f;

        /// <summary>The peak the sound is normalised to, the other effects' headroom.</summary>
        public const float PEAK = 0.9f;

        //A note of the run: a few milliseconds of attack so it never clicks, and a quick decay so the run is glints
        private const float ATTACK_SECONDS = 0.003f;
        private const float RUN_DECAY_PER_SECOND = 14f;

        //The chord rings longer, and its lowest notes swell in rather than strike, which is what makes the bed under it warm
        private const float CHORD_DECAY_PER_SECOND = 3.4f;
        private const float BED_ATTACK_SECONDS = 0.035f;

        //The partials, against the fundamental's 1: the quiet octave and the struck bar's inharmonic 2.76, which dies faster
        private const float OCTAVE = 0.22f;
        private const float BAR_PARTIAL = 2.76f;
        private const float BAR_PARTIAL_LEVEL = 0.16f;
        private const float BAR_PARTIAL_DECAY = 2.8f;

        //The top note's partials reach past what the speakers carry: rolled off twice, as the other bright sounds are
        public const float ROUNDING_CUTOFF_HZ = 3200f;

        //The whole sound faded to nothing over its last stretch, so the chord's ring never stops on a click
        private const float RELEASE_SECONDS = 0.06f;

        /// <summary>When the chord is struck, in seconds: with the last note of the run.</summary>
        public static float ChordAt => (RUN_HZ.Length - 1) * NOTE_SPACING;

        /// <summary>How long the sound lasts, in seconds.</summary>
        public static float Duration => ChordAt + RING_SECONDS;

        /// <summary>The sound of a new best, mono at <paramref name="sampleRate"/>: the same every time.</summary>
        public static float[] Render(int sampleRate) => Render(sampleRate, 1f);

        /// <summary>
        /// <see cref="Render(int)"/> with every pitch scaled by <paramref name="pitchScale"/>. Only the tests ask for another scale than 1: the
        /// same glitter transposed down into the bass is the planted fault the band check has to refuse.
        /// </summary>
        internal static float[] Render(int sampleRate, float pitchScale)
        {
            float[] signal = new float[(int)(sampleRate * Duration)];

            for (int n = 0; n < RUN_HZ.Length; n++)
                AddNote(signal, sampleRate, n * NOTE_SPACING, RUN_HZ[n] * pitchScale, 1f, RUN_DECAY_PER_SECOND, ATTACK_SECONDS);

            //The chord's low notes swell in, its high ones are struck: the bed under the bells
            foreach ((float hz, float level) in CHORD)
                AddNote(signal, sampleRate, ChordAt, hz * pitchScale, level, CHORD_DECAY_PER_SECOND,
                    hz < 700f ? BED_ATTACK_SECONDS : ATTACK_SECONDS);

            signal = LowPass(LowPass(signal, ROUNDING_CUTOFF_HZ, sampleRate), ROUNDING_CUTOFF_HZ, sampleRate);

            int release = (int)(RELEASE_SECONDS * sampleRate);
            for (int i = 0; i < release && i < signal.Length; i++)
                signal[signal.Length - 1 - i] *= SmoothStep((float)i / release);

            Normalize(signal, PEAK);
            return signal;
        }

        /// <summary>One struck note from <paramref name="start"/> to the end of the buffer.</summary>
        private static void AddNote(float[] signal, int sampleRate, float start, float hz, float level, float decayPerSecond, float attack)
        {
            int first = (int)(start * sampleRate);
            int count = signal.Length - first;

            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                phase += 2f * MathF.PI * hz / sampleRate;

                float body = MathF.Min(1f, t / attack) * MathF.Exp(-t * decayPerSecond);
                float bar = MathF.Exp(-t * decayPerSecond * BAR_PARTIAL_DECAY);

                float tone = MathF.Sin(phase) + OCTAVE * MathF.Sin(2f * phase);
                signal[first + i] += level * (body * tone + body * bar * BAR_PARTIAL_LEVEL * MathF.Sin(BAR_PARTIAL * phase));
            }
        }

        /// <summary>A one-pole low-pass over a whole array; the same arithmetic as <c>ProceduralAudio.LowPassArray</c>.</summary>
        private static float[] LowPass(float[] input, float cutoff, int sampleRate)
        {
            float dt = 1f / sampleRate;
            float rc = 1f / (2f * MathF.PI * cutoff);
            float alpha = dt / (rc + dt);

            float[] output = new float[input.Length];
            float previous = 0f;
            for (int i = 0; i < input.Length; i++)
            {
                previous += alpha * (input[i] - previous);
                output[i] = previous;
            }

            return output;
        }

        private static void Normalize(float[] signal, float target)
        {
            float peak = 0f;
            for (int i = 0; i < signal.Length; i++) peak = MathF.Max(peak, MathF.Abs(signal[i]));
            if (peak < 1e-6f) return;

            float scale = target / peak;
            for (int i = 0; i < signal.Length; i++) signal[i] *= scale;
        }

        private static float SmoothStep(float x) => x * x * (3f - 2f * x);
    }
}

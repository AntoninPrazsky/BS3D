using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The sound of a streak breaking (#845): a short run of struck notes <b>falling</b>, as many notes as the multiplier
    /// that was lost (one per unit of it, not per <c>ScoreKeeper.MultiplierStep</c>), so a lost ×5 falls five notes from
    /// higher up and a lost ×3 the last three of them. Both
    /// end on the same note: the run is the streak falling back to ×1. It sounds with the badge cracking on the HUD
    /// (<c>PlayHud.BreakStreak</c>); the session decides when (<c>StreakCues.BreakIsTold</c>), and never over the result.
    /// <para>
    /// <b>The owner's rules for the game's sounds, kept by construction</b> (<c>docs/game-feedback/sound-effects.md</c>):
    /// pleasant over credible, so a struck, rounded tone and no shatter of glassy noise (there is no noise in it at all,
    /// so nothing to hiss); its identity in 300 Hz-4 kHz, where his monitor's own speakers play (the lowest note is E4,
    /// 330 Hz, the highest partial is rolled off twice at <see cref="ROUNDING_CUTOFF_HZ"/>); no drive, peak-normalised.
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>The notes</b> fall down an A minor arpeggio, A5 E5 C5 A4 E4: a falling minor figure is the
    /// game's word for "no" and "lost" (the refused shot's two low notes fall too), and ending on
    /// the fifth leaves it unresolved, which a loss should be. Each note glides a little further down as it rings
    /// (<see cref="GLIDE"/>), the deflating half of the gesture.</description></item>
    /// <item><description><b>The tone</b> is a soft mallet's: the fundamental, a quiet octave, and the struck bar's own
    /// inharmonic 2.76 partial for the glassy edge of something breaking (the landing's metal row uses the same ratio),
    /// each note rung on and left to decay into the next.</description></item>
    /// <item><description><b>It starts after <see cref="LEAD_SECONDS"/></b> of silence, so the thud of the landing that broke
    /// the streak (or the moment of the miss) is heard first and the run does not drown it.</description></item>
    /// </list>
    /// Pure arithmetic in a file of its own, as <see cref="AimStopSynth"/> is, so the tests compile the lines the game plays.
    /// </summary>
    internal static class StreakBreakSynth
    {
        /// <summary>The run from its top, each a note's fundamental in Hz: a lost ×N plays the last N of them.</summary>
        public static readonly float[] NOTES_HZ = { 880f, 659.26f, 523.25f, 440f, 329.63f };

        /// <summary>Seconds of silence before the first note: the miss is heard first.</summary>
        public const float LEAD_SECONDS = 0.03f;

        /// <summary>Seconds from one note's onset to the next.</summary>
        public const float NOTE_SPACING = 0.095f;

        /// <summary>How long the last note is left to ring before the sound ends, in seconds.</summary>
        public const float TAIL_SECONDS = 0.36f;

        /// <summary>The peak the sound is normalised to, the other effects' headroom.</summary>
        public const float PEAK = 0.9f;

        /// <summary>How far each note slides down while it rings, as a ratio of its pitch at its end.</summary>
        public const float GLIDE = 0.97f;

        //How a note comes on and dies: a few milliseconds of attack so it never clicks, a decay that lets it ring into
        //the next, and the whole sound faded to nothing over its last RELEASE_SECONDS
        private const float ATTACK_SECONDS = 0.003f;
        private const float DECAY_PER_SECOND = 7.5f;
        private const float RELEASE_SECONDS = 0.03f;

        //The partials, against the fundamental's 1, and how much faster each dies (a struck bar's high partials go first)
        private const float OCTAVE = 0.22f;
        private const float BAR_PARTIAL = 2.76f;
        private const float BAR_PARTIAL_LEVEL = 0.2f;
        private const float BAR_PARTIAL_DECAY = 2.5f;

        //The highest note's partials reach past what the speakers carry: rolled off twice, as the aim stop's are
        public const float ROUNDING_CUTOFF_HZ = 3000f;

        /// <summary>How many notes a lost streak of <paramref name="lost"/> plays: one per unit of the multiplier, at least two, at most the run.</summary>
        public static int NotesFor(int lost) => Math.Clamp(lost, 2, NOTES_HZ.Length);

        /// <summary>How long the sound of a lost streak of <paramref name="lost"/> lasts, in seconds.</summary>
        public static float Duration(int lost) => LEAD_SECONDS + (NotesFor(lost) - 1) * NOTE_SPACING + TAIL_SECONDS;

        /// <summary>The sound of losing a streak of <paramref name="lost"/>, mono at <paramref name="sampleRate"/>: the same every time.</summary>
        public static float[] Render(int sampleRate, int lost) => Render(sampleRate, lost, 1f);

        /// <summary>
        /// <see cref="Render(int, int)"/> with every pitch scaled by <paramref name="pitchScale"/>. Only the tests ask for
        /// another scale than 1: a run transposed down into the bass is the planted fault the band check has to refuse.
        /// </summary>
        internal static float[] Render(int sampleRate, int lost, float pitchScale)
        {
            int notes = NotesFor(lost);
            float[] signal = new float[(int)(sampleRate * Duration(lost))];

            for (int n = 0; n < notes; n++)
            {
                float hz = NOTES_HZ[NOTES_HZ.Length - notes + n] * pitchScale;
                AddNote(signal, sampleRate, LEAD_SECONDS + n * NOTE_SPACING, hz);
            }

            signal = LowPass(LowPass(signal, ROUNDING_CUTOFF_HZ, sampleRate), ROUNDING_CUTOFF_HZ, sampleRate);

            //The end faded to silence, so the last note's ring never stops on a click
            int release = (int)(RELEASE_SECONDS * sampleRate);
            for (int i = 0; i < release && i < signal.Length; i++)
                signal[signal.Length - 1 - i] *= SmoothStep((float)i / release);

            Normalize(signal, PEAK);
            return signal;
        }

        /// <summary>One struck note from <paramref name="start"/> to the end of the buffer, gliding down by <see cref="GLIDE"/>.</summary>
        private static void AddNote(float[] signal, int sampleRate, float start, float hz)
        {
            int first = (int)(start * sampleRate);
            int count = signal.Length - first;
            float ring = (signal.Length - first) / (float)sampleRate;

            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                phase += 2f * MathF.PI * hz * MathF.Pow(GLIDE, t / ring) / sampleRate;

                float body = MathF.Min(1f, t / ATTACK_SECONDS) * MathF.Exp(-t * DECAY_PER_SECOND);
                float bar = MathF.Exp(-t * DECAY_PER_SECOND * BAR_PARTIAL_DECAY);

                float tone = MathF.Sin(phase) + OCTAVE * MathF.Sin(2f * phase);
                signal[first + i] += body * tone + body * bar * BAR_PARTIAL_LEVEL * MathF.Sin(BAR_PARTIAL * phase);
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

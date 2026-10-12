using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The blue step's sound as a bake (#726): what plays when the glass comes down to feed a tall level, or the Brake lifts
    /// it, and <c>Sfx/ceiling-feed.ogg</c> is not there. The recording is the sound; this stands behind a missing file the way
    /// every generated effect's bake does (<c>ProceduralAudio.FromSfxOrBake</c>), and it is built to say what the recording
    /// says: good news, rising, in the band the owner's speakers play.
    /// <para>
    /// <b>Why the blue step has a sound of its own.</b> Until #726 it played the red step's recording turned down to 0.35, and
    /// that recording is a heavy plate's drone and settle with 86.7 % of its energy under 200 Hz. The owner listens on his
    /// monitor's built-in speakers, which carry almost nothing down there, so the pressure step (louder) was heard and the feed
    /// step was not heard at all. A feed is a reward (the tall level handing the player more of its column) and the lift is
    /// the player's own Brake, so the sound is the red step's opposite in every way the ear can tell: <b>rising</b> where the
    /// red one falls and settles (every rising gesture in this game is a reward, every falling one a "no"), <b>tonal</b> where
    /// it is a rumble, short, and carried in 300 Hz-4 kHz with only a light warm body under it.
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Two struck notes a fifth apart, rising</b>: E4 then B4 <see cref="SECOND_NOTE_SECONDS"/> later,
    /// each sliding a little further up as it rings (<see cref="GLIDE"/>). The first note's octave below (E3, 165 Hz) is
    /// the body, faster to die, there for the speakers that can play it.</description></item>
    /// <item><description><b>A soft mallet's tone</b>: the fundamental, a quiet octave and a quieter twelfth, rolled off twice at
    /// <see cref="ROUNDING_CUTOFF_HZ"/>. No noise at all, so nothing to hiss; no drive; peak-normalised like the tones.</description></item>
    /// </list>
    /// Pure arithmetic in a file of its own, as <see cref="AimStopSynth"/> is, so the tests compile the lines the game plays.
    /// </summary>
    internal static class CeilingFeedSynth
    {
        /// <summary>The length of the whole sound, in seconds.</summary>
        public const float DURATION = 0.8f;

        /// <summary>When the second, higher note is struck, in seconds; the first is struck at once.</summary>
        public const float SECOND_NOTE_SECONDS = 0.11f;

        /// <summary>The two notes' fundamentals at their strike, in Hz: E4 and the B4 a fifth above it, both over 300 Hz.</summary>
        public const float FIRST_NOTE_HZ = 329.63f;
        public const float SECOND_NOTE_HZ = 493.88f;

        /// <summary>How far each note has slid up by the end of the sound, as a ratio of its pitch at the strike.</summary>
        public const float GLIDE = 1.03f;

        /// <summary>The peak the sound is normalised to, the other effects' headroom.</summary>
        public const float PEAK = 0.9f;

        //How a note comes on and dies: a few milliseconds of attack so it never clicks, a decay that lets the first ring
        //into the second, and the whole sound faded to nothing over its last RELEASE_SECONDS
        private const float ATTACK_SECONDS = 0.005f;
        private const float DECAY_PER_SECOND = 4.5f;
        private const float RELEASE_SECONDS = 0.06f;

        //The partials, against the fundamental's 1, and the body an octave under the first note
        private const float OCTAVE = 0.3f;
        private const float TWELFTH = 0.1f;
        private const float BODY = 0.5f;
        private const float BODY_DECAY_PER_SECOND = 6f;

        //Rounded off twice, as the aim stop's and the streak break's are: one pole alone leaves an edge on each strike
        public const float ROUNDING_CUTOFF_HZ = 3000f;

        /// <summary>The sound's samples, mono, at <paramref name="sampleRate"/>: the same ones every time.</summary>
        public static float[] Render(int sampleRate) => Render(sampleRate, 1f);

        /// <summary>
        /// <see cref="Render(int)"/> with every pitch scaled by <paramref name="pitchScale"/>. Only the tests ask for another
        /// scale than 1: the same sound transposed down into the bass is the planted fault the band check has to refuse.
        /// </summary>
        internal static float[] Render(int sampleRate, float pitchScale)
        {
            float[] signal = new float[(int)(sampleRate * DURATION)];

            AddNote(signal, sampleRate, 0f, FIRST_NOTE_HZ * pitchScale, body: true);
            AddNote(signal, sampleRate, SECOND_NOTE_SECONDS, SECOND_NOTE_HZ * pitchScale, body: false);

            signal = LowPass(LowPass(signal, ROUNDING_CUTOFF_HZ, sampleRate), ROUNDING_CUTOFF_HZ, sampleRate);

            //The release over the whole sound's end, after the low-passes, so their lag cannot leave a tail past it
            int fade = (int)(RELEASE_SECONDS * sampleRate);
            for (int i = 0; i < fade && i < signal.Length; i++)
                signal[signal.Length - 1 - i] *= SmoothStep((float)i / fade);

            Normalize(signal, PEAK);
            return signal;
        }

        /// <summary>One struck note from <paramref name="start"/> to the end of the sound, sliding up by <see cref="GLIDE"/>.</summary>
        private static void AddNote(float[] signal, int sampleRate, float start, float hz, bool body)
        {
            int first = (int)(start * sampleRate);
            int count = signal.Length - first;
            float length = (float)count / sampleRate;

            float phase = 0f, bodyPhase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float rising = hz * MathF.Pow(GLIDE, t / length);
                phase += 2f * MathF.PI * rising / sampleRate;

                float attack = MathF.Min(1f, t / ATTACK_SECONDS);
                float envelope = attack * MathF.Exp(-t * DECAY_PER_SECOND);

                float tone = MathF.Sin(phase) + OCTAVE * MathF.Sin(2f * phase) * MathF.Exp(-t * 3f)
                    + TWELFTH * MathF.Sin(3f * phase) * MathF.Exp(-t * 6f);

                float sample = tone * envelope;

                if (body)
                {
                    bodyPhase += 2f * MathF.PI * rising * 0.5f / sampleRate;
                    sample += BODY * MathF.Sin(bodyPhase) * attack * MathF.Exp(-t * BODY_DECAY_PER_SECOND);
                }

                signal[first + i] += sample;
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

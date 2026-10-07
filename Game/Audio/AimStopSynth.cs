using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The sound of the barrel meeting its stop (#811): the player is pushing the aim for higher (or lower) than the gun
    /// will go. Two short, firm tones, the second lower - an "uh-uh", "not there" - timed to what the gun itself does at
    /// the stop: the first as the barrel runs into it, the second as it springs back off its rubber (#431).
    /// <para>
    /// <b>The second design, on the owner's verdict of 2026-10-07</b> ("Vrať"): the first was a stick-slip creak ending in a
    /// soft puff of air, from his own brief ("a creak, a squeak-fart as the pressure rises"), and in the game it read as
    /// "a croak, or a grotesque burp". What he asked for instead: <i>"more technical, closer to the sound you hear when you
    /// can't fire; it could even copy the cannon's animation - 'you can't go here', 'e-e'."</i> So this is the refused
    /// shot's own figure - two notes, each gliding down, the second a step below the first - an octave higher, shorter and
    /// with a harder, reedier tone, so the two read as one family that says two different things: pulling the trigger at
    /// the stop says "no", pressing into it says "uh-uh".
    /// </para>
    /// <para>
    /// Pure arithmetic in a file of its own, as <see cref="DrumLayer"/> is, so the tests compile the very lines the game
    /// plays and can measure what the sound promises: its identity in the 300 Hz - 4 kHz the owner's monitor speakers
    /// reproduce (a bass-only sound is inaudible to him), no hiss (there is no noise in it at all), two beats and not one,
    /// the second lower - and it is not the refusal, whose energy lies under 300 Hz.
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>The tone</b> is a fundamental with odd harmonics a square wave would have, softened (the third at
    /// 0.42, the fifth at 0.16), and a little second harmonic for body: a buzzer's edge, which is what "technical" asks
    /// for, rounded by two one-pole low-passes at <see cref="ROUNDING_CUTOFF_HZ"/> so it is never harsh.</description></item>
    /// <item><description><b>The beats</b>: 440 down to 415 Hz for 0.11 s - the refusal's first note, 220 to 208, an
    /// octave up - then from 0.16 s 392 down to 349 Hz for 0.14 s, a step below the first where the refusal's second note
    /// falls a third; its 0.16 s spacing kept, so the ear hears the relation.</description></item>
    /// </list>
    /// </summary>
    internal static class AimStopSynth
    {
        /// <summary>The length of the whole sound, in seconds.</summary>
        public const float DURATION = 0.34f;

        /// <summary>When the second beat begins, in seconds; the first begins at once.</summary>
        public const float SECOND_BEAT_SECONDS = 0.16f;

        /// <summary>How long each beat sounds, in seconds.</summary>
        public const float FIRST_BEAT_LENGTH = 0.11f;
        public const float SECOND_BEAT_LENGTH = 0.14f;

        /// <summary>The peak the sound is normalised to, which leaves the headroom the other effects leave.</summary>
        public const float PEAK = 0.9f;

        //The two glides: the first the refusal's first note an octave up, the second a step below it
        private const float FIRST_FROM_HZ = 440f;
        private const float FIRST_TO_HZ = 415f;
        private const float SECOND_FROM_HZ = 392f;
        private const float SECOND_TO_HZ = 349f;

        //How each beat comes on and dies: a few milliseconds of attack so it never clicks, a fall that leaves it firm rather
        //than a pluck, and a fade to nothing at its end
        private const float ATTACK_SECONDS = 0.004f;
        private const float DECAY_PER_SECOND = 7f;
        private const float RELEASE_SECONDS = 0.02f;

        //The partials, against the fundamental's 1
        private const float SECOND_HARMONIC = 0.18f;
        private const float THIRD_HARMONIC = 0.42f;
        private const float FIFTH_HARMONIC = 0.16f;

        //Rounded off twice at the top of what the speakers carry: one pole alone leaves the fifth's edge on each attack
        public const float ROUNDING_CUTOFF_HZ = 2800f;

        /// <summary>The sound's samples, mono, at <paramref name="sampleRate"/>: the same ones every time.</summary>
        public static float[] Render(int sampleRate)
        {
            float[] signal = new float[(int)(sampleRate * DURATION)];

            AddBeat(signal, sampleRate, 0f, FIRST_BEAT_LENGTH, FIRST_FROM_HZ, FIRST_TO_HZ);
            AddBeat(signal, sampleRate, SECOND_BEAT_SECONDS, SECOND_BEAT_LENGTH, SECOND_FROM_HZ, SECOND_TO_HZ);

            signal = LowPass(LowPass(signal, ROUNDING_CUTOFF_HZ, sampleRate), ROUNDING_CUTOFF_HZ, sampleRate);

            Normalize(signal, PEAK);
            return signal;
        }

        /// <summary>One beat, gliding from <paramref name="fromHz"/> to <paramref name="toHz"/> across its length.</summary>
        private static void AddBeat(float[] signal, int sampleRate, float start, float length, float fromHz, float toHz)
        {
            int first = (int)(start * sampleRate);
            int count = Math.Min((int)(length * sampleRate), signal.Length - first);

            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                phase += 2f * MathF.PI * fromHz * MathF.Pow(toHz / fromHz, t / length) / sampleRate;

                float envelope = MathF.Min(1f, t / ATTACK_SECONDS) * MathF.Exp(-t * DECAY_PER_SECOND)
                    * SmoothStep(Math.Clamp((length - t) / RELEASE_SECONDS, 0f, 1f));

                float tone = MathF.Sin(phase) + SECOND_HARMONIC * MathF.Sin(2f * phase) + THIRD_HARMONIC * MathF.Sin(3f * phase)
                    + FIFTH_HARMONIC * MathF.Sin(5f * phase);

                signal[first + i] += tone * envelope;
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

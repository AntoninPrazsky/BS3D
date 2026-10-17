using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The Swap's sound (#871): a soft "whoosh-swap" for the two rounds in the bore sliding through each other. Until #871
    /// the press was the mechanism's click (<c>PlayUiClick</c>, the menus' own) and nothing else, so the power-up the player
    /// presses most had no voice of its own.
    /// <para>
    /// It is <b>two voices that cross</b>, which is the picture the ear should follow: one glides up a fifth
    /// (<see cref="LOW_HZ"/> to <see cref="HIGH_HZ"/>, the round arriving at the muzzle, the louder) while the other glides
    /// down the same fifth (the round going back, softer), each swelling to its loudest where they cross and fading as it
    /// arrives, over <see cref="SWEEP_SECONDS"/> - the motion's own length (<c>SwapMotion.SECONDS</c>, a test holds the two
    /// together). Under the crossing sits a soft low body, a round "whump" for the speakers that can play one, and as the
    /// rounds settle one soft note (<see cref="SETTLE_HZ"/>) says they are home. No noise at all, so nothing can hiss (a
    /// whoosh made of air was the first thing the owner ruled out of every sound here), and no drive, so nothing is bent: the
    /// standing rule is pleasant before credible (<c>docs/game-feedback/sound-effects.md</c>). Its identity is in 300 Hz-4
    /// kHz, where the owner's monitor speakers play.
    /// </para>
    /// <para>
    /// Not the shot (a crack and a boom), the landing (one low note a colour), the Cut's charge (one rising voice with a
    /// whirr and a bright "ting"), the ceiling's feed or the menus' click: the Swap is the only sound that both rises and
    /// falls at once. Pure arithmetic in a file of its own, as <see cref="CutSynth"/> is, so the tests compile the lines the
    /// game plays (<c>SwapSoundTests</c>).
    /// </para>
    /// </summary>
    internal static class SwapSynth
    {
        /// <summary>The sound's whole length, in seconds: the sweep and a short settle.</summary>
        public const float DURATION = 0.5f;

        /// <summary>How long the two voices take to cross and arrive, in seconds: the motion's own length.</summary>
        public const float SWEEP_SECONDS = 0.35f;

        /// <summary>The two ends of the glides, in Hz: a fifth apart, both over 300 Hz.</summary>
        public const float LOW_HZ = 330f;
        public const float HIGH_HZ = 495f;

        //The returning voice is softer than the arriving one, so the ear follows the round the player is about to fire
        private const float ARRIVING_LEVEL = 1f;
        private const float RETURNING_LEVEL = 0.55f;

        //Each voice is a fundamental with a soft octave: a round tone, not a reed
        private const float OCTAVE_LEVEL = 0.28f;

        //The swell: a sine bump raised to this power, so the voices fade in and out softly and peak where they cross
        private const float SWELL_POWER = 1.4f;

        //The soft low body under the crossing: a round sine, its bump centred where the voices cross
        public const float BODY_HZ = 165f;
        private const float BODY_LEVEL = 0.3f;
        private const float BODY_WIDTH = 0.16f;

        //The settle: one soft note as the rounds arrive, dying fast
        public const float SETTLE_HZ = 247f;
        private const float SETTLE_LEVEL = 0.42f;
        private const float SETTLE_DECAY_PER_SECOND = 13f;

        //Rounded off twice, as the other tones are
        public const float CUTOFF_HZ = 2400f;

        /// <summary>The sound's samples, mono, at <paramref name="sampleRate"/>: the same ones every time.</summary>
        public static float[] Render(int sampleRate) => Render(sampleRate, 1f);

        /// <summary><see cref="Render(int)"/> with every pitch scaled: the tests' planted bass-only fault.</summary>
        internal static float[] Render(int sampleRate, float pitchScale)
        {
            float[] signal = new float[(int)(sampleRate * DURATION)];

            double arrivingPhase = 0.0, returningPhase = 0.0, bodyPhase = 0.0, settlePhase = 0.0;

            for (int i = 0; i < signal.Length; i++)
            {
                float t = (float)i / sampleRate;

                //How far through the crossing, eased so the glides are slow at the ends and quick as they pass
                float u = MathF.Min(1f, t / SWEEP_SECONDS);
                float eased = u * u * (3f - 2f * u);

                float arrivingHz = LOW_HZ * pitchScale * MathF.Pow(HIGH_HZ / LOW_HZ, eased);
                float returningHz = HIGH_HZ * pitchScale * MathF.Pow(LOW_HZ / HIGH_HZ, eased);
                arrivingPhase += 2.0 * Math.PI * arrivingHz / sampleRate;
                returningPhase += 2.0 * Math.PI * returningHz / sampleRate;

                //The swell, over the sweep alone and nothing after it
                float swell = u >= 1f ? 0f : MathF.Pow(MathF.Sin(MathF.PI * u), SWELL_POWER);

                float arriving = MathF.Sin((float)arrivingPhase) + OCTAVE_LEVEL * MathF.Sin(2f * (float)arrivingPhase);
                float returning = MathF.Sin((float)returningPhase) + OCTAVE_LEVEL * MathF.Sin(2f * (float)returningPhase);
                float sample = swell * (ARRIVING_LEVEL * arriving + RETURNING_LEVEL * returning);

                //The body: a round bump centred on the crossing
                bodyPhase += 2.0 * Math.PI * BODY_HZ * pitchScale / sampleRate;
                float off = (t - SWEEP_SECONDS * 0.5f) / (SWEEP_SECONDS * BODY_WIDTH * 2f);
                sample += BODY_LEVEL * MathF.Sin((float)bodyPhase) * MathF.Exp(-off * off);

                //The settle, struck as the rounds arrive
                if (t >= SWEEP_SECONDS * 0.92f)
                {
                    float since = t - SWEEP_SECONDS * 0.92f;
                    settlePhase += 2.0 * Math.PI * SETTLE_HZ * pitchScale / sampleRate;
                    float strike = MathF.Min(1f, since / 0.012f);
                    sample += SETTLE_LEVEL * MathF.Sin((float)settlePhase) * strike * MathF.Exp(-since * SETTLE_DECAY_PER_SECOND);
                }

                //A few milliseconds of attack so it never clicks
                signal[i] = sample * MathF.Min(1f, t / 0.006f);
            }

            return CutSynth.Finish(signal, sampleRate, CUTOFF_HZ, 0.07f);
        }
    }
}

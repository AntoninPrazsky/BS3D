using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The Cut's two sounds (#767): the <b>charge</b> as the round in the bore becomes a cutter, and the <b>slice</b> as it
    /// severs a storey. Until #767 the press was the mechanism's click (the Swap's) and the strike the ordinary landing's
    /// thunk, so the one thing a player spends a charge on and aims had no voice of its own, and a cut that freed forty balls
    /// sounded like a ball sticking.
    /// <para>
    /// Both are tones with no noise in them at all, so nothing can hiss, and no drive, so nothing is bent (the standing rule:
    /// pleasant before credible, <c>docs/game-feedback/sound-effects.md</c>). Both carry their identity in 300 Hz-4 kHz, where
    /// the owner's monitor speakers play, with only a warm body under it for the speakers that can play one.
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>The charge</b> is a saw spooling up: a blade's tone (a fundamental, its octave and twelfth, and the
    /// struck bar's inharmonic 2.76, the landing's metal ratio) gliding up <see cref="CHARGE_FROM_HZ"/> to
    /// <see cref="CHARGE_TO_HZ"/> as it swells, with a whirr on it - the teeth passing, an amplitude ripple whose rate rises
    /// with the pitch - and, as it arrives (<see cref="CHARGE_RISE_SECONDS"/>), one bright struck "ting": charged. Rising
    /// like every rewarding gesture in this game, and unlike the blue step's two struck notes, it is one continuous voice
    /// that whirrs.</description></item>
    /// <item><description><b>The slice</b> is a blade going through and ringing: a fast "zing" falling from
    /// <see cref="ZING_FROM_HZ"/> to <see cref="ZING_TO_HZ"/> in the first few hundredths of a second, then the struck blade's
    /// ring (<see cref="RING_HZ"/> times 1, 2.76 and 5.40, each partial a pair a few cents apart so the ring shimmers as a
    /// saw blade does and not as a bell) dying over about a second, over a soft warm thump for the weight. It is neither the
    /// bomb's report (bass and nothing else), nor the zap's, nor a landing (one low note a colour): it rings high and
    /// long.</description></item>
    /// </list>
    /// Pure arithmetic in a file of its own, as <see cref="AimStopSynth"/> is, so the tests compile the lines the game plays
    /// (<c>CutSoundTests</c>).
    /// </summary>
    internal static class CutSynth
    {
        /// <summary>The peak both sounds are normalised to, the other effects' headroom.</summary>
        public const float PEAK = 0.9f;

        #region The charge

        /// <summary>The charge's whole length, in seconds.</summary>
        public const float CHARGE_DURATION = 0.78f;

        /// <summary>How long the charge takes to swell and glide up to its top, in seconds: the "ting" lands here. The gun's
        /// own charge (<c>CutStaging.CHARGE_SECONDS</c>) peaks on the same beat.</summary>
        public const float CHARGE_RISE_SECONDS = 0.42f;

        /// <summary>The blade's fundamental at the press and at the top of the glide, in Hz: both over 300 Hz.</summary>
        public const float CHARGE_FROM_HZ = 330f;
        public const float CHARGE_TO_HZ = 660f;

        //The whirr: the teeth passing, an amplitude ripple from this rate to that one as the blade comes up to speed, at this
        //depth. Under 20 Hz at the start it is heard as separate teeth, by the top as a buzz in the tone
        private const float WHIRR_FROM_HZ = 11f;
        private const float WHIRR_TO_HZ = 38f;
        private const float WHIRR_DEPTH = 0.32f;

        //The blade's tone: the octave, the twelfth and the struck bar's inharmonic partial, against the fundamental's 1
        private const float CHARGE_OCTAVE = 0.42f;
        private const float CHARGE_TWELFTH = 0.2f;
        private const float CHARGE_BAR = 0.16f;

        //The warm body an octave under the glide, for the speakers that can play it
        private const float CHARGE_BODY = 0.3f;

        //The "ting" at the top: one struck partial, high in the band and dying fast
        private const float TING_RATIO = 2.76f;
        private const float TING_LEVEL = 0.55f;
        private const float TING_DECAY_PER_SECOND = 9f;

        //After the top the tone settles and fades out by the end
        private const float CHARGE_SETTLE_PER_SECOND = 4.5f;

        //Rounded off twice, as the other tones are
        public const float CHARGE_CUTOFF_HZ = 3200f;

        /// <summary>The charge's samples, mono, at <paramref name="sampleRate"/>: the same ones every time.</summary>
        public static float[] RenderCharge(int sampleRate) => RenderCharge(sampleRate, 1f);

        /// <summary><see cref="RenderCharge(int)"/> with every pitch scaled: the tests' planted bass-only fault.</summary>
        internal static float[] RenderCharge(int sampleRate, float pitchScale)
        {
            float[] signal = new float[(int)(sampleRate * CHARGE_DURATION)];

            double phase = 0.0, bodyPhase = 0.0, whirrPhase = 0.0, tingPhase = 0.0;
            float top = CHARGE_TO_HZ * pitchScale;

            for (int i = 0; i < signal.Length; i++)
            {
                float t = (float)i / sampleRate;

                //How far up the rise: eased out, so the glide is quick at first and arrives softly
                float rise = MathF.Min(1f, t / CHARGE_RISE_SECONDS);
                float eased = 1f - (1f - rise) * (1f - rise);

                float hz = CHARGE_FROM_HZ * pitchScale * MathF.Pow(CHARGE_TO_HZ / CHARGE_FROM_HZ, eased);
                phase += 2.0 * Math.PI * hz / sampleRate;
                bodyPhase += Math.PI * hz / sampleRate;

                float whirrHz = WHIRR_FROM_HZ + (WHIRR_TO_HZ - WHIRR_FROM_HZ) * eased;
                whirrPhase += 2.0 * Math.PI * whirrHz / sampleRate;
                float whirr = 1f - WHIRR_DEPTH * (0.5f + 0.5f * MathF.Cos((float)whirrPhase));

                //Swelling to the top, then settling away; a few milliseconds of attack so it never clicks
                float attack = MathF.Min(1f, t / 0.006f);
                float swell = 0.25f + 0.75f * rise * rise;
                float settle = t <= CHARGE_RISE_SECONDS ? 1f : MathF.Exp(-(t - CHARGE_RISE_SECONDS) * CHARGE_SETTLE_PER_SECOND);

                float p = (float)phase;
                float tone = MathF.Sin(p) + CHARGE_OCTAVE * MathF.Sin(2f * p) + CHARGE_TWELFTH * MathF.Sin(3f * p)
                    + CHARGE_BAR * MathF.Sin(2.76f * p);

                float sample = (tone * whirr + CHARGE_BODY * MathF.Sin((float)bodyPhase)) * attack * swell * settle;

                //The ting, struck as the blade reaches the top
                if (t >= CHARGE_RISE_SECONDS)
                {
                    float since = t - CHARGE_RISE_SECONDS;
                    tingPhase += 2.0 * Math.PI * top * TING_RATIO / sampleRate;
                    float strike = MathF.Min(1f, since / 0.003f);
                    sample += TING_LEVEL * MathF.Sin((float)tingPhase) * strike * MathF.Exp(-since * TING_DECAY_PER_SECOND);
                }

                signal[i] = sample;
            }

            return Finish(signal, sampleRate, CHARGE_CUTOFF_HZ, 0.08f);
        }

        #endregion

        #region The slice

        /// <summary>The slice's whole length, in seconds.</summary>
        public const float SLICE_DURATION = 1.15f;

        /// <summary>The zing's fall, in Hz, over <see cref="ZING_SECONDS"/>: the blade going through.</summary>
        public const float ZING_FROM_HZ = 2400f;
        public const float ZING_TO_HZ = 1150f;
        public const float ZING_SECONDS = 0.05f;
        private const float ZING_LEVEL = 0.6f;

        /// <summary>The struck blade's fundamental, in Hz (D5): its partials sit at 1, 2.76 and 5.40 times it, the bar's own.</summary>
        public const float RING_HZ = 587.33f;

        //Each partial's level and how fast it dies, and its twin: a few cents sharp and quieter, so the pair beats slowly and
        //never cancels (equal twins a few cents apart dipped the whole ring to nothing a fifth of a second in), each partial
        //at a detune of its own so their beats never line up
        private static readonly float[] RING_RATIOS = { 1f, 2.76f, 5.40f };
        private static readonly float[] RING_LEVELS = { 1f, 0.5f, 0.2f };
        private static readonly float[] RING_DECAYS = { 2.4f, 4.0f, 8.0f };
        private static readonly float[] RING_TWINS = { 1.0013f, 1.0022f, 1.0035f };
        private const float RING_TWIN_LEVEL = 0.45f;

        //The warm thump under it: a sine falling into the low register, over fast
        private const float THUMP_FROM_HZ = 150f;
        private const float THUMP_TO_HZ = 62f;
        private const float THUMP_LEVEL = 0.55f;
        private const float THUMP_DECAY_PER_SECOND = 11f;

        //Rounded off twice a little higher than the charge, so the ring's top partial (3.2 kHz) is kept
        public const float SLICE_CUTOFF_HZ = 4200f;

        /// <summary>The slice's samples, mono, at <paramref name="sampleRate"/>: the same ones every time.</summary>
        public static float[] RenderSlice(int sampleRate) => RenderSlice(sampleRate, 1f);

        /// <summary><see cref="RenderSlice(int)"/> with every pitch scaled: the tests' planted bass-only fault.</summary>
        internal static float[] RenderSlice(int sampleRate, float pitchScale)
        {
            float[] signal = new float[(int)(sampleRate * SLICE_DURATION)];

            double zingPhase = 0.0, thumpPhase = 0.0;
            double[] ring = new double[RING_RATIOS.Length * 2];

            for (int i = 0; i < signal.Length; i++)
            {
                float t = (float)i / sampleRate;
                float attack = MathF.Min(1f, t / 0.002f);

                //The zing: a sine with a soft octave, falling exponentially, gone by a little after its glide
                float zingAt = MathF.Min(1f, t / ZING_SECONDS);
                float zingHz = ZING_FROM_HZ * pitchScale * MathF.Pow(ZING_TO_HZ / ZING_FROM_HZ, zingAt);
                zingPhase += 2.0 * Math.PI * zingHz / sampleRate;
                float zing = (MathF.Sin((float)zingPhase) + 0.25f * MathF.Sin(2f * (float)zingPhase))
                    * ZING_LEVEL * MathF.Exp(-t / (ZING_SECONDS * 0.9f));

                //The ring, struck a hair after the zing begins so the two are heard as one gesture
                float ringing = 0f;
                float ringT = MathF.Max(0f, t - 0.012f);
                float ringAttack = MathF.Min(1f, ringT / 0.004f);
                for (int k = 0; k < RING_RATIOS.Length; k++)
                {
                    double hz = RING_HZ * pitchScale * RING_RATIOS[k];
                    ring[k * 2] += 2.0 * Math.PI * hz / sampleRate;
                    ring[k * 2 + 1] += 2.0 * Math.PI * hz * RING_TWINS[k] / sampleRate;

                    float level = RING_LEVELS[k] * MathF.Exp(-ringT * RING_DECAYS[k]);
                    ringing += level * (MathF.Sin((float)ring[k * 2]) + RING_TWIN_LEVEL * MathF.Sin((float)ring[k * 2 + 1]))
                        / (1f + RING_TWIN_LEVEL);
                }

                //The thump: the weight under it
                float thumpAt = MathF.Min(1f, t / 0.12f);
                float thumpHz = THUMP_FROM_HZ * pitchScale * MathF.Pow(THUMP_TO_HZ / THUMP_FROM_HZ, thumpAt);
                thumpPhase += 2.0 * Math.PI * thumpHz / sampleRate;
                float thump = THUMP_LEVEL * MathF.Sin((float)thumpPhase) * MathF.Exp(-t * THUMP_DECAY_PER_SECOND);

                signal[i] = (zing + ringing * ringAttack + thump) * attack;
            }

            return Finish(signal, sampleRate, SLICE_CUTOFF_HZ, 0.12f);
        }

        #endregion

        //Rounded off twice, faded to nothing over its last stretch (after the low-passes, so their lag cannot leave a tail),
        //and peak-normalised like the other tones. Internal because the Swap's tone (SwapSynth, #871) finishes the same way
        internal static float[] Finish(float[] signal, int sampleRate, float cutoff, float releaseSeconds)
        {
            signal = LowPass(LowPass(signal, cutoff, sampleRate), cutoff, sampleRate);

            int fade = (int)(releaseSeconds * sampleRate);
            for (int i = 0; i < fade && i < signal.Length; i++)
            {
                float x = (float)i / fade;
                signal[signal.Length - 1 - i] *= x * x * (3f - 2f * x);
            }

            float peak = 0f;
            for (int i = 0; i < signal.Length; i++) peak = MathF.Max(peak, MathF.Abs(signal[i]));
            if (peak > 1e-6f)
            {
                float scale = PEAK / peak;
                for (int i = 0; i < signal.Length; i++) signal[i] *= scale;
            }

            return signal;
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
    }
}

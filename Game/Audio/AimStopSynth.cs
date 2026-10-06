using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The sound of the barrel meeting its stop (#811): the player is pushing the aim for higher (or lower) than the gun
    /// will go. A strained <b>creak</b> whose pitch climbs as the pressure does, ending in a short soft <b>puff</b> of air
    /// - the owner's words were "a creak, a squeak-fart as the pressure rises", in the register of Half-Life 1's
    /// mechanical sounds: friendly, a little comic, never harsh. Inspired and not sampled; it is arithmetic.
    /// <para>
    /// Pure arithmetic in a file of its own, as <see cref="DrumLayer"/> is, so that the tests compile the very lines the
    /// game plays and can measure what the sound promises: it carries its identity in the 300 Hz - 4 kHz the owner's
    /// monitor speakers reproduce (a bass-only sound is inaudible to him), it has no sustained hiss (the standing rule
    /// on this game's effects), and it does not resemble the refused shot's low "bwom-bwoww" - the two are a pair, and
    /// the player learns by their difference: pressing into the stop creaks, pulling the trigger there says no.
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>The creak is a stick-slip</b>: a train of slips whose rate climbs from about 34 to 58 a
    /// second with a wobble, each slip a short ramp, driven through two resonances that climb together (650 to 1100 Hz
    /// and about twice that). That is what a rubbed leather or a strained hinge does, and the climb is the pressure.</description></item>
    /// <item><description><b>The puff is a low-passed breath</b>, not a hiss: noise confined to 350 - 1700 Hz, a 15 ms
    /// attack so it never starts on a click, a fall over about a tenth of a second, and a sputter on its first part so
    /// it reads as air through something rubbery rather than as a steady rush. The owner asked for the "pfft"; the
    /// no-hiss rule forbids a sustained noise bed - this is the short, rounded one that satisfies both, and his ear
    /// is the judge.</description></item>
    /// </list>
    /// </summary>
    internal static class AimStopSynth
    {
        /// <summary>The length of the whole sound, in seconds.</summary>
        public const float DURATION = 0.5f;

        /// <summary>Where the creak ends, in seconds; the puff begins a little before it, so the two overlap.</summary>
        public const float CREAK_SECONDS = 0.28f;

        /// <summary>Where the puff begins, in seconds.</summary>
        public const float PUFF_START_SECONDS = 0.2f;

        /// <summary>The peak the sound is normalised to, which leaves the headroom the other effects leave.</summary>
        public const float PEAK = 0.9f;

        //The slips' rate, climbing across the creak, and how far the wobble may carry it either way
        private const float SLIP_RATE_FROM = 34f;
        private const float SLIP_RATE_TO = 58f;
        private const float SLIP_WOBBLE = 0.08f;

        //What the wobble below comes out at, one standard deviation: a one-pole low-pass (0.0008) of uniform noise is
        //sqrt(1/3 x 0.0008 / (2 - 0.0008)). Dividing by it makes SLIP_WOBBLE the spread of the slips' rate, as a fraction of it
        private const float WOBBLE_SIGMA = 0.01155f;

        //The first resonance's centre, climbing; the second stands at a fixed ratio above it, at half the weight
        private const float RESONANCE_FROM = 650f;
        private const float RESONANCE_TO = 1100f;
        private const float RESONANCE_Q = 9f;
        private const float SECOND_RESONANCE_RATIO = 1.9f;
        private const float SECOND_RESONANCE_WEIGHT = 0.5f;

        //The breath's band and how it falls
        private const float PUFF_LOW_HZ = 350f;
        private const float PUFF_HIGH_HZ = 1700f;
        private const float PUFF_ATTACK_SECONDS = 0.015f;
        private const float PUFF_DECAY_PER_SECOND = 9.5f;
        private const float PUFF_SPUTTER_HZ = 32f;
        private const float PUFF_SPUTTER_DEPTH = 0.65f;
        private const float PUFF_SPUTTER_FADE_PER_SECOND = 18f;

        //Each layer is brought to a peak of 1 and then mixed at these weights, so the balance does not depend on how loud
        //the filters happened to leave either of them
        private const float CREAK_WEIGHT = 0.8f;
        private const float PUFF_WEIGHT = 0.55f;

        //The whole is rounded off once at the top of what the speakers carry, and faded over the last samples so it
        //never ends on a step
        private const float ROUNDING_CUTOFF_HZ = 3800f;
        private const float END_FADE_SECONDS = 0.02f;

        /// <summary>The sound's samples, mono, at <paramref name="sampleRate"/>: the same ones every time.</summary>
        public static float[] Render(int sampleRate)
        {
            int length = (int)(sampleRate * DURATION);

            float[] creak = RenderCreak(sampleRate, length);
            float[] puff = RenderPuff(sampleRate, length);

            float[] signal = new float[length];
            for (int i = 0; i < length; i++) signal[i] = creak[i] * CREAK_WEIGHT + puff[i] * PUFF_WEIGHT;

            signal = LowPass(signal, ROUNDING_CUTOFF_HZ, sampleRate);

            int fade = (int)(sampleRate * END_FADE_SECONDS);
            for (int i = 0; i < fade && i < length; i++) signal[length - 1 - i] *= (float)i / fade;

            Normalize(signal, PEAK);
            return signal;
        }

        private static float[] RenderCreak(int sampleRate, int length)
        {
            float[] creak = new float[length];
            int creakSamples = Math.Min(length, (int)(sampleRate * CREAK_SECONDS));

            //Chamberlin state-variable band-passes, two of them, their centres moved every sample
            float low1 = 0f, band1 = 0f, low2 = 0f, band2 = 0f;
            float phase = 0f, wobble = 0f, slip = 1f;
            int slips = 0;
            float damping = 1f / RESONANCE_Q;

            for (int i = 0; i < creakSamples; i++)
            {
                float t = (float)i / sampleRate;
                float u = t / CREAK_SECONDS;

                //The wobble is slow noise (a one-pole low-pass of the hash noise, a few Hz), so the slips drift in time
                //like a hand's pressure does and never settle into a metronome
                wobble += (Noise(i, 1) - wobble) * 0.0008f;
                float rate = (SLIP_RATE_FROM + (SLIP_RATE_TO - SLIP_RATE_FROM) * MathF.Pow(u, 0.8f))
                    * (1f + wobble * (SLIP_WOBBLE / WOBBLE_SIGMA));

                phase += rate / sampleRate;
                if (phase >= 1f)
                {
                    phase -= 1f;

                    //Each slip lets go a little differently
                    slip = 0.65f + 0.35f * (Noise(slips++, 2) * 0.5f + 0.5f);
                }

                //A ramp that falls across each cycle and jumps back at the slip: rich in harmonics, which is what the
                //resonances have to ring on
                float excitation = slip * (1f - phase) * (1f - phase);

                float centre = RESONANCE_FROM + (RESONANCE_TO - RESONANCE_FROM) * u;
                float f1 = 2f * MathF.Sin(MathF.PI * centre / sampleRate);
                float f2 = 2f * MathF.Sin(MathF.PI * MathF.Min(centre * SECOND_RESONANCE_RATIO, 3600f) / sampleRate);

                low1 += f1 * band1;
                float high1 = excitation - low1 - damping * band1;
                band1 += f1 * high1;

                low2 += f2 * band2;
                float high2 = excitation - low2 - damping * band2;
                band2 += f2 * high2;

                //It swells a little as the pressure rises, comes in over 30 ms and goes out over the last 60
                float attack = MathF.Sin(MathF.PI * 0.5f * MathF.Min(1f, t / 0.03f));
                float release = SmoothStep(Math.Clamp((CREAK_SECONDS - t) / 0.06f, 0f, 1f));
                creak[i] = (band1 + SECOND_RESONANCE_WEIGHT * band2) * attack * attack * (0.75f + 0.25f * u) * release;
            }

            Normalize(creak, 1f);
            return creak;
        }

        private static float[] RenderPuff(int sampleRate, int length)
        {
            int start = (int)(sampleRate * PUFF_START_SECONDS);
            float[] noise = new float[length - start];
            for (int i = 0; i < noise.Length; i++) noise[i] = Noise(i, 7);

            //Confined to the band: two poles down from the top, one pole up from the bottom
            float[] lower = LowPass(LowPass(noise, PUFF_HIGH_HZ, sampleRate), PUFF_HIGH_HZ, sampleRate);
            float[] floor = LowPass(lower, PUFF_LOW_HZ, sampleRate);

            float[] puff = new float[length];
            for (int i = 0; i < noise.Length; i++)
            {
                float t = (float)i / sampleRate;

                float attack = MathF.Sin(MathF.PI * 0.5f * MathF.Min(1f, t / PUFF_ATTACK_SECONDS));
                float fall = MathF.Exp(-t * PUFF_DECAY_PER_SECOND);

                //The sputter: the air is chopped about thirty times a second at first, and smooths out as it dies
                float depth = PUFF_SPUTTER_DEPTH * MathF.Exp(-t * PUFF_SPUTTER_FADE_PER_SECOND);
                float sputter = 1f - depth * (0.5f - 0.5f * MathF.Cos(2f * MathF.PI * PUFF_SPUTTER_HZ * t));

                puff[start + i] = (lower[i] - floor[i]) * attack * attack * fall * sputter;
            }

            Normalize(puff, 1f);
            return puff;
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

        /// <summary>The hash noise <c>ProceduralAudio.Noise</c> is, in -1..1: a function of the index and a seed.</summary>
        private static float Noise(int i, int seed)
        {
            uint h = (uint)(i * 2654435761u) ^ 0x9E3779B9u ^ (uint)(seed * 374761393);
            h ^= h >> 13;
            h *= 0x85EBCA6Bu;
            h ^= h >> 16;
            return (h / (float)uint.MaxValue) * 2f - 1f;
        }
    }
}

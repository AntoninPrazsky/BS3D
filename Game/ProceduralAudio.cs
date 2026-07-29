using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Prazsky.BS3D.GameStructure;
using System;

namespace BS3D
{
    /// <summary>
    /// Procedurally generated sound effects, synthesized from raw 16-bit PCM at startup and played back cheaply
    /// at runtime. No content files, no pipeline step — the <see cref="SoundEffect"/> constructor takes a PCM
    /// buffer directly.
    /// <para>
    /// The synthesis is a small signal chain run entirely on a float buffer: layers of additive-harmonic tones,
    /// low-passed noise transients and a sub-bass weight are mixed, the result is run through a Schroeder
    /// reverb (four parallel comb filters into two series all-passes — the classic Freeverb topology) for a
    /// sense of space, and only then is it quantized to PCM. That chain is what moves the sound off the bare
    /// beep of a first pass and gives it body, weight and a tail.
    /// </para>
    /// <para>
    /// Every play is nudged by a small random pitch, the landed sound is chosen by ball type and panned against
    /// the camera, and the reverb decays independently per render — so no two shots and no two landings sound
    /// exactly alike.
    /// </para>
    /// </summary>
    public sealed class ProceduralAudio : IDisposable
    {
        /// <summary>A single master gain applied to every effect. A constant for now; a settings slider later.</summary>
        public const float MASTER_VOLUME = 0.7f;

        private const int SAMPLE_RATE = 44100;

        private readonly SoundEffect _shoot;
        private readonly SoundEffect[] _landed;
        private readonly Random _random = new();

        public ProceduralAudio()
        {
            _shoot = BakeShoot();
            _landed = new SoundEffect[9];   //indexed by BallType value (1..8); slot 0 unused
            for (int type = 1; type <= 8; type++) _landed[type] = BakeLanded(type);
        }

        /// <summary>The shot leaving the barrel: centred, with a small random pitch so a burst never sounds flat.</summary>
        public void PlayShoot()
        {
            _shoot.Play(MASTER_VOLUME, NextPitch(0.12f), 0f);
        }

        /// <summary>
        /// A ball snapping into the lattice. The <paramref name="type"/> selects a tone (one per colour), and the
        /// world position is panned against the camera so a hit on the left of the field is heard on the left.
        /// </summary>
        public void PlayLanded(BallType type, Vector3 world, RecoilCamera camera)
        {
            int index = (int)type;
            if (index < 1 || index >= _landed.Length || _landed[index] == null) return;

            float pan = PanFor(world, camera, out float distance);
            float volume = VolumeForDistance(distance) * MASTER_VOLUME;
            _landed[index].Play(volume, NextPitch(0.1f), pan);
        }

        /// <summary>
        /// Stereo pan of a world point relative to the camera: project the sound's offset onto the camera's
        /// right axis and clamp. A point straight ahead is 0; one fully to the lens's right is +1.
        /// </summary>
        private static float PanFor(Vector3 world, RecoilCamera camera, out float distance)
        {
            Vector3 forward = camera.Target - camera.Position;
            float forwardLen = forward.Length();
            Vector3 forwardN = forwardLen > 1e-4f ? forward / forwardLen : Vector3.Forward;

            //The same basis the camera builds in RecoilCamera.Recalculate (right-handed: forward × up).
            Vector3 right = Vector3.Cross(forwardN, Vector3.Up);
            right = right.LengthSquared() > 1e-6f ? Vector3.Normalize(right) : Vector3.Right;

            Vector3 toSound = world - camera.Position;
            distance = toSound.Length();

            float lateral = Vector3.Dot(toSound, right);
            return MathHelper.Clamp(lateral / PAN_FULL_WIDTH, -1f, 1f);
        }

        /// <summary>Falls off gently with distance so a far landing is quieter but never inaudible.</summary>
        private static float VolumeForDistance(float distance)
        {
            const float MIN = 0.45f;
            const float FALLOFF = 60f;
            return MIN + (1f - MIN) * MathHelper.Clamp(1f - distance / FALLOFF, 0f, 1f);
        }

        private float NextPitch(float amplitude) => 1f + (float)(_random.NextDouble() * 2.0 - 1.0) * amplitude;

        //How far off-centre a sound has to be to reach full left/right pan. Sized to the cluster's span.
        private const float PAN_FULL_WIDTH = 18f;

        #region The signal chain

        /// <summary>
        /// The shot, layered like a real discharge rather than a single tone. A cannon going off is not one
        /// sound — it is a body of compressed air, the energy of the charge, the friction and the resistance —
        /// and each of those lives in a different part of the spectrum. This bakes five of them together so the
        /// shot reads as massive, present and exciting at <i>any</i> playback volume, which matters because a
        /// game is usually played quieter than a real cannon would be:
        /// <list type="bullet">
        /// <item><b>Boom</b> — a kick-drum pitch drop (150→60 Hz, fast) plus a low rumble. The weight: "boooom".
        /// Dominates the energy, and lives in the band ordinary speakers can reproduce.</item>
        /// <item><b>Zap</b> — a short buzzy sawtooth sweep (400→150 Hz). The energy of the charge itself: "vrrzzzzt".
        /// Adds midrange excitement that the boom alone does not have.</item>
        /// <item><b>Crackle</b> — high band-passed noise with a tremolo, trailing off. The air resistance and
        /// flying debris: "pšššchřchřchř". Crucially this sits in the 1.5–5 kHz band where the ear is most
        /// sensitive, so it keeps the shot audible and cutting even when the volume is turned down low.</item>
        /// <item><b>Thud</b> — the muzzle transient that opens the whole thing.</item>
        /// </list>
        /// Then a reverb tail so the layered report carries and decays like a discharge in a space.
        /// </summary>
        private SoundEffect BakeShoot()
        {
            const float duration = 0.65f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //Layer 1 — the boom: a fast pitch drop, not a slow sweep. The pitch envelope is the heart of why
            //this reads as a cannon rather than a tube: it crashes down in the first 30 ms and then sustains,
            //so the ear hears an attack followed by a ring instead of a note sliding down.
            const float pitchDropTime = 0.030f;
            const float pitchStart = 150f, pitchEnd = 60f;
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Pitch falls exponentially over the attack window, then holds at the low note and lets it ring.
                float freq = t < pitchDropTime
                    ? pitchStart * MathF.Pow(pitchEnd / pitchStart, t / pitchDropTime)
                    : pitchEnd;

                phase += 2f * MathF.PI * freq / SAMPLE_RATE;

                //A couple of harmonics for body, but few and quiet: this is not a bright tone.
                float boom = MathF.Sin(phase) + 0.30f * MathF.Sin(2f * phase) + 0.10f * MathF.Sin(3f * phase);

                //A slow amplitude decay so the note rings out and booms rather than going "plick" and vanishing.
                float amp = MathF.Exp(-t * 5f);

                signal[i] += boom * 0.6f * amp;
            }

            //The boom's grit: white noise low-passed to 190 Hz, which is the band that turns the clean tone above
            //into an explosive "rrrmmm". This band is also what small speakers can reproduce, so the weight
            //survives on monitor speakers where a sub-bass would not.
            float[] rumble = LowPassArray(MakeNoiseArray(samples), 190f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                signal[i] += rumble[i] * 0.55f * MathF.Exp(-t * 4.5f);
            }

            //Layer 2 — the zap: the energy of the charge itself, a short buzzy sawtooth sweep. A sawtooth built
            //additively (eight partials at 1/k) is what gives the buzzy "vrrzzzzt"; a sine here would just be
            //another tone. Kept short — a flash on top of the boom, not a drone — and modest in gain so it is an
            //accent that excites the midrange, never the main event.
            const float zapDuration = 0.09f;
            int zapSamples = Math.Min(samples, (int)(SAMPLE_RATE * zapDuration));
            float zapPhase = 0f;
            for (int i = 0; i < zapSamples; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Another fast pitch drop, but in a higher register (400→150 Hz) so it reads as the charge's whine
                //rather than the body's thump.
                float freq = 400f * MathF.Pow(150f / 400f, t / zapDuration);
                zapPhase += 2f * MathF.PI * freq / SAMPLE_RATE;

                float saw = 0f;
                for (int k = 1; k <= 8; k++) saw += MathF.Sin(k * zapPhase) / k;

                //A 4 ms attack ramp then a fast decay: a zap, not a sustained note.
                float env = MathF.Exp(-t * 22f) * MathF.Min(1f, t / 0.004f);
                signal[i] += saw * 0.18f * env;
            }

            //Layer 3 — the crackle: the air resistance and debris, a high band-passed noise with a tremolo. This
            //is the layer that makes the shot cut through at low volume: it lives in the 1.5–5 kHz band where the
            //ear is most sensitive (Fletcher-Munson), so even quiet it reads as present and exciting. The tremolo
            //at ~45 Hz is what turns steady hiss into the "chřchř" crackle texture.
            float[] air = BandPass(MakeNoiseArray(samples), 1500f, 5000f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tremolo = 0.55f + 0.45f * MathF.Sin(2f * MathF.PI * 45f * t);
                signal[i] += air[i] * 0.22f * tremolo * MathF.Exp(-t * 8f);
            }

            //Layer 4 — the muzzle thud: a short, low-cutoff noise burst that opens the whole thing, kept low so
            //it is a thud rather than the sharp click that made earlier passes read as cheap.
            AddNoiseBurst(signal, window: 0.05f, decay: 25f, gain: 1.0f, cutoff: 1200f);

            //Space: a longer reverb tail so the layered report carries and decays like a discharge in a space.
            ApplyReverb(signal, roomScale: 0.5f, wet: 0.34f, decay: 0.36f);

            Normalize(signal, 0.95f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// A landing: a low "thunk" with harmonic content — one base note per ball type, so each colour lands on
        /// its own pitch — fronted by a filtered click of contact and underpinned by a sub thump. Shorter and
        /// duller than the shot: a ball meeting a lattice of its own kind should sound solid, not explosive.
        /// </summary>
        private SoundEffect BakeLanded(int type)
        {
            const float duration = 0.30f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //Eight steps across a low register, so adjacent colours sit a tone apart rather than a fraction the
            //ear cannot tell apart.
            const float root = 150f;
            float freq = root * MathF.Pow(2f, (type - 1) / 7f * 1.5f);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = MathF.Exp(-t * 26f);

                //Additive harmonics: the fundamental plus the 2nd and a little 3rd give the thunk a wooden,
                //solid character a pure sine lacks.
                float tone = 0f;
                tone += MathF.Sin(2f * MathF.PI * freq * t);
                tone += 0.5f * MathF.Sin(2f * MathF.PI * freq * 2f * t);
                tone += 0.2f * MathF.Sin(2f * MathF.PI * freq * 3f * t);

                //A sub thump an octave below the fundamental adds the physical weight of contact.
                float sub = MathF.Sin(2f * MathF.PI * freq * 0.5f * t) * MathF.Exp(-t * 30f);

                signal[i] = (tone * 0.45f * env) + (sub * 0.55f);
            }

            //The click of contact: brighter, very short, low-passed so it reads as a knock rather than a tick.
            AddNoiseBurst(signal, window: 0.015f, decay: 90f, gain: 0.9f, cutoff: 4000f);

            ApplyReverb(signal, roomScale: 0.4f, wet: 0.26f, decay: 0.22f);

            Normalize(signal, 0.9f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// Mixes a short burst of low-passed noise into <paramref name="signal"/> at its start. White noise
        /// alone hisses; the one-pole low-pass rounds it into a transient with mass.
        /// </summary>
        private static void AddNoiseBurst(float[] signal, float window, float decay, float gain, float cutoff)
        {
            int windowSamples = (int)(SAMPLE_RATE * window);
            if (windowSamples > signal.Length) windowSamples = signal.Length;

            //One-pole low-pass: a single weighted average of the previous output, cheap and stable enough for a
            //transient. The coefficient comes from the cutoff.
            float dt = 1f / SAMPLE_RATE;
            float rc = 1f / (2f * MathF.PI * cutoff);
            float alpha = dt / (rc + dt);
            float prev = 0f;

            for (int i = 0; i < windowSamples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float white = Noise(i);

                //Filter the noise in place before mixing it in.
                prev += alpha * (white - prev);
                float filtered = prev;

                signal[i] += filtered * gain * MathF.Exp(-t * decay);
            }
        }

        /// <summary>
        /// A one-pole low-pass applied to a whole array, returning a new filtered array. Used to shape a noise
        /// layer into a low rumble: white noise low-passed to a few hundred Hz stops hissing and starts to boom.
        /// </summary>
        private static float[] LowPassArray(float[] input, float cutoff)
        {
            float dt = 1f / SAMPLE_RATE;
            float rc = 1f / (2f * MathF.PI * cutoff);
            float alpha = dt / (rc + dt);

            float[] output = new float[input.Length];
            float prev = 0f;
            for (int i = 0; i < input.Length; i++)
            {
                prev += alpha * (input[i] - prev);
                output[i] = prev;
            }

            return output;
        }

        /// <summary>An array of deterministic white noise, for layers that need a sustained (if brief) noise source.</summary>
        private static float[] MakeNoiseArray(int samples)
        {
            float[] noise = new float[samples];
            for (int i = 0; i < samples; i++) noise[i] = Noise(i);
            return noise;
        }

        /// <summary>
        /// A one-pole band-pass: low-pass to <paramref name="hi"/> then subtract the low-passed-to-<paramref name="lo"/>
        /// residue, isolating the band between them. Used to confine a noise layer to the ear's most sensitive
        /// range for presence at low playback volume.
        /// </summary>
        private static float[] BandPass(float[] input, float lo, float hi)
        {
            float[] lower = LowPassArray(input, hi);
            float[] band = LowPassArray(lower, lo);

            float[] output = new float[input.Length];
            for (int i = 0; i < input.Length; i++) output[i] = lower[i] - band[i];
            return output;
        }

        /// <summary>
        /// A Schroeder reverb — four parallel comb filters summed and passed through two series all-pass filters
        /// (the Freeverb topology). The combs build the dense tail; the all-passes smear it so it reads as
        /// ambience rather than distinct echoes. Processed in place, adding a "wet" tail to the dry signal.
        /// </summary>
        private static void ApplyReverb(float[] signal, float roomScale, float wet, float decay)
        {
            int length = signal.Length;

            //Comb and all-pass delay lengths in samples (prime-ish, from Freeverb's constants), scaled by the
            //room so a smaller space claps back sooner.
            int[] combDelays =
            {
                (int)(1116 * roomScale), (int)(1188 * roomScale),
                (int)(1277 * roomScale), (int)(1356 * roomScale)
            };
            int[] allpassDelays = { (int)(556 * roomScale), (int)(441 * roomScale) };

            //Feedback gain derived from the decay time: longer tail, higher feedback — but capped short of the
            //point where the comb rings forever.
            float feedback = MathHelper.Clamp(0.7f + decay * 0.25f, 0.5f, 0.84f);

            float[] wetSignal = new float[length];

            //Each comb is a feedback delay line; their staggered lengths are what make the tail dense rather
            //than a single echo.
            foreach (int delay in combDelays)
            {
                if (delay < 1) continue;
                float[] buffer = new float[delay];
                int idx = 0;
                for (int i = 0; i < length; i++)
                {
                    float output = buffer[idx];
                    buffer[idx] = signal[i] + output * feedback;
                    if (++idx >= delay) idx = 0;
                    wetSignal[i] += output;
                }
            }

            //Average the four combs before the all-passes colour them.
            float combScale = 0.25f;
            for (int i = 0; i < length; i++) wetSignal[i] *= combScale;

            //Two all-passes in series smear the comb output into an even wash.
            foreach (int delay in allpassDelays)
            {
                if (delay < 1) continue;
                float[] buffer = new float[delay];
                int idx = 0;
                for (int i = 0; i < length; i++)
                {
                    float buffered = buffer[idx];
                    float output = -wetSignal[i] + buffered;
                    buffer[idx] = wetSignal[i] + buffered * 0.5f;
                    if (++idx >= delay) idx = 0;
                    wetSignal[i] = output;
                }
            }

            //Dry/wet mix. The wet path is scaled down a touch — it is ambience, not the main event.
            float dryGain = 1f - wet * 0.5f;
            for (int i = 0; i < length; i++)
                signal[i] = signal[i] * dryGain + wetSignal[i] * wet;
        }

        /// <summary>
        /// Scales <paramref name="signal"/> so its peak magnitude reaches <paramref name="target"/>. Prevents the
        /// layered sources from clipping when summed, and keeps every effect at a comparable loudness.
        /// </summary>
        private static void Normalize(float[] signal, float target)
        {
            float peak = 0f;
            for (int i = 0; i < signal.Length; i++)
            {
                float a = MathF.Abs(signal[i]);
                if (a > peak) peak = a;
            }

            if (peak < 1e-6f) return;
            float scale = target / peak;
            for (int i = 0; i < signal.Length; i++) signal[i] *= scale;
        }

        /// <summary>Wraps a float signal as a 16-bit little-endian PCM mono <see cref="SoundEffect"/>.</summary>
        private static SoundEffect ToSoundEffect(float[] signal)
        {
            byte[] pcm = new byte[signal.Length * 2];
            for (int i = 0; i < signal.Length; i++)
            {
                float s = MathHelper.Clamp(signal[i], -1f, 1f);
                short v = (short)(s * short.MaxValue);
                pcm[i * 2] = (byte)(v & 0xff);
                pcm[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }

            return new SoundEffect(pcm, SAMPLE_RATE, AudioChannels.Mono);
        }

        /// <summary>A cheap deterministic noise source for transients — quality is irrelevant for a few ms of crackle.</summary>
        private static float Noise(int i)
        {
            uint h = (uint)(i * 2654435761u) ^ 0x9E3779B9u;
            h ^= h >> 13;
            h *= 0x85EBCA6Bu;
            h ^= h >> 16;
            return (h / (float)uint.MaxValue) * 2f - 1f;
        }

        #endregion

        public void Dispose()
        {
            _shoot?.Dispose();
            if (_landed != null) foreach (SoundEffect effect in _landed) effect?.Dispose();
        }
    }
}

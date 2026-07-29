using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Prazsky.BS3D.GameStructure;
using System;

namespace BS3D
{
    /// <summary>
    /// Procedurally generated sound effects, synthesized from raw 16-bit PCM at startup and played back cheaply
    /// at runtime. No content files, no pipeline step â€” the <see cref="SoundEffect"/> constructor takes a PCM
    /// buffer directly.
    /// <para>
    /// The synthesis is a small signal chain run entirely on a float buffer: layers of additive-harmonic tones,
    /// low-passed noise transients and a sub-bass weight are mixed, the result is run through a Schroeder
    /// reverb (four parallel comb filters into two series all-passes â€” the classic Freeverb topology) for a
    /// sense of space, and only then is it quantized to PCM. That chain is what moves the sound off the bare
    /// beep of a first pass and gives it body, weight and a tail.
    /// </para>
    /// <para>
    /// Every play is nudged by a small random pitch, the landed sound is chosen by ball type and panned against
    /// the camera, and the reverb decays independently per render â€” so no two shots and no two landings sound
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
        private readonly SoundEffect _fireworkLaunch;
        private readonly SoundEffect _fireworkBurst;
        private readonly SoundEffect _partyPopper;
        private readonly Random _random = new();

        public ProceduralAudio()
        {
            _shoot = BakeShoot();
            _landed = new SoundEffect[9];   //indexed by BallType value (1..8); slot 0 unused
            for (int type = 1; type <= 8; type++) _landed[type] = BakeLanded(type);

            _fireworkLaunch = BakeFireworkLaunch();
            _fireworkBurst = BakeFireworkBurst();
            _partyPopper = BakePartyPopper();
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
        /// A shell leaving the ground: the rising whistle, panned where it was fired from. Pitched a little
        /// each time and, because a whistle is a near-pure tone, pitched <i>widely</i> â€” two shells going up
        /// together on the same note read as one loud shell rather than as two.
        /// </summary>
        public void PlayFireworkLaunch(Vector3 world, RecoilCamera camera)
        {
            //Well under the report. The bang is the event; the whistle only says one is coming, and with a
            //launch every fraction of a second a loud whistle turns the display into a chorus of kettles.
            float pan = PanFor(world, camera, SKY_PAN_WIDTH, out _);
            _fireworkLaunch.Play(0.22f * MASTER_VOLUME, NextPitch(0.22f), pan);
        }

        /// <summary>
        /// A shell going off. <paramref name="size"/> (0â€¦1) is how big the burst is: it drives the volume and,
        /// inversely, the pitch â€” a big shell is a deeper, louder report, which is the whole of how the ear
        /// tells a large firework from a small one at a distance.
        /// </summary>
        public void PlayFireworkBurst(Vector3 world, RecoilCamera camera, float size)
        {
            float pan = PanFor(world, camera, SKY_PAN_WIDTH, out float distance);

            //A near-flat distance term. A firework IS far away â€” that is what it is for â€” so falling off the
            //way a landing does would make every burst a whisper; this only separates the near from the far.
            float volume = (0.85f + 0.15f * size) * (0.8f + 0.2f * MathHelper.Clamp(1f - distance / 260f, 0f, 1f));

            _fireworkBurst.Play(MathHelper.Clamp(volume * MASTER_VOLUME, 0f, 1f),
                MathHelper.Clamp(NextPitch(0.12f) - size * 0.28f, -1f, 1f), pan);
        }

        /// <summary>The party popper that opens the celebration: one dry crack of paper and confetti, centred.</summary>
        public void PlayPartyPopper()
        {
            _partyPopper.Play(0.9f * MASTER_VOLUME, NextPitch(0.08f), 0f);
        }

        /// <summary>
        /// Stereo pan of a world point relative to the camera: project the sound's offset onto the camera's
        /// right axis and clamp. A point straight ahead is 0; one fully to the lens's right is +1.
        /// </summary>
        private static float PanFor(Vector3 world, RecoilCamera camera, out float distance)
            => PanFor(world, camera, PAN_FULL_WIDTH, out distance);

        /// <summary>
        /// <inheritdoc cref="PanFor(Vector3, RecoilCamera, out float)"/>
        /// <para>
        /// <paramref name="fullWidth"/> is how far off-centre a sound has to be to reach full left/right. It is
        /// per-source rather than one constant because the two things that make noise here live at completely
        /// different scales: the cluster spans a couple of dozen units, and a firework bursts a hundred up and
        /// as far out again â€” at the cluster's width every shell but the one straight overhead would pan hard
        /// to one side.
        /// </para>
        /// </summary>
        private static float PanFor(Vector3 world, RecoilCamera camera, float fullWidth, out float distance)
        {
            Vector3 forward = camera.Target - camera.Position;
            float forwardLen = forward.Length();
            Vector3 forwardN = forwardLen > 1e-4f ? forward / forwardLen : Vector3.Forward;

            //The same basis the camera builds in RecoilCamera.Recalculate (right-handed: forward Ă— up).
            Vector3 right = Vector3.Cross(forwardN, Vector3.Up);
            right = right.LengthSquared() > 1e-6f ? Vector3.Normalize(right) : Vector3.Right;

            Vector3 toSound = world - camera.Position;
            distance = toSound.Length();

            float lateral = Vector3.Dot(toSound, right);
            return MathHelper.Clamp(lateral / fullWidth, -1f, 1f);
        }

        /// <summary>Falls off gently with distance so a far landing is quieter but never inaudible.</summary>
        private static float VolumeForDistance(float distance)
        {
            const float MIN = 0.45f;
            const float FALLOFF = 60f;
            return MIN + (1f - MIN) * MathHelper.Clamp(1f - distance / FALLOFF, 0f, 1f);
        }

        /// <summary>
        /// A small random pitch offset for a play, so a burst of the same effect never sounds flat.
        /// <para>
        /// <b>Centred on ZERO, and that is not a detail.</b> <see cref="SoundEffect.Play(float, float, float)"/>
        /// takes its pitch as an offset in OCTAVES over â’1â€¦1, where 0 is the sound as baked and 1 is a full
        /// octave up. This used to return <c>1 + jitter</c> â€” written as if it were a frequency multiplier â€”
        /// which pinned every sound in the game at the top of that range: the shot, every landing, the whistle
        /// and the report all played <b>an octave above</b> what they were synthesized as. Everything sounded
        /// thin, toy-like and squeaky, and no amount of retuning the synthesis could have fixed it, because
        /// the synthesis was never what was wrong.
        /// </para>
        /// </summary>
        private float NextPitch(float amplitude) => (float)(_random.NextDouble() * 2.0 - 1.0) * amplitude;

        //How far off-centre a sound has to be to reach full left/right pan. Sized to the cluster's span.
        private const float PAN_FULL_WIDTH = 18f;

        //The same, for things happening in the sky. A firework bursts a hundred units up and as far out again.
        private const float SKY_PAN_WIDTH = 90f;

        #region The signal chain

        /// <summary>
        /// The shot, layered like a real discharge rather than a single tone. A cannon going off is not one
        /// sound â€” it is a body of compressed air, the energy of the charge, the friction and the resistance â€”
        /// and each of those lives in a different part of the spectrum. This bakes five of them together so the
        /// shot reads as massive, present and exciting at <i>any</i> playback volume, which matters because a
        /// game is usually played quieter than a real cannon would be:
        /// <list type="bullet">
        /// <item><b>Boom</b> â€” a kick-drum pitch drop (150â†’60 Hz, fast) plus a low rumble. The weight: "boooom".
        /// Dominates the energy, and lives in the band ordinary speakers can reproduce.</item>
        /// <item><b>Zap</b> â€” a short buzzy sawtooth sweep (400â†’150 Hz). The energy of the charge itself: "vrrzzzzt".
        /// Adds midrange excitement that the boom alone does not have.</item>
        /// <item><b>Crackle</b> â€” high band-passed noise with a tremolo, trailing off. The air resistance and
        /// flying debris: "pĹˇĹˇĹˇchĹ™chĹ™chĹ™". Crucially this sits in the 1.5â€“5 kHz band where the ear is most
        /// sensitive, so it keeps the shot audible and cutting even when the volume is turned down low.</item>
        /// <item><b>Thud</b> â€” the muzzle transient that opens the whole thing.</item>
        /// </list>
        /// Then a reverb tail so the layered report carries and decays like a discharge in a space.
        /// </summary>
        private SoundEffect BakeShoot()
        {
            const float duration = 0.65f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //Layer 1 â€” the boom: a fast pitch drop, not a slow sweep. The pitch envelope is the heart of why
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

            //Layer 2 â€” the zap: the energy of the charge itself, a short buzzy sawtooth sweep. A sawtooth built
            //additively (eight partials at 1/k) is what gives the buzzy "vrrzzzzt"; a sine here would just be
            //another tone. Kept short â€” a flash on top of the boom, not a drone â€” and modest in gain so it is an
            //accent that excites the midrange, never the main event.
            const float zapDuration = 0.09f;
            int zapSamples = Math.Min(samples, (int)(SAMPLE_RATE * zapDuration));
            float zapPhase = 0f;
            for (int i = 0; i < zapSamples; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Another fast pitch drop, but in a higher register (400â†’150 Hz) so it reads as the charge's whine
                //rather than the body's thump.
                float freq = 400f * MathF.Pow(150f / 400f, t / zapDuration);
                zapPhase += 2f * MathF.PI * freq / SAMPLE_RATE;

                float saw = 0f;
                for (int k = 1; k <= 8; k++) saw += MathF.Sin(k * zapPhase) / k;

                //A 4 ms attack ramp then a fast decay: a zap, not a sustained note.
                float env = MathF.Exp(-t * 22f) * MathF.Min(1f, t / 0.004f);
                signal[i] += saw * 0.18f * env;
            }

            //Layer 3 â€” the crackle: the air resistance and debris, a high band-passed noise with a tremolo. This
            //is the layer that makes the shot cut through at low volume: it lives in the 1.5â€“5 kHz band where the
            //ear is most sensitive (Fletcher-Munson), so even quiet it reads as present and exciting. The tremolo
            //at ~45 Hz is what turns steady hiss into the "chĹ™chĹ™" crackle texture.
            float[] air = BandPass(MakeNoiseArray(samples), 1500f, 5000f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tremolo = 0.55f + 0.45f * MathF.Sin(2f * MathF.PI * 45f * t);
                signal[i] += air[i] * 0.22f * tremolo * MathF.Exp(-t * 8f);
            }

            //Layer 4 â€” the muzzle thud: a short, low-cutoff noise burst that opens the whole thing, kept low so
            //it is a thud rather than the sharp click that made earlier passes read as cheap.
            AddNoiseBurst(signal, window: 0.05f, decay: 25f, gain: 1.0f, cutoff: 1200f);

            //Space: a longer reverb tail so the layered report carries and decays like a discharge in a space.
            ApplyReverb(signal, roomScale: 0.5f, wet: 0.34f, decay: 0.36f);

            //The cannon has the same shape as the firework's report â€” a crack over a body â€” so it takes the
            //same treatment and for the same reason: peak-normalising it to the muzzle transient is what kept
            //the boom underneath thin.
            Loudness(signal, targetRms: 0.27f, ceiling: 0.98f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// A landing: a low "thunk" with harmonic content â€” one base note per ball type, so each colour lands on
        /// its own pitch â€” fronted by a filtered click of contact and underpinned by a sub thump. Shorter and
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
        /// The shell going up: the rising whistle every firework opens with. It is the one sound here that is
        /// almost a pure TONE â€” a whistling shell is a resonant cavity, not an explosion â€” and that is what
        /// makes it read against a scene full of broadband noise.
        /// <list type="bullet">
        /// <item><b>The rise is the whole effect.</b> The pitch climbs 620 â†’ 1950 Hz over the flight, which is
        /// what says "going up" without anything on screen having to. It is eased rather than linear so it
        /// slows as the shell does.</item>
        /// <item><b>Vibrato, or it reads as a test tone.</b> A few per cent of wobble at ~7 Hz is the shell
        /// spinning; dead-steady pitch sounds synthetic in a way no amount of harmonics fixes.</item>
        /// <item><b>A thin noise bed</b> band-passed around the tone: the air over the case. Quiet, but
        /// without it the whistle sits outside the scene rather than in it.</item>
        /// </list>
        /// </summary>
        private SoundEffect BakeFireworkLaunch()
        {
            const float duration = 1.15f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //Kept lower than a real firework whistle actually is. A shell's whistle is genuinely piercing, but
            //it is also thirty metres away and heard once; here it repeats every fraction of a second through
            //a whole display, and up in the piccolo register that reads as a toy rather than as a firework.
            const float startHz = 430f, endHz = 1320f;
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float u = t / duration;

                //Eased rise: fast at first and flattening, like a shell losing speed against gravity.
                float climb = 1f - (1f - u) * (1f - u);
                float freq = startHz + (endHz - startHz) * climb;

                //The spin — a SHIMMER, not a wobble. At 2.8 % and 7 Hz this was a slow, wide warble that read
                //as "vu-vu-vu-vu" rather than as a whistle: at that rate the ear tracks each swing as its own
                //note instead of hearing one tone with a texture. Shallow and fast is what a spinning shell
                //actually does to a tone.
                freq *= 1f + 0.006f * MathF.Sin(2f * MathF.PI * 16f * t);

                phase += 2f * MathF.PI * freq / SAMPLE_RATE;

                //A whistle is nearly a sine; a little second and third keep it from being a lab tone.
                float tone = MathF.Sin(phase) + 0.22f * MathF.Sin(2f * phase) + 0.06f * MathF.Sin(3f * phase);

                //In fast, hold, then fade as it gets away from the listener â€” and cut before the burst lands.
                float env = MathF.Min(1f, t / 0.05f) * MathF.Min(1f, (duration - t) / 0.28f);

                signal[i] += tone * 0.62f * env;
            }

            //The fizz. A firework leaving the ground is a burning fuse and a jet of gas before it is a tone at
            //all, and this layer carries far more of the character than the whistle does — it is what makes
            //the launch read as "bzzzt-pshhh" rather than as a kettle. Loud enough here to sit alongside the
            //tone rather than under it, and band-passed wide so it keeps some grit.
            float[] air = BandPass(MakeNoiseArray(samples, seed: 7717), 450f, 6500f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = MathF.Min(1f, t / 0.05f) * MathF.Min(1f, (duration - t) / 0.28f);

                //Thickest at the start, where the motor is doing the most work, and thinning as it climbs
                //away — the reverse of the tone, which is what makes the two read as one object.
                signal[i] += air[i] * 0.34f * env * (1.25f - 0.6f * (t / duration));
            }

            //Barely any room on this one: the shell is climbing away into open sky, and a long tail on a rising
            //tone smears the pitch into a chord.
            ApplyReverb(signal, roomScale: 0.55f, wet: 0.16f, decay: 0.20f);

            Normalize(signal, 0.92f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// The shell going off, in three parts, because a real report is three things arriving together and
        /// leaving at different rates:
        /// <list type="bullet">
        /// <item><b>The crack</b> â€” a very short, bright, barely-filtered noise transient. This is the part the
        /// ear times the event by, and it has to be the first sample: any attack ramp at all turns a bang into
        /// a whoomph.</item>
        /// <item><b>The boom</b> â€” a fast 110 â†’ 38 Hz pitch drop under it, carrying the weight. The same trick
        /// the cannon's boom uses, an octave lower and slower to decay, because this one is meant to sound big
        /// and far away rather than close and sharp.</item>
        /// <item><b>The crackle</b> â€” the burning stars, and the longest-lived of the three: high band-passed
        /// noise gated by a fast random stutter so it breaks into individual pops rather than hissing. It
        /// outlasts the boom by half a second, which is exactly what makes a firework sound like a firework
        /// and not like a gunshot.</item>
        /// </list>
        /// A long, wet reverb over the lot: this happens a hundred units up over an open scene.
        /// </summary>
        private SoundEffect BakeFireworkBurst()
        {
            const float duration = 2.6f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //The body. Deep, and it does NOT decay quickly â€” a big shell's report is felt for the best part of
            //a second before the roll takes over. Two oscillators an octave apart rather than one with a
            //harmonic: the sub carries the chest thump and the fundamental carries the pitch, and keeping them
            //separate lets the sub outlast the note, which is what large explosions do.
            const float pitchDropTime = 0.07f;
            const float pitchStart = 128f, pitchEnd = 33f;
            float phase = 0f, subPhase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                float freq = t < pitchDropTime
                    ? pitchStart * MathF.Pow(pitchEnd / pitchStart, t / pitchDropTime)
                    : pitchEnd;

                phase += 2f * MathF.PI * freq / SAMPLE_RATE;
                subPhase += 2f * MathF.PI * (freq * 0.5f) / SAMPLE_RATE;

                signal[i] += MathF.Sin(phase) * 0.85f * MathF.Exp(-t * 2.6f);
                signal[i] += MathF.Sin(subPhase) * 0.95f * MathF.Exp(-t * 1.9f);
            }

            //The blast's own noise, low-passed hard so it is pressure rather than hiss. This is most of what
            //makes the bang read as an explosion instead of as a drum.
            float[] blast = LowPassArray(MakeNoiseArray(samples, seed: 4231), 220f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                signal[i] += blast[i] * 0.9f * MathF.Exp(-t * 2.2f);
            }

            //The crack: the first few milliseconds, broadband and with no attack ramp at all. Any ramp turns a
            //bang into a whoomph, and this is the part the ear times the whole event by.
            AddNoiseBurst(signal, window: 0.03f, decay: 85f, gain: 1.5f, cutoff: 11000f);

            //The crackling stars. The stutter is what separates this from hiss: a fast, coarse random gate
            //held for a few milliseconds at a time, so the tail breaks into countable little pops.
            float[] stars = BandPass(MakeNoiseArray(samples, seed: 9091), 1700f, 6500f);
            const int holdSamples = 220;   //~5 ms per gate step
            float gate = 0f;

            for (int i = 0; i < samples; i++)
            {
                if (i % holdSamples == 0) gate = Noise(i / holdSamples, 5150) > 0.15f ? 1f : 0.15f;

                float t = (float)i / SAMPLE_RATE;

                //Starts a hair after the crack (the stars have to be thrown before they burn) and decays
                //slowest of everything here. Kept well under the body: the stars garnish the bang, and when
                //they lead it the whole thing reads as a sparkler rather than as a shell.
                float env = MathF.Min(1f, t / 0.04f) * MathF.Exp(-t * 1.7f);
                signal[i] += stars[i] * 0.22f * gate * env;
            }

            //The roll: the report coming back off everything around. This, more than the reverb, is what puts
            //the burst over a landscape instead of in a box.
            //Nine taps starting at 23 ms and stretching by an irrational ratio each time: dense enough at the
            //front to fuse into the bang itself, thinning into a rumble behind it.
            RollingEcho(signal, taps: 9, firstDelaySeconds: 0.023f, spread: 1.37f, feedback: 0.78f, mix: 0.42f);

            ApplyReverb(signal, roomScale: 1.0f, wet: 0.42f, decay: 0.7f);

            //Driven and softly saturated rather than peak-normalised â€” see Loudness. Normalising this to its
            //crack is exactly what left it a click with a thud behind it.
            Loudness(signal, targetRms: 0.30f, ceiling: 0.99f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// The party popper that opens the celebration: a dry paper crack and the rustle of confetti after it.
        /// Deliberately close and dry where the shells are big and wet â€” it is the one sound in the celebration
        /// that happens in the room rather than in the sky, which is what makes the shells read as distant.
        /// </summary>
        private SoundEffect BakePartyPopper()
        {
            const float duration = 0.55f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //The crack of the paper: bright, hard and over almost at once.
            AddNoiseBurst(signal, window: 0.02f, decay: 130f, gain: 1.0f, cutoff: 9000f);

            //A short body under it so it has a pop rather than a click.
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float freq = 220f * MathF.Pow(0.45f, t / 0.05f);
                signal[i] += MathF.Sin(2f * MathF.PI * freq * t) * 0.45f * MathF.Exp(-t * 40f);
            }

            //Confetti: a fine, dry rustle falling away. High and quiet, no reverb of its own to speak of.
            float[] confetti = BandPass(MakeNoiseArray(samples, seed: 2718), 3200f, 9000f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                signal[i] += confetti[i] * 0.20f * MathF.Exp(-t * 6.5f) * MathF.Min(1f, t / 0.02f);
            }

            ApplyReverb(signal, roomScale: 0.35f, wet: 0.18f, decay: 0.18f);

            //Driven rather than peak-normalised, for the reason the burst is: this is a transient with a tail,
            //and scaling it by its own crack leaves the crack loud and everything under it inaudible.
            Loudness(signal, targetRms: 0.24f, ceiling: 0.98f);
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
        private static float[] MakeNoiseArray(int samples) => MakeNoiseArray(samples, seed: 0);

        /// <summary>
        /// <inheritdoc cref="MakeNoiseArray(int)"/>
        /// <para>
        /// The seed matters whenever one bake mixes <b>two</b> noise layers: <see cref="Noise(int, int)"/> is a
        /// function of the sample index, so two unseeded layers are the SAME sequence and only differ by how
        /// they were filtered. Summing two filtered copies of one noise sequence is correlated â€” it thins out
        /// and starts to sound like a single filtered source rather than like two separate things happening.
        /// </para>
        /// </summary>
        private static float[] MakeNoiseArray(int samples, int seed)
        {
            float[] noise = new float[samples];
            for (int i = 0; i < samples; i++) noise[i] = Noise(i, seed);
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
        /// A Schroeder reverb â€” four parallel comb filters summed and passed through two series all-pass filters
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

            //Feedback gain derived from the decay time: longer tail, higher feedback â€” but capped short of the
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

            //Dry/wet mix. The wet path is scaled down a touch â€” it is ambience, not the main event.
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

        /// <summary>
        /// Drives <paramref name="signal"/> to a target RMS and then saturates it softly to
        /// <paramref name="ceiling"/> through a <c>tanh</c>. What a big report needs instead of
        /// <see cref="Normalize"/>.
        /// <para>
        /// <b>Peak normalisation is exactly wrong for an explosion.</b> Its loudest sample is the crack â€” a few
        /// milliseconds tall and thin â€” so scaling the peak to 1 scales the <i>body</i> down by whatever that
        /// transient happened to reach, and what is left is a tick with a quiet thud behind it. That is the
        /// difference between a bang and a click, and it is why every explosion sound effect ever made is
        /// compressed rather than normalised. Driving the RMS instead sets how loud the sound actually IS, and
        /// the tanh rounds the transient off rather than clipping it square.
        /// </para>
        /// </summary>
        private static void Loudness(float[] signal, float targetRms, float ceiling)
        {
            double sum = 0.0;
            for (int i = 0; i < signal.Length; i++) sum += signal[i] * (double)signal[i];

            float rms = (float)Math.Sqrt(sum / Math.Max(1, signal.Length));
            if (rms < 1e-6f) return;

            float drive = targetRms / rms;
            for (int i = 0; i < signal.Length; i++) signal[i] = MathF.Tanh(signal[i] * drive) * ceiling;
        }

        /// <summary>
        /// Slap-back repeats: a few discrete, progressively darker and quieter copies of the signal delayed
        /// behind it. This â€” not the reverb â€” is what makes a firework sound like it went off <i>over</i>
        /// something: the report reaches you once through the air and then again off every building, hillside
        /// and water surface around, which the ear reads as a long rolling rumble after the bang. A reverb
        /// alone gives a smooth wash and no roll.
        /// </summary>
        private static void RollingEcho(float[] signal, int taps, float firstDelaySeconds, float spread, float feedback, float mix)
        {
            float[] dry = (float[])signal.Clone();

            //Delays are spaced by an IRRATIONAL ratio and start inside the ear's fusion window. Both matter,
            //and getting them wrong is audible immediately: at four taps 60-85 ms apart the ear resolves each
            //one as its own event and the report comes out as "bum-bum-bum-bum" — a flutter echo, the sound of
            //a corridor, not of a firework over a city. Under ~40 ms and closely spaced they fuse into one
            //event with a tail, and an irrational spacing stops the repeats reinforcing into a pitch.
            for (int tap = 1; tap <= taps; tap++)
            {
                int delay = (int)(SAMPLE_RATE * firstDelaySeconds * MathF.Pow(spread, tap - 1));
                if (delay >= signal.Length) break;

                //Scaled by mix as well as by the decay, and that is what keeps the ATTACK at the front. The
                //first taps land 20-60 ms behind the crack, so at a high gain they add a second wavefront on
                //top of it: the envelope stops peaking on the first sample and climbs to a maximum a tenth of
                //a second in, which the ear hears as a soft "whoomph" instead of a hard "BUM". The roll has to
                //sit clearly UNDER the direct report, not double it.
                float gain = mix * MathF.Pow(feedback, tap);

                //Each repeat is darker than the last: air and distance take the top off first, which is what
                //makes the roll recede rather than just repeat.
                float[] tapSignal = LowPassArray(dry, 2600f / (1f + tap * 0.55f));

                for (int i = delay; i < signal.Length; i++) signal[i] += tapSignal[i - delay] * gain;
            }
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

        /// <summary>A cheap deterministic noise source for transients â€” quality is irrelevant for a few ms of crackle.</summary>
        private static float Noise(int i) => Noise(i, 0);

        /// <summary><inheritdoc cref="Noise(int)"/> <paramref name="seed"/> gives an independent sequence â€” see <see cref="MakeNoiseArray(int, int)"/>.</summary>
        private static float Noise(int i, int seed)
        {
            uint h = (uint)(i * 2654435761u) ^ 0x9E3779B9u ^ (uint)(seed * 374761393);
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
            _fireworkLaunch?.Dispose();
            _fireworkBurst?.Dispose();
            _partyPopper?.Dispose();
        }
    }
}

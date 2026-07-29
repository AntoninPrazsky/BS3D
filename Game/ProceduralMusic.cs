using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;

namespace BS3D
{
    /// <summary>
    /// The level theme: a sixteen-bar eurodance loop synthesized from raw PCM at load and played on a loop.
    /// No asset, no tracker file, no pipeline step — the score is a handful of arrays and the instruments are
    /// oscillators, the same line the sound effects, the meshes and the surface textures all take.
    /// <para>
    /// Everything about it is aimed at one thing: being catchy at speed. 146 BPM, a four-on-the-floor kick, an
    /// off-beat bass, a sixteenth arpeggio and a lead built from ONE rhythmic motif transposed across the
    /// chords rather than four different phrases — repetition with variation is what a hook is, and a melody
    /// that never repeats is a melody nobody can hum.
    /// </para>
    /// </summary>
    public sealed class ProceduralMusic : IDisposable
    {
        private const int SAMPLE_RATE = 44100;

        //146 is up in the range where a four-to-the-floor kick stops reading as a pulse and starts reading as
        //a dance; much past 150 and the sixteenth arpeggio turns into a buzz.
        private const float BPM = 146f;
        private const int STEPS_PER_BEAT = 4;      //sixteenths
        private const int STEPS_PER_BAR = 16;
        private const int BARS = 16;
        private const int TOTAL_STEPS = BARS * STEPS_PER_BAR;

        /// <summary>Overall level of the music, well under the effects — a soundtrack is not an event.</summary>
        public const float MUSIC_VOLUME = 0.34f;

        //A minor: vi-IV-I-V, the progression underneath a very large fraction of every catchy record ever
        //made. One chord a bar, four bars round, four times over the loop.
        private static readonly int[] CHORD_ROOT = { 45, 41, 48, 43 };                 //A2, F2, C3, G2

        //The arpeggio's four notes per chord, low to high. Kept in one octave band so the arp does not leap
        //register as the chords move under it.
        private static readonly int[][] CHORD_ARP =
        {
            new[] { 57, 60, 64, 69 },   //Am: A3 C4 E4 A4
            new[] { 53, 57, 60, 65 },   //F : F3 A3 C4 F4
            new[] { 60, 64, 67, 72 },   //C : C4 E4 G4 C5
            new[] { 55, 59, 62, 67 }    //G : G3 B3 D4 G4
        };

        //THE HOOK. One rhythmic figure, transposed onto each chord — which is the whole trick. Four different
        //phrases over four chords is a tune nobody remembers; the same shape moving under changing harmony is
        //something the ear has learned by its second time round.
        //
        //Steps within the bar that carry a note. The gaps matter as much as the notes: everything lands on or
        //just after a beat except the one on step 7, and that single syncopation is what stops it marching.
        private static readonly int[] MOTIF_A_STEPS = { 0, 2, 4, 5, 7, 9, 12 };

        //Scale degrees above each chord's own arp root, as offsets in SEMITONES from the chord's third arp
        //note (its "home" for the melody). Written per chord rather than as one transposition because a minor
        //chord and a major one do not take the same intervals.
        private static readonly int[][] MOTIF_A_NOTES =
        {
            new[] { 69, 72, 76, 76, 72, 69, 64 },   //over Am
            new[] { 65, 69, 72, 72, 69, 65, 60 },   //over F
            new[] { 72, 76, 79, 79, 76, 72, 67 },   //over C
            new[] { 67, 71, 74, 74, 71, 67, 62 }    //over G
        };

        //The second eight bars: longer notes, higher, fewer of them. A chorus is not a busier verse — it is
        //usually a SIMPLER one, and the contrast is what makes the loop feel like it has two halves rather
        //than being one phrase played four times.
        private static readonly int[] MOTIF_B_STEPS = { 0, 3, 6, 8, 10, 12 };

        private static readonly int[][] MOTIF_B_NOTES =
        {
            new[] { 76, 81, 79, 76, 72, 69 },   //over Am
            new[] { 72, 77, 76, 72, 69, 65 },   //over F
            new[] { 76, 84, 79, 76, 72, 67 },   //over C
            new[] { 74, 79, 77, 74, 71, 67 }    //over G
        };

        //The turnaround: a descending run over the last bar of each eight, which is what hands the loop back
        //to its own beginning instead of just stopping and starting again.
        private static readonly int[] FILL_STEPS = { 8, 10, 11, 12, 13, 14, 15 };
        private static readonly int[] FILL_NOTES = { 79, 77, 76, 74, 72, 71, 69 };

        private readonly SoundEffect _track;
        private readonly SoundEffectInstance _instance;

        /// <summary>True while the loop is running.</summary>
        public bool IsPlaying => _instance != null && _instance.State == SoundState.Playing;

        public ProceduralMusic()
        {
            float[] mix = Bake();

            _track = ToSoundEffect(mix);
            _instance = _track.CreateInstance();
            _instance.IsLooped = true;
            _instance.Volume = MUSIC_VOLUME;
        }

        /// <summary>Starts the loop, or does nothing if it is already running.</summary>
        public void Play()
        {
            if (_instance == null || _instance.State == SoundState.Playing) return;
            _instance.Play();
        }

        /// <summary>Stops the loop and rewinds it, so the next level opens on the downbeat rather than mid-bar.</summary>
        public void Stop() => _instance?.Stop();

        #region The score

        /// <summary>
        /// Renders the whole loop into one float buffer. Every voice writes into the same mix additively, and
        /// the lot is soft-limited at the end — a mix that is peak-normalised is one whose loudest single
        /// coincidence of kick and lead decides how loud everything else is.
        /// </summary>
        private static float[] Bake()
        {
            float secondsPerStep = 60f / (BPM * STEPS_PER_BEAT);
            int samplesPerStep = (int)(SAMPLE_RATE * secondsPerStep);

            //A whole number of samples per step, and the loop's length taken FROM that rather than from the
            //nominal tempo: rounding per step and then trusting the nominal length leaves a fraction of a step
            //of silence at the seam, which on a loop is an audible hiccup once a bar-cycle.
            int samples = samplesPerStep * TOTAL_STEPS;
            float[] mix = new float[samples];

            for (int step = 0; step < TOTAL_STEPS; step++)
            {
                int at = step * samplesPerStep;
                int bar = step / STEPS_PER_BAR;
                int inBar = step % STEPS_PER_BAR;
                int chord = bar % 4;

                //DRUMS ---------------------------------------------------------------------------------
                //Four on the floor. The whole genre rests on this one line.
                if (inBar % 4 == 0) Kick(mix, at);

                //Clap on two and four, the backbeat.
                if (inBar == 4 || inBar == 12) Clap(mix, at);

                //Eighth-note hats, with the off-beats opened up — closed on the beat, open between, which is
                //what gives the bar its swing without changing a single note's timing.
                if (inBar % 2 == 0) Hat(mix, at, open: inBar % 4 == 2, level: 0.34f);

                //A sixteenth run into every bar's last beat: the ear needs telling that the bar is about to
                //turn over.
                if (inBar >= 14) Hat(mix, at, open: false, level: 0.22f);

                //BASS ----------------------------------------------------------------------------------
                //Off-beat eighths against the kick. Playing it ON the beat doubles the kick and both go to
                //mud; playing it between is what makes the two read as a pump.
                if (inBar % 4 == 2) Bass(mix, at, CHORD_ROOT[chord] + 12, secondsPerStep * 1.7f);

                //And a root on the downbeat, short, so the harmony lands with the kick.
                if (inBar == 0) Bass(mix, at, CHORD_ROOT[chord], secondsPerStep * 1.4f);

                //ARPEGGIO ------------------------------------------------------------------------------
                //Straight sixteenths, up and down the chord. This is the engine of the thing: it never stops,
                //so the track keeps moving even where the melody rests.
                int[] arp = CHORD_ARP[chord];
                int arpIndex = inBar % 8;
                int arpNote = arp[arpIndex < 4 ? arpIndex : 7 - arpIndex];
                Arp(mix, at, arpNote + 12, secondsPerStep * 0.9f, level: 0.16f);

                //LEAD ----------------------------------------------------------------------------------
                //Bars 0-6 and 8-14 carry the motif; bars 7 and 15 hand the phrase back with the fill.
                bool fillBar = bar == 7 || bar == 15;
                bool chorus = bar >= 8;

                if (fillBar)
                {
                    int index = Array.IndexOf(FILL_STEPS, inBar);
                    if (index >= 0) Lead(mix, at, FILL_NOTES[index], secondsPerStep * 1.6f, level: 0.30f);
                }
                else
                {
                    int[] steps = chorus ? MOTIF_B_STEPS : MOTIF_A_STEPS;
                    int[] notes = (chorus ? MOTIF_B_NOTES : MOTIF_A_NOTES)[chord];

                    int index = Array.IndexOf(steps, inBar);
                    if (index >= 0)
                    {
                        //The chorus holds its notes; the verse spits them out. Same instrument, and the
                        //difference between the two halves is almost entirely this.
                        float length = secondsPerStep * (chorus ? 2.6f : 1.5f);
                        Lead(mix, at, notes[index], length, level: chorus ? 0.34f : 0.30f);
                    }
                }
            }

            //Soft-limited, not peak-normalised — see ProceduralAudio.Loudness for why that distinction matters
            //to anything with a transient in it, and a kick is nothing but transient.
            Limit(mix, targetRms: 0.20f, ceiling: 0.95f);

            return mix;
        }

        #endregion

        #region The instruments

        /// <summary>
        /// The kick: a sine whose pitch collapses from 150 Hz to 45 in forty milliseconds, plus a click on the
        /// very first samples. The pitch envelope IS the kick — a fixed low sine is a hum, and the drop is what
        /// the ear reads as something being struck.
        /// </summary>
        private static void Kick(float[] mix, int at)
        {
            int length = (int)(SAMPLE_RATE * 0.26f);
            float phase = 0f;

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                float freq = 45f + 105f * MathF.Exp(-t * 26f);
                phase += 2f * MathF.PI * freq / SAMPLE_RATE;

                float body = MathF.Sin(phase) * MathF.Exp(-t * 8.5f);
                float click = (i < 90) ? Noise(i, 11) * 0.5f * (1f - i / 90f) : 0f;

                mix[at + i] += (body * 0.95f + click) * 0.9f;
            }
        }

        /// <summary>
        /// The clap: three noise bursts a few milliseconds apart and then a longer tail. The stutter is what
        /// makes it a clap rather than a snare — a room full of hands does not land on one sample.
        /// </summary>
        private static void Clap(float[] mix, int at)
        {
            int length = (int)(SAMPLE_RATE * 0.22f);
            int[] offsets = { 0, (int)(SAMPLE_RATE * 0.009f), (int)(SAMPLE_RATE * 0.018f) };

            foreach (int offset in offsets)
                for (int i = 0; i < SAMPLE_RATE * 0.03f && at + offset + i < mix.Length; i++)
                {
                    float t = (float)i / SAMPLE_RATE;
                    mix[at + offset + i] += BandNoise(i, 1400f, 5200f, 23) * 0.5f * MathF.Exp(-t * 90f);
                }

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                mix[at + i] += BandNoise(i, 1100f, 4200f, 29) * 0.26f * MathF.Exp(-t * 22f);
            }
        }

        /// <summary>The hat: high noise, very short closed and a good deal longer open.</summary>
        private static void Hat(float[] mix, int at, bool open, float level)
        {
            float decay = open ? 26f : 95f;
            int length = (int)(SAMPLE_RATE * (open ? 0.16f : 0.05f));

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                mix[at + i] += BandNoise(i, 6000f, 15000f, 37) * level * MathF.Exp(-t * decay);
            }
        }

        /// <summary>
        /// The bass: a saw run through a one-pole low-pass that opens with the note's own envelope, which is
        /// the cheapest thing that sounds like a filter sweep and is most of what a dance bass is.
        /// </summary>
        private static void Bass(float[] mix, int at, int note, float seconds)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float freq = Frequency(note);
            float phase = 0f;
            float lp = 0f;

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = MathF.Min(1f, t / 0.004f) * MathF.Exp(-t * 6.5f);

                phase += freq / SAMPLE_RATE;
                if (phase >= 1f) phase -= 1f;

                float saw = PolyBlepSaw(phase, freq / SAMPLE_RATE);

                //Cutoff tracks the envelope: bright on the attack, closing as it decays.
                float cutoff = 190f + 2600f * env;
                float alpha = CutoffToAlpha(cutoff);
                lp += alpha * (saw - lp);

                mix[at + i] += lp * 0.55f * env;
            }
        }

        /// <summary>The arpeggio: a plain square, short and quiet. The DOS-era voice, and it is meant to sound like one.</summary>
        private static void Arp(float[] mix, int at, int note, float seconds, float level)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float freq = Frequency(note);
            float phase = 0f;

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = MathF.Min(1f, t / 0.002f) * MathF.Exp(-t * 15f);

                phase += freq / SAMPLE_RATE;
                if (phase >= 1f) phase -= 1f;

                mix[at + i] += PolyBlepSquare(phase, freq / SAMPLE_RATE) * level * env;
            }
        }

        /// <summary>
        /// The lead: two saws detuned a few cents apart. That beating between them is the whole "supersaw"
        /// sound the genre is built on, and two oscillators are enough for it — the width comes from the
        /// detune, not from the count.
        /// </summary>
        private static void Lead(float[] mix, int at, int note, float seconds, float level)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float freq = Frequency(note);

            //About twelve cents apart. Much less and they sound like one flat oscillator; much more and the
            //beating turns into an out-of-tune chord.
            float freqA = freq * 0.9965f;
            float freqB = freq * 1.0035f;

            float phaseA = 0f, phaseB = 0.5f;
            float lp = 0f;

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //A real attack rather than an instant one, and a long release: the lead is the one voice here
                //that is supposed to sing rather than to hit.
                float env = MathF.Min(1f, t / 0.012f) * MathF.Exp(-t * 3.6f);

                //A slow vibrato that only comes in after the note has settled, which is what a player does.
                float vibrato = 1f + 0.004f * MathF.Sin(2f * MathF.PI * 5.2f * t) * MathF.Min(1f, t / 0.25f);

                phaseA += freqA * vibrato / SAMPLE_RATE; if (phaseA >= 1f) phaseA -= 1f;
                phaseB += freqB * vibrato / SAMPLE_RATE; if (phaseB >= 1f) phaseB -= 1f;

                float saw = PolyBlepSaw(phaseA, freqA / SAMPLE_RATE) + PolyBlepSaw(phaseB, freqB / SAMPLE_RATE);

                //Gently low-passed so the top end does not shriek over the arp.
                lp += CutoffToAlpha(4200f) * (saw * 0.5f - lp);

                mix[at + i] += lp * level * env;
            }
        }

        #endregion

        #region Oscillators and helpers

        private static float Frequency(int midiNote) => 440f * MathF.Pow(2f, (midiNote - 69) / 12f);

        private static float CutoffToAlpha(float cutoff)
        {
            float dt = 1f / SAMPLE_RATE;
            float rc = 1f / (2f * MathF.PI * cutoff);
            return dt / (rc + dt);
        }

        /// <summary>
        /// A saw, anti-aliased by PolyBLEP: the naive ramp with a polynomial correction applied either side of
        /// its discontinuity.
        /// <para>
        /// This matters more here than anywhere else in the project. A naive saw or square has energy at every
        /// harmonic, and at 44.1 kHz everything above Nyquist folds back down as inharmonic tones — on a lead
        /// playing an A5 that is a chorus of wrong notes moving the opposite way to the melody, which reads as
        /// cheapness rather than as a bug. Two lines of correction remove nearly all of it, and unlike summing
        /// band-limited harmonics it costs the same at any pitch.
        /// </para>
        /// </summary>
        private static float PolyBlepSaw(float phase, float increment)
        {
            float value = 2f * phase - 1f;
            return value - PolyBlep(phase, increment);
        }

        /// <summary>A square, anti-aliased the same way — a correction at each of its two edges per cycle.</summary>
        private static float PolyBlepSquare(float phase, float increment)
        {
            float value = phase < 0.5f ? 1f : -1f;

            value -= PolyBlep(phase, increment);
            value += PolyBlep((phase + 0.5f) % 1f, increment);

            return value;
        }

        /// <summary>The correction itself: a parabola spanning one sample either side of the discontinuity.</summary>
        private static float PolyBlep(float t, float dt)
        {
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1f;
            }

            if (t > 1f - dt)
            {
                t = (t - 1f) / dt;
                return t * t + t + t + 1f;
            }

            return 0f;
        }

        /// <summary>Deterministic white noise, seeded so two layers in one instrument are not the same sequence.</summary>
        private static float Noise(int i, int seed)
        {
            uint h = (uint)(i * 2654435761u) ^ 0x9E3779B9u ^ (uint)(seed * 374761393);
            h ^= h >> 13;
            h *= 0x85EBCA6Bu;
            h ^= h >> 16;
            return (h / (float)uint.MaxValue) * 2f - 1f;
        }

        /// <summary>
        /// Band-passed noise evaluated a sample at a time, by running two one-pole filters as running state.
        /// Cheaper than filtering a whole array per hit, and the drums only ever need a few thousand samples.
        /// </summary>
        private static float BandNoise(int i, float low, float high, int seed)
        {
            //A stateless approximation: the difference of two smoothed noise reads. It is not a textbook
            //band-pass, but for a hat or a clap the ear is judging the band and the decay, not the skirts.
            float a = 0f, b = 0f;
            float alphaHigh = CutoffToAlpha(high);
            float alphaLow = CutoffToAlpha(low);

            for (int k = Math.Max(0, i - 24); k <= i; k++)
            {
                float white = Noise(k, seed);
                a += alphaHigh * (white - a);
                b += alphaLow * (white - b);
            }

            return a - b;
        }

        /// <summary>Drives the mix to a target RMS and saturates it softly — the music's own limiter.</summary>
        private static void Limit(float[] signal, float targetRms, float ceiling)
        {
            double sum = 0.0;
            for (int i = 0; i < signal.Length; i++) sum += signal[i] * (double)signal[i];

            float rms = (float)Math.Sqrt(sum / Math.Max(1, signal.Length));
            if (rms < 1e-6f) return;

            float drive = targetRms / rms;
            for (int i = 0; i < signal.Length; i++) signal[i] = MathF.Tanh(signal[i] * drive) * ceiling;
        }

        private static SoundEffect ToSoundEffect(float[] signal)
        {
            byte[] pcm = new byte[signal.Length * 2];

            for (int i = 0; i < signal.Length; i++)
            {
                short v = (short)(MathHelper.Clamp(signal[i], -1f, 1f) * short.MaxValue);
                pcm[i * 2] = (byte)(v & 0xff);
                pcm[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }

            return new SoundEffect(pcm, SAMPLE_RATE, AudioChannels.Mono);
        }

        #endregion

        public void Dispose()
        {
            _instance?.Dispose();
            _track?.Dispose();
        }
    }
}

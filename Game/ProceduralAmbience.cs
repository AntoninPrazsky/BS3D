using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Prazsky.Core.Render;
using System;
using System.Threading.Tasks;

namespace BS3D
{
    /// <summary>
    /// The scenes' ambient beds (#46): one looping texture per backdrop — surf for the sea, a wind for each
    /// terrain, a traffic rumble for the cities, a near-subliminal drone for space — synthesized from raw PCM
    /// at startup like every other sound in the game, and crossfaded when the scene changes.
    /// <para>
    /// Everything here is <b>filtered noise, never a tone</b>: a bed sits under the music and the effects for
    /// the whole run, and anything with a pitch would either fight the theme (which transposes itself per
    /// pass) or be noticed — and a bed that is noticed is a bed that is too loud. Each bed is a loop sealed
    /// the menu piece's way (its ring-out folded back onto its head, see <see cref="BakeAll"/>), with every
    /// envelope running a whole number of cycles per loop so nothing jumps at the seam.
    /// </para>
    /// <para>
    /// The beds run whatever is on the stack, pause included — the scene is on screen whether or not a
    /// session stands, so its sound is too, the same rule the clouds and the city's windows follow.
    /// </para>
    /// </summary>
    public sealed class ProceduralAmbience : IDisposable
    {
        /// <summary>
        /// The authored level of the beds, under everything else — atmosphere, not an event. Retuned by ear
        /// from 0.16 to a quarter of it: at 0.16 the beds sat OVER the theme and buried it, and an ambience
        /// the ear picks out over the music is an ambience that is too loud by definition.
        /// </summary>
        public const float AMBIENCE_VOLUME = 0.04f;

        //A scene change sweeps the whole backdrop in one frame; the sound follows over this instead, because
        //an atmosphere that cuts is a light switch and one that drags arrives after the scene has.
        private const float CROSSFADE_SECONDS = 1.5f;

        private const int SAMPLE_RATE = 44100;

        //Long enough that the ear has let go of a swell before the loop returns to it; every envelope below
        //is written in whole cycles of this length, which is half of what makes the seam inaudible.
        private const float LOOP_SECONDS = 16f;

        private const int SCENES = (int)SceneKind.Space + 1;

        private Task<float[][]> _bake;
        private SoundEffect[] _beds;

        //Only the two instances a crossfade needs are ever alive: the one fading out and the one fading in.
        //A bed's instance is made per arrival and disposed once it has fully faded — a scene change is a
        //click on the scene page, not a per-frame path.
        private SoundEffectInstance _from, _to;
        private int _toScene = -1;
        private float _blend = 1f;      //0 = all _from, 1 = all _to

        private int _wanted = -1;
        private float _gain = 1f;
        private bool _volumesDirty;
        private bool _failed;

        public ProceduralAmbience()
        {
            //All nine beds bake on one background task — they are a fraction of one music pass's arithmetic,
            //and nothing needs them until the first frame of the scene is already on screen.
            _bake = Task.Run(BakeAll);
        }

        /// <summary>
        /// The player's volume settings (master × ambience) — the beds have a row of their own, so taste in
        /// atmosphere is not chained to the effects. Written by the host's <c>ApplyVolumes</c>, the one writer.
        /// </summary>
        public float Gain
        {
            get => _gain;
            set { _gain = value; _volumesDirty = true; }
        }

        /// <summary>
        /// Names the scene whose bed should be sounding. Callable before the bakes have landed — the wish is
        /// kept and honoured the frame they do. Called from the host's <c>SetScene</c>, the one scene writer.
        /// </summary>
        public void SetScene(SceneKind scene) => _wanted = (int)scene;

        /// <summary>Advances the crossfade and realizes the bakes. Called once a frame with wall-clock time.</summary>
        public void Update(float elapsed)
        {
            if (_failed) return;

            //Realized once, the frame the synthesis finishes. Guarded like the music's: an atmosphere that
            //cannot play must not take the game down with it.
            if (_bake != null && _bake.IsCompleted)
            {
                Task<float[][]> ready = _bake;
                _bake = null;

                try
                {
                    float[][] baked = ready.Result;
                    _beds = new SoundEffect[SCENES];
                    for (int i = 0; i < SCENES; i++) _beds[i] = ToSoundEffect(baked[i]);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[audio] the scene beds could not be realized: {exception.Message}");
                    _failed = true;
                    return;
                }
            }

            if (_beds == null) return;

            //A new scene retargets the fade: the outgoing bed (if one is still fading) is let go where it
            //stands — a hard cut at partial volume, audible only when scenes are clicked through faster than
            //the fade, which is the scene picker and not play — and the current bed becomes the one fading out.
            if (_wanted >= 0 && _wanted != _toScene)
            {
                _from?.Stop();
                _from?.Dispose();
                _from = _to;

                _to = _beds[_wanted].CreateInstance();
                _to.IsLooped = true;
                _to.Volume = 0f;
                _to.Play();

                //From silence (first scene of the run) the bed fades IN over the same window; between scenes
                //the weights pick up where the outgoing bed's volume leaves them.
                _blend = _from == null ? 0f : 1f - MathHelper.Clamp(_blend, 0f, 1f);
                _toScene = _wanted;
                _volumesDirty = true;
            }

            if (_blend < 1f)
            {
                _blend = MathF.Min(1f, _blend + elapsed / CROSSFADE_SECONDS);
                _volumesDirty = true;

                if (_blend >= 1f && _from != null)
                {
                    _from.Stop();
                    _from.Dispose();
                    _from = null;
                }
            }

            //Volumes are written only when the fade or the settings moved them — most frames touch nothing.
            if (!_volumesDirty) return;
            _volumesDirty = false;

            //Equal-power: the weights are square roots, so the summed loudness holds through the middle of
            //the fade instead of dipping.
            float level = AMBIENCE_VOLUME * _gain;
            if (_to != null) _to.Volume = MathHelper.Clamp(MathF.Sqrt(_blend) * level, 0f, 1f);
            if (_from != null) _from.Volume = MathHelper.Clamp(MathF.Sqrt(1f - _blend) * level, 0f, 1f);
        }

        #region The beds

        /// <summary>
        /// All nine beds. Each is layered band-passed noise under envelopes written as whole cycles per loop,
        /// rendered one second past the loop point and <b>folded back onto the head</b> (equal-power), so the
        /// noise content is continuous across the seam the same way the envelopes are.
        /// </summary>
        private static float[][] BakeAll()
        {
            float[][] beds = new float[SCENES][];

            for (int scene = 0; scene < SCENES; scene++) beds[scene] = BakeScene((SceneKind)scene);

            return beds;
        }

        private static float[] BakeScene(SceneKind scene)
        {
            int loopSamples = (int)(SAMPLE_RATE * LOOP_SECONDS);
            int tailSamples = SAMPLE_RATE;   //one second of ring past the loop point, folded back below

            float[] mix = new float[loopSamples + tailSamples];

            //Seeds are per scene and per layer, because two layers fed the same sequence differ only in how
            //they were filtered, and summing two filtered copies of one sequence is correlated — the
            //celebration sounds learned this first.
            int seed = 100 + (int)scene * 17;

            switch (scene)
            {
                case SceneKind.Sea:
                    //The wash: big low swells, two waves per loop. The foam follows a quarter turn behind —
                    //the hiss of a wave arrives after its weight.
                    AddBand(mix, seed, 0f, 420f, 1.0f, t => Swell(t, 2, 0.35f, 0f));
                    AddBand(mix, seed + 1, 900f, 3200f, 0.5f, t => Square(Swell(t, 2, 0.15f, -MathF.PI / 2f)));
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.16f);

                case SceneKind.Savanna:
                    //A warm wind, wandering on two incommensurate-feeling cycles, and the campfire the scene
                    //visibly carries: a crackle of band noise gated by a fast random stutter, the firework
                    //crackle's trick at a hearth's scale.
                    AddBand(mix, seed, 100f, 650f, 1.0f, t => 0.55f + 0.25f * Cycle(t, 3, 0f) + 0.2f * Cycle(t, 5, 1.3f));
                    AddCrackle(mix, seed + 1, 1500f, 5000f, 0.3f);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.13f);

                case SceneKind.Desert:
                    //A dry, steady wind, thinner than the savanna's, with the faintest whistle over it.
                    AddBand(mix, seed, 250f, 1200f, 1.0f, t => Swell(t, 2, 0.8f, 0f));
                    AddBand(mix, seed + 1, 1700f, 2100f, 0.18f, t => Square(Swell(t, 3, 0.3f, 2.1f)));
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.11f);

                case SceneKind.Mountain:
                    //Cold gusts — the swell squared, so the wind arrives in waves rather than breathing — with
                    //thin air hissing on the same gusts.
                    AddBand(mix, seed, 80f, 900f, 1.0f, t => Square(Swell(t, 3, 0.25f, 0f)));
                    AddBand(mix, seed + 1, 2000f, 6000f, 0.22f, t => Swell(t, 3, 0.4f, 0f));
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.14f);

                case SceneKind.Meadow:
                    //A gentle breeze and a high insect shimmer under a light, fast tremolo.
                    AddBand(mix, seed, 120f, 500f, 0.9f, t => Swell(t, 2, 0.6f, 0f));
                    AddBand(mix, seed + 1, 3800f, 6500f, 0.16f, t => Swell(t, 1, 0.5f, 0f) * (0.75f + 0.25f * Cycle(t, 128, 0f)));
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.10f);

                case SceneKind.City:
                    //Distant traffic: a deep rumble that never quite settles, and a mid murmur over it.
                    AddBand(mix, seed, 0f, 180f, 1.0f, t => Swell(t, 2, 0.7f, 0f));
                    AddBand(mix, seed + 1, 150f, 450f, 0.35f, t => Swell(t, 3, 0.75f, 0.9f));
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.12f);

                case SceneKind.NeonCity:
                    //The same city a few hours later: the rumble quieter, a steady electric hum in a narrow
                    //band (noise, not a tone — mains hum as texture), and a whisper of neon fizz on top.
                    AddBand(mix, seed, 0f, 160f, 1.0f, t => Swell(t, 2, 0.75f, 0f));
                    AddBand(mix, seed + 1, 100f, 150f, 0.55f, _ => 1f);
                    AddBand(mix, seed + 2, 3000f, 8000f, 0.08f, t => Swell(t, 5, 0.5f, 0f));
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.12f);

                case SceneKind.Forest:
                    //Leaves answering gusts: the rustle band swells with the wind (raised to a power, so calm
                    //is genuinely calm) and flutters finely on top; a soft low wind under it.
                    AddBand(mix, seed, 100f, 400f, 0.6f, t => Swell(t, 2, 0.6f, 0f));
                    AddBand(mix, seed + 1, 800f, 4200f, 1.0f,
                        t => (0.35f + 0.65f * MathF.Pow(Swell(t, 3, 0f, 0f), 1.5f)) * (0.8f + 0.2f * Cycle(t, 96, 0f)));
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.12f);

                default:
                    //Space: the void, one very deep and very slow breath per loop, mixed near-subliminal —
                    //the scene's whole point is silence with weight.
                    AddBand(mix, seed, 0f, 55f, 1.0f, t => Swell(t, 1, 0.6f, 0f));
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.05f);
            }
        }

        /// <summary>A raised cosine running <paramref name="cycles"/> whole cycles per loop, from <paramref name="floor"/> to 1.</summary>
        private static float Swell(float t, int cycles, float floor, float phase)
            => floor + (1f - floor) * (0.5f + 0.5f * MathF.Sin(2f * MathF.PI * cycles * t / LOOP_SECONDS + phase));

        /// <summary>A plain sine of whole cycles per loop, for wander written as a sum of two.</summary>
        private static float Cycle(float t, int cycles, float phase)
            => MathF.Sin(2f * MathF.PI * cycles * t / LOOP_SECONDS + phase);

        private static float Square(float x) => x * x;

        /// <summary>
        /// Adds band-passed noise under an envelope. The band is the difference of two one-pole low-passes —
        /// crude by filter standards and exactly enough for a texture; <paramref name="lowCut"/> of zero
        /// degenerates to a plain low-pass.
        /// </summary>
        private static void AddBand(float[] mix, int seed, float lowCut, float highCut, float gain, Func<float, float> envelope)
        {
            float alphaHigh = Alpha(highCut);
            float alphaLow = Alpha(lowCut);
            float lpHigh = 0f, lpLow = 0f;

            for (int i = 0; i < mix.Length; i++)
            {
                float noise = Noise(i, seed);

                lpHigh += alphaHigh * (noise - lpHigh);
                lpLow += alphaLow * (lpHigh - lpLow);

                mix[i] += (lpHigh - lpLow) * gain * envelope((float)i / SAMPLE_RATE);
            }
        }

        /// <summary>
        /// The campfire: band noise gated by a fast random stutter — the firework crackle's trick at a
        /// hearth's scale. The gate is re-rolled ~47 times a second, which is what turns a steady hiss into
        /// separate burning snaps.
        /// </summary>
        private static void AddCrackle(float[] mix, int seed, float lowCut, float highCut, float gain)
        {
            float alphaHigh = Alpha(highCut);
            float alphaLow = Alpha(lowCut);
            float lpHigh = 0f, lpLow = 0f;
            float gate = 0f;

            for (int i = 0; i < mix.Length; i++)
            {
                float noise = Noise(i, seed);

                lpHigh += alphaHigh * (noise - lpHigh);
                lpLow += alphaLow * (lpHigh - lpLow);

                //A snap opens the gate at once; the gate then falls quickly, so each snap has a tail rather
                //than a square edge.
                int slot = i / (SAMPLE_RATE / 47);
                bool snap = Noise(slot, seed + 9) > 0.62f;
                gate = snap ? 1f : gate * (1f - 220f / SAMPLE_RATE);

                mix[i] += (lpHigh - lpLow) * gain * gate;
            }
        }

        private static float Alpha(float cutoff)
            => cutoff <= 0f ? 0f : 1f - MathF.Exp(-2f * MathF.PI * cutoff / SAMPLE_RATE);

        /// <summary>A cheap hash noise: a pure function of the index, so two seeds are genuinely uncorrelated.</summary>
        private static float Noise(int i, int seed)
        {
            uint h = unchecked((uint)i * 374761393u + (uint)seed * 668265263u);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 8388607.5f - 1f;
        }

        /// <summary>
        /// Seals the loop and sets its level: the second past the loop point is folded back onto the head
        /// under an equal-power ramp, the result is cut to the loop, and the whole bed is scaled to the
        /// target RMS — plain scaling, these are textures with no transient to protect.
        /// </summary>
        private static float[] Seal(float[] mix, int loopSamples, int tailSamples, float targetRms)
        {
            float[] loop = new float[loopSamples];
            Array.Copy(mix, loop, loopSamples);

            for (int i = 0; i < tailSamples; i++)
            {
                float w = (float)i / tailSamples;
                loop[i] = loop[i] * MathF.Sqrt(w) + mix[loopSamples + i] * MathF.Sqrt(1f - w);
            }

            double sum = 0;
            for (int i = 0; i < loop.Length; i++) sum += loop[i] * loop[i];
            float rms = (float)Math.Sqrt(sum / loop.Length);

            if (rms > 1e-6f)
            {
                float scale = targetRms / rms;
                for (int i = 0; i < loop.Length; i++) loop[i] = MathHelper.Clamp(loop[i] * scale, -0.98f, 0.98f);
            }

            return loop;
        }

        private static SoundEffect ToSoundEffect(float[] signal)
        {
            byte[] pcm = new byte[signal.Length * 2];

            for (int i = 0; i < signal.Length; i++)
            {
                short sample = (short)(MathHelper.Clamp(signal[i], -1f, 1f) * short.MaxValue);
                pcm[i * 2] = (byte)(sample & 0xFF);
                pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return new SoundEffect(pcm, SAMPLE_RATE, AudioChannels.Mono);
        }

        #endregion

        public void Dispose()
        {
            _failed = true;   //so a late Update cannot resurrect it

            _from?.Dispose();
            _to?.Dispose();
            if (_beds != null) foreach (SoundEffect bed in _beds) bed?.Dispose();
        }
    }
}

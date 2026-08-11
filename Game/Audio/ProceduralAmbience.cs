using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Prazsky.Core.Render;
using System;
using System.Threading.Tasks;

namespace BS3D.Audio
{
    /// <summary>
    /// The scenes' ambient beds (#46): one looping texture per backdrop — surf for the sea, a wind for each
    /// terrain, a traffic rumble for the cities, a near-subliminal drone for space, a shimmer for the dream,
    /// hollow dripping air for the cavern — synthesized from raw PCM at startup like every other sound in
    /// the game, and crossfaded when the scene changes.
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

        //One bed per SceneKind, counted where every other question about a SceneKind is answered rather than
        //as "the last member + 1" — that spelling had to be edited by hand for every new scene, and the one
        //time it is forgotten the last scene in the enum gets no bed at all.
        private static readonly int SCENES = SceneRenderer.SceneCount;

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
            //Every scene's bed bakes on one background task — they are a fraction of one music pass's
            //arithmetic, and nothing needs them until the first frame of the scene is already on screen.
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
        /// One bed per scene. Each is layered band-passed noise under envelopes written as whole cycles per loop,
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

            float[] mix = NewMix(loopSamples + tailSamples);

            //Seeds are per scene and per layer, because two layers fed the same sequence differ only in how
            //they were filtered, and summing two filtered copies of one sequence is correlated — the
            //celebration sounds learned this first.
            int seed = 100 + (int)scene * 17;

            switch (scene)
            {
                case SceneKind.Sea:
                    //The wash: big low swells, two waves per loop. The foam follows a quarter turn behind —
                    //the hiss of a wave arrives after its weight.
                    //
                    //The width follows the same shape as the sound: the swell's weight is a broad low body of
                    //air and keeps a middle, where the FOAM is the part that is genuinely all around you.
                    AddBand(mix, seed, 0f, 420f, 1.0f, t => Swell(t, 2, 0.35f, 0f), WIDTH_AIR);
                    AddBand(mix, seed + 1, 900f, 3200f, 0.5f, t => Square(Swell(t, 2, 0.15f, -MathF.PI / 2f)), WIDTH_AROUND);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.16f);

                case SceneKind.Savanna:
                    //A warm wind, wandering on two incommensurate-feeling cycles, and the campfire the scene
                    //visibly carries: a crackle of band noise gated by a fast random stutter, the firework
                    //crackle's trick at a hearth's scale.
                    //
                    //The fire is an OBJECT in the scene and the one thing here with a place, so its snaps are
                    //kept to a narrow spread — a hearth a few feet across, not a ring of fires around the
                    //player, which is what a wide scatter of snaps would say.
                    AddBand(mix, seed, 100f, 650f, 1.0f,
                        t => 0.55f + 0.25f * Cycle(t, 3, 0f) + 0.2f * Cycle(t, 5, 1.3f), WIDTH_AIR);
                    AddCrackle(mix, seed + 1, 1500f, 5000f, 0.3f, spread: 0.35f);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.13f);

                case SceneKind.Desert:
                    //A dry, steady wind, thinner than the savanna's, with the faintest whistle over it.
                    AddBand(mix, seed, 250f, 1200f, 1.0f, t => Swell(t, 2, 0.8f, 0f), WIDTH_AIR);
                    AddBand(mix, seed + 1, 1700f, 2100f, 0.18f, t => Square(Swell(t, 3, 0.3f, 2.1f)), WIDTH_AROUND);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.11f);

                case SceneKind.Mountain:
                    //Cold gusts — the swell squared, so the wind arrives in waves rather than breathing — with
                    //thin air hissing on the same gusts. The widest bed in the set: an exposed basin is the one
                    //place here with nothing at all between the player and the weather.
                    AddBand(mix, seed, 80f, 900f, 1.0f, t => Square(Swell(t, 3, 0.25f, 0f)), WIDTH_AROUND);
                    AddBand(mix, seed + 1, 2000f, 6000f, 0.22f, t => Swell(t, 3, 0.4f, 0f), WIDTH_AROUND);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.14f);

                case SceneKind.Meadow:
                    //A gentle breeze and a high insect shimmer under a light, fast tremolo. The insects are a
                    //chorus with no source — the one layer in the set that should have no middle at all.
                    AddBand(mix, seed, 120f, 500f, 0.9f, t => Swell(t, 2, 0.6f, 0f), WIDTH_AIR);
                    AddBand(mix, seed + 1, 3800f, 6500f, 0.16f,
                        t => Swell(t, 1, 0.5f, 0f) * (0.75f + 0.25f * Cycle(t, 128, 0f)), WIDTH_AROUND);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.10f);

                case SceneKind.City:
                    //Distant traffic: a deep rumble that never quite settles, and a mid murmur over it.
                    //
                    //The rumble is held NEAR the middle and the murmur opened out, which is the general rule
                    //for the low bands here: a very low sound gives the ear almost no direction anyway, so
                    //decorrelating it buys little image and costs the most on a mono downmix.
                    AddBand(mix, seed, 0f, 180f, 1.0f, t => Swell(t, 2, 0.7f, 0f), WIDTH_NEAR);
                    AddBand(mix, seed + 1, 150f, 450f, 0.35f, t => Swell(t, 3, 0.75f, 0.9f), WIDTH_AIR);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.12f);

                case SceneKind.NeonCity:
                    //The same city a few hours later: the rumble quieter, a steady electric hum in a narrow
                    //band (noise, not a tone — mains hum as texture), and a whisper of neon fizz on top.
                    //The hum stays close: it is a sign a few metres away, not weather.
                    AddBand(mix, seed, 0f, 160f, 1.0f, t => Swell(t, 2, 0.75f, 0f), WIDTH_NEAR);
                    AddBand(mix, seed + 1, 100f, 150f, 0.55f, _ => 1f, WIDTH_CLOSE);
                    AddBand(mix, seed + 2, 3000f, 8000f, 0.08f, t => Swell(t, 5, 0.5f, 0f), WIDTH_AROUND);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.12f);

                case SceneKind.Forest:
                    //Leaves answering gusts: the rustle band swells with the wind (raised to a power, so calm
                    //is genuinely calm) and flutters finely on top; a soft low wind under it. The rustle is the
                    //canopy, which is above and around the player on every side — wide as the mountain's gusts.
                    AddBand(mix, seed, 100f, 400f, 0.6f, t => Swell(t, 2, 0.6f, 0f), WIDTH_AIR);
                    AddBand(mix, seed + 1, 800f, 4200f, 1.0f,
                        t => (0.35f + 0.65f * MathF.Pow(Swell(t, 3, 0f, 0f), 1.5f)) * (0.8f + 0.2f * Cycle(t, 96, 0f)),
                        WIDTH_AROUND);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.12f);

                case SceneKind.Space:
                    //The void, one very deep and very slow breath per loop, mixed near-subliminal — the
                    //scene's whole point is silence with weight. It stays nearly a POINT, and that is the
                    //case that says width is not a quality dial: an enveloping void is a contradiction, and a
                    //bed the player suddenly notices is a bed that is too loud whatever its level says.
                    AddBand(mix, seed, 0f, 55f, 1.0f, t => Swell(t, 1, 0.6f, 0f), WIDTH_CLOSE);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.05f);

                case SceneKind.Dream:
                    //The dream: an ethereal shimmer. A deep slow drone and a high glassy hiss breathing in
                    //OPPOSITE phase — as one recedes the other rises, the scene's own contrast — with a mid
                    //band wandering between them. Filtered noise like everything here: the hallucination is
                    //in the motion, never in a tone.
                    //
                    //The width is put on the SAME contrast: the drone stays close and the shimmer is the
                    //widest thing in the bed, so as the pair breathes the image opens and closes with it. This
                    //is the scene that gains most from stereo, because its whole idea was already a pair.
                    AddBand(mix, seed, 0f, 70f, 1.0f, t => Swell(t, 1, 0.45f, 0f), WIDTH_NEAR);
                    AddBand(mix, seed + 1, 2600f, 7000f, 0.35f, t => Swell(t, 1, 0.3f, MathF.PI), WIDTH_AROUND);
                    AddBand(mix, seed + 2, 350f, 900f, 0.3f, t => Swell(t, 3, 0.4f, 1.1f), WIDTH_AIR);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.07f);

                case SceneKind.Cavern:
                    //The cavern: hollow underground air — a deep near-steady body of it — the river as a
                    //soft high trickle, and sparse water DRIPS: the crackle machinery slowed right down, a
                    //few soft taps a second with long tails, which is the sound that says "cave" before
                    //anything else does.
                    //The drips get the WIDEST scatter in the set, and they are the reason this scene wanted
                    //stereo at all: a drip is a point event with a place, so each one landing somewhere new is
                    //what turns a hollow noise into a room with a roof over it. The air itself stays near the
                    //middle — a cave encloses, and widening the body of it would read as being outdoors.
                    AddBand(mix, seed, 0f, 90f, 1.0f, t => Swell(t, 2, 0.75f, 0f), WIDTH_NEAR);
                    AddBand(mix, seed + 1, 900f, 2600f, 0.28f, t => Swell(t, 3, 0.55f, 0.8f), WIDTH_AIR);
                    AddCrackle(mix, seed + 2, 1200f, 4000f, 0.5f, ratePerSecond: 3f, threshold: 0.86f,
                        tailRate: 60f, spread: 0.95f);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.08f);

                case SceneKind.Moon:
                    //Vacuum. Even quieter than space's void and with none of its breath — a near-static
                    //sub-bass presence (the pressure of a helmet, not a wind; wind is the one thing this
                    //scene cannot have) under the faintest, slowest high hiss, more suggestion than sound.
                    //The quietest bed in the set, deliberately: the stillest scene in the game should be the
                    //one the ear notices least.
                    //Both layers stay close, for the reason the scene exists: there is no air out there to
                    //carry a sound around anybody. Space's rule, harder.
                    AddBand(mix, seed, 0f, 45f, 1.0f, t => 0.85f + 0.15f * Cycle(t, 1, 0f), WIDTH_CLOSE);
                    AddBand(mix, seed + 1, 5000f, 9000f, 0.06f, t => Swell(t, 1, 0.6f, MathF.PI / 2f), WIDTH_CLOSE);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.035f);

                case SceneKind.Outback:
                    //Hot, still air over red ground. The wind is thinner even than the Sahara's — there is
                    //nothing out here for it to move — and over it the shrill of cicadas, which is what heat
                    //actually sounds like: a narrow high band under a fast tremolo, the chorus swelling and
                    //dying twice a loop rather than droning flat.
                    //The cicadas are the meadow's insects again and want the same treatment: a chorus with no
                    //source, coming from every direction at once.
                    AddBand(mix, seed, 200f, 900f, 1.0f, t => Swell(t, 2, 0.72f, 0f), WIDTH_AIR);
                    AddBand(mix, seed + 1, 4200f, 5200f, 0.22f,
                        t => Swell(t, 2, 0.25f, 0.7f) * (0.6f + 0.4f * Cycle(t, 160, 0f)), WIDTH_AROUND);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.115f);

                default:
                    //A NEW SCENE LANDS HERE, AND IT IS MEANT TO BE OBVIOUS. This arm was the cavern's until
                    //#125 gave the Moon its own, and it was the Moon's until #112 gave the outback one — each
                    //time, a new scene silently inherited an atmosphere written for somewhere else (cave
                    //drips on the Moon, then vacuum over the outback), which is a fault nobody reports
                    //because it sounds like something. It is a near-silent neutral bed now: audibly missing
                    //rather than plausibly wrong, so the next scene's author hears the gap on the first run.
                    //Narrow as well as quiet: this arm is meant to be conspicuously featureless, and an
                    //enveloping placeholder would be one more thing that sounds deliberate.
                    AddBand(mix, seed, 60f, 900f, 1.0f, t => Swell(t, 2, 0.7f, 0f), WIDTH_CLOSE);
                    return Seal(mix, loopSamples, tailSamples, targetRms: 0.02f);
            }
        }

        #region The stereo image (#146)

        //The beds were baked mono: surf, wind and traffic all arriving from one point between the speakers.
        //They are interleaved stereo now — frame n is mix[2n] left, mix[2n + 1] right.
        //
        //THE MUSIC'S TECHNIQUE DOES NOT WORK HERE, and reaching for it is the trap this was written around.
        //#119 got the music's width by SEATING instruments: pan a voice and it moves. A bed is filtered noise,
        //and panning noise does not widen it — it moves the point it comes from. Two channels carrying the
        //same sequence at different levels are still one source, wherever the ear puts it.
        //
        //Width for noise is DECORRELATION: the two sides have to be genuinely different sequences. That is
        //nearly free here, because Noise(i, seed) is a pure function of the index and two seeds are already
        //uncorrelated by construction — the same property BakeScene already spends a seed per layer for.
        //
        //Each layer therefore draws a SHARED sequence and two sides of its own, and mixes them constant-power:
        //
        //    left  = shared*cos(theta) + uniqueLeft *sin(theta)      theta = width * pi/2
        //    right = shared*cos(theta) + uniqueRight*sin(theta)
        //
        //which gives an inter-channel correlation of exactly cos^2(theta) and, because the three sequences are
        //independent and unit-variance, leaves the power of each channel at 1 for every width. So width is a
        //dial that changes the IMAGE and not the level — which matters more here than anywhere else in the
        //game, the beds' authored level having been settled by ear once already (see AMBIENCE_VOLUME).
        //
        //Width 0 is the old mono bed exactly; width 1 is fully decorrelated. Summing a fully decorrelated bed
        //to mono costs 3 dB against a centred one and cancels nothing, which is the honest trade and is
        //measured rather than assumed.

        /// <summary>How far apart the two sides of a layer are drawn: 0 is the old mono bed, 1 fully decorrelated.</summary>
        private const float WIDTH_MONO = 0f;

        //A drone, a vacuum or a near-silent neutral bed is supposed to read as ENCLOSED. Wide is not
        //automatically better: a bed that suddenly surrounds the player is a bed being noticed, and this
        //file's own rule is that a bed which is noticed is too loud. Space and the Moon stay nearly a point.
        private const float WIDTH_CLOSE = 0.2f;

        /// <summary>An object in the scene rather than the air in it — a hearth, a rumble with a direction.</summary>
        private const float WIDTH_NEAR = 0.45f;

        /// <summary>The default for air: open, but still with a middle to it.</summary>
        private const float WIDTH_AIR = 0.75f;

        /// <summary>Weather that is genuinely all around — surf, gusts, a chorus of insects.</summary>
        private const float WIDTH_AROUND = 0.95f;

        /// <summary>How many stereo frames the mix holds — its length is two floats per frame.</summary>
        private static int Frames(float[] mix) => mix.Length / 2;

        /// <summary>A mix buffer for <paramref name="frames"/> stereo frames.</summary>
        private static float[] NewMix(int frames) => new float[frames * 2];

        /// <summary>
        /// The two gains a width becomes: how much of the shared sequence each side keeps, and how much of its
        /// own it adds. They square to 1 together, so a layer's level is the same at every width.
        /// </summary>
        private static void WidthGains(float width, out float shared, out float unique)
        {
            float theta = MathHelper.Clamp(width, 0f, 1f) * MathF.PI * 0.5f;

            shared = MathF.Cos(theta);
            unique = MathF.Sin(theta);
        }

        /// <summary>Adds a sample to each side of one frame. The one place the interleaving is stated.</summary>
        private static void Add(float[] mix, int frame, float left, float right)
        {
            int offset = frame * 2;

            mix[offset] += left;
            mix[offset + 1] += right;
        }

        #endregion

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
        /// <param name="width">
        /// How far apart the two sides are drawn (see the stereo region). The filtering runs <b>per side</b>
        /// and not on a summed source: the whole point is that the two channels are different sequences, and
        /// filtering one sequence and splitting it afterwards would put them back to being the same one.
        /// </param>
        private static void AddBand(float[] mix, int seed, float lowCut, float highCut, float gain,
            Func<float, float> envelope, float width = WIDTH_AIR)
        {
            float alphaHigh = Alpha(highCut);
            float alphaLow = Alpha(lowCut);

            float lpHighL = 0f, lpLowL = 0f, lpHighR = 0f, lpLowR = 0f;

            WidthGains(width, out float shared, out float unique);

            //Offsets, not seed + 1: the callers already spend consecutive seeds on their own layers, so a side
            //drawn at seed + 1 would be the very next layer's sequence and the two would be correlated for a
            //reason nobody would ever look for.
            int seedLeft = seed + 5101, seedRight = seed + 9173;

            int frames = Frames(mix);

            for (int i = 0; i < frames; i++)
            {
                float common = Noise(i, seed) * shared;

                float noiseL = common + Noise(i, seedLeft) * unique;
                float noiseR = common + Noise(i, seedRight) * unique;

                lpHighL += alphaHigh * (noiseL - lpHighL);
                lpLowL += alphaLow * (lpHighL - lpLowL);

                lpHighR += alphaHigh * (noiseR - lpHighR);
                lpLowR += alphaLow * (lpHighR - lpLowR);

                float level = gain * envelope((float)i / SAMPLE_RATE);

                Add(mix, i, (lpHighL - lpLowL) * level, (lpHighR - lpLowR) * level);
            }
        }

        /// <summary>
        /// Band noise gated by a random stutter — the firework crackle's trick. At the defaults it is the
        /// savanna's campfire: the gate re-rolled ~47 times a second, which is what turns a steady hiss into
        /// separate burning snaps. Slowed right down (a few rolls a second, a high threshold, a long tail)
        /// the same machinery is the cavern's water drips.
        /// </summary>
        /// <param name="spread">
        /// How far across the field successive snaps land. <b>This one really is panned</b>, unlike the washes
        /// (see the stereo region): a snap is a point event — one burning knot, one falling drop — so it HAS a
        /// place, and giving each its own is worth more than widening the air it happens in. Each snap holds
        /// its side for its whole tail, so a drip does not slide across the room while it rings.
        /// </param>
        private static void AddCrackle(float[] mix, int seed, float lowCut, float highCut, float gain,
            float ratePerSecond = 47f, float threshold = 0.62f, float tailRate = 220f, float spread = 0.7f)
        {
            float alphaHigh = Alpha(highCut);
            float alphaLow = Alpha(lowCut);
            float lpHigh = 0f, lpLow = 0f;
            float gate = 0f;

            int slotSamples = (int)(SAMPLE_RATE / ratePerSecond);

            //Where the snap that is currently ringing sits. Constant power, so a snap at the edge is as loud
            //as one in the middle - otherwise the fire would flicker in level as well as in place.
            float gainLeft = MathF.Sqrt(0.5f), gainRight = MathF.Sqrt(0.5f);

            int frames = Frames(mix);

            for (int i = 0; i < frames; i++)
            {
                float noise = Noise(i, seed);

                lpHigh += alphaHigh * (noise - lpHigh);
                lpLow += alphaLow * (lpHigh - lpLow);

                //A snap opens the gate at once; the gate then falls, so each snap has a tail rather than a
                //square edge.
                int slot = i / slotSamples;
                bool snap = Noise(slot, seed + 9) > threshold;

                if (snap)
                {
                    //Rolled from the slot's own index, so it is stable across bakes and lands differently for
                    //each snap. A third seed, clear of the gate's own seed + 9.
                    float pan = Noise(slot, seed + 4271) * spread;
                    float angle = (pan * 0.5f + 0.5f) * MathF.PI * 0.5f;

                    gainLeft = MathF.Cos(angle);
                    gainRight = MathF.Sin(angle);
                }

                gate = snap ? 1f : gate * (1f - tailRate / SAMPLE_RATE);

                float value = (lpHigh - lpLow) * gain * gate;

                Add(mix, i, value * gainLeft, value * gainRight);
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
        /// <remarks>
        /// <paramref name="loopSamples"/> and <paramref name="tailSamples"/> are counted in <b>frames</b>, so
        /// every index into the interleaved buffer is twice one and the ramp's weight is shared by a frame's
        /// two channels. Fold one channel and not the other and the seam opens <i>in one ear only</i>, which
        /// is markedly harder to notice than a seam in both — the same trap the menu piece's fold sets.
        /// <para>
        /// The level is set from the RMS of the whole interleaved buffer and applied as <b>one scale</b>. Both
        /// halves of that matter: measuring per channel and scaling each to the target would silently undo the
        /// width (it would force the two sides to the same level however differently they were drawn), and it
        /// is what keeps the authored <see cref="AMBIENCE_VOLUME"/> meaning what it meant when it was tuned.
        /// </para>
        /// </remarks>
        private static float[] Seal(float[] mix, int loopSamples, int tailSamples, float targetRms)
        {
            float[] loop = NewMix(loopSamples);
            Array.Copy(mix, loop, loopSamples * 2);

            for (int i = 0; i < tailSamples; i++)
            {
                float w = (float)i / tailSamples;
                float head = MathF.Sqrt(w), tail = MathF.Sqrt(1f - w);

                loop[i * 2] = loop[i * 2] * head + mix[(loopSamples + i) * 2] * tail;
                loop[i * 2 + 1] = loop[i * 2 + 1] * head + mix[(loopSamples + i) * 2 + 1] * tail;
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

            //Stereo since #146. The buffer is already interleaved left-then-right, which is the layout 16-bit
            //PCM wants, so the loop above needs no notion of channels — only this line does.
            //
            //Safe HERE and not in ProceduralAudio: a bed plays through a plain SoundEffectInstance, where the
            //effects are placed by Apply3D, which takes a MONO source. Stereo there would not widen them, it
            //would take their placement away — so this is not a change to copy across to that file.
            return new SoundEffect(pcm, SAMPLE_RATE, AudioChannels.Stereo);
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

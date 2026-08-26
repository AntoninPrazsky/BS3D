using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The parameters of the cloud layer, and the C# half of it.
    /// <para>
    /// The clouds are one field on a flat plane at a finite altitude, read by three consumers: the sky
    /// shader crosses it with the view ray and draws what it finds, the scene shader crosses it with the
    /// sun ray and darkens the key light, and this class crosses it with the sun ray to dim the whole
    /// light rig. They agree because they are the same field, not because they were tuned to match — see
    /// the header of <c>Shaders/Clouds.fxh</c>, which this file is the mirror of.
    /// </para>
    /// <para>
    /// The sun disc drawn in the sky shader since #220 is pushed from here too, for the same reason: the
    /// disc, the silver lining and the cloud shadow all answer one sun, and one push telling all of them is
    /// what keeps it so. The disc occludes behind the deck because it is added to the dome before the deck
    /// composites over it — see <c>Shaders/Sky.fx</c>.
    /// </para>
    /// <para>
    /// Only the coarse <see cref="Weather"/> layer is mirrored here. The fine octaves exist solely in the
    /// sky shader, and nothing the CPU or the shadow does looks at them — which is exactly why the usual
    /// problem with an effect like this, keeping a CPU noise and a GPU noise in step, does not arise. Two
    /// octaves of gradient noise built out of nothing but frac, dot and multiply reproduce exactly; a
    /// sine-based hash, which is the usual way to write one, would not.
    /// </para>
    /// <para>
    /// The layer's shape is the properties below and its look the constants beside them — one set of both,
    /// shared by every executable that draws weather, so a dial cannot be tuned in one and forgotten in the
    /// next. The one figure the clouds visibly borrow from elsewhere is the lit side's radiance: that is the
    /// light rig's own sun colour, so it is handed to <see cref="ApplyPalette"/> rather than kept here.
    /// </para>
    /// </summary>
    public sealed class CloudField
    {
        //THE SHAPE AND THE CHARACTER ARE ONE WEATHER SINCE #221. They were seven settable properties and a
        //row of shared constants before it, which is exactly why one weather was all there could be: a
        //scene could nudge the coverage and nothing at all could change what KIND of cloud it was. Both
        //halves live in WeatherLook now, a WeatherPreset names one, and this holds the one being drawn -
        //which during a change of sky is neither of them but a point between (see SetWeather).
        private WeatherLook _look = WeatherLooks.Of(WeatherPreset.Scattered);
        private WeatherLook _from = WeatherLooks.Of(WeatherPreset.Scattered);
        private WeatherPreset _preset = WeatherPreset.Scattered;
        private float _blend = 1f;

        //The dome's half of the shadowed underside's colour, taken in ApplyDome and multiplied by the
        //weather's half every frame. White until a dome has been handed over, so a caller pushing the field
        //before its first ApplyDome gets the weather's own radiance rather than black.
        private Vector3 _skyTint = Vector3.One;

        /// <summary>Which authored sky is up. Set through <see cref="SetWeather"/>, which fades to it.</summary>
        public WeatherPreset Preset => _preset;

        /// <summary>
        /// How long a change of sky takes. The ambience crossfades its beds rather than cutting them on the
        /// frame the scene changes, and weather follows the sound's rule and not the frame's for the same
        /// reason: a backdrop may be swapped in one frame because it is a different PLACE, where a sky
        /// closing over is the same place changing. Longer than the ambience's 1.5 s because a sky is bigger
        /// and slower than a sound — and because <see cref="SkyLightRig.StepOvercast"/> lerps the light
        /// rig's own answer over 2.5 s, so matching it is what makes the deck and the rig arrive together.
        /// </summary>
        private const float WEATHER_FADE_SECONDS = 2.5f;

        /// <summary>World Y the cloud plane sits at — the weather's, blended while one is changing.</summary>
        public float PlaneY => _look.PlaneY;

        /// <summary>Noise units per world unit; the reciprocal is roughly one weather feature across.</summary>
        public float Scale => _look.Scale;

        /// <summary>Wind, in world units per second.</summary>
        public Vector2 Wind => _look.Wind;

        /// <summary>Wall clock driving the wind, in seconds.</summary>
        public float Time { get; set; }

        /// <summary>
        /// Where the coverage threshold sits: negative opens the sky, positive closes it over. It is also
        /// the dial that decides whether the weather is *noticeable*, which is a different question from
        /// whether it looks right — a shadow only lands on the arena when cloud happens to sit over the
        /// one patch of sky its sun ray crosses, so a handsome sky with a fifth of it covered leaves the
        /// ground in unbroken sun for minutes at a time. That observation is what #221 was filed about, and
        /// what a scene naming its own weather now answers.
        /// </summary>
        public float CoverageBias => _look.CoverageBias;

        /// <summary>How sharply the field crosses that threshold.</summary>
        public float CoverageGain => _look.CoverageGain;

        /// <summary>Least sun that reaches through the thickest cloud, and how fast the shadow deepens.</summary>
        public float ShadowFloor => _look.ShadowFloor;

        public float ShadowGain => _look.ShadowGain;

        /// <summary>
        /// Puts a sky up. Called with the preset already up it does nothing at all — which is what lets a
        /// caller state its scene's weather on every scene change without having to remember whether it
        /// changed — and with a different one it fades from wherever the deck currently stands, so a change
        /// caught mid-fade carries on from where it is rather than snapping back to start again.
        /// </summary>
        public void SetWeather(WeatherPreset preset)
        {
            if (preset == _preset) return;

            _from = _look;
            _preset = preset;
            _blend = 0f;
        }

        /// <summary>
        /// Puts a sky up with no fade — what a caller building a scene from nothing wants, where a fade
        /// would be the first two seconds of every level spent arriving at its own weather.
        /// </summary>
        public void SetWeatherImmediately(WeatherPreset preset)
        {
            _preset = preset;
            _look = _from = WeatherLooks.Of(preset);
            _blend = 1f;
        }

        /// <summary>
        /// Advances a change of sky. Costs one compare on the frames nothing is changing, which is nearly
        /// all of them.
        /// </summary>
        /// <param name="elapsedSeconds">The frame's own wall-clock time.</param>
        public void Step(float elapsedSeconds)
        {
            if (_blend >= 1f) return;

            _blend = MathF.Min(1f, _blend + elapsedSeconds / WEATHER_FADE_SECONDS);

            //Smoothstep rather than the bare ramp: a sky that starts and stops closing over abruptly reads
            //as a dial being turned, which is the one thing a fade exists to hide.
            _look = WeatherLook.Lerp(_from, WeatherLooks.Of(_preset), _blend * _blend * (3f - 2f * _blend));
        }

        //WHAT IS LEFT HERE IS WHAT EVERY WEATHER SHARES. The five that told one kind of cloud from another —
        //the detail strength, the opacity, the two billow figures and the character swing — moved into
        //WeatherLook with #221, because a shredded storm deck and a smooth overcast differ in exactly those
        //and in nothing the coverage alone can say. These are the ones a storm and a fair-weather sky have
        //no reason to disagree about: how a cloud swallows light on the way through, how the sun scatters
        //forward through its edge, and where the deck fades into haze. They are still pushed once at load.
        //
        //(The figures the five carried are Scattered's own in WeatherLooks, to the last digit, so a scene
        //that says nothing about its sky draws exactly what it drew before there was anything to say.)

        private static readonly float HorizonFade = 0.16f;

        /// <summary>How far along the sun the shading looks to decide whether a piece of cloud is backlit.</summary>
        private static readonly float SunStep = 90f;

        /// <summary>
        /// How much light a piece of cloud swallows on the way through its own body, and how much the cloud
        /// between it and the sun swallows first. The body term is the one that matters: it is what turns a
        /// flat white field into undersides with dark cores and edges the light comes through.
        /// </summary>
        private static readonly float SelfAbsorption = 2.5f;

        private static readonly float SunAbsorption = 1f;

        /// <summary>The silver lining: forward scattering towards the sun, and how tightly it hugs it.</summary>
        private static readonly float SilverStrength = 1.2f;

        private static readonly float SilverPower = 12f;

        /// <summary>
        /// How much further the clouds' lit side is carried towards the dome's horizon colour than the rig
        /// already carried it. The rig's sun keeps most of its daylight radiance under every dome — the
        /// scene under it needs a working key light even at dusk — but a cloud is <b>in</b> the sky, and a
        /// deck lit with daylight grey over the neon city's dusk dome read as pasted on (issue #50). The
        /// second lerp both hues the lit side towards the dome's own evening and scales it down with a dark
        /// horizon; over a bright near-white day horizon it is close to a no-op.
        /// </summary>
        private static readonly float SunTintStrength = 0.5f;

        //The shadowed underside moved into WeatherLook with #221 and is the one value that carries most of a
        //storm's darkness: a fair-weather deck's undersides sit at 0.18-0.28 of linear radiance and a
        //storm's at a quarter of that, which the ACES curve leaves as real black rather than as grey. Its
        //own reasoning — that it has to sit WELL below the lit side rather than a shade under it, because
        //the curve compresses the highlights hard and two close values up there tonemap to the same white —
        //is what every preset's figure is chosen against.

        /// <summary>
        /// The shadowed side of a cloud sees no sun at all — only sky — so it takes the zenith colour far more
        /// completely than any surface the rig lights from two sides.
        /// </summary>
        private static readonly float ShadowTintStrength = 0.8f;

        /// <summary>
        /// The sun disc's angular radius, and half the width its edge fades over. Deliberately far larger
        /// than the true sun's quarter degree: the frame goes through the glare pass, whose sparse sampling
        /// grid flickers on a small bright thing (the Moon's Earth had to stay under the glare threshold
        /// for exactly this reason, and it is ~30 px across), while a disc hot and wide enough to be
        /// sampled coherently blooms steadily instead. Sized by looking, like every figure here.
        /// </summary>
        private static readonly float SunDiscAngularRadius = MathHelper.ToRadians(1.6f);

        private static readonly float SunDiscEdgeAngle = MathHelper.ToRadians(0.5f);

        /// <summary>
        /// How many times the rig's sun radiance the disc emits. The lit side of a cloud is the sun's
        /// light after the deck has swallowed most of it, so the source itself has to be well over it —
        /// and well over the glare threshold, or the sun is in the sky and nothing blooms it.
        /// </summary>
        private static readonly float SunDiscRadiance = 4f;

        /// <summary>
        /// The cloud parameters of one effect, resolved once. A missing parameter stays null and is skipped
        /// on every apply, preserving the contract that a shader declaring only part of the field costs
        /// nothing to support.
        /// </summary>
        private sealed class EffectSlots
        {
            public EffectParameter PlaneY, Scale, Time, CoverageBias, CoverageGain, ShadowFloor, ShadowGain, Wind;

            public EffectParameter SunColor, ShadowColor, DetailStrength, Opacity, HorizonFade, SunStep,
                SelfAbsorption, SunAbsorption, SilverStrength, SilverPower, SunDirection,
                FormStrength, ShapeStrength, CharacterStrength, SunDiscCos, SunDiscEdge, SunDiscColor;
        }

        //Callers apply the field to the same two or three effects every frame, and the by-name indexer is
        //a linear scan over the effect's parameter list (~70 entries on the instanced-model effect) — so
        //the whole cloud surface of an effect is resolved by one scan per name per effect, ever, and every
        //apply path below is direct SetValue calls.
        private readonly Dictionary<Effect, EffectSlots> _slotsByEffect = new();

        /// <summary>
        /// Hands the shared parameters to any effect that declares them — the sky shader and the scene
        /// shader both do. Setting them from one place is what stops the drawn cloud and the shadow it
        /// throws from being tuned apart from each other by accident. Parameters the effect does not
        /// declare are skipped, so a shader using only part of the field costs nothing to support.
        /// </summary>
        public void ApplyTo(Effect effect)
        {
            EffectSlots slots = SlotsOf(effect);

            slots.PlaneY?.SetValue(_look.PlaneY);
            slots.Scale?.SetValue(_look.Scale);
            slots.Time?.SetValue(Time);
            slots.CoverageBias?.SetValue(_look.CoverageBias);
            slots.CoverageGain?.SetValue(_look.CoverageGain);
            slots.ShadowFloor?.SetValue(_look.ShadowFloor);
            slots.ShadowGain?.SetValue(_look.ShadowGain);
            slots.Wind?.SetValue(_look.Wind);

            //THE CHARACTER GOES OUT HERE TOO SINCE #221, where it was pushed once at load. It has to: these
            //five are what tell a shredded storm deck from a smooth overcast, so they change when the
            //weather does — and while a sky is changing they change every frame. Five floats on top of the
            //eight above, on a path that already ran per frame per effect.
            slots.DetailStrength?.SetValue(_look.DetailStrength);
            slots.Opacity?.SetValue(_look.Opacity);
            slots.FormStrength?.SetValue(_look.FormStrength);
            slots.ShapeStrength?.SetValue(_look.ShapeStrength);
            slots.CharacterStrength?.SetValue(_look.CharacterStrength);

            //The shadowed underside is the weather's AND the dome's — a cloud's underside is the colour of
            //the sky it sees — so it is the one value needing both, and it is pushed here because the
            //weather's half moves per frame while the dome's moves once a scene. <see cref="ApplyDome"/>
            //remembers its half for exactly this.
            slots.ShadowColor?.SetValue(_look.ShadowColor * _skyTint);
        }

        /// <summary>
        /// Everything about the clouds that does not change frame to frame, pushed once when the shaders are
        /// loaded. The per-frame half is <see cref="ApplyTo"/>; everything that follows the dome — the two
        /// cloud colours, the disc's colour and the sun's own direction — is <see cref="ApplyDome"/>'s job.
        /// <para>
        /// The direction was pushed from here until #220, when a dome stopped being a pair of colours and
        /// started stating where its light lives; the disc's <b>shape</b> stayed, being one angular size for
        /// every sky.
        /// </para>
        /// </summary>
        /// <param name="skyEffect">The sky shader, which draws the cloud and so wants the whole look.</param>
        public void ApplyStaticParameters(Effect skyEffect)
        {
            EffectSlots slots = SlotsOf(skyEffect);

            slots.HorizonFade?.SetValue(HorizonFade);
            slots.SunStep?.SetValue(SunStep);
            slots.SelfAbsorption?.SetValue(SelfAbsorption);
            slots.SunAbsorption?.SetValue(SunAbsorption);
            slots.SilverStrength?.SetValue(SilverStrength);
            slots.SilverPower?.SetValue(SilverPower);
            slots.SunDiscCos?.SetValue(MathF.Cos(SunDiscAngularRadius));
            slots.SunDiscEdge?.SetValue(MathF.Sin(SunDiscAngularRadius) * SunDiscEdgeAngle);
        }

        /// <summary>
        /// Hands both shaders everything about the sky that follows the dome: the clouds' lit side and
        /// shadowed underside, the sun disc's colour, and the direction all three of them — plus the shadow
        /// the scene shader throws — answer. Re-run on every dome and every scene switch, and never per frame.
        /// <para>
        /// The direction is here rather than in <see cref="ApplyStaticParameters"/> because since #220 it is
        /// the dome's (<see cref="SkyDome.SunDirection"/>) and no longer one constant. Pushing it in the same
        /// call as the colours is what keeps the drawn disc, the silver lining and the shadow on the ground
        /// answering one sun: there is no path here that moves one of them without the others.
        /// </para>
        /// <para>
        /// Every dome was getting the same cold white cloud, and over eighteen skies running from turquoise
        /// day to blood-red dusk to near-black night it read as a grey smear pasted over the sky rather than
        /// as weather in it. A cloud has no colour of its own: its lit side is the colour of the sun and its
        /// underside the colour of the sky.
        /// </para>
        /// <para>
        /// The dome may never change for a given caller, but this is not therefore optional: neither colour
        /// has a shader-side default, and left unset the whole deck comes out black.
        /// </para>
        /// </summary>
        /// <param name="skyEffect">The sky shader, which draws the cloud and the disc.</param>
        /// <param name="instancedEffect">
        /// The scene shader, which only needs the direction it shadows along — or null for a caller that draws
        /// a sky and nothing under it. As on every other apply path, an effect declaring just part of the
        /// cloud surface costs nothing to support.
        /// </param>
        /// <param name="sunDirection">
        /// Towards the sun, as the light rig resolved it for this dome and scene. Taken as a direction rather
        /// than from the rig's <c>KeyLightPosition</c>, which is a point forty units off the middle of the
        /// arena: near enough that its direction fans right across the scene, while a cloud shadow has to
        /// arrive in parallel bands over a city hundreds of units wide. The rig's key light is what stands in
        /// for the sun, so the clouds are lit by whatever lights everything under them and the scene is
        /// shadowed along the very same direction — one sun, told to both shaders from here.
        /// </param>
        /// <param name="sunRadiance">
        /// The lit side's radiance as the rig hands it out: the sun's own radiance already carried towards
        /// the dome's horizon colour by the light rig's tint strength — the rig's figures, which stay with
        /// the rig — and bit-for-bit the sun colour the scene shaders are handed for the same frame. The
        /// clouds carry it <b>further</b> towards the dome than the scene does (<see cref="SunTintStrength"/>,
        /// applied here against <paramref name="horizonLinear"/>): the scene under a dusk dome still needs a
        /// working key light, but a cloud is in the sky itself, and a deck keeping daylight radiance over a
        /// dark dome is exactly what issue #50 filed. Over a bright day horizon the extra lerp is near a
        /// no-op, so the day domes keep their look.
        /// </param>
        /// <param name="zenithLinear">
        /// The dome's zenith colour, decoded to linear. The underside sees only sky, so this is what tints it,
        /// harder than the rig tints anything (<see cref="ShadowTintStrength"/>).
        /// </param>
        /// <param name="horizonLinear">
        /// The dome's horizon colour, decoded to linear — what the lit side is carried further towards, hue
        /// and brightness both, so a dark dome dims its clouds instead of only recolouring them.
        /// </param>
        public void ApplyDome(Effect skyEffect, Effect instancedEffect, Vector3 sunDirection,
            Vector3 sunRadiance, Vector3 zenithLinear, Vector3 horizonLinear)
        {
            EffectSlots slots = SlotsOf(skyEffect);

            slots.SunDirection?.SetValue(sunDirection);
            if (instancedEffect != null) SlotsOf(instancedEffect).SunDirection?.SetValue(sunDirection);

            //The dome's half of the underside's colour, REMEMBERED rather than pushed: since #221 the other
            //half is the weather's, and that half moves per frame while a sky is changing, so the two are
            //multiplied together in ApplyTo. Held as the tint rather than as the finished colour, which
            //keeps this the one place the dome is read.
            _skyTint = Vector3.Lerp(Vector3.One, zenithLinear, ShadowTintStrength);

            Vector3 domeTint = Vector3.Lerp(Vector3.One, horizonLinear, SunTintStrength);

            slots.SunColor?.SetValue(sunRadiance * domeTint);

            //The sun disc takes the sun's radiance straight, without the second lerp towards the horizon the
            //clouds' lit side gets: a cloud is a lit surface and the sun is the source, and carrying the disc
            //towards the dome's evening is how the sun of a dusk dome would end up the colour of the dusk —
            //the rig's own first carry (already inside sunRadiance) is as tinted as the source itself gets.
            slots.SunDiscColor?.SetValue(sunRadiance * SunDiscRadiance);
        }

        /// <summary>
        /// Tells an effect there is no weather at all: a coverage gain of zero, which is the value
        /// <c>CloudSunlight</c> in <c>Clouds.fxh</c> reads as "full sun, no shadow" and returns a flat 1 for.
        /// <para>
        /// A scene with no sky to hang cloud in still shades its balls, island and gun through
        /// <c>InstancedModel.fx</c>, which calls <c>CloudSunlight</c> unconditionally — so without this the
        /// space scene would be crossed by the shadows of a cloud deck it does not draw. Zeroing the gain
        /// rather than skipping <see cref="ApplyTo"/> is what makes it safe on the frame a scene is switched:
        /// the parameters keep their last value on the effect between frames, so a gain left standing from
        /// the scene before would go on shadowing the new one.
        /// </para>
        /// </summary>
        public void SuppressOn(Effect effect) => SlotsOf(effect).CoverageGain?.SetValue(0f);

        /// <summary>This effect's cloud parameters, resolved on first use and cached (see <see cref="_slotsByEffect"/>).</summary>
        private EffectSlots SlotsOf(Effect effect)
        {
            if (_slotsByEffect.TryGetValue(effect, out EffectSlots slots)) return slots;

            slots = new EffectSlots
            {
                PlaneY = effect.Parameters["CloudPlaneY"],
                Scale = effect.Parameters["CloudScale"],
                Time = effect.Parameters["CloudTime"],
                CoverageBias = effect.Parameters["CloudCoverageBias"],
                CoverageGain = effect.Parameters["CloudCoverageGain"],
                ShadowFloor = effect.Parameters["CloudShadowFloor"],
                ShadowGain = effect.Parameters["CloudShadowGain"],
                Wind = effect.Parameters["CloudWind"],

                SunColor = effect.Parameters["CloudSunColor"],
                ShadowColor = effect.Parameters["CloudShadowColor"],
                DetailStrength = effect.Parameters["CloudDetailStrength"],
                Opacity = effect.Parameters["CloudOpacity"],
                HorizonFade = effect.Parameters["CloudHorizonFade"],
                SunStep = effect.Parameters["CloudSunStep"],
                SelfAbsorption = effect.Parameters["CloudSelfAbsorption"],
                SunAbsorption = effect.Parameters["CloudSunAbsorption"],
                SilverStrength = effect.Parameters["CloudSilverStrength"],
                SilverPower = effect.Parameters["CloudSilverPower"],
                FormStrength = effect.Parameters["CloudFormStrength"],
                ShapeStrength = effect.Parameters["CloudShapeStrength"],
                CharacterStrength = effect.Parameters["CloudCharacterStrength"],

                //The one name without the Cloud prefix: the sun is shared with the light rig and the scene
                //shading, and the clouds only borrow it
                SunDirection = effect.Parameters["SunDirection"],
                SunDiscCos = effect.Parameters["SunDiscCos"],
                SunDiscEdge = effect.Parameters["SunDiscEdge"],
                SunDiscColor = effect.Parameters["SunDiscColor"]
            };
            _slotsByEffect.Add(effect, slots);

            return slots;
        }

        /// <summary>
        /// The coarse layer, mirroring <c>CloudWeather</c>. Keep the two in step: this is the one piece of
        /// the field that exists twice.
        /// </summary>
        public float Weather(Vector2 world)
        {
            Vector2 drift = Wind * Time;

            float w = 0.62f * Noise((world + drift) * Scale);
            w += 0.38f * (Noise((world + drift * 1.6f) * (Scale * 2.7f) + new Vector2(31.4f)));

            return w;
        }

        /// <summary>0 = clear sky, 1 = solid cloud, mirroring <c>CloudCover</c>.</summary>
        public float Cover(Vector2 world) => MathHelper.Clamp((Weather(world) + CoverageBias) * CoverageGain, 0f, 1f);

        /// <summary>
        /// How much cloud stands over a patch of the world rather than over a point in it — five taps out
        /// to <paramref name="radius"/>, averaged.
        /// <para>
        /// This is the number to drive ambient light by, and a single tap is the wrong one. "How much sky
        /// is over me" is an average across the hemisphere by definition, and one sample of it lurches
        /// between nothing and solid every time a cloud edge crosses the point, which no amount of
        /// smoothing afterwards turns back into the quantity that was wanted.
        /// </para>
        /// </summary>
        public float CoverAround(Vector2 world, float radius)
        {
            float sum = Cover(world);

            sum += Cover(world + new Vector2(radius, 0f));
            sum += Cover(world + new Vector2(-radius, 0f));
            sum += Cover(world + new Vector2(0f, radius));
            sum += Cover(world + new Vector2(0f, -radius));

            return sum * 0.2f;
        }

        /// <summary>
        /// How much of the sun reaches a point in the world, mirroring <c>CloudSunlight</c>. This is what
        /// the light rig is dimmed by, and the scene shader computes the very same number per pixel.
        /// </summary>
        public float SunlightAt(Vector3 worldPosition, Vector3 sunDirection)
        {
            float climb = MathF.Max(sunDirection.Y, 0.05f);
            float distanceToPlane = MathF.Max((PlaneY - worldPosition.Y) / climb, 0f);

            Vector2 hit = new(
                worldPosition.X + sunDirection.X * distanceToPlane,
                worldPosition.Z + sunDirection.Z * distanceToPlane);

            return MathHelper.Lerp(1f, ShadowFloor, MathHelper.Clamp(Cover(hit) * ShadowGain, 0f, 1f));
        }

        //Scalar rather than vectorised, and componentwise rather than clever, because every line of it has
        //to correspond to a line of HLSL that can be read next to it.
        private static float Frac(float value) => value - MathF.Floor(value);

        private static Vector2 Hash22(float px, float py)
        {
            float x = Frac(px * 0.1031f);
            float y = Frac(py * 0.1030f);
            float z = Frac(px * 0.0973f);

            float d = x * (y + 33.33f) + y * (z + 33.33f) + z * (x + 33.33f);

            x += d;
            y += d;
            z += d;

            return new Vector2(Frac((x + y) * z) * 2f - 1f, Frac((x + z) * y) * 2f - 1f);
        }

        private static float Noise(Vector2 p)
        {
            float cellX = MathF.Floor(p.X);
            float cellY = MathF.Floor(p.Y);

            float fx = p.X - cellX;
            float fy = p.Y - cellY;

            //Quintic, matching the shader: the sky is shaded off this field's slope, so its second
            //derivative has to be continuous as well
            float ux = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float uy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);

            float a = Dot(Hash22(cellX, cellY), fx, fy);
            float b = Dot(Hash22(cellX + 1f, cellY), fx - 1f, fy);
            float c = Dot(Hash22(cellX, cellY + 1f), fx, fy - 1f);
            float d = Dot(Hash22(cellX + 1f, cellY + 1f), fx - 1f, fy - 1f);

            return MathHelper.Lerp(MathHelper.Lerp(a, b, ux), MathHelper.Lerp(c, d, ux), uy);
        }

        private static float Dot(Vector2 gradient, float x, float y) => gradient.X * x + gradient.Y * y;
    }
}

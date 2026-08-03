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
        /// <summary>World Y the cloud plane sits at.</summary>
        public float PlaneY { get; set; } = 190f;

        /// <summary>Noise units per world unit; the reciprocal is roughly one weather feature across.</summary>
        public float Scale { get; set; } = 1f / 450f;

        /// <summary>Wind, in world units per second.</summary>
        public Vector2 Wind { get; set; } = new(9f, 4f);

        /// <summary>Wall clock driving the wind, in seconds.</summary>
        public float Time { get; set; }

        /// <summary>
        /// Where the coverage threshold sits: negative opens the sky, positive closes it over. It is also
        /// the dial that decides whether the weather is *noticeable*, which is a different question from
        /// whether it looks right — a shadow only lands on the arena when cloud happens to sit over the
        /// one patch of sky its sun ray crosses, so a handsome sky with a fifth of it covered leaves the
        /// ground in unbroken sun for minutes at a time.
        /// </summary>
        public float CoverageBias { get; set; } = 0.02f;

        /// <summary>How sharply the field crosses that threshold.</summary>
        public float CoverageGain { get; set; } = 2.8f;

        /// <summary>Least sun that reaches through the thickest cloud, and how fast the shadow deepens.</summary>
        public float ShadowFloor { get; set; } = 0.38f;

        public float ShadowGain { get; set; } = 1.3f;

        //The look, as opposed to the shape above: tuned once and not a dial anything varies at runtime, which
        //is why these are constants rather than properties. They are pushed by ApplyStaticParameters and
        //ApplyPalette; the shape is pushed per frame by ApplyTo.

        /// <summary>
        /// How hard the fine octaves chew at the shape the weather layer drew. Has to be read against
        /// <see cref="CoverageGain"/>, which is what the weather is multiplied by: at 0.55 against a gain of
        /// 2.8 the detail was modulating the thickness by about six percent and the clouds came out
        /// airbrushed. It wants to be a decent fraction of the weather's own amplitude.
        /// </summary>
        private static readonly float DetailStrength = 2.5f;

        /// <summary>
        /// Opacity of the densest cloud, and the elevation over which cloud fades into haze. Well over 1, so
        /// a cloud reaches solid at about half density and only its edges stay translucent — at 1.15 the whole
        /// layer was semi-transparent everywhere and read as haze rather than as weather.
        /// </summary>
        private static readonly float Opacity = 2.4f;

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
        /// The shadowed underside, in **linear radiance** — a quantity of light, not an sRGB paint colour, so
        /// nothing decodes it.
        /// <para>
        /// Well below the lit side's radiance rather than a shade under it. The frame goes through an ACES
        /// curve that compresses the highlights hard, so two linear values close together up there come out of
        /// the tonemapper as the same white — at 0.45 the undersides were indistinguishable from the tops and
        /// the whole layer read as flat paper.
        /// </para>
        /// </summary>
        private static readonly Vector3 ShadowColor = new(0.18f, 0.21f, 0.28f);

        /// <summary>
        /// The shadowed side of a cloud sees no sun at all — only sky — so it takes the zenith colour far more
        /// completely than any surface the rig lights from two sides.
        /// </summary>
        private static readonly float ShadowTintStrength = 0.8f;

        /// <summary>
        /// The cloud parameters of one effect, resolved once. A missing parameter stays null and is skipped
        /// on every apply, preserving the contract that a shader declaring only part of the field costs
        /// nothing to support.
        /// </summary>
        private sealed class EffectSlots
        {
            public EffectParameter PlaneY, Scale, Time, CoverageBias, CoverageGain, ShadowFloor, ShadowGain, Wind;

            public EffectParameter SunColor, ShadowColor, DetailStrength, Opacity, HorizonFade, SunStep,
                SelfAbsorption, SunAbsorption, SilverStrength, SilverPower, SunDirection;
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

            slots.PlaneY?.SetValue(PlaneY);
            slots.Scale?.SetValue(Scale);
            slots.Time?.SetValue(Time);
            slots.CoverageBias?.SetValue(CoverageBias);
            slots.CoverageGain?.SetValue(CoverageGain);
            slots.ShadowFloor?.SetValue(ShadowFloor);
            slots.ShadowGain?.SetValue(ShadowGain);
            slots.Wind?.SetValue(Wind);
        }

        /// <summary>
        /// Everything about the clouds that does not change frame to frame, pushed once when the shaders are
        /// loaded. The per-frame half is <see cref="ApplyTo"/>; the two dome-derived colours are not set here
        /// because they follow the dome, which is <see cref="ApplyPalette"/>'s job.
        /// </summary>
        /// <param name="skyEffect">The sky shader, which draws the cloud and so wants the whole look.</param>
        /// <param name="instancedEffect">
        /// The scene shader, which only needs the direction it shadows along — or null for a caller that draws
        /// a sky and nothing under it. As on every other apply path, an effect declaring just part of the
        /// cloud surface costs nothing to support.
        /// </param>
        /// <param name="sunDirection">
        /// Towards the sun. Taken as a direction rather than from the rig's <c>KeyLightPosition</c>, which is
        /// a point forty units off the middle of the arena: near enough that its direction fans right across
        /// the scene, while a cloud shadow has to arrive in parallel bands over a city hundreds of units wide.
        /// The rig's key light is what stands in for the sun, so the clouds are lit by whatever lights
        /// everything under them and the scene is shadowed along the very same direction — one sun, told to
        /// both shaders from here.
        /// </param>
        public void ApplyStaticParameters(Effect skyEffect, Effect instancedEffect, Vector3 sunDirection)
        {
            EffectSlots slots = SlotsOf(skyEffect);

            slots.DetailStrength?.SetValue(DetailStrength);
            slots.Opacity?.SetValue(Opacity);
            slots.HorizonFade?.SetValue(HorizonFade);
            slots.SunStep?.SetValue(SunStep);
            slots.SelfAbsorption?.SetValue(SelfAbsorption);
            slots.SunAbsorption?.SetValue(SunAbsorption);
            slots.SilverStrength?.SetValue(SilverStrength);
            slots.SilverPower?.SetValue(SilverPower);
            slots.SunDirection?.SetValue(sunDirection);

            if (instancedEffect != null) SlotsOf(instancedEffect).SunDirection?.SetValue(sunDirection);
        }

        /// <summary>
        /// Colours the clouds with the dome they hang in — the lit side and the shadowed underside. Re-run on
        /// every dome and every scene switch, and never per frame.
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
        /// <param name="sunRadiance">
        /// The lit side's radiance, taken exactly as it comes: it is the sun's own radiance already carried
        /// towards the dome's horizon colour by the light rig's tint strength — the rig's figures, which stay
        /// with the rig — and it is bit-for-bit the sun colour the scene shaders are handed for the same
        /// frame. That identity is the point: the clouds are lit by literally the same light as everything
        /// under them. There is deliberately no tint or scale applied here to be "restored" later.
        /// </param>
        /// <param name="zenithLinear">
        /// The dome's zenith colour, decoded to linear. The underside sees only sky, so this is what tints it,
        /// harder than the rig tints anything (<see cref="ShadowTintStrength"/>).
        /// </param>
        public void ApplyPalette(Effect skyEffect, Vector3 sunRadiance, Vector3 zenithLinear)
        {
            EffectSlots slots = SlotsOf(skyEffect);

            Vector3 skyTint = Vector3.Lerp(Vector3.One, zenithLinear, ShadowTintStrength);

            slots.SunColor?.SetValue(sunRadiance);
            slots.ShadowColor?.SetValue(ShadowColor * skyTint);
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

                //The one name without the Cloud prefix: the sun is shared with the light rig and the scene
                //shading, and the clouds only borrow it
                SunDirection = effect.Parameters["SunDirection"]
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

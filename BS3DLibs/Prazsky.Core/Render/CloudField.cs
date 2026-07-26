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

        /// <summary>
        /// The cloud parameters of one effect, resolved once. A missing parameter stays null and is skipped
        /// on every apply, preserving the contract that a shader declaring only part of the field costs
        /// nothing to support.
        /// </summary>
        private sealed class EffectSlots
        {
            public EffectParameter PlaneY, Scale, Time, CoverageBias, CoverageGain, ShadowFloor, ShadowGain, Wind;
        }

        //Callers apply the field to the same two or three effects every frame, and the by-name indexer is
        //a linear scan over the effect's parameter list (~70 entries on the instanced-model effect) — so
        //the names are resolved once per effect and the per-frame path is direct SetValue calls.
        private readonly Dictionary<Effect, EffectSlots> _slotsByEffect = new();

        /// <summary>
        /// Hands the shared parameters to any effect that declares them — the sky shader and the scene
        /// shader both do. Setting them from one place is what stops the drawn cloud and the shadow it
        /// throws from being tuned apart from each other by accident. Parameters the effect does not
        /// declare are skipped, so a shader using only part of the field costs nothing to support.
        /// </summary>
        public void ApplyTo(Effect effect)
        {
            if (!_slotsByEffect.TryGetValue(effect, out EffectSlots slots))
            {
                slots = new EffectSlots
                {
                    PlaneY = effect.Parameters["CloudPlaneY"],
                    Scale = effect.Parameters["CloudScale"],
                    Time = effect.Parameters["CloudTime"],
                    CoverageBias = effect.Parameters["CloudCoverageBias"],
                    CoverageGain = effect.Parameters["CloudCoverageGain"],
                    ShadowFloor = effect.Parameters["CloudShadowFloor"],
                    ShadowGain = effect.Parameters["CloudShadowGain"],
                    Wind = effect.Parameters["CloudWind"]
                };
                _slotsByEffect.Add(effect, slots);
            }

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

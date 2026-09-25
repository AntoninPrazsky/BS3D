using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The Moon's and Mars's ground, as far as a camera needs to know it</b> (#559). Neither scene has a CPU
    /// mirror of its height field — nothing is planted on either, and the crater lattice, the boulders and the
    /// mesas are hashes all the way down — so a chapter intro that flies low over them had nothing to keep its
    /// lens out of the ground with. This is that, and deliberately no more than that:
    /// <list type="bullet">
    /// <item>a <b>ceiling</b> (<see cref="MoonCeiling"/>, <see cref="MarsCeiling"/>) — a height the ground at a
    /// point can never stand above, exact where the field is cheap to mirror (the mare undulation, the highland
    /// belt, the curvature, the mesas) and a bound where it is not (every crater lip at its highest, every
    /// boulder at its tallest). A lens held a clearance over the ceiling is over the ground, whatever the hash
    /// rolled there;</item>
    /// <item>the <b>craters of the Moon's top octave</b> (<see cref="MoonCraters"/>), the one crater a camera can
    /// be pointed at: the same cell, rolls and jitter <c>Moon.fx</c>'s <c>CraterLayer</c> draws them with.</item>
    /// </list>
    /// <para>
    /// <b>Keep each in step with its shader in the same change.</b> The noise and the hash are
    /// <see cref="ShaderMath"/>'s — the same line-for-line copy the savanna's and the tropical beach's plantings
    /// stand on — and every constant below names the HLSL line it mirrors. A drift here does not show in a
    /// picture of the scene; it shows as a chapter intro with its lens in a mesa.
    /// </para>
    /// <para>
    /// Called once when an intro begins, never per frame; a few thousand evaluations at most.
    /// </para>
    /// </summary>
    public static class OffworldGround
    {
        //Both CraterFields (Moon.fx, Mars.fx): each layer is `bowl + rim * depth * 0.62` with the bowl never
        //positive and rim and depth at most 1, and the layers are weighted 0.58 + 0.29 + 0.13 = 1 — so no sum of
        //them stands higher than 0.62 of the amplitude. The bound a ceiling needs, and not a guess.
        private const float CRATER_FIELD_MAX = 0.62f;

        //Moon.fx CraterField's top octave: turned 13 degrees, a 129-unit period, seed 11.3, chance 0.86, weighted
        //0.58; CraterLayer's radius range and its rolls' own offsets.
        private static readonly Vector2 MOON_CRATER_TURN = new(0.97437f, 0.22495f);
        private const float MOON_CRATER_PERIOD = 129f;
        private const float MOON_CRATER_SEED = 11.3f;
        private const float MOON_CRATER_CHANCE = 0.86f;
        private const float MOON_CRATER_WEIGHT = 0.58f;
        private const float CRATER_MIN_RADIUS = 0.12f;
        private const float CRATER_MAX_RADIUS = 0.21f;

        /// <summary>One crater of the Moon's largest octave, in world units.</summary>
        public readonly struct Crater
        {
            /// <summary>The bowl's centre on the plain's level (y = the terrain's <c>LevelY</c>).</summary>
            public readonly Vector3 Centre;

            /// <summary>The rim's radius — the lip stands just outside it.</summary>
            public readonly float Radius;

            /// <summary>How deep the bowl is at its centre, with the clearing's ramp applied.</summary>
            public readonly float Depth;

            public Crater(Vector3 centre, float radius, float depth)
            {
                Centre = centre;
                Radius = radius;
                Depth = depth;
            }
        }

        /// <summary>
        /// The highest the Moon's ground can stand at a world point: <c>Moon.fx</c>'s <c>MoonHeight</c> with the
        /// crater field at its bound and everything else exact — the mare, the highland belt shaped by it, and
        /// the curvature that closes the horizon.
        /// </summary>
        public static float MoonCeiling(float x, float z, MoonTerrainConfig terrain)
        {
            float dist = MathF.Sqrt(x * x + z * z);
            float ramp = ShaderMath.SmoothStep(terrain.ClearingRadius, terrain.ClearingRadius + terrain.ClearingTransition, dist);
            float mare = MareBase(x, z);

            //HighlandBelt: the rise times the shape, the shape being the mare's own swing stretched over 0..1
            //and lifted onto the saddle floor.
            float shape = MathHelper.Clamp(mare * 0.75f + 0.5f, 0f, 1f);
            float belt = terrain.HighlandHeight
                * ShaderMath.SmoothStep(terrain.HighlandInnerRadius, terrain.HighlandCrestRadius, dist)
                * MathHelper.Lerp(terrain.HighlandSaddleFloor, 1f, shape);

            return terrain.LevelY + terrain.CraterAmplitude * ramp * (CRATER_FIELD_MAX + mare * 0.18f) + belt
                - terrain.Curvature * dist * dist;
        }

        /// <summary>
        /// The highest Mars's ground can stand at a world point: <c>Mars.fx</c>'s <c>MarsHeight</c> with the
        /// crater field, the boulders and the pebbles at their bounds, plus its <c>MesaField</c> exactly — the
        /// mesas are the one thing on the plain tall enough to put a lens inside, and they are not all out on
        /// their ring: the noise that raises them can clear the threshold well inside <c>MesaInnerRadius</c>.
        /// </summary>
        public static float MarsCeiling(float x, float z, MarsTerrainConfig terrain)
        {
            float dist = MathF.Sqrt(x * x + z * z);
            float ramp = ShaderMath.SmoothStep(terrain.ClearingRadius, terrain.ClearingRadius + terrain.ClearingTransition, dist);

            return terrain.LevelY + terrain.CraterAmplitude * ramp * (CRATER_FIELD_MAX + MareBase(x, z) * 0.18f)
                + terrain.RockHeight + terrain.PebbleHeight + MarsMesa(x, z, terrain);
        }

        /// <summary>
        /// <c>Mars.fx</c>'s <c>MesaField</c>: how tall the mesa standing at a world point is, 0 on open plain. A
        /// thresholded low-frequency noise, its ring ramp fed into the threshold, a third of the profile pulled
        /// to quarter steps for the ledges.
        /// </summary>
        public static float MarsMesa(float x, float z, MarsTerrainConfig terrain)
        {
            float ring = ShaderMath.SmoothStep(terrain.MesaInnerRadius, terrain.MesaInnerRadius + 140f, MathF.Sqrt(x * x + z * z));

            float shapeNoise = ShaderMath.Noise(new Vector2(x, z) * 0.0042f) * 0.7f
                + ShaderMath.Noise(new Vector2(x, z) * 0.0115f + new Vector2(3.7f)) * 0.3f;
            float rise = shapeNoise + (ring - 1f) * 0.5f;

            float profile = ShaderMath.SmoothStep(terrain.MesaThreshold - 0.10f, terrain.MesaThreshold + 0.03f, rise);
            float ledges = MathF.Floor(profile * 4f + 0.5f) / 4f;

            return terrain.MesaHeight * MathHelper.Lerp(profile, ledges, 0.3f);
        }

        /// <summary>
        /// Every crater of the Moon's top octave whose centre lies between <paramref name="minRadius"/> and
        /// <paramref name="maxRadius"/> of the arena, handed to <paramref name="visit"/>: <c>CraterLayer</c>'s
        /// own cell walk — the turned domain, the existence roll, the squared radius roll, the jitter held clear
        /// of the cell's edge by the crater's reach — with the cell turned back into the world.
        /// </summary>
        public static void MoonCraters(MoonTerrainConfig terrain, float minRadius, float maxRadius, Action<Crater> visit)
        {
            //The turned domain's cells that can hold a centre within maxRadius: the annulus's bounding square,
            //turned, is still inside a square of the same half-diagonal.
            int reach = (int)MathF.Ceiling(maxRadius * MathF.Sqrt(2f) / MOON_CRATER_PERIOD) + 1;

            for (int cy = -reach; cy <= reach; cy++)
            {
                for (int cx = -reach; cx <= reach; cx++)
                {
                    Vector2 rollA = Roll(cx + MOON_CRATER_SEED, cy + MOON_CRATER_SEED);
                    if (rollA.X > MOON_CRATER_CHANCE) continue;

                    Vector2 rollB = Roll(cx + MOON_CRATER_SEED + 47.9f, cy + MOON_CRATER_SEED + 47.9f);
                    Vector2 rollC = Roll(cx + MOON_CRATER_SEED + 91.7f, cy + MOON_CRATER_SEED + 91.7f);

                    float radius = MathHelper.Lerp(CRATER_MIN_RADIUS, CRATER_MAX_RADIUS, rollB.X * rollB.X);
                    float margin = radius * 1.6f;
                    Vector2 cell = new Vector2(cx, cy) + new Vector2(margin) + rollC * (1f - 2f * margin);

                    //Out of the turned cell and back into the world: TurnCrater's rotation undone (its transpose).
                    Vector2 turned = cell * MOON_CRATER_PERIOD;
                    Vector2 t = MOON_CRATER_TURN;
                    Vector2 world = new(turned.X * t.X + turned.Y * t.Y, -turned.X * t.Y + turned.Y * t.X);

                    float distance = world.Length();
                    if (distance < minRadius || distance > maxRadius) continue;

                    float ramp = ShaderMath.SmoothStep(terrain.ClearingRadius, terrain.ClearingRadius + terrain.ClearingTransition, distance);
                    float depth = MathHelper.Lerp(0.25f, 1f, rollB.Y * rollB.Y);

                    visit(new Crater(new Vector3(world.X, terrain.LevelY, world.Y), radius * MOON_CRATER_PERIOD,
                        terrain.CraterAmplitude * ramp * MOON_CRATER_WEIGHT * depth));
                }
            }
        }

        /// <summary>
        /// Phobos's direction, placed as <c>Mars.fx</c> places it (<see cref="SceneRenderer"/>'s own
        /// elevation/azimuth convention, the one the shader's <c>PhobosDirection</c> is pushed with).
        /// </summary>
        public static Vector3 PhobosDirection(MarsMoonsConfig moons) =>
            SceneRenderer.DirectionFromElevationAzimuth(moons.PhobosElevation, moons.PhobosAzimuth);

        //MareBase, in both shaders: two octaves of gradient noise.
        private static float MareBase(float x, float z)
        {
            Vector2 p = new(x, z);

            return ShaderMath.Noise(p * 0.011f) * 0.65f + ShaderMath.Noise(p * 0.031f + new Vector2(7.3f)) * 0.35f;
        }

        //NoiseHash22 * 0.5 + 0.5, the rolls' own remap.
        private static Vector2 Roll(float x, float y) => ShaderMath.Hash22(x, y) * 0.5f + new Vector2(0.5f);
    }
}

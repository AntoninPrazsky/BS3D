using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The CPU mirrors of three terrain shaders' height fields</b> — <c>Desert.fx</c>'s dunes,
    /// <c>Mountain.fx</c>'s range and <c>Outback.fx</c>'s plain with its monoliths — written for the Game's
    /// chapter-intro shots (#559), which have to know where the ground is to keep a lens off it and to find
    /// the thing a shot is about (a dune crest, a summit, a monolith) at all. The volcano's
    /// <see cref="SceneRenderer.VolcanoGroundHeight"/>, the forest's and the tropical beach's mirrors are the
    /// same idea, and these follow them: copied line for line from the shader beside them, so a line here
    /// can be read against a line there, and built on <see cref="ShaderMath"/>'s one copy of the noise.
    /// <para>
    /// <b>A mirror is exact where it matters and no further.</b> Only the height is mirrored — the gradients,
    /// the ribs' streak bearings, the material masks and everything else the pixel shaders read are not —
    /// and the mountain's two <c>sin</c> terms of basin relief (1.5 units in the clearing) may differ from the
    /// GPU's by a rounding. Callers stand a lens several units off the answer, never on it.
    /// </para>
    /// <para>
    /// None of these fields is seeded by <c>sceneseed=</c>: the dunes, the range and the monoliths are the
    /// same every launch, so a shot built on them is the same land whatever the roll.
    /// </para>
    /// </summary>
    public static class TerrainMirror
    {
        #region The desert (Desert.fx: DuneField, DesertHeight)

        private const float DUNE_SPACING = 64f;
        private const float DUNE_WINDWARD = 0.75f;
        private const float DUNE_MEAN = 0.30f;

        /// <summary>World Y of the sand at a world XZ, as <c>Desert.fx</c>'s <c>DesertHeight</c> displaces it.</summary>
        public static float Desert(float x, float z, DesertSceneConfig config)
        {
            Vector2 p = new(x, z);
            float dist = MathF.Max(p.Length(), 1e-3f);
            float t = MathHelper.Clamp((dist - config.ClearingRadius) / config.ClearingTransition, 0f, 1f);
            float ramp = t * t * (3f - 2f * t);

            return config.LevelY + config.DuneAmplitude * ramp * DuneField(p, config.Wind.ToVector2());
        }

        /// <summary>
        /// The desert's wind in the XZ plane, normalised exactly as <c>Desert.fx</c> does it — the dunes' crests
        /// run across it, their long windward slopes face into it and the slip faces fall away downwind.
        /// </summary>
        public static Vector2 DesertWind(DesertSceneConfig config) => Vector2.Normalize(config.Wind.ToVector2() + new Vector2(1e-4f, 0f));

        private static float DuneField(Vector2 p, Vector2 windSetting)
        {
            Vector2 wind = Vector2.Normalize(windSetting + new Vector2(1e-4f, 0f));
            Vector2 across = new(-wind.Y, wind.X);

            float along = Vector2.Dot(p, wind);
            float side = Vector2.Dot(p, across);

            float n1 = ShaderMath.Noise(new Vector2(side * 0.012f, along * 0.004f) + new Vector2(3.7f));
            float n2 = ShaderMath.Noise(p * 0.021f + new Vector2(11f));
            float warped = along + n1 * 70f + n2 * 12f;

            float n3 = ShaderMath.Noise(new Vector2(side * 0.009f, along * 0.006f) + new Vector2(5.3f));
            float strength = ShaderMath.SmoothStep(0.2f, 0.75f, 0.6f + 1.2f * n3);
            float major = DuneProfile(warped / DUNE_SPACING) * strength;

            Vector2 wind2 = new(wind.X * 0.819f - wind.Y * 0.574f, wind.X * 0.574f + wind.Y * 0.819f);
            float n4 = ShaderMath.Noise(p * 0.017f + new Vector2(29f));
            float n5 = ShaderMath.Noise(p * 0.008f + new Vector2(41f));
            float minorStrength = ShaderMath.SmoothStep(0.2f, 0.75f, 0.45f + 1.2f * n5);
            float minor = DuneProfile((Vector2.Dot(p, wind2) + n4 * 20f) / (DUNE_SPACING * 0.46f)) * 0.5f * minorStrength;

            float swell = 0.22f * MathF.Sin(p.X * 0.017f + p.Y * 0.011f) + 0.12f * MathF.Sin(p.X * -0.009f + p.Y * 0.021f + 1.7f);

            return MathF.Max(major, minor) + swell - DUNE_MEAN;
        }

        private static float DuneProfile(float cycles)
        {
            float t = cycles - MathF.Floor(cycles);
            float rise = t / DUNE_WINDWARD;
            float fall = (1f - t) / (1f - DUNE_WINDWARD);
            float h = MathHelper.Clamp(MathF.Min(rise, fall), 0f, 1f);

            return h * MathF.Sqrt(h);
        }

        #endregion

        #region The mountains (Mountain.fx: TerrainHeight, MountainField)

        private const float MOUNTAIN_RIDGE_SPACING = 130f;
        private const int MOUNTAIN_FIELD_OCTAVES = 5;
        private const float MOUNTAIN_FIELD_FLOOR = 0.24f;
        private const float MOUNTAIN_FIELD_SPAN = 0.72f;
        private const float MASSIF_SPACING = 300f;
        private const float MASSIF_LOW = 0.35f;
        private const float MASSIF_HIGH = 1.6f;

        /// <summary>World Y of the range at a world XZ, as <c>Mountain.fx</c>'s <c>TerrainHeight</c> displaces it.</summary>
        public static float Mountain(float x, float z, MountainSceneConfig config)
        {
            Vector2 p = new(x, z);
            float ramp = ShaderMath.SmoothStep(config.ClearingRadius, config.ClearingRadius + config.ClearingTransition, p.Length());

            float basin = config.ClearingRelief * (MathF.Sin(p.X * 0.06f + p.Y * 0.04f) + 0.6f * MathF.Sin(p.X * -0.05f + p.Y * 0.08f + 2f));
            float massif = MathHelper.Lerp(MASSIF_LOW, MASSIF_HIGH,
                MathHelper.Clamp(0.5f + 0.8f * ShaderMath.Noise(p / MASSIF_SPACING + new Vector2(7.3f)), 0f, 1f));

            float ridged = RidgedFbm2(p / MOUNTAIN_RIDGE_SPACING, MOUNTAIN_FIELD_OCTAVES);
            float shaped = MathHelper.Clamp((ridged - MOUNTAIN_FIELD_FLOOR) / MOUNTAIN_FIELD_SPAN, 0f, 1f);

            return config.LevelY + basin + config.Height * ramp * massif * shaped * shaped * shaped;
        }

        //Noise.fxh's RidgedFbm2, with NOISE_ROTATE2 = float2x2(0.80, 0.60, -0.60, 0.80) applied as mul(M, p).
        private static float RidgedFbm2(Vector2 p, int octaves)
        {
            float value = 0f;
            float amplitude = 0.5f;
            float weight = 1f;

            for (int i = 0; i < octaves; i++)
            {
                float ridge = 1f - MathF.Abs(ShaderMath.Noise(p));
                ridge *= ridge * weight;
                weight = MathHelper.Clamp(ridge * 2f, 0f, 1f);

                value += ridge * amplitude;
                p = new Vector2(0.80f * p.X + 0.60f * p.Y, -0.60f * p.X + 0.80f * p.Y) * 2.02f;
                amplitude *= 0.5f;
            }

            return value;
        }

        #endregion

        #region The outback (Outback.fx: OutbackHeight, RockLayer, Bornhardt)

        private const float TALUS_REACH = 1.3f;

        //The two lattices' fixed figures, as OutbackHeight passes them to RockLayer.
        private const float ROCK_SEED = 13.7f, OUTCROP_SEED = 71.3f;
        private static readonly Vector2 OUTCROP_AXIS = new(0.8253f, 0.5647f);

        /// <summary>
        /// One monolith of the outback: where it stands, how far its apron reaches, and how high its crown
        /// can rise — an upper bound (the plain's swell added at its most), never below the rock.
        /// </summary>
        public readonly struct Formation
        {
            public readonly Vector2 Centre;
            public readonly float Reach;
            public readonly float CrownY;

            public Formation(Vector2 centre, float reach, float crownY)
            {
                Centre = centre;
                Reach = reach;
                CrownY = crownY;
            }
        }

        /// <summary>World Y of the outback's ground at a world XZ, as <c>Outback.fx</c>'s <c>OutbackHeight</c> displaces it.</summary>
        public static float Outback(float x, float z, OutbackSceneConfig config)
        {
            OutbackTerrainConfig terrain = config.Terrain;
            Vector2 p = new(x, z);

            float swell = ShaderMath.Noise(p * 0.0062f) * 0.70f + ShaderMath.Noise(p * 0.0185f + new Vector2(5.1f)) * 0.30f;

            float rock = RockLayer(p, MathF.Max(terrain.RockSpacing, 1f), ROCK_SEED, terrain.RockChance, terrain.RockHeight,
                0.10f, 0.16f, 1.8f, config.Surface.RibDepth, 0.30f, 2.2f, 5.0f, terrain, config.Surface.RibCount);

            Vector2 rotated = new(Vector2.Dot(p, OUTCROP_AXIS), Vector2.Dot(p, new Vector2(-OUTCROP_AXIS.Y, OUTCROP_AXIS.X)));
            float outcrop = RockLayer(rotated, MathF.Max(terrain.OutcropSpacing, 1f), OUTCROP_SEED, terrain.OutcropChance, terrain.OutcropHeight,
                0.045f, 0.085f, 1.35f, 0f, 0.475f, 1.5f, 5.5f, terrain, config.Surface.RibCount);

            return terrain.LevelY + terrain.PlainRelief * swell + rock + outcrop;
        }

        /// <summary>
        /// Every monolith (the big lattice, not the boulders) whose centre stands within
        /// <paramref name="maxRadius"/> of the arena — the same cells, rolls and clearing ramp
        /// <c>RockLayer</c> decides them by, so a formation listed here is one the shader draws.
        /// </summary>
        public static List<Formation> OutbackMonoliths(OutbackSceneConfig config, float maxRadius)
        {
            OutbackTerrainConfig terrain = config.Terrain;
            float cellSize = MathF.Max(terrain.RockSpacing, 1f);
            int cells = (int)MathF.Ceiling(maxRadius / cellSize) + 1;
            var found = new List<Formation>();

            for (int cz = -cells; cz <= cells; cz++)
            {
                for (int cx = -cells; cx <= cells; cx++)
                {
                    if (!Roll(cx, cz, cellSize, ROCK_SEED, terrain.RockChance, 0.10f, 0.16f, 1.8f, config.Surface.RibDepth, terrain,
                        out Vector2 centreWorld, out float radius, out float elongation, out float ramp, out Vector2 rollB))
                        continue;

                    if (centreWorld.Length() > maxRadius) continue;

                    float reach = radius * TALUS_REACH * (1f + config.Surface.RibDepth) * elongation * cellSize;
                    float crown = terrain.LevelY + terrain.PlainRelief + terrain.RockHeight * MathHelper.Lerp(0.62f, 1f, rollB.Y) * ramp
                        + terrain.OutcropHeight;
                    found.Add(new Formation(centreWorld, reach, crown));
                }
            }

            return found;
        }

        //RockLayer's placement rolls for one cell, shared by the height and the listing: false where the cell
        //carries no formation or the clearing has it.
        private static bool Roll(float cellX, float cellZ, float cellSize, float seed, float chance,
            float minRadius, float maxRadius, float maxElongation, float ribDepth, OutbackTerrainConfig terrain,
            out Vector2 centreWorld, out float radius, out float elongation, out float ramp, out Vector2 rollB)
        {
            centreWorld = Vector2.Zero;
            radius = elongation = ramp = 0f;
            rollB = Vector2.Zero;

            Vector2 rollA = Roll01(cellX + seed, cellZ + seed);
            if (rollA.X > chance) return false;

            rollB = Roll01(cellX + seed + 23.7f, cellZ + seed + 23.7f);
            Vector2 rollC = Roll01(cellX + seed + 57.1f, cellZ + seed + 57.1f);
            Vector2 rollD = Roll01(cellX + seed + 91.3f, cellZ + seed + 91.3f);

            radius = MathHelper.Lerp(minRadius, maxRadius, rollB.X);
            elongation = MathHelper.Lerp(1f, maxElongation, rollD.Y);

            float margin = MathF.Min(radius * TALUS_REACH * (1f + ribDepth) * elongation, 0.45f);
            Vector2 centre = new Vector2(margin) + rollC * (1f - 2f * margin);

            centreWorld = (new Vector2(cellX, cellZ) + centre) * cellSize;
            ramp = ShaderMath.SmoothStep(terrain.ClearingRadius, terrain.ClearingRadius + terrain.ClearingTransition, centreWorld.Length());

            return ramp > 0f;
        }

        private static float RockLayer(Vector2 p, float cellSize, float seed, float chance, float height,
            float minRadius, float maxRadius, float maxElongation, float ribDepth,
            float wall, float minSquareness, float maxSquareness, OutbackTerrainConfig terrain, float ribCount)
        {
            Vector2 q = p / cellSize;
            float cellX = MathF.Floor(q.X), cellZ = MathF.Floor(q.Y);
            Vector2 f = q - new Vector2(cellX, cellZ);

            if (!Roll(cellX, cellZ, cellSize, seed, chance, minRadius, maxRadius, maxElongation, ribDepth, terrain,
                out Vector2 centreWorld, out float radius, out float elongation, out float ramp, out Vector2 rollB))
                return 0f;

            Vector2 rollA = Roll01(cellX + seed, cellZ + seed);
            Vector2 rollD = Roll01(cellX + seed + 91.3f, cellZ + seed + 91.3f);
            Vector2 centre = centreWorld / cellSize - new Vector2(cellX, cellZ);

            Vector2 axis = RollDirection(rollD);
            Vector2 offset = f - centre;
            Vector2 local = new(Vector2.Dot(offset, axis), Vector2.Dot(offset, new Vector2(-axis.Y, axis.X)));
            local.X /= elongation;

            float d1 = local.Length() / radius;

            Vector2 radial = local / MathF.Sqrt(MathF.Max(Vector2.Dot(local, local), 1e-6f));
            float rib = ShaderMath.Noise(radial * ribCount + new Vector2(cellX, cellZ) * 13.1f + new Vector2(seed));
            float ribbed = d1 * (1f + rib * ribDepth * ShaderMath.SmoothStep(0.20f, 0.62f, d1));

            Vector2 rollE = Roll01(cellX + seed + 131.9f, cellZ + seed + 131.9f);
            float lobeScale = MathHelper.Lerp(0.40f, 0.60f, rollE.Y);
            Vector2 lobeCentre = RollDirection(rollE) * radius * 0.5f;
            float d2 = (local - lobeCentre).Length() / (radius * lobeScale);

            float squareness = MathHelper.Lerp(minSquareness, maxSquareness, rollA.Y);
            float mainBody = Bornhardt(ribbed, squareness, wall);
            float lobeBody = Bornhardt(d2, squareness, MathF.Min(wall / lobeScale, 0.9f)) * MathHelper.Lerp(0.55f, 0.90f, rollE.X);
            float body = MathF.Max(mainBody, lobeBody);

            float apron = ShaderMath.SmoothStep(TALUS_REACH, 1f, MathF.Min(d1, d2));

            return (body * 0.9f + apron * 0.1f) * height * MathHelper.Lerp(0.62f, 1f, rollB.Y) * ramp;
        }

        private static float Bornhardt(float t, float squareness, float wall)
        {
            float clamped = MathHelper.Clamp(t, 0f, 1f);

            return MathF.Sqrt(MathHelper.Clamp(1f - MathF.Pow(MathF.Max(clamped, 1e-4f), squareness), 0f, 1f))
                * ShaderMath.SmoothStep(1f, 1f - wall, clamped);
        }

        //NoiseHash22(v) * 0.5 + 0.5: a roll in 0..1 on each axis.
        private static Vector2 Roll01(float x, float y) => ShaderMath.Hash22(x, y) * 0.5f + new Vector2(0.5f);

        private static Vector2 RollDirection(Vector2 roll)
        {
            Vector2 v = roll * 2f - Vector2.One;

            return v / MathF.Sqrt(MathF.Max(Vector2.Dot(v, v), 1e-4f));
        }

        #endregion
    }
}

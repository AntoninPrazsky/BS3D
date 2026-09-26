using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>Every CPU mirror of a terrain shader's height field, in one place</b> (#590) — <c>Desert.fx</c>'s
    /// dunes, <c>Mountain.fx</c>'s range, <c>Outback.fx</c>'s plain with its monoliths, <c>Polar.fx</c>'s
    /// icesheet, <c>Savanna.fx</c>'s grassland, <c>Tropical.fx</c>'s beach, <c>Meadow.fx</c>'s hills,
    /// <c>Forest.fx</c>'s floor (which <c>Aurora.fx</c> draws too, on its own terrain config) and
    /// <c>Volcano.fx</c>'s cone. They are what the scatters plant on, what the scene lights stand on, and
    /// since #559 what the chapter-intro shots keep a lens off. Copied line for line from the shader beside
    /// them, so a line here can be read against a line there, and built on <see cref="ShaderMath"/>'s one
    /// copy of the noise, the hash, <c>smoothstep</c> and <c>frac</c>. Static and config-taking: the renderer
    /// wraps the ones a host asks about on its live config (<see cref="SceneRenderer.SavannaGroundHeight"/>,
    /// <see cref="SceneRenderer.VolcanoGroundHeight"/>, <see cref="SceneRenderer.PolarGroundHeight"/>).
    /// <para>
    /// <b>The GPU checks them now.</b> The five that stood in <see cref="SceneRenderer"/> until #590 said
    /// "a drift here plants trees underground or floating, and there is nothing to catch it but the eye".
    /// The Testbed's <c>mirrorcheck</c> reads each shader's own height function back off the GPU at a grid of
    /// points and compares it with the mirror here (<see cref="SceneRenderer.TryGetTerrainProbe"/>; the
    /// figures are in <c>docs/scenes.md</c>, "The terrain mirrors") — run it after touching either side.
    /// </para>
    /// <para>
    /// <b>A mirror is exact where it matters and no further.</b> Only the height is mirrored — the gradients,
    /// the ribs' streak bearings, the material masks and everything else the pixel shaders read are not — and
    /// the volcano leaves its scoria out (1.35 units off the drawn ground at most, measured). Callers stand a
    /// lens several units off the answer, never on it.
    /// </para>
    /// <para>
    /// <b>The four built on <see cref="ShaderMath.Noise"/> — desert, mountain, outback, polar — did not match their
    /// shaders until #598</b>, measured by <c>mirrorcheck</c> (#590): up to 16.2 units off in the mountains. The
    /// noise's hash takes the fraction of a product near 20 000, where a float carries a fraction to about 1/500,
    /// and the shader compiler fuses the hash's dot into multiply-adds and folds the outback's <c>seed + 23.7</c>
    /// constants. <see cref="ShaderMath.Hash22"/> now fuses the same way and the outback's rolls add their
    /// constants first, and all four pass under a thousandth (<c>docs/scenes.md</c>, "The terrain mirrors").
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

            rollB = Roll01(cellX + (seed + 23.7f), cellZ + (seed + 23.7f));
            Vector2 rollC = Roll01(cellX + (seed + 57.1f), cellZ + (seed + 57.1f));
            Vector2 rollD = Roll01(cellX + (seed + 91.3f), cellZ + (seed + 91.3f));

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
            Vector2 rollD = Roll01(cellX + (seed + 91.3f), cellZ + (seed + 91.3f));
            Vector2 centre = centreWorld / cellSize - new Vector2(cellX, cellZ);

            Vector2 axis = RollDirection(rollD);
            Vector2 offset = f - centre;
            Vector2 local = new(Vector2.Dot(offset, axis), Vector2.Dot(offset, new Vector2(-axis.Y, axis.X)));
            local.X /= elongation;

            float d1 = local.Length() / radius;

            Vector2 radial = local / MathF.Sqrt(MathF.Max(Vector2.Dot(local, local), 1e-6f));
            float rib = ShaderMath.Noise(radial * ribCount + new Vector2(cellX, cellZ) * 13.1f + new Vector2(seed));
            float ribbed = d1 * (1f + rib * ribDepth * ShaderMath.SmoothStep(0.20f, 0.62f, d1));

            Vector2 rollE = Roll01(cellX + (seed + 131.9f), cellZ + (seed + 131.9f));
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

        #region The icesheet (Polar.fx: PressureRidge, CrevasseField, PolarHeight)

        /// <summary>
        /// World Y of the ice at a world XZ, as <c>Polar.fx</c>'s <c>PolarHeight</c> displaces it — the swells,
        /// the pressure front's plates and the crevasses cut into them (#559). The sastrugi are a normal and
        /// not a height (see the scene's header), so this is the whole of it.
        /// </summary>
        public static float Polar(float x, float z, PolarSceneConfig config)
        {
            Vector2 p = new(x, z);
            float ramp = ShaderMath.SmoothStep(config.ClearingRadius, config.ClearingRadius + config.ClearingTransition, p.Length());
            float swells = ShaderMath.Noise(p * 0.008f) * 0.7f + ShaderMath.Noise(p * 0.021f + new Vector2(7.3f)) * 0.3f;
            float ridge = PolarRidge(p, config);

            return config.LevelY + config.SwellAmplitude * ramp * swells + config.RidgeHeight * ridge
                - config.CrevasseDepth * PolarCrevasse(p, ridge, config);
        }

        /// <summary>How deep into a crevasse slot a point stands, 0–1: <c>Polar.fx</c>'s <c>CrevasseField</c>.</summary>
        public static float PolarCrevasse(float x, float z, PolarSceneConfig config)
        {
            Vector2 p = new(x, z);
            return PolarCrevasse(p, PolarRidge(p, config), config);
        }

        /// <summary>How much of the pressure front stands at a point, 0–1: <c>Polar.fx</c>'s <c>PressureRidge</c>.</summary>
        public static float PolarRidge(float x, float z, PolarSceneConfig config) => PolarRidge(new Vector2(x, z), config);

        private static float PolarRidge(Vector2 p, PolarSceneConfig config)
        {
            float dist = p.Length();
            float bearing = MathF.Atan2(p.Y, p.X);

            float wander = ShaderMath.Noise(new Vector2(MathF.Cos(bearing), MathF.Sin(bearing)) * 3.1f) * config.RidgeWidth * 1.4f;

            //WrapAngle: the shortest signed way round, so a front centred near atan2's seam is not cut in half.
            float offset = bearing - MathHelper.ToRadians(config.RidgeBearingDegrees);
            float fromFront = MathF.Abs(offset - MathHelper.TwoPi * MathF.Floor((offset + MathHelper.Pi) / MathHelper.TwoPi));
            float span = MathHelper.ToRadians(config.RidgeSpanDegrees);
            float sector = 1f - ShaderMath.SmoothStep(span * 0.5f, span * 0.5f + 0.5f, fromFront);
            if (sector <= 0f) return 0f;

            float band = MathF.Abs(dist - (config.RidgeRadius + wander)) / MathF.Max(config.RidgeWidth, 1e-3f);
            float crest = MathHelper.Clamp(1f - band, 0f, 1f);

            float cellX = MathF.Floor(p.X / config.SlabSize), cellY = MathF.Floor(p.Y / config.SlabSize);
            Vector2 within = p / config.SlabSize - new Vector2(cellX, cellY) - new Vector2(0.5f);

            Vector2 hash = ShaderMath.Hash22(cellX, cellY);
            float hashZ = ShaderMath.Hash22(cellX + 37f, cellY + 37f).X;

            float plate = 0.35f + 0.65f * hash.X;
            float tilt = Vector2.Dot(within, (new Vector2(hash.Y, hashZ) - new Vector2(0.5f)) * 2f) * config.SlabTilt;

            return MathHelper.Clamp(crest * crest * (3f - 2f * crest), 0f, 1f) * MathHelper.Clamp(plate + tilt, 0f, 1f) * sector;
        }

        private static float PolarCrevasse(Vector2 p, float ridge, PolarSceneConfig config)
        {
            float along = Vector2.Dot(p, config.Wind.ToVector2());

            float lines = MathF.Sin(along * config.CrevasseFrequency + ShaderMath.Noise(p * 0.010f) * 3.4f);
            float slot = MathHelper.Clamp(1f - MathF.Abs(lines) * config.CrevasseSharpness, 0f, 1f);
            float field = ShaderMath.SmoothStep(0.05f, 0.55f, ShaderMath.Noise(p * 0.005f + new Vector2(19f)) + 0.34f);
            float clear = ShaderMath.SmoothStep(config.ClearingRadius * 0.8f, config.ClearingRadius * 1.5f, p.Length());

            return slot * MathHelper.Clamp(field + ridge * 1.2f, 0f, 1f) * clear;
        }

        #endregion

        #region The savanna (Savanna.fx: TerrainHeight)

        /// <summary>
        /// The savanna terrain height at a world point, mirroring <c>Savanna.fx</c>'s <c>TerrainHeight</c>, so the
        /// acacia trees can be planted on the ground the shader draws.
        /// </summary>
        public static float Savanna(float x, float z, SavannaSceneConfig config)
        {
            float dist = MathF.Sqrt(x * x + z * z);
            //The clearing's ramp spelled out as the clamp-then-hermite it is, rather than through
            //ShaderMath.SmoothStep: that divides by (R + T) - R, which is not T to the last bit, and this mirror
            //was moved here (#590) with its results held bit for bit.
            float t = MathHelper.Clamp((dist - config.ClearingRadius) / config.ClearingTransition, 0f, 1f);
            float ramp = t * t * (3f - 2f * t); //smoothstep, as in the shader

            float rolling = 0.5f * MathF.Sin(x * 0.016f + z * 0.012f)
                + 0.3f * MathF.Sin(x * -0.011f + z * 0.020f + 1.5f)
                + 0.2f * MathF.Sin(x * 0.026f + z * 0.021f + 3.0f);

            float gentle = config.ClearingRelief * (MathF.Sin(x * 0.04f + z * 0.03f) + 0.6f * MathF.Sin(x * -0.055f + z * 0.048f + 2.1f));

            return config.LevelY + gentle + config.HillHeight * ramp * (rolling * 0.5f + 0.5f);
        }

        #endregion

        #region The tropical beach (Tropical.fx: TropicalHeight, CoastRadius, ShoreRingRadius, ChannelMask)

        /// <summary>
        /// The tropical terrain height at a world point, mirroring <c>Tropical.fx</c>'s
        /// <c>TropicalHeight</c> term for term (and its <c>CoastRadius</c>/<c>ShoreRingRadius</c>/
        /// <c>ChannelMask</c> beside it), so the palms and the waterline's rocks can be planted on the
        /// ground the shader draws. Keep this and the shader in the same change: a drift here plants palms in
        /// the surf — which the Testbed's <c>mirrorcheck</c> now catches (#590).
        /// </summary>
        public static float Tropical(float x, float z, TropicalSceneConfig config)
        {
            TropicalTerrainConfig terrain = config.Terrain;

            float r = MathF.Sqrt(x * x + z * z);
            float b = MathF.Atan2(z, x);

            float gentle = terrain.ClearingRelief * 0.5f
                * (MathF.Sin(x * 0.043f + z * 0.031f) + 0.6f * MathF.Sin(-x * 0.052f + z * 0.046f + 2.1f));

            float d = r - TropicalCoastRadius(b, terrain);

            //GLSL smoothstep(edge0, edge1, x) is clamp-then-hermite, which MathHelper.SmoothStep is not
            //(the forest's comment records the trap) — ShaderMath's, which spells it as the shader does.
            float toWaterline = ShaderMath.SmoothStep(-terrain.BeachRise, 0f, d);
            float toBed = ShaderMath.SmoothStep(0f, terrain.BeachRun, d);

            float h = MathHelper.Lerp(terrain.LevelY + gentle, config.Water.LevelY, toWaterline);
            h = MathHelper.Lerp(h, terrain.SeabedY, toBed);

            float ring = ShaderMath.SmoothStep(0f, terrain.RingWidth, r - TropicalRingRadius(b, terrain))
                * (1f - TropicalChannelMask(b, terrain));

            float qx = x + 26f * MathF.Sin(z * 0.011f + 2f);
            float qz = z + 26f * MathF.Sin(x * 0.013f + 5f);

            float rolling = 0.40f * MathF.Sin(qx * 0.020f + qz * 0.015f)
                + 0.27f * MathF.Sin(-qx * 0.013f + qz * 0.024f + 1.5f)
                + 0.19f * MathF.Sin(qx * 0.031f + qz * 0.026f + 3.0f)
                + 0.14f * MathF.Sin(-qx * 0.056f + qz * 0.041f + 0.7f);

            h += ring * terrain.HillHeight * (0.55f + 0.45f * (0.5f + 0.5f * rolling));

            return h;
        }

        //The waterline's radius at a bearing — Tropical.fx's CoastRadius, in one change with it.
        private static float TropicalCoastRadius(float b, TropicalTerrainConfig terrain) =>
            terrain.ShoreRadius + terrain.CoastNoise
                * (0.45f * MathF.Sin(2f * b + 0.7f)
                    + 0.35f * MathF.Sin(3f * b + 1.3f)
                    + 0.20f * MathF.Sin(5f * b + 4.1f));

        //The far shore's coastline — Tropical.fx's ShoreRingRadius.
        private static float TropicalRingRadius(float b, TropicalTerrainConfig terrain) =>
            terrain.RingRadius + terrain.RingNoise
                * (0.40f * MathF.Sin(2f * b + 2.9f)
                    + 0.34f * MathF.Sin(3f * b + 0.6f)
                    + 0.26f * MathF.Sin(7f * b + 3.4f));

        //The channel through the far ridge — Tropical.fx's ChannelMask.
        private static float TropicalChannelMask(float b, TropicalTerrainConfig terrain) =>
            MathF.Pow(MathF.Max(0f, MathF.Cos(b - terrain.ChannelBearing)), terrain.ChannelSharpness);

        #endregion

        #region The meadow (Meadow.fx: TerrainHeight)

        /// <summary>
        /// The meadow's ground height at a world point, mirroring <c>Meadow.fx</c>'s <c>TerrainHeight</c> term
        /// for term, for a host laying a camera path over the hills (the chapter intro's prologue, #559).
        /// Keep this and the shader in the same change.
        /// </summary>
        public static float Meadow(float x, float z, MeadowSceneConfig config)
        {
            float dist = MathF.Sqrt(x * x + z * z);
            float ramp = ShaderMath.SmoothStep(config.ClearingRadius, config.ClearingRadius + config.ClearingTransition, dist);

            float rolling = 0.5f * MathF.Sin(x * 0.020f + z * 0.015f)
                + 0.3f * MathF.Sin(x * -0.013f + z * 0.024f + 1.5f)
                + 0.2f * MathF.Sin(x * 0.031f + z * 0.026f + 3.0f);

            float basin = config.ClearingRelief * MathF.Sin(x * 0.05f + z * 0.035f);

            return config.LevelY + basin + config.HillHeight * ramp * (rolling * 0.5f + 0.5f);
        }

        #endregion

        #region The forest, and the aurora's night wood on the same field (ForestGround.fxh: TerrainHeight)

        /// <summary>
        /// The forest terrain height at a world point, mirroring <see cref="ForestSceneConfig"/>'s
        /// <c>Forest.fx</c> <c>TerrainHeight</c> field — and <c>Aurora.fx</c>'s, which is the same field on the
        /// aurora's own <see cref="AuroraSceneConfig.Terrain"/>; both include the one copy in <c>ForestGround.fxh</c> (#581). Config-taking so the forest scatter can plant
        /// trees on the ground the shader draws before the renderer itself exists, and so it stays in step with
        /// whatever config the caller holds. Keep this and that <c>TerrainHeight</c> in the same change:
        /// a drift here plants trees underground or floating, which the Testbed's <c>mirrorcheck</c> catches
        /// (#590) where only the eye did before.
        /// </summary>
        public static float Forest(float x, float z, ForestSceneConfig config)
        {
            float dist = MathF.Sqrt(x * x + z * z);
            //GLSL smoothstep(edge0, edge1, x) = hermite over the clamped (x-edge0)/(edge1-edge0). MonoGame's
            //MathHelper.SmoothStep is NOT that: it takes (value1, value2, amount) with amount in 0..1, so
            //passing it the raw distance (hundreds of units) makes the ramp explode and the scatter plants trees
            //thousands of units up. Mirroring the savanna's clamp-then-hermite instead, which matches Forest.fx
            //(spelled out rather than through ShaderMath.SmoothStep for the savanna's bit-for-bit reason).
            float t = MathHelper.Clamp((dist - config.ClearingRadius) / config.ClearingTransition, 0f, 1f);
            float ramp = t * t * (3f - 2f * t);

            //The domain warp, five octaves and the lump mask all mirror ForestGround.fxh's TerrainHeight term for
            //term — see there for why each exists. Kept in ONE change with the shader.
            float qx = x + 26f * MathF.Sin(z * 0.011f + 2f);
            float qz = z + 26f * MathF.Sin(x * 0.013f + 5f);

            float rolling = 0.40f * MathF.Sin(qx * 0.020f + qz * 0.015f)
                + 0.26f * MathF.Sin(qx * -0.013f + qz * 0.024f + 1.5f)
                + 0.17f * MathF.Sin(qx * 0.031f + qz * 0.026f + 3.0f)
                + 0.10f * MathF.Sin(qx * 0.056f + qz * -0.041f + 0.7f)
                + 0.07f * MathF.Sin(qx * -0.083f + qz * 0.062f + 2.4f);

            float basin = config.ClearingRelief * MathF.Sin(x * 0.05f + z * 0.035f);

            float f = config.FloorLumpFrequency;
            float mask = 0.55f + 0.45f * MathF.Sin(x * 0.021f + z * -0.017f + 4f);
            float lumps = MathF.Sin(x * f + z * f * 0.7f)
                + 0.5f * MathF.Sin(x * -f * 0.8f + z * f * 1.1f + 2.0f)
                + 0.35f * MathF.Sin(x * f * 1.9f + z * f * 1.4f + 5.1f);
            float lumpHeight = config.FloorLumpStrength * lumps * mask * (1.0f - ramp * 0.5f);

            return config.LevelY + basin + lumpHeight + config.HillHeight * ramp * (rolling * 0.5f + 0.5f);
        }

        #endregion

        #region The volcano (Volcano.fx: TerrainHeight without its scoria, VolcanoMassing)

        /// <summary>
        /// The volcano's ground height at a world point: <c>Volcano.fx</c>'s <c>TerrainHeight</c> without its
        /// scoria fBm term, which is the one thing this mirror leaves out and can afford to — three units of
        /// clinker under a lamp or a vent is invisible, and reproducing four octaves of gradient noise on the
        /// CPU to place them would be the tail wagging the dog. Everything that decides where the cone, the
        /// crater and the gullies are is here term for term.
        /// </summary>
        public static float Volcano(float x, float z, VolcanoSceneConfig volcano)
        {
            float ramp = ShaderMath.SmoothStep(volcano.ClearingRadius, volcano.ClearingRadius + MathF.Max(volcano.ClearingTransition, 1f),
                MathF.Sqrt(x * x + z * z));

            Vector2 cone = volcano.ConeCenter.ToVector2();
            float dx = x - cone.X;
            float dz = z - cone.Y;
            float r = MathF.Sqrt(dx * dx + dz * dz);
            float bearing = MathF.Atan2(dz, dx);

            //Clamped to CraterRadius, not r itself - Volcano.fx's VolcanoMassing has the why: evaluated at r
            //the flank is maximal exactly at the vent for any profile, so the crater term below could only
            //ever steepen the approach to a point, never move the true summit off it. The clamp is what
            //plateaus the flank at the rim's own height, which is the surface the bowl is cut into.
            float craterRadius = MathF.Max(volcano.CraterRadius, 1f);
            float flankRadius = MathF.Max(r, craterRadius);
            float t = Math.Clamp(1f - flankRadius / MathF.Max(volcano.ConeRadius, 1f), 0f, 1f);
            float flank = volcano.ConeHeight * MathF.Pow(t, MathF.Max(volcano.ConeProfile, 0.1f));

            float crater = volcano.CraterDepth * ShaderMath.SmoothStep(craterRadius, 0f, r);

            float gullyCount = MathF.Round(MathF.Max(volcano.GullyCount, 1f));
            float rake = 0.5f - 0.5f * MathF.Cos(bearing * gullyCount + 2f * MathF.Sin(bearing * 3f));
            //Volcano.fx's band to the figure. This copy carried its own (1.15, 0.45, 1.05, 0.62) from the day the
            //scene was built, so on the lower flank - where the flow fronts' lamps run - it put the ground up to
            //seven units above the channel the shader draws, and at a side vent's radius three and a half.
            float gullyBand = ShaderMath.SmoothStep(craterRadius * 1.3f, volcano.ConeRadius * 0.30f, r)
                * ShaderMath.SmoothStep(volcano.ConeRadius * 1.15f, volcano.ConeRadius * 0.85f, r);

            return volcano.LevelY + ramp * (flank - crater - volcano.GullyDepth * rake * gullyBand);
        }

        #endregion
    }
}

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>Every CPU mirror of a terrain shader's height field, in one place</b> (#590) — <c>Desert.fx</c>'s
    /// dunes, <c>Mountain.fx</c>'s range, <c>Outback.fx</c>'s plain with its monoliths, <c>Polar.fx</c>'s
    /// icesheet, <c>Savanna.fx</c>'s grassland, <c>Tropical.fx</c>'s beach, <c>Meadow.fx</c>'s hills,
    /// <c>Forest.fx</c>'s floor (which <c>Aurora.fx</c> draws too, on its own terrain config),
    /// <c>Volcano.fx</c>'s cone and, since the boulders, <c>Moon.fx</c>'s crater plain. They are what the scatters plant on, what the scene lights stand on, and
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
        private const float DUNE_MEAN = 0.337f; //sampled through this mirror over 30 million points far outside the clearing (0.30 with every crest sharp and a plain max, before #688; 0.403 while #688 rounded them all)
        private const float CREST_ROUND = 1.0f;
        private const float CREST_REACH = 0.3f;
        private const float CREST_GATE = 0.2f;
        private const float CREST_GRID_WIDTH = 5.6f;
        private const float SET_BLEND = 0.35f;

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
            float majorCycles = warped / DUNE_SPACING;

            Vector2 wind2 = new(wind.X * 0.819f - wind.Y * 0.574f, wind.X * 0.574f + wind.Y * 0.819f);
            float n4 = ShaderMath.Noise(p * 0.017f + new Vector2(29f));
            float n5 = ShaderMath.Noise(p * 0.008f + new Vector2(41f));
            float minorStrength = ShaderMath.SmoothStep(0.2f, 0.75f, 0.45f + 1.2f * n5);
            float minorCycles = (Vector2.Dot(p, wind2) + n4 * 20f) / (DUNE_SPACING * 0.46f);

            //Desert.fx's crests (#688): sharp, and rounded only where the two sets come together in height - the gap read
            //off heights at the grid's floor radius, smooth across a crest, as Desert.fx reads it
            float majorFloor = CrestRadiusFor(DUNE_SPACING);
            float minorFloor = CrestRadiusFor(DUNE_SPACING * 0.46f);
            float majorHeight = DuneProfile(majorCycles, majorFloor) * strength;
            float minorHeight = DuneProfile(minorCycles, minorFloor) * 0.5f * minorStrength;
            float x = (majorHeight - minorHeight) / CREST_REACH;
            float q = MathF.Max(1f - x * x, 0f);
            float y = MathHelper.Clamp(majorHeight * minorHeight / (CREST_GATE * CREST_GATE), 0f, 1f);
            float meeting = CREST_ROUND * q * q * y * y * (3f - 2f * y);

            //And never narrower than the vertex grid resolves (Desert.fx's CREST_GRID_WIDTH): the vertices' field, not the normal's
            float major = DuneProfile(majorCycles, MathF.Sqrt(meeting * meeting + majorFloor * majorFloor)) * strength;
            float minor = DuneProfile(minorCycles, MathF.Sqrt(meeting * meeting + minorFloor * minorFloor)) * 0.5f * minorStrength;

            float swell = 0.22f * MathF.Sin(p.X * 0.017f + p.Y * 0.011f) + 0.12f * MathF.Sin(p.X * -0.009f + p.Y * 0.021f + 1.7f);

            //Desert.fx's smooth maximum of the two sets (#688): a saddle where they cross, not a point
            float w = MathF.Max(SET_BLEND - MathF.Abs(major - minor), 0f) / SET_BLEND;
            return MathF.Max(major, minor) + 0.25f * SET_BLEND * w * w + swell - DUNE_MEAN;
        }

        private static float CrestRadiusFor(float spacing) => CREST_GRID_WIDTH / (2f * DUNE_WINDWARD * (1f - DUNE_WINDWARD) * spacing);

        private static float DuneProfile(float cycles, float radius)
        {
            float t = cycles - MathF.Floor(cycles);
            float rise = t / DUNE_WINDWARD;
            float fall = (1f - t) / (1f - DUNE_WINDWARD);

            //Desert.fx's crest (#688): the quadratic smooth minimum of the two slopes within the radius of the corner, the
            //radius held off zero by the same hair
            float r = MathF.Max(radius, 1e-4f);
            float w = MathF.Max(r - MathF.Abs(rise - fall), 0f) / r;
            float m = MathF.Min(rise, fall) - 0.25f * r * w * w;
            float h = MathHelper.Clamp(m, 0f, 1f);

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

        //Outback.fx's constants of the same names (#967): a loaf's wall, a cluster's domes, the gullies' floor and
        //the apron's share of the height.
        private const float LOAF_WALL = 0.20f, DOME_WALL = 0.42f, DOME_RADIUS_MIN = 0.46f, DOME_RADIUS_MAX = 0.62f,
            DOME_REACH = 1.25f, RIB_SHARPNESS = 2.4f, APRON_SHARE = 0.05f;
        private const int MAX_DOMES = 5;

        //The golden angle a cluster's domes are turned round its centre by, as the rotation the shader multiplies by
        //(its cosine and sine as literals on both sides, so no sin/cos approximation stands between the two).
        private const float GOLDEN_COS = -0.7373688f, GOLDEN_SIN = 0.6754903f;

        //The two lattices' fixed figures, as OutbackHeight passes them to RockLayer (MONOLITH_ARGS, OUTCROP_ARGS).
        private const float ROCK_SEED = 13.7f, OUTCROP_SEED = 71.3f;
        private const float ROCK_MIN_RADIUS = 0.10f, ROCK_MAX_RADIUS = 0.16f, ROCK_MAX_ELONGATION = 1.8f;
        private const float OUTCROP_MIN_RADIUS = 0.045f, OUTCROP_MAX_RADIUS = 0.085f, OUTCROP_MAX_ELONGATION = 1.35f;
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

        /// <summary>
        /// One formation's footprint (#967): the ellipse <c>Outback.fx</c>'s plain stops on and the formation's own
        /// polar mesh is laid over. <see cref="Axis"/> is its long axis in world XZ; the semi-axes are world units.
        /// </summary>
        public readonly struct Footprint
        {
            public readonly Vector2 Centre;
            public readonly Vector2 Axis;
            public readonly float SemiLong;
            public readonly float SemiShort;
            public readonly bool Outcrop;

            public Footprint(Vector2 centre, Vector2 axis, float semiLong, float semiShort, bool outcrop)
            {
                Centre = centre;
                Axis = axis;
                SemiLong = semiLong;
                SemiShort = semiShort;
                Outcrop = outcrop;
            }
        }

        //Outback.fx's RockRoll: one lattice cell's formation, if it has one
        private struct RockRoll
        {
            public float Ramp;
            public Vector2 Centre;
            public float Radius;
            public float Elongation;
            public Vector2 Axis;
            public Vector2 RollA, RollB, RollE, RollF;
        }

        /// <summary>World Y of the outback's ground at a world XZ, as <c>Outback.fx</c>'s <c>OutbackHeight</c> displaces it.</summary>
        public static float Outback(float x, float z, OutbackSceneConfig config)
        {
            OutbackTerrainConfig terrain = config.Terrain;
            Vector2 p = new(x, z);

            float rock = RockLayer(p, MathF.Max(terrain.RockSpacing, 1f), ROCK_SEED, terrain.RockChance, ROCK_MIN_RADIUS, ROCK_MAX_RADIUS,
                ROCK_MAX_ELONGATION, config.Surface.RibDepth, terrain.RockHeight, LOAF_WALL, 2.0f, 3.4f, true, config);

            float outcrop = RockLayer(RotateIntoOutcropFrame(p), MathF.Max(terrain.OutcropSpacing, 1f), OUTCROP_SEED, terrain.OutcropChance,
                OUTCROP_MIN_RADIUS, OUTCROP_MAX_RADIUS, OUTCROP_MAX_ELONGATION, 0f, terrain.OutcropHeight, 0.475f, 1.5f, 5.5f, false, config);

            return OutbackPlain(p, terrain) + rock + outcrop;
        }

        /// <summary>World Y of the outback's plain alone (<c>Outback.fx</c>'s <c>PlainHeight</c>): the swell, no rock.</summary>
        public static float OutbackPlain(Vector2 p, OutbackTerrainConfig terrain)
        {
            float swell = ShaderMath.Noise(p * 0.0062f) * 0.70f + ShaderMath.Noise(p * 0.0185f + new Vector2(5.1f)) * 0.30f;

            return terrain.LevelY + terrain.PlainRelief * swell;
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
                    RockRoll roll = RollRock(cx, cz, cellSize, ROCK_SEED, terrain.RockChance, ROCK_MIN_RADIUS, ROCK_MAX_RADIUS,
                        ROCK_MAX_ELONGATION, config.Surface.RibDepth, terrain);
                    if (roll.Ramp <= 0f) continue;

                    Vector2 centreWorld = (new Vector2(cx, cz) + roll.Centre) * cellSize;
                    if (centreWorld.Length() > maxRadius) continue;

                    float reach = roll.Radius * TALUS_REACH * (1f + config.Surface.RibDepth) * roll.Elongation * cellSize;
                    float crown = terrain.LevelY + terrain.PlainRelief + terrain.RockHeight * MathF.Max(config.Terrain.ClusterHeight, 1f)
                        * MathHelper.Lerp(0.62f, 1f, roll.RollB.Y) * roll.Ramp + terrain.OutcropHeight;
                    found.Add(new Formation(centreWorld, reach, crown));
                }
            }

            return found;
        }

        /// <summary>
        /// Every footprint of both lattices (#967) whose centre stands within <paramref name="maxRadius"/> of the arena:
        /// the ellipses <c>Outback.fx</c>'s <c>InsideFootprint</c> tests, in world units, which the backdrop lays a polar
        /// mesh over. The same rolls the height field is placed by, so a footprint listed here holds the formation the
        /// shader raises, its apron included.
        /// </summary>
        public static List<Footprint> OutbackFootprints(OutbackSceneConfig config, float maxRadius)
        {
            OutbackTerrainConfig terrain = config.Terrain;
            var found = new List<Footprint>();

            AddFootprints(found, MathF.Max(terrain.RockSpacing, 1f), ROCK_SEED, terrain.RockChance, ROCK_MIN_RADIUS, ROCK_MAX_RADIUS,
                ROCK_MAX_ELONGATION, config.Surface.RibDepth, terrain, maxRadius, outcrop: false);
            AddFootprints(found, MathF.Max(terrain.OutcropSpacing, 1f), OUTCROP_SEED, terrain.OutcropChance, OUTCROP_MIN_RADIUS,
                OUTCROP_MAX_RADIUS, OUTCROP_MAX_ELONGATION, 0f, terrain, maxRadius, outcrop: true);

            return found;
        }

        private static void AddFootprints(List<Footprint> found, float cellSize, float seed, float chance, float minRadius, float maxRadius,
            float maxElongation, float ribDepth, OutbackTerrainConfig terrain, float searchRadius, bool outcrop)
        {
            int cells = (int)MathF.Ceiling(searchRadius / cellSize) + 2;

            for (int cz = -cells; cz <= cells; cz++)
            {
                for (int cx = -cells; cx <= cells; cx++)
                {
                    RockRoll roll = RollRock(cx, cz, cellSize, seed, chance, minRadius, maxRadius, maxElongation, ribDepth, terrain);
                    if (roll.Ramp <= 0f) continue;

                    //The roll lives in its lattice's own frame: the outcrops' is turned by OUTCROP_AXIS, so its centre and
                    //axis are turned back into the world's (RotateInto's inverse)
                    Vector2 centre = (new Vector2(cx, cz) + roll.Centre) * cellSize;
                    Vector2 axis = roll.Axis;
                    if (outcrop)
                    {
                        centre = FromOutcropFrame(centre);
                        axis = FromOutcropFrame(axis);
                    }

                    if (centre.Length() > searchRadius) continue;

                    float semiShort = roll.Radius * TALUS_REACH * (1f + ribDepth) * cellSize;
                    found.Add(new Footprint(centre, axis, semiShort * roll.Elongation, semiShort, outcrop));
                }
            }
        }

        //Outback.fx's RotateInto(p, OUTCROP_FRAME), and back
        private static Vector2 RotateIntoOutcropFrame(Vector2 p) =>
            new(Vector2.Dot(p, OUTCROP_AXIS), Vector2.Dot(p, new Vector2(-OUTCROP_AXIS.Y, OUTCROP_AXIS.X)));

        private static Vector2 FromOutcropFrame(Vector2 r) => OUTCROP_AXIS * r.X + new Vector2(-OUTCROP_AXIS.Y, OUTCROP_AXIS.X) * r.Y;

        //Outback.fx's RollRock, for one cell. The constants are added to the seed FIRST, as the shader compiler folds them.
        private static RockRoll RollRock(float cellX, float cellZ, float cellSize, float seed, float chance,
            float minRadius, float maxRadius, float maxElongation, float ribDepth, OutbackTerrainConfig terrain)
        {
            RockRoll roll = default;
            roll.RollA = Roll01(cellX + seed, cellZ + seed);
            roll.RollB = Roll01(cellX + (seed + 23.7f), cellZ + (seed + 23.7f));
            Vector2 rollC = Roll01(cellX + (seed + 57.1f), cellZ + (seed + 57.1f));
            Vector2 rollD = Roll01(cellX + (seed + 91.3f), cellZ + (seed + 91.3f));

            roll.Radius = MathHelper.Lerp(minRadius, maxRadius, roll.RollB.X);
            roll.Elongation = MathHelper.Lerp(1f, maxElongation, rollD.Y);
            roll.Axis = RollDirection(rollD);

            float margin = MathF.Min(roll.Radius * TALUS_REACH * (1f + ribDepth) * roll.Elongation, 0.45f);
            roll.Centre = new Vector2(margin) + rollC * (1f - 2f * margin);

            Vector2 centreWorld = (new Vector2(cellX, cellZ) + roll.Centre) * cellSize;
            roll.Ramp = roll.RollA.X > chance ? 0f
                : ShaderMath.SmoothStep(terrain.ClearingRadius, terrain.ClearingRadius + terrain.ClearingTransition, centreWorld.Length());

            roll.RollE = Roll01(cellX + (seed + 131.9f), cellZ + (seed + 131.9f));
            roll.RollF = Roll01(cellX + (seed + 173.3f), cellZ + (seed + 173.3f));

            return roll;
        }

        private static float RockLayer(Vector2 p, float cellSize, float seed, float chance, float minRadius, float maxRadius,
            float maxElongation, float ribDepth, float height, float wall, float minSquareness, float maxSquareness, bool clusters,
            OutbackSceneConfig config)
        {
            OutbackTerrainConfig terrain = config.Terrain;
            Vector2 q = p / cellSize;
            float cellX = MathF.Floor(q.X), cellZ = MathF.Floor(q.Y);

            RockRoll roll = RollRock(cellX, cellZ, cellSize, seed, chance, minRadius, maxRadius, maxElongation, ribDepth, terrain);
            if (roll.Ramp <= 0f) return 0f;

            Vector2 offset = q - new Vector2(cellX, cellZ) - roll.Centre;
            Vector2 local = new(Vector2.Dot(offset, roll.Axis), Vector2.Dot(offset, new Vector2(-roll.Axis.Y, roll.Axis.X)));
            local.X /= roll.Elongation;

            float radius = roll.Radius;
            float d1 = local.Length() / radius;
            Vector2 radial = local / MathF.Sqrt(MathF.Max(Vector2.Dot(local, local), 1e-6f));

            float rise, apron;

            if (clusters && roll.RollF.X < terrain.ClusterChance)
            {
                int count = 3 + (int)(roll.RollF.Y * 2.999f);
                float best = 0f;
                float nearest = 1e4f;
                Vector2 direction = RollDirection(roll.RollE);

                for (int i = 0; i < MAX_DOMES; i++)
                {
                    Vector2 dhRaw = roll.RollE * (3.17f + 2.31f * i) + new Vector2(roll.RollF.Y, roll.RollF.X) * (1.93f + 0.77f * i)
                        + new Vector2(0.43f, 0.71f) * i;
                    Vector2 dh = new(ShaderMath.Frac(dhRaw.X), ShaderMath.Frac(dhRaw.Y));
                    float spread = i == 0 ? 0.12f * dh.X : MathHelper.Lerp(0.32f, 0.50f, dh.X);
                    Vector2 domeCentre = direction * radius * spread;
                    float domeRadius = radius * MathHelper.Lerp(DOME_RADIUS_MIN, DOME_RADIUS_MAX, i == 0 ? 0.6f + 0.4f * dh.Y : dh.Y);

                    float d = (local - domeCentre).Length() / domeRadius;
                    float present = i < count ? 1f : 0f;
                    float tall = (i == 0 ? 1f : MathHelper.Lerp(0.55f, 0.85f, dh.Y)) * present;
                    float h = Bornhardt(d, MathHelper.Lerp(2.0f, 2.5f, dh.X), DOME_WALL) * tall;

                    best = MathF.Max(best, h);
                    nearest = MathF.Min(nearest, MathHelper.Lerp(1e4f, d, present));

                    direction = new Vector2(direction.X * GOLDEN_COS - direction.Y * GOLDEN_SIN, direction.X * GOLDEN_SIN + direction.Y * GOLDEN_COS);
                }

                apron = ShaderMath.SmoothStep(DOME_REACH, 1f, nearest);
                rise = best * terrain.ClusterHeight;
            }
            else
            {
                float n = ShaderMath.Noise(radial * config.Surface.RibCount + new Vector2(cellX, cellZ) * 13.1f + new Vector2(seed));
                float gully = 1f - MathHelper.Clamp(MathF.Abs(n) * RIB_SHARPNESS, 0f, 1f);
                gully *= gully;

                float ribbed = d1 * (1f + gully * ribDepth * ShaderMath.SmoothStep(0.20f, 0.62f, d1));

                float lobeScale = MathHelper.Lerp(0.40f, 0.60f, roll.RollE.Y);
                Vector2 lobeCentre = RollDirection(roll.RollE) * radius * 0.5f;
                float d2 = (local - lobeCentre).Length() / (radius * lobeScale);

                float squareness = MathHelper.Lerp(minSquareness, maxSquareness, roll.RollA.Y);
                float mainBody = Bornhardt(ribbed, squareness, wall);
                float lobeBody = Bornhardt(d2, squareness, MathF.Min(wall / lobeScale, 0.9f)) * MathHelper.Lerp(0.55f, 0.90f, roll.RollE.X);

                rise = MathF.Max(mainBody, lobeBody);
                apron = ShaderMath.SmoothStep(TALUS_REACH, 1f, MathF.Min(d1, d2));
            }

            return (rise * (1f - APRON_SHARE) + apron * APRON_SHARE) * height * MathHelper.Lerp(0.62f, 1f, roll.RollB.Y) * roll.Ramp;
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

            //The ice round the research camp (#747, Polar.fx's CampClear)
            float camp = ShaderMath.SmoothStep(config.CampClearRadius, config.CampClearRadius * 1.5f, Vector2.Distance(p, config.CampCentre));

            return slot * MathHelper.Clamp(field + ridge * 1.2f, 0f, 1f) * clear * camp;
        }

        #endregion

        #region The savanna (Savanna.fx: TerrainHeight)

        /// <summary>
        /// The savanna terrain height at a world point, mirroring <c>Savanna.fx</c>'s <c>TerrainHeight</c>, so the
        /// acacia trees can be planted on the ground the shader draws: the plain's relief (<see cref="SavannaPlain"/>)
        /// plus the mound each campfire stands on (<see cref="SavannaMound"/>, #887).
        /// </summary>
        public static float Savanna(float x, float z, SavannaSceneConfig config) =>
            SavannaPlain(x, z, config) + SavannaMound(x, z, config);

        /// <summary>
        /// The plain's own height, the rolling relief without the fires' mounds: what <c>Savanna.fx</c>'s <c>TerrainHeight</c>
        /// returned before #887, and still its first term.
        /// </summary>
        public static float SavannaPlain(float x, float z, SavannaSceneConfig config)
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

        /// <summary>
        /// How high fire <paramref name="fire"/>'s mound stands at its own centre (#887): the rise from the plain there to the seat,
        /// <c>ArenaIsland.TOP_Y</c> less <see cref="CampfireConfig.SeatBelowPlate"/>, or 0 where the plain is already that high.
        /// The one figure the mound's mirror and <c>Savanna.fx</c>'s <c>HearthLift</c> both take.
        /// </summary>
        public static float SavannaMoundLift(SavannaSceneConfig config, int fire)
        {
            Vec2 centre = SavannaBackdrop.CampfireRingXZ(config, fire);
            float seat = ArenaIsland.TOP_Y - config.Campfire.SeatBelowPlate;
            return MathF.Max(0f, seat - SavannaPlain(centre.X, centre.Y, config));
        }

        /// <summary>
        /// The mounds' flat top and foot in world units as the ground is built with them (#887), or (0, 0) with the mounds off: the config's
        /// (<see cref="CampfireConfig.MoundTop"/>, <see cref="CampfireConfig.MoundFoot"/>) with the foot held under half the ring's centre-to-centre
        /// spacing, so that a point is inside one mound at most and the NEAREST fire's mound is its mound (the shader finds that one in a single
        /// pass, and a mirror that took the highest of overlapping mounds would stand off it). Shipped, the foot is 10.6 against 12.8.
        /// </summary>
        public static (float top, float foot) SavannaMoundShape(SavannaSceneConfig config)
        {
            CampfireConfig fires = config.Campfire;
            float foot = fires.MoundFoot;
            if (foot <= 0f) return (0f, 0f);

            int count = SavannaBackdrop.CampfireRingCount(config);
            if (count > 1)
            {
                Vec2 anchor = fires.GroundXZ;
                float ring = MathF.Sqrt(anchor.X * anchor.X + anchor.Y * anchor.Y);
                foot = MathF.Min(foot, ring * MathF.Sin(MathF.PI / count) * 0.999f);
            }

            foot = MathF.Max(foot, 0.1f);
            return (MathF.Min(fires.MoundTop, foot - 0.05f), foot);
        }

        /// <summary>
        /// The height the campfires' mounds add to the plain at a world point (#887), mirroring <c>Savanna.fx</c>'s <c>HearthMound</c>: the
        /// <b>nearest</b> fire's <see cref="SavannaMoundLift"/> times one minus a smoothstep from the flat top to the foot
        /// (<see cref="SavannaMoundShape"/>), nothing past the foot and nothing outside the ring's band (the early-out the shader has), and
        /// nothing at all with <see cref="CampfireConfig.MoundFootScale"/> 0.
        /// </summary>
        public static float SavannaMound(float x, float z, SavannaSceneConfig config)
        {
            (float top, float foot) = SavannaMoundShape(config);
            if (foot <= 0f) return 0f;

            //The band the fires' centres stand in, as the shader measures it (HearthNear/HearthFar): a point outside it by more than the foot is in no mound
            Vec2 anchor = config.Campfire.GroundXZ;
            float ring = MathF.Sqrt(anchor.X * anchor.X + anchor.Y * anchor.Y);
            float r = MathF.Sqrt(x * x + z * z);
            if (MathF.Abs(r - ring) > foot) return 0f;

            //The nearest fire, the first of equals, as the shader's strict comparison finds it
            int count = SavannaBackdrop.CampfireRingCount(config), nearest = 0;
            float best = float.MaxValue;
            for (int fire = 0; fire < count; fire++)
            {
                Vec2 centre = SavannaBackdrop.CampfireRingXZ(config, fire);
                float dx = x - centre.X, dz = z - centre.Y;
                float d2 = dx * dx + dz * dz;
                if (d2 < best) { best = d2; nearest = fire; }
            }

            float d = MathF.Sqrt(best);
            return d >= foot ? 0f : SavannaMoundLift(config, nearest) * (1f - ShaderMath.SmoothStep(top, foot, d));
        }

        #endregion

        #region The tropical beach (TropicalTerrain.fxh: TropicalHeight, CoastRadius, ShoreRingRadius, ChannelMask)

        /// <summary>
        /// The tropical terrain height at a world point, mirroring <c>TropicalTerrain.fxh</c>'s
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

        //The waterline's radius at a bearing — TropicalTerrain.fxh's CoastRadius, in one change with it.
        private static float TropicalCoastRadius(float b, TropicalTerrainConfig terrain) =>
            terrain.ShoreRadius + terrain.CoastNoise
                * (0.45f * MathF.Sin(2f * b + 0.7f)
                    + 0.35f * MathF.Sin(3f * b + 1.3f)
                    + 0.20f * MathF.Sin(5f * b + 4.1f));

        //The far shore's coastline — TropicalTerrain.fxh's ShoreRingRadius.
        private static float TropicalRingRadius(float b, TropicalTerrainConfig terrain) =>
            terrain.RingRadius + terrain.RingNoise
                * (0.40f * MathF.Sin(2f * b + 2.9f)
                    + 0.34f * MathF.Sin(3f * b + 0.6f)
                    + 0.26f * MathF.Sin(7f * b + 3.4f));

        //The channel through the far ridge — TropicalTerrain.fxh's ChannelMask.
        private static float TropicalChannelMask(float b, TropicalTerrainConfig terrain) =>
            MathF.Pow(MathF.Max(0f, MathF.Cos(b - terrain.ChannelBearing)), terrain.ChannelSharpness);

        #endregion

        #region The meadow (Meadow.fx: TerrainHeight)

        /// <summary>
        /// The meadow's ground height at a world point, mirroring <c>Meadow.fx</c>'s <c>TerrainHeight</c> term
        /// for term: the field with the brook's swale and the pond's crater cut into it (<see cref="MeadowCarve"/>, #892),
        /// what everything planted stands on. Keep this and the shader in the same change.
        /// </summary>
        public static float Meadow(float x, float z, MeadowSceneConfig config) => MeadowCarve.Height(x, z, config);

        /// <summary>
        /// The meadow's field before the brook and the pond are cut into it - <c>Meadow.fx</c>'s <c>GroundHeight</c>, what its
        /// camera grid displaces by: the hills, the pond's ground levelled and the knoll. What the Potato path's grid and
        /// planting stand on (its cells are 15 units, far too coarse for a cut), what the brook's water is measured from, and
        /// what the chapter intro lifts its lenses over (#892).
        /// </summary>
        public static float MeadowGround(float x, float z, MeadowSceneConfig config)
        {
            //The pond lies level (#609's third round): the ground under it and round it eased to the height at its
            //centre, and the knoll the footpath climbs to its tree raised on top of everything
            (float px, float pz) = MeadowPath.PondCentre(config);
            float pondDistance = MathF.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
            float pondLevel = 1f - ShaderMath.SmoothStep(config.PondRadius * MEADOW_POND_LEVEL_FROM,
                config.PondRadius * MEADOW_POND_LEVEL_FROM + MEADOW_POND_LEVEL_FADE, pondDistance);

            (float kx, float kz) = MeadowPath.PathEnd(config);
            float knoll = Math.Clamp(1f - ((x - kx) * (x - kx) + (z - kz) * (z - kz)) / (config.KnollRadius * config.KnollRadius), 0f, 1f);

            float natural = MeadowNatural(x, z, config);
            return natural + (MeadowNatural(px, pz, config) - natural) * pondLevel + config.KnollHeight * knoll * knoll;
        }

        /// <summary>How far round the pond, as a multiple of its mean radius, the ground lies at the pond's level, and
        /// over how many world units past that it eases back into the hills — <c>Meadow.fx</c>'s own figures.</summary>
        public const float MEADOW_POND_LEVEL_FROM = 1.3f, MEADOW_POND_LEVEL_FADE = 14f;

        /// <summary>The meadow's rolling field before the pond and the knoll: the clearing and the hills.</summary>
        public static float MeadowNatural(float x, float z, MeadowSceneConfig config)
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

        #region The aurora's snowfield (Aurora.fx: SnowDrift, SnowBank, SnowBaseHeight, SnowHeight)

        /// <summary>
        /// The aurora's ground before the mounds round the planting (#963): the forest's field on
        /// <see cref="AuroraSceneConfig.Terrain"/>, the wind's drifts and the bank against the island — <c>Aurora.fx</c>'s
        /// <c>SnowBaseHeight</c>, term for term. What the wood, the far stand and the mound map are planted on.
        /// </summary>
        public static float AuroraBase(float x, float z, AuroraSceneConfig config) =>
            Forest(x, z, config.Terrain) + SnowDrift(x, z, config.Snowfield) + SnowBank(x, z, config.Snowfield);

        /// <summary>
        /// The ground as <c>Aurora.fx</c> draws it (<c>SnowHeight</c>): <see cref="AuroraBase"/> plus the drifts round the
        /// planting, read off <paramref name="mounds"/> with the shader's own bilinear arithmetic. Null mounds is the base.
        /// </summary>
        public static float Aurora(float x, float z, AuroraSceneConfig config, AuroraMoundMap mounds) =>
            AuroraBase(x, z, config) + (mounds?.Height(x, z) ?? 0f);

        /// <summary>The wind normalised as <c>DriftWind</c> is pushed: the one place it is, so the mirror and the uniform cannot disagree.</summary>
        public static Vector2 SnowWind(AuroraSnowfieldConfig snowfield)
        {
            Vector2 wind = snowfield.Wind.ToVector2();
            return wind.LengthSquared() > 1e-8f ? Vector2.Normalize(wind) : Vector2.UnitX;
        }

        /// <summary><c>Aurora.fx</c>'s <c>SnowDrift</c>: long low dunes, their crests across the wind, in patches.</summary>
        public static float SnowDrift(float x, float z, AuroraSnowfieldConfig snowfield)
        {
            Vector2 wind = SnowWind(snowfield);
            float acrossX = -wind.Y, acrossZ = wind.X;

            float qx = (x * wind.X + z * wind.Y) / snowfield.DriftAlong;
            float qz = (x * acrossX + z * acrossZ) / snowfield.DriftAcross;
            qx += 1.4f * ShaderMath.Noise(new Vector2(x * 0.009f + 3.7f, z * 0.009f + 8.1f));

            float dune = ShaderMath.Noise(new Vector2(qx, qz))
                + 0.45f * ShaderMath.Noise(new Vector2(qx * 2.1f + 5.2f, qz * 1.7f + 1.3f));
            float patch = MathHelper.Clamp(0.6f + 0.8f * ShaderMath.Noise(new Vector2(x * 0.0065f + 9.1f, z * 0.0065f + 2.4f)), 0f, 1f);

            return snowfield.DriftHeight * dune * patch;
        }

        /// <summary><c>Aurora.fx</c>'s <c>SnowBank</c>: the snow piled against the island's drum, deeper on the lee side.</summary>
        public static float SnowBank(float x, float z, AuroraSnowfieldConfig snowfield)
        {
            Vector2 wind = SnowWind(snowfield);
            float r = MathF.Sqrt(x * x + z * z);
            float inverse = 1f / MathF.Max(r, 1e-3f);
            float lee = 1f + snowfield.BankLee * (x * inverse * wind.X + z * inverse * wind.Y);
            float rise = MathF.Exp(-MathF.Max(r - snowfield.BankRadius, 0f) / snowfield.BankReach);

            return snowfield.BankHeight * rise * lee;
        }

        #endregion

        #region The volcano (Volcano.fx: TerrainHeight without its scoria, VolcanoMassing)

        /// <summary>
        /// The volcano's ground height at a world point: <c>Volcano.fx</c>'s <c>TerrainHeight</c> without its
        /// scoria fBm term, which is the one thing this mirror leaves out and can afford to — three units of
        /// clinker under a lamp or a vent is invisible, and reproducing four octaves of gradient noise on the
        /// CPU to place them would be the tail wagging the dog. Everything that decides where the cone, the
        /// crater and the gullies are is here term for term, and since #748 the field's front and the country past
        /// it (<see cref="VolcanoCountry"/>, solved once per config object).
        /// </summary>
        public static float Volcano(float x, float z, VolcanoSceneConfig volcano) => Volcano(x, z, volcano, VolcanoCountry.Of(volcano));

        /// <summary>
        /// <see cref="Volcano(float, float, VolcanoSceneConfig)"/> on a country the caller holds: what a per-frame caller (the
        /// lamps, through <see cref="VolcanoLava"/>) asks, so no frame looks a config up.
        /// </summary>
        public static float Volcano(float x, float z, VolcanoSceneConfig volcano, VolcanoCountry country)
        {
            float ramp = ShaderMath.SmoothStep(volcano.ClearingRadius, volcano.ClearingRadius + MathF.Max(volcano.ClearingTransition, 1f),
                MathF.Sqrt(x * x + z * z));

            //The field's front and, past it, the land (#748): Volcano.fx's TerrainHeightAt, after the massing, in its order
            Vector2 cone = volcano.ConeCenter.ToVector2();
            float coneR = MathF.Sqrt((x - cone.X) * (x - cone.X) + (z - cone.Y) * (z - cone.Y));
            float outside = coneR - country.FieldEdge(x, z);
            float relative = VolcanoMassing(x, z, volcano) + country.FrontProfile(outside);
            if (outside > 0f) relative = country.CountryHeight(x, z, coneR, outside, relative);

            return volcano.LevelY + ramp * relative;
        }

        /// <summary>
        /// The cone alone, on its plain: <see cref="Volcano(float, float, VolcanoSceneConfig)"/> without the field's front and
        /// the country past it (#748). What the Potato path's grid stands on, which draws the cone, its flows and its lake and
        /// no country (the meadow's <see cref="MeadowGround"/> is the same kind of answer).
        /// </summary>
        public static float VolcanoCone(float x, float z, VolcanoSceneConfig volcano)
        {
            float ramp = ShaderMath.SmoothStep(volcano.ClearingRadius, volcano.ClearingRadius + MathF.Max(volcano.ClearingTransition, 1f),
                MathF.Sqrt(x * x + z * z));

            return volcano.LevelY + ramp * VolcanoMassing(x, z, volcano);
        }

        /// <summary><c>Volcano.fx</c>'s <c>VolcanoMassing</c>: the flank, the crater bitten out of its summit and the gullies, over the field's level.</summary>
        private static float VolcanoMassing(float x, float z, VolcanoSceneConfig volcano)
        {
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

            return flank - crater - volcano.GullyDepth * rake * gullyBand;
        }

        #endregion

        #region The Moon (Moon.fx: MoonHeight, CraterField, CraterLayer's height)

        //Moon.fx's CraterField, octave by octave: the turn (Craters.fxh), the period, the seed, the chance and the weight
        private static readonly (Vector2 Turn, float Period, float Seed, float Chance, float Weight)[] MOON_CRATER_OCTAVES =
        {
            (new Vector2(0.97437f, 0.22495f), 129f, 11.3f, 0.86f, 0.58f),
            (new Vector2(0.75471f, 0.65606f), 49f, 37.7f, 0.82f, 0.29f),
            (new Vector2(0.27564f, 0.96126f), 18.6f, 71.1f, 0.70f, 0.13f),
        };

        //Moon.fx's CRATER_MIN_RADIUS / CRATER_MAX_RADIUS
        private const float MOON_CRATER_MIN_RADIUS = 0.12f, MOON_CRATER_MAX_RADIUS = 0.21f;

        /// <summary>
        /// The Moon's ground height at a world point: <c>Moon.fx</c>'s <c>MoonHeight</c> term for term — the clearing's
        /// ramp, the three crater octaves the vertex shader displaces the grid by, the mare under them, the highland belt
        /// the mare shapes and the curvature that closes the horizon. The pixel shader's fourth, 7.2-unit octave and its
        /// regolith relief are normal-only (they shade, they never move the ground), so nothing of them is here. Until
        /// this the Moon had only <see cref="OffworldGround.MoonCeiling"/>, a bound, and nothing could stand on it: the
        /// one attempt at boulders (#508) was painted and read as shadows without rocks.
        /// </summary>
        public static float Moon(float x, float z, MoonTerrainConfig terrain)
        {
            float dist = MathF.Sqrt(x * x + z * z);
            float ramp = ShaderMath.SmoothStep(terrain.ClearingRadius, terrain.ClearingRadius + terrain.ClearingTransition, dist);

            float mare = OffworldGround.MareBase(x, z);

            float field = 0f;
            foreach ((Vector2 turn, float period, float seed, float chance, float weight) in MOON_CRATER_OCTAVES)
            {
                //TurnCrater, then the layer's own cell size - as a reciprocal, the way the shader writes it
                Vector2 p = new Vector2(x * turn.X - z * turn.Y, x * turn.Y + z * turn.X) * (1f / period);
                field += MoonCraterLayer(p, seed, chance) * weight;
            }
            field += mare * 0.18f;

            float shape = MathHelper.Clamp(mare * 0.75f + 0.5f, 0f, 1f);
            float belt = terrain.HighlandHeight
                * ShaderMath.SmoothStep(terrain.HighlandInnerRadius, terrain.HighlandCrestRadius, dist)
                * MathHelper.Lerp(terrain.HighlandSaddleFloor, 1f, shape);

            return terrain.LevelY + terrain.CraterAmplitude * ramp * field + belt - terrain.Curvature * dist * dist;
        }

        /// <summary>
        /// The height a terrain grid of <paramref name="gridN"/> vertices over <paramref name="extent"/> DRAWS at a point:
        /// <paramref name="height"/> sampled at the grid's vertices and interpolated across its triangles, which is the
        /// surface the depth buffer holds and so the one a thing standing on the ground has to stand on. A mirror is the
        /// exact field; where the field is sharper than a cell (the Moon's middle crater octave, rims about a unit wide
        /// on a 3.34-unit grid) the two part by up to a couple of units. The grids are recentred on the camera snapped to
        /// whole cells (<c>TerrainPass</c>, <c>MoonBackdrop.Draw</c>), so a vertex always stands at the same world point
        /// whatever the camera - a whole number of cells from the origin, plus half a cell when the vertex count is even -
        /// and <c>TerrainGridCache</c> splits every cell along its (x+1, z)-(x, z+1) diagonal.
        /// </summary>
        public static float OnGrid(Func<float, float, float> height, int gridN, float extent, float x, float z)
        {
            float step = extent / (gridN - 1);
            float phase = ShaderMath.Frac((gridN - 1) * 0.5f);
            float gx = x / step - phase, gz = z / step - phase;
            float ix = MathF.Floor(gx), iz = MathF.Floor(gz);
            float u = gx - ix, v = gz - iz;
            float x0 = (ix + phase) * step, z0 = (iz + phase) * step;

            float hb = height(x0 + step, z0), hc = height(x0, z0 + step);
            if (u + v <= 1f)
            {
                float ha = height(x0, z0);
                return ha + u * (hb - ha) + v * (hc - ha);
            }
            float hd = height(x0 + step, z0 + step);
            return hd + (1f - u) * (hc - hd) + (1f - v) * (hb - hd);
        }

        /// <summary><c>Moon.fx</c>'s <c>CraterLayer</c>, its height alone (the ejecta and the shadow are colour, not ground).</summary>
        private static float MoonCraterLayer(Vector2 p, float seed, float chance)
        {
            float cellX = MathF.Floor(p.X), cellY = MathF.Floor(p.Y);
            float fx = p.X - cellX, fy = p.Y - cellY;

            Vector2 rollA = ShaderMath.Hash22(cellX + seed, cellY + seed) * 0.5f + new Vector2(0.5f);
            if (rollA.X > chance) return 0f;

            //The seed and the roll's own offset added first: the compiler folds the two literals once CraterField's
            //seed is inlined, the outback's lesson (#598)
            Vector2 rollB = ShaderMath.Hash22(cellX + (seed + 47.9f), cellY + (seed + 47.9f)) * 0.5f + new Vector2(0.5f);
            Vector2 rollC = ShaderMath.Hash22(cellX + (seed + 91.7f), cellY + (seed + 91.7f)) * 0.5f + new Vector2(0.5f);

            float radius = MathHelper.Lerp(MOON_CRATER_MIN_RADIUS, MOON_CRATER_MAX_RADIUS, rollB.X * rollB.X);
            float margin = radius * 1.6f;
            float centreX = margin + rollC.X * (1f - 2f * margin);
            float centreY = margin + rollC.Y * (1f - 2f * margin);

            float dx = fx - centreX, dy = fy - centreY;
            float d = MathF.Sqrt(dx * dx + dy * dy) / radius;
            if (d >= 1.6f) return 0f;

            float depth = MathHelper.Lerp(0.25f, 1f, rollB.Y * rollB.Y);
            float cup = MathHelper.Clamp(1f - d * d, 0f, 1f);
            float bowl = -cup * cup * depth;

            float rimWidth = MathHelper.Lerp(0.18f, 0.42f, rollA.Y);
            float rimT = (d - 1f) / rimWidth;
            float rim = MathF.Exp(-rimT * rimT) * ShaderMath.SmoothStep(1.6f, 1.1f, d);

            return bowl + rim * depth * 0.62f;
        }

        #endregion
    }
}

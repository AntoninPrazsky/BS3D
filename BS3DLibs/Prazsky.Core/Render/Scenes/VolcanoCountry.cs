using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The land past the volcano's young lava field, as figures</b> (#748): where the field ends, how the country beyond
    /// it rises into hills, ridges and a dormant cone, the river's course and the level of its water, and where woods grow.
    /// <c>Volcano.fx</c> draws the ground from these (its <c>LandAt</c>, <c>FrontProfile</c>, <c>CountryHeight</c>,
    /// <c>RiverCourse</c> and <c>ForestCover</c>, term for term), <see cref="TerrainMirror.Volcano(float, float, VolcanoSceneConfig, VolcanoCountry)"/>
    /// mirrors its height off them for everything that stands on it, and <see cref="VolcanoCountryPlanting"/> stands the
    /// woods, the snags and the village by the same masks. No GPU in it: the Potato path and the tests build one too.
    /// <para>
    /// <b>The constants are the land's shape</b> and the shader's own figures, line for line (the meadow's rule: a designer
    /// turning one is redesigning the land). Change one here and change it there: the Testbed's <c>mirrorcheck</c> reads the
    /// height back off the GPU and fails on a drift ("The terrain mirrors" in <c>docs/scenes/terrain-grids-and-mirrors.md</c>).
    /// </para>
    /// <para>
    /// <b>The river is a course and not a formula.</b> A water river has to run downhill, round the field and out of the
    /// world at both ends, which no closed form of the country gives; so it is an authored line of points
    /// (<see cref="RIVER_COURSE"/>), rounded by a Catmull-Rom pass, meandering off it by two sines of the distance along it,
    /// and its water level at each point is solved here off the land under it, never rising downstream. The shader gets
    /// the rounded points with their running length and level (<see cref="RiverPath"/>) and finds the nearest; the vertex
    /// shader does it once a vertex and hands the across, along and outside figures to the pixels, which pay no loop.
    /// </para>
    /// </summary>
    public sealed class VolcanoCountry
    {
        //--- The field's edge: three octaves of the clouds' noise on the world (seam-free, unlike a bearing) - tongues hundreds
        //of units long where one flow ran further than the next, lobes on them, and the margin's own ragged run. One octave
        //drew a wavy circle that read from the cone's far side as the rim of a plate. Their slopes summed stay under one
        //(0.83 at most over the world), so the distance past the edge is a distance.
        internal const float EDGE_F1 = 0.0017f, EDGE_O1 = 3.1f, EDGE_W1 = 1.4f;
        internal const float EDGE_F2 = 0.0055f, EDGE_O2 = 7.7f, EDGE_W2 = 0.55f;
        internal const float EDGE_F3 = 0.016f, EDGE_O3 = 1.3f, EDGE_W3 = 0.3f;

        //--- The front: its face falls from FRONT_LIP inside the edge to FRONT_FOOT past it, under a levee LEVEE_HEIGHT proud
        //that rises over LEVEE_RUN inside it (a flow's margin stands higher than the field behind it)
        internal const float FRONT_LIP = 2f, FRONT_FOOT = 5f, LEVEE_HEIGHT = 1.1f, LEVEE_RUN = 16f;

        //--- The country. Its own terms come in over LAND_RAMP past the front's foot; it rises over LAND_RISE_RUN from the
        //field's mean reach; the hills between HILLS_FROM and HILLS_TO past the edge, the ridges between RIDGES_FROM and
        //RIDGES_TO; each a sum of two octaves at these frequencies and offsets
        internal const float LAND_RAMP = 30f, LAND_RISE_RUN = 600f;
        internal const float HILLS_FROM = 40f, HILLS_TO = 260f, HILL_F1 = 0.0075f, HILL_O1 = 1.3f, HILL_F2 = 0.019f, HILL_O2 = 5.1f;
        internal const float RIDGES_FROM = 250f, RIDGES_TO = 650f, RIDGE_F1 = 0.0028f, RIDGE_O1 = 9.4f, RIDGE_F2 = 0.0071f, RIDGE_O2 = 2.6f;
        internal const float RIDGE_F3 = 0.018f, RIDGE_O3 = 5.7f, RIDGE_W3 = 0.12f;

        //--- The dormant cone: its flank plateaus at DORMANT_SUMMIT of its radius (the main cone's clamp, for its reason), on
        //this profile, with a shallow worn hollow in the plateau
        internal const float DORMANT_SUMMIT = 0.1f, DORMANT_PROFILE = 1.6f, DORMANT_HOLLOW = 0.04f;

        //--- The valley: the land's hills ease down into it between the bank and VALLEY_HALF (wider by VALLEY_WIDENING in the
        //ridges, or the ranges would stand as walls over the water); the gravel bar rises GRAVEL_RISE over BANK_RUN from the
        //water and the flood plain on at FLOOD_SLOPE; RIVER_SINK is how far under the land beside it the water lies
        internal const float VALLEY_HALF = 80f, VALLEY_WIDENING = 140f, BANK_RUN = 9f, GRAVEL_RISE = 0.8f, FLOOD_SLOPE = 0.1f;
        internal const float RIVER_SINK = 3.5f;

        //--- The meanders: two sines of the distance along the course (world units, radians per unit, phase)
        internal const float MEANDER_A1 = 20f, MEANDER_K1 = 0.0273f, MEANDER_P1 = 0.7f;
        internal const float MEANDER_A2 = 7f, MEANDER_K2 = 0.0661f, MEANDER_P2 = 2.9f;

        //--- The woods: a two-octave field thresholded into belts, kept off the front, the river and the village's fields
        internal const float FOREST_F1 = 0.0062f, FOREST_O1 = 4.4f, FOREST_F2 = 0.019f, FOREST_O2 = 1.9f;
        internal const float FOREST_LOW = 0.04f, FOREST_HIGH = 0.24f, FOREST_FROM = 70f, FOREST_RUN = 50f;
        internal const float FOREST_RIVER_FROM = 16f, FOREST_RIVER_TO = 44f;

        //--- The village's fields reach this far round it; its houses keep to its core
        internal const float FIELDS_REACH = 260f, VILLAGE_CORE = 62f;

        /// <summary>Matched by <c>MAX_RIVER_POINTS</c> in <c>Volcano.fx</c>: the size of the course's uniform array.</summary>
        internal const int MAX_RIVER_POINTS = 28;

        /// <summary>
        /// The river's course, upstream to downstream, in world XZ: out of the far ranges on the left of the arena's far side,
        /// down through the hills to pass the field 120-170 units clear of its edge, past the village, and away into the
        /// ranges on the right. Both ends lie past where the far ground has become the sky (<c>FarField.FAR_FADE_END</c>), so
        /// the river neither starts nor stops anywhere a camera can see.
        /// </summary>
        internal static readonly Vector2[] RIVER_COURSE =
        {
            new(-820f, 1300f), new(-600f, 1110f), new(-440f, 930f), new(-300f, 770f), new(-170f, 610f), new(-60f, 480f),
            new(60f, 395f), new(180f, 365f), new(300f, 385f), new(410f, 460f), new(520f, 600f), new(640f, 800f),
            new(780f, 1040f), new(930f, 1290f),
        };

        private readonly VolcanoSceneConfig _config;

        /// <summary>The rounded course as <c>Volcano.fx</c>'s <c>RiverPath</c> takes it: XZ, the distance along it, and the
        /// water's level there over <see cref="VolcanoSceneConfig.LevelY"/>.</summary>
        public Vector4[] RiverPath { get; } = new Vector4[MAX_RIVER_POINTS];

        /// <summary>How many of <see cref="RiverPath"/>'s slots are the course.</summary>
        public int RiverPoints { get; }

        public VolcanoCountry(VolcanoSceneConfig config)
        {
            _config = config;
            VolcanoCountryConfig country = config.Country;

            //Round the authored corners: one Catmull-Rom midpoint per span, so the valley and the banks have no kink a camera
            //on the hills could see; the meanders do the rest
            int n = RIVER_COURSE.Length;
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                RiverPath[count++] = new Vector4(RIVER_COURSE[i], 0f, 0f);
                if (i == n - 1) break;
                Vector2 p0 = RIVER_COURSE[Math.Max(i - 1, 0)], p1 = RIVER_COURSE[i], p2 = RIVER_COURSE[i + 1], p3 = RIVER_COURSE[Math.Min(i + 2, n - 1)];
                Vector2 mid = Vector2.CatmullRom(p0, p1, p2, p3, 0.5f);
                RiverPath[count++] = new Vector4(mid, 0f, 0f);
            }
            RiverPoints = count;

            //The running length, and the water's level: under the land's rise at each point by RIVER_SINK, and never higher
            //than upstream - a river does not climb, so where the land rises again towards the right-hand ranges the water
            //stays down and the valley deepens round it
            float along = 0f;
            float level = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = new(RiverPath[i].X, RiverPath[i].Y);
                if (i > 0) along += Vector2.Distance(p, new Vector2(RiverPath[i - 1].X, RiverPath[i - 1].Y));
                float coneR = Vector2.Distance(p, config.ConeCenter.ToVector2());
                float rise = country.LandRise * ShaderMath.SmoothStep(0f, LAND_RISE_RUN, coneR - country.FieldReach);
                level = MathF.Min(level, -country.FrontHeight + rise - RIVER_SINK);
                RiverPath[i] = new Vector4(p, along, level);
            }
        }

        //A config's own, for the callers that hold only a config (the tests, the intro shots' mirror): nothing edits a config at
        //runtime, so one solved course per config object is the course
        private static readonly ConditionalWeakTable<VolcanoSceneConfig, VolcanoCountry> _solved = new();

        /// <summary>The country solved for <paramref name="config"/>, once per config object.</summary>
        public static VolcanoCountry Of(VolcanoSceneConfig config) => _solved.GetValue(config, c => new VolcanoCountry(c));

        /// <summary>How far from the cone's axis the young field reaches at this point (<c>Volcano.fx</c>'s <c>LandAt</c>).</summary>
        public float FieldEdge(float x, float z)
        {
            VolcanoCountryConfig country = _config.Country;
            return country.FieldReach + country.FieldLobes
                * (EDGE_W1 * ShaderMath.Noise(new Vector2(x * EDGE_F1 + EDGE_O1, z * EDGE_F1 + EDGE_O1))
                   + EDGE_W2 * ShaderMath.Noise(new Vector2(x * EDGE_F2 + EDGE_O2, z * EDGE_F2 + EDGE_O2))
                   + EDGE_W3 * ShaderMath.Noise(new Vector2(x * EDGE_F3 + EDGE_O3, z * EDGE_F3 + EDGE_O3)));
        }

        /// <summary>How far past the field's edge a point stands, in world units: negative on the field.</summary>
        public float Outside(float x, float z)
        {
            Vector2 cone = _config.ConeCenter.ToVector2();
            float dx = x - cone.X, dz = z - cone.Y;
            return MathF.Sqrt(dx * dx + dz * dz) - FieldEdge(x, z);
        }

        /// <summary><c>Volcano.fx</c>'s <c>FrontProfile</c>: the levee and the front's face, relative to the field's level.</summary>
        public float FrontProfile(float outside)
        {
            float levee = LEVEE_HEIGHT * ShaderMath.SmoothStep(-LEVEE_RUN, -FRONT_LIP, outside);
            float fall = ShaderMath.SmoothStep(-FRONT_LIP, FRONT_FOOT, outside);
            return levee * (1f - fall) - _config.Country.FrontHeight * fall;
        }

        /// <summary>
        /// <c>Volcano.fx</c>'s <c>RiverCourse</c>: the signed distance across the river's meandering centre line, how far along
        /// the course the nearest point is, and the water's level there over <see cref="VolcanoSceneConfig.LevelY"/>.
        /// </summary>
        public (float Across, float Along, float Level) River(float x, float z)
        {
            float best = 1e12f, side = 1f, along = 0f, level = 0f;
            for (int i = 0; i < RiverPoints - 1; i++)
            {
                Vector4 a = RiverPath[i], b = RiverPath[i + 1];
                float ex = b.X - a.X, ez = b.Y - a.Y;
                float wx = x - a.X, wz = z - a.Y;
                float t = Math.Clamp((wx * ex + wz * ez) / (ex * ex + ez * ez), 0f, 1f);
                float qx = wx - ex * t, qz = wz - ez * t;
                float d2 = qx * qx + qz * qz;
                if (d2 < best)
                {
                    best = d2;
                    side = ex * wz - ez * wx < 0f ? -1f : 1f;
                    along = a.Z + (b.Z - a.Z) * t;
                    level = a.W + (b.W - a.W) * t;
                }
            }

            return (side * MathF.Sqrt(best) - MeanderAt(along), along, level);
        }

        /// <summary>How far the river's meandering centre line swings off its course at a distance along it.</summary>
        public static float MeanderAt(float along) =>
            MEANDER_A1 * MathF.Sin(along * MEANDER_K1 + MEANDER_P1) + MEANDER_A2 * MathF.Sin(along * MEANDER_K2 + MEANDER_P2);

        /// <summary><c>Volcano.fx</c>'s <c>Ridge</c>: rounded ranges along the zero contours of a broad noise, over a finer one, with
        /// knolls and saddles along them from a third.</summary>
        private static float Ridge(float x, float z)
        {
            float n1 = ShaderMath.Noise(new Vector2(x * RIDGE_F1 + RIDGE_O1, z * RIDGE_F1 + RIDGE_O1));
            float n2 = ShaderMath.Noise(new Vector2(x * RIDGE_F2 + RIDGE_O2, z * RIDGE_F2 + RIDGE_O2));
            float crest = Math.Clamp(1f - 2.6f * n1 * n1, 0f, 1f);
            return 0.75f * crest * crest + 0.25f * Math.Clamp(1f - 2.6f * n2 * n2, 0f, 1f)
                + RIDGE_W3 * ShaderMath.Noise(new Vector2(x * RIDGE_F3 + RIDGE_O3, z * RIDGE_F3 + RIDGE_O3));
        }

        /// <summary><c>Volcano.fx</c>'s <c>DormantRise</c>: the old cone on the horizon over the land round it.</summary>
        private float DormantRise(float x, float z)
        {
            VolcanoCountryConfig country = _config.Country;
            Vector2 at = country.DormantCone.ToVector2();
            float radius = MathF.Max(country.DormantRadius, 1f);
            float r = MathF.Sqrt((x - at.X) * (x - at.X) + (z - at.Y) * (z - at.Y));
            float t = Math.Clamp(1f - MathF.Max(r, radius * DORMANT_SUMMIT) / radius, 0f, 1f);
            return country.DormantHeight * (MathF.Pow(t, DORMANT_PROFILE) - DORMANT_HOLLOW * ShaderMath.SmoothStep(radius * DORMANT_SUMMIT, 0f, r));
        }

        /// <summary>
        /// <c>Volcano.fx</c>'s <c>CountryHeight</c>, term for term: <paramref name="relative"/> (the cone's massing and the front,
        /// over the field's level) with the land added past the front and the river's valley cut into it. Only for a point past
        /// the field's edge (<paramref name="outside"/> above 0); on the field the shader adds nothing.
        /// </summary>
        public float CountryHeight(float x, float z, float coneR, float outside, float relative)
        {
            VolcanoCountryConfig country = _config.Country;
            (float across, float _, float level) = River(x, z);
            float d = MathF.Abs(across);
            float land = ShaderMath.SmoothStep(0f, LAND_RAMP, outside);

            float rise = country.LandRise * ShaderMath.SmoothStep(0f, LAND_RISE_RUN, coneR - country.FieldReach);
            float hills = country.HillHeight * ShaderMath.SmoothStep(HILLS_FROM, HILLS_TO, outside)
                * (ShaderMath.Noise(new Vector2(x * HILL_F1 + HILL_O1, z * HILL_F1 + HILL_O1))
                   + 0.5f * ShaderMath.Noise(new Vector2(x * HILL_F2 + HILL_O2, z * HILL_F2 + HILL_O2)));
            float ridgeShare = ShaderMath.SmoothStep(RIDGES_FROM, RIDGES_TO, outside);
            float ridges = country.RidgeHeight * ridgeShare * Ridge(x, z);
            float valleyHalf = VALLEY_HALF + VALLEY_WIDENING * ridgeShare;
            float shoulder = ShaderMath.SmoothStep(country.RiverHalfWidth + BANK_RUN, valleyHalf, d);
            float ground = rise + (hills + ridges) * shoulder;
            ground = MathF.Max(ground, rise + DormantRise(x, z));

            relative += land * ground;

            float bank = level + GRAVEL_RISE * ShaderMath.SmoothStep(country.RiverHalfWidth, country.RiverHalfWidth + BANK_RUN, d)
                + FLOOD_SLOPE * MathF.Max(d - country.RiverHalfWidth - BANK_RUN, 0f);
            relative += (MathF.Min(relative, bank) - relative) * (land * ShaderMath.SmoothStep(valleyHalf, valleyHalf * 0.5f, d));
            relative += (level - relative) * (land * (1f - ShaderMath.SmoothStep(country.RiverHalfWidth - 0.5f, country.RiverHalfWidth + 0.5f, d)));
            return relative;
        }

        /// <summary>
        /// <c>Volcano.fx</c>'s <c>ForestCover</c>: how wooded the land is at a point, 0 to 1 - the belts the planting stands its
        /// spruces in and the shader darkens to a forest floor (and, past the planting, to a wooded hill). Not mirrored to the
        /// last bit and needs not be: it is a mask, not a height.
        /// </summary>
        public float ForestCover(float x, float z, float outside, float riverDistance)
        {
            VolcanoCountryConfig country = _config.Country;
            float n = ShaderMath.Noise(new Vector2(x * FOREST_F1 + FOREST_O1, z * FOREST_F1 + FOREST_O1))
                + 0.5f * ShaderMath.Noise(new Vector2(x * FOREST_F2 + FOREST_O2, z * FOREST_F2 + FOREST_O2));
            Vector2 village = country.Village.ToVector2();
            float villageD = MathF.Sqrt((x - village.X) * (x - village.X) + (z - village.Y) * (z - village.Y));
            return ShaderMath.SmoothStep(FOREST_LOW, FOREST_HIGH, n)
                * ShaderMath.SmoothStep(FOREST_FROM, FOREST_FROM + FOREST_RUN, outside)
                * ShaderMath.SmoothStep(country.RiverHalfWidth + FOREST_RIVER_FROM, country.RiverHalfWidth + FOREST_RIVER_TO, riverDistance)
                * ShaderMath.SmoothStep(FIELDS_REACH * 0.7f, FIELDS_REACH * 1.05f, villageD);
        }
    }
}

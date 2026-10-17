using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The brook's groove and the pond's crater</b> (#892), cut into the meadow's field: the CPU copy of the carve in
    /// <c>Meadow.fx</c> (<c>CarvedHeight</c> there), term for term, as <see cref="MeadowPath"/> is of its lines. The owner:
    /// the brook and the pond "have no real geometric depth - the brook should be a groove cut into the ground and the pond
    /// a kind of irregular crater - only inside them is then the water surface". Change the one and the other has to follow,
    /// then run the Testbed's <c>mirrorcheck</c> on the meadow.
    /// <para>
    /// <b>A cut is the lower of the field and a channel.</b> The brook's channel is a flat bed under level water, a wall
    /// climbing steeply past the water's edge and on above any ground near it, so <c>min</c> with the field puts the lip
    /// wherever the wall meets the hill, higher on the uphill side as a real cut is. Its water lies <see cref="BROOK_DROP"/>
    /// under the LOWEST of three samples across it (<see cref="BrookReference"/>), so a cross slope can never tip the water
    /// over the lower lip. The pond's crater is a bowl round the shore <see cref="MeadowPath.PondOutline"/> draws: a shelf
    /// and a deeper middle under the water, a short steep step of earth at the waterline, a grassy bank whose width varies
    /// round it, and a raised lip at its top that comes and goes. The lower of two is rounded (<see cref="CarveMin"/>), so a
    /// lip is a curve. Sines, hermite ramps, min, max and products only, so the GPU and this agree to the last few
    /// thousandths (#598: no gradient noise in a mirrored height).
    /// </para>
    /// <para>
    /// <b>Only geometry fine enough can carry it</b>, and the meadow's camera grid is 5.5 units a cell: a cut narrower than
    /// several cells falls between its vertices (the icesheet's lesson). So the coarse grid is drawn without it and has a
    /// hole where it is, and a patch of its own (<see cref="MeadowPatchGeometry"/>) covers the pond and the brook to
    /// <see cref="BROOK_CARVE_END"/>, where the groove has shallowed back into the brook painted on the field.
    /// </para>
    /// </summary>
    public static class MeadowCarve
    {
        /// <summary>How far the brook's water lies under the lower of the ground at its centreline and either side of it.</summary>
        public const float BROOK_DROP = 1.25f;

        /// <summary>How far the brook's flat bed lies under its water.</summary>
        public const float BROOK_BED = 0.55f;

        /// <summary>How far over its water the channel's wall has climbed at <see cref="BROOK_WALL_TO"/>, before the tail.</summary>
        public const float BROOK_TOP = 2.2f;

        /// <summary>Where the wall's hermite starts and ends, in world units past the water's edge: the bed is flat inside
        /// <see cref="BROOK_WALL_FROM"/>, the water's edge is where the wall crosses the water (about 0), and the lip about a
        /// quarter of a unit past it - a wall of about four in one.</summary>
        public const float BROOK_WALL_FROM = -0.25f, BROOK_WALL_TO = 0.6f;

        /// <summary>How fast a channel goes on climbing past its hermite (per world unit), so that the min with the field
        /// always meets it however steep the hill beside it.</summary>
        public const float CARVE_TAIL = 3f;

        /// <summary>How far either side of the brook's centreline the two other samples of its reference height stand.</summary>
        public const float BROOK_REFERENCE = 2.4f;

        /// <summary>Where the groove starts to shallow, and where it has become the painted brook again, as distances from
        /// the arena. The reeds stand along it to 420.</summary>
        public const float BROOK_CARVE_FADE = 400f, BROOK_CARVE_END = 470f;

        /// <summary>How far past <see cref="BROOK_CARVE_END"/> the coarse grid's hole and the patch run (the patch to the end
        /// of the block this falls in): the patch eases onto the coarse grid's own surface over it.</summary>
        public const float BROOK_HOLE_END = BROOK_CARVE_END + 2f, BROOK_PATCH_END = BROOK_HOLE_END + 1.5f;

        /// <summary>How far to the side the groove can reach (measured: 2.86 on the steepest cross slope), where the coarse
        /// grid's hole ends, and how far the patch reaches past it - every block of it with a vertex this near is drawn
        /// (<see cref="MeadowPatchGeometry.LOD_BLOCK"/>) - all in world units of lateral.</summary>
        public const float BROOK_CARVE_HALF = 3.2f, BROOK_HOLE_HALF = 4.6f, BROOK_PATCH_HALF = 5.2f;

        /// <summary>How far the pond's water lies under its levelled ground - the brook's own drop, so the two waters meet
        /// at the mouth at one level.</summary>
        public const float POND_DROP = BROOK_DROP;

        /// <summary>The pond's bed: a shelf <see cref="POND_SHELF_DEPTH"/> under the water within <see cref="POND_SHELF"/> of
        /// the shore, falling <see cref="POND_DEEP"/> further over the next <see cref="POND_DEEP_OVER"/> units.</summary>
        public const float POND_SHELF_DEPTH = 0.35f, POND_SHELF = 3f, POND_DEEP = 1f, POND_DEEP_OVER = 6f;

        /// <summary>The bank: an earth step this high at the waterline, then grass up to <see cref="POND_RISE"/> over the
        /// levelled ground across <see cref="POND_BANK"/> units (varied round the pond), and a lip up to
        /// <see cref="POND_LIP"/> high at its top.</summary>
        public const float POND_STEP = 0.35f, POND_RISE = 0.6f, POND_BANK = 3f, POND_LIP = 0.35f;

        /// <summary>How far from the pond's centre the crater can reach (measured: 17.6), where the coarse grid's hole ends,
        /// and how far the patch reaches past it (every block of it with a vertex this near).</summary>
        public const float POND_CARVE_RADIUS = 19f, POND_HOLE_RADIUS = 21.5f, POND_PATCH_RADIUS = 22.4f;

        /// <summary>How far in height the lower of the field and a channel is rounded (<see cref="CarveMin"/>).</summary>
        public const float CARVE_ROUND = 0.35f;

        /// <summary>Far below or above anything: the answer of a channel that is not there.</summary>
        private const float NONE = 1e4f;

        /// <summary>Half the brook's water width at distance <paramref name="d"/> from the arena: it narrows and widens over
        /// tens of units and its walls waver over a few (1.24 to 2.14 at the default width).</summary>
        public static float BrookWaterHalf(float d, MeadowSceneConfig config) =>
            config.BrookWidth * 0.5f * (1f + 0.12f * MathF.Sin(d * 0.23f + 0.4f) + 0.07f * MathF.Sin(d * 0.61f + 2f)
                + 0.05f * MathF.Sin(d * 1.9f + 0.7f) + 0.03f * MathF.Sin(d * 3.7f + 2.9f));

        /// <summary>How much of the groove there is at distance <paramref name="d"/>: 1, shallowing to 0 past
        /// <see cref="BROOK_CARVE_FADE"/>.</summary>
        public static float BrookPresence(float d) => 1f - ShaderMath.SmoothStep(BROOK_CARVE_FADE, BROOK_CARVE_END, d);

        /// <summary>The brook's reference height at distance <paramref name="d"/>: the lowest of the field on its centreline
        /// and <see cref="BROOK_REFERENCE"/> either side.</summary>
        public static float BrookReference(float d, MeadowSceneConfig config)
        {
            (float cx, float cz) = MeadowPath.BrookPoint(d, 0f, config);
            (float lx, float lz) = MeadowPath.BrookPoint(d, -BROOK_REFERENCE, config);
            (float rx, float rz) = MeadowPath.BrookPoint(d, BROOK_REFERENCE, config);
            float centre = TerrainMirror.MeadowGround(cx, cz, config);
            return MathF.Min(centre, MathF.Min(TerrainMirror.MeadowGround(lx, lz, config), TerrainMirror.MeadowGround(rx, rz, config)));
        }

        /// <summary>The brook's water level at distance <paramref name="d"/>: what the water surface is drawn at, and what a
        /// lens over the water is lifted from.</summary>
        public static float BrookWaterLevel(float d, MeadowSceneConfig config) =>
            BrookReference(d, config) - BROOK_DROP * BrookPresence(d);

        /// <summary>The channel's surface <paramref name="s"/> units past the water's edge (negative over the water), the
        /// water at <paramref name="water"/>.</summary>
        public static float BrookChannel(float s, float water) =>
            water - BROOK_BED + (BROOK_BED + BROOK_TOP) * ShaderMath.SmoothStep(BROOK_WALL_FROM, BROOK_WALL_TO, s)
                + CARVE_TAIL * MathF.Max(s - BROOK_WALL_TO, 0f);

        /// <summary>The level the pond's ground is eased to (<c>PondLevel</c> in the shader).</summary>
        public static float PondLevel(MeadowSceneConfig config)
        {
            (float px, float pz) = MeadowPath.PondCentre(config);
            return TerrainMirror.MeadowNatural(px, pz, config);
        }

        /// <summary>The pond's water level.</summary>
        public static float PondWaterLevel(MeadowSceneConfig config) => PondLevel(config) - POND_DROP;

        /// <summary>The width of the pond's bank at bearing <paramref name="a"/> round its centre: 1.2 to 4.8 units, so the
        /// crater is steep on one side and a gentle bowl on another.</summary>
        public static float PondBankWidth(float a) =>
            POND_BANK * (1f + 0.35f * MathF.Sin(4f * a + 0.9f) + 0.25f * MathF.Sin(7f * a + 2.6f));

        /// <summary>The crater's surface over the pond's water, <paramref name="e"/> units outside the shore (negative in the
        /// water), with a bank <paramref name="bank"/> wide.</summary>
        public static float PondCrater(float e, float bank) =>
            -POND_SHELF_DEPTH * (1f - ShaderMath.SmoothStep(-POND_SHELF, 0f, e))
                - POND_DEEP * (1f - ShaderMath.SmoothStep(-POND_SHELF - POND_DEEP_OVER, -POND_SHELF, e))
                + POND_STEP * ShaderMath.SmoothStep(-0.1f, 0.35f, e)
                + (POND_DROP + POND_RISE - POND_STEP) * ShaderMath.SmoothStep(0.2f, bank, e)
                + CARVE_TAIL * MathF.Max(e - bank, 0f);

        /// <summary>The raised lip round the crater's top at bearing <paramref name="a"/>: a hermite bump centred where the bank
        /// meets the levelled ground, its height varying round the pond to nothing in places.</summary>
        public static float PondLip(float e, float bank, float a)
        {
            float height = POND_LIP * (0.6f + 0.4f * MathF.Sin(3f * a + 1.7f) + 0.25f * MathF.Sin(5f * a + 0.2f));
            float crest = 0.2f + 0.6f * (bank - 0.2f) + 0.4f;
            return height * (1f - ShaderMath.SmoothStep(0f, 1.3f, MathF.Abs(e - crest)));
        }

        /// <summary>
        /// The lower of two heights, rounded over <see cref="CARVE_ROUND"/> (a polynomial smooth minimum): the cut's lip is a
        /// curve the grass rolls over, not a crease, which the patch's triangles drew as a zigzag along the wall's top.
        /// </summary>
        public static float CarveMin(float a, float b)
        {
            float h = MathF.Max(CARVE_ROUND - MathF.Abs(a - b), 0f) / CARVE_ROUND;
            return MathF.Min(a, b) - h * h * CARVE_ROUND * 0.25f;
        }

        /// <summary>
        /// The meadow's ground with the brook's groove and the pond's crater cut into it: <see cref="TerrainMirror.Meadow"/>,
        /// what everything planted stands on. <c>Meadow.fx</c>'s <c>CarvedHeight</c>.
        /// </summary>
        public static float Height(float x, float z, MeadowSceneConfig config) => Height(x, z, config, out _);

        /// <summary>
        /// <see cref="Height(float, float, MeadowSceneConfig)"/>, and the level of the water standing over the point
        /// (<see cref="float.MinValue"/> where there is none): the pond's where the crater is the cut, the brook's where its
        /// channel is. <c>Meadow.fx</c>'s <c>CarvedHeight</c> hands the same down to the pixel shader.
        /// </summary>
        public static float Height(float x, float z, MeadowSceneConfig config, out float water)
        {
            float height = TerrainMirror.MeadowGround(x, z, config);
            water = float.MinValue;

            //The pond: its lip raises the field, its crater is a surface the field is cut down to
            float crater = NONE;
            (float px, float pz) = MeadowPath.PondCentre(config);
            float qx = x - px, qz = z - pz;
            float r2 = qx * qx + qz * qz;
            if (r2 < POND_CARVE_RADIUS * POND_CARVE_RADIUS)
            {
                float a = MathF.Atan2(qz, qx);
                float e = MathF.Sqrt(r2) - config.PondRadius * MeadowPath.PondOutline(a);
                float bank = PondBankWidth(a);
                height += PondLip(e, bank, a);
                crater = PondLevel(config) - POND_DROP + PondCrater(e, bank);
                water = PondLevel(config) - POND_DROP;
            }

            //The brook: the field cut down to its channel, less as it shallows towards its painted end
            float lateral = MathF.Abs(MeadowPath.BrookLateral(x, z, config));
            if (lateral < BROOK_CARVE_HALF)
            {
                float d = MathF.Sqrt(x * x + z * z);
                float presence = BrookPresence(d);
                float reference = BrookReference(d, config);
                float cut = presence * (height - CarveMin(height, BrookChannel(lateral - BrookWaterHalf(d, config), reference - BROOK_DROP)));
                height -= cut;
                if (cut > 0f && height < crater) water = reference - BROOK_DROP * presence;
            }

            return CarveMin(height, crater);
        }
    }
}

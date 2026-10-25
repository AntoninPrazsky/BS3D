using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The brook's swale and the pond's crater</b> (#892), cut into the meadow's field: the CPU copy of the carve in
    /// <c>Meadow.fx</c> (<c>CarvedHeight</c> there), term for term, as <see cref="MeadowPath"/> is of its lines. The owner:
    /// the brook and the pond "have no real geometric depth - the brook should be a groove cut into the ground and the pond
    /// a kind of irregular crater - only inside them is then the water surface"; and of the first groove, which had earth
    /// walls four in one, "it looks as if an excavator dug a hole there; a natural brook looks a little different". Change
    /// the one and the other has to follow, then run the Testbed's <c>mirrorcheck</c> on the meadow.
    /// <para>
    /// <b>The brook lies in a wide shallow swale</b>, as the references draw a meadow brook: the bed under level water
    /// shelving up to its edge (<see cref="BrookBed"/>), a margin of gravel just over the water, wide on one side where the
    /// other's is narrow (<see cref="BrookMargin"/>), a low step of earth at its back, and from there a grassy bank easing up
    /// to the field (<see cref="BrookBank"/>), its top a low rounded lip that comes and goes (<see cref="BrookLevee"/>). The
    /// bank is a hermite blend from the step's top to the field's OWN height, so it meets the field with no crease whatever
    /// the hill beside it does, and widens where it has further to climb, so the uphill bank is not a wall. Its water lies
    /// <see cref="BROOK_DROP"/> under the LOWEST of three samples across it (<see cref="BrookReference"/>), so a cross slope
    /// can never tip the water over the lower bank. The pond's crater is a bowl round the shore
    /// <see cref="MeadowPath.PondOutline"/> draws: a shelf and a deeper middle under the water, a short steep step of earth at
    /// the waterline, a grassy bank whose width varies round it, and a raised lip at its top that comes and goes. The lower of
    /// two is rounded (<see cref="CarveMin"/>), so a lip is a curve. Sines, hermite ramps, min, max and products only, so the
    /// GPU and this agree to the last few thousandths (#598: no gradient noise in a mirrored height).
    /// </para>
    /// <para>
    /// <b>Only geometry fine enough can carry it</b>, and the meadow's camera grid is 5.5 units a cell: a bank two or three
    /// units wide falls between its vertices (the icesheet's lesson). So the coarse grid is drawn without it and has a hole
    /// where it is, and a patch of its own (<see cref="MeadowPatchGeometry"/>) covers the pond and the brook to
    /// <see cref="BROOK_CARVE_END"/>, its spring.
    /// </para>
    /// <para>
    /// <b>The water falls all the way from its spring to the pond</b> (#979, the owner: "the water does not flow downhill").
    /// Its level follows the ground, so it can only fall towards the pond if the ground under its course does, and the
    /// course was chosen so it does: the pond lies at the foot of the valley the brook comes down
    /// (<see cref="MeadowPath.BROOK_START"/>), and the spring stands on the hillside short of the crest
    /// (<see cref="BROOK_CARVE_END"/>). Over the crest the old brook, painted on the field, climbed sixteen units to the
    /// next valley and on over the hills, and the pond had lain on the slope a unit and a half over the valley's foot.
    /// <c>MeadowCarveTests.TheBrookFallsAllTheWayToThePond</c> holds it.
    /// </para>
    /// </summary>
    public static class MeadowCarve
    {
        /// <summary>How far the brook's water lies under the lower of the ground at its centreline and either side of it.</summary>
        public const float BROOK_DROP = 1.25f;

        /// <summary>How far the brook's bed lies under its water in the middle, and how far in from the water's edge it shelves
        /// up to it.</summary>
        public const float BROOK_BED = 0.55f, BROOK_SHALLOWS = 0.8f;

        /// <summary>The gravel margin: how far over the water it lies, and its width past the water's edge, narrowest and
        /// widest (<see cref="BrookMargin"/>).</summary>
        public const float BROOK_MARGIN_RISE = 0.1f, BROOK_MARGIN_MIN = 0.1f, BROOK_MARGIN_MAX = 0.9f;

        /// <summary>The low earth step at the margin's back: its height and the run it climbs over.</summary>
        public const float BROOK_STEP = 0.22f, BROOK_STEP_RUN = 0.3f;

        /// <summary>The grassy bank from the step's top to the field: its mean width on level ground (varied along the brook
        /// and side to side, <see cref="BrookBank"/>); at least <see cref="BROOK_BANK_PER_RISE"/> times the height it climbs,
        /// so the uphill bank of a cross slope widens instead of steepening (its steepest is about 1.5 over that, 0.75); and
        /// never wider than <see cref="BROOK_BANK_MAX"/>.</summary>
        public const float BROOK_BANK = 2.4f, BROOK_BANK_PER_RISE = 2f, BROOK_BANK_MAX = 4.4f;

        /// <summary>The highest the lip at the bank's top stands over the bank's own blend (<see cref="BrookLevee"/>).</summary>
        public const float BROOK_LEVEE = 0.12f;

        /// <summary>How fast the pond's crater goes on climbing past its bank (per world unit), so that the min with the field
        /// always meets it however steep the hill beside it.</summary>
        public const float CARVE_TAIL = 3f;

        /// <summary>How far either side of the brook's centreline the two other samples of its reference height stand.</summary>
        public const float BROOK_REFERENCE = 2.4f;

        /// <summary>Where the swale starts to shallow, and its spring, where it has shallowed into the field, as distances from
        /// the arena (#979): short of the crest the brook comes down, 206 out on the default meadow. Its water narrows and
        /// fades with it, and its reeds and stones stop at the first.</summary>
        public const float BROOK_CARVE_FADE = 168f, BROOK_CARVE_END = 190f;

        /// <summary>How far past <see cref="BROOK_CARVE_END"/> the coarse grid's hole and the patch run (the patch to the end
        /// of the block this falls in): the patch eases onto the coarse grid's own surface over it.</summary>
        public const float BROOK_HOLE_END = BROOK_CARVE_END + 2f, BROOK_PATCH_END = BROOK_HOLE_END + 1.5f;

        /// <summary>How far to the side the swale can reach (the widest water, margin, step and bank at their widest stretch:
        /// 7.8), where the coarse grid's hole ends, and how far the patch reaches past it - every block of it with a vertex
        /// this near is drawn (<see cref="MeadowPatchGeometry.LOD_BLOCK"/>) - all in world units of lateral.</summary>
        public const float BROOK_CARVE_HALF = 7.9f, BROOK_HOLE_HALF = 9.3f, BROOK_PATCH_HALF = 9.9f;

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

        /// <summary>
        /// <b>The mouth</b> (#970), where the brook runs into the pond as one water. The owner saw "a sharp straight line in the
        /// water separating the brook from the pond ... here it must connect smoothly". The two water meshes still meet at the
        /// shore, each clipping the other's side, but nothing the water is shaded by depends on which drew a pixel
        /// (<see cref="WaterShading"/>): how far up the brook from its mouth its current starts to slacken, how far into the
        /// pond the current carries, how fast it fans out there (per unit in) and over how much its sides fade.
        /// </summary>
        public const float MOUTH_UP = 3f, MOUTH_INTO = 7f, MOUTH_SPREAD = 0.5f, MOUTH_SOFT = 2f;

        /// <summary>How far either side of the centreline the current is whole at the mouth: the brook's water mesh's own
        /// half-width, so the current is whole wherever that mesh draws.</summary>
        public const float MOUTH_CURRENT_HALF = MeadowPatchGeometry.WATER_HALF;

        /// <summary>How far in height the lower of the field and a channel is rounded (<see cref="CarveMin"/>).</summary>
        public const float CARVE_ROUND = 0.35f;

        /// <summary>Far below or above anything: the answer of a channel that is not there.</summary>
        private const float NONE = 1e4f;

        /// <summary>Half the brook's water width at distance <paramref name="d"/> from the arena: it narrows and widens over
        /// tens of units and its edges waver over a few (1.24 to 2.14 at the default width), and it narrows to two fifths of
        /// that towards its spring (<see cref="BrookPresence"/>, #979).</summary>
        public static float BrookWaterHalf(float d, MeadowSceneConfig config) =>
            config.BrookWidth * 0.5f * (1f + 0.12f * MathF.Sin(d * 0.23f + 0.4f) + 0.07f * MathF.Sin(d * 0.61f + 2f)
                + 0.05f * MathF.Sin(d * 1.9f + 0.7f) + 0.03f * MathF.Sin(d * 3.7f + 2.9f)) * (0.4f + 0.6f * BrookPresence(d));

        /// <summary>The gravel margin's width past the water's edge at distance <paramref name="d"/> on side
        /// <paramref name="side"/> (-1 or 1 of the centreline): wide on one side where the other's is narrow, swapping over
        /// tens of units, as a brook leaves a gravel bar on the inside of a bend and cuts its outside.</summary>
        public static float BrookMargin(float d, float side) =>
            BROOK_MARGIN_MIN + (BROOK_MARGIN_MAX - BROOK_MARGIN_MIN)
                * ShaderMath.SmoothStep(-0.6f, 0.6f, side * (0.6f * MathF.Sin(d * 0.071f + 0.8f) + 0.4f * MathF.Sin(d * 0.19f + 2.1f)));

        /// <summary>The grassy bank's width on level ground at distance <paramref name="d"/> on side <paramref name="side"/>:
        /// 1.8 to 3.0 at <see cref="BROOK_BANK"/>, each side on its own wobble.</summary>
        public static float BrookBank(float d, float side) =>
            BROOK_BANK * (1f + 0.16f * MathF.Sin(d * 0.053f + 1.7f + 1.3f * side) + 0.08f * MathF.Sin(d * 0.137f + 0.4f + 2.2f * side));

        /// <summary>How high the lip at the bank's top stands at distance <paramref name="d"/> on side <paramref name="side"/>:
        /// up to <see cref="BROOK_LEVEE"/>, and nothing over long stretches.</summary>
        public static float BrookLevee(float d, float side) =>
            BROOK_LEVEE * ShaderMath.SmoothStep(-0.2f, 0.8f, 0.6f * MathF.Sin(d * 0.087f + 0.3f + 1.9f * side) + 0.4f * MathF.Sin(d * 0.23f + 1.2f + 0.7f * side));

        /// <summary>The brook's bed and margin <paramref name="s"/> units past the water's edge (negative over the water), over
        /// the water's own level, with a margin <paramref name="margin"/> wide: the bed shelving up to the edge, the margin
        /// just over the water, and the earth step at its back.</summary>
        public static float BrookBed(float s, float margin) =>
            -BROOK_BED * (1f - ShaderMath.SmoothStep(-BROOK_SHALLOWS, 0f, s))
                + BROOK_MARGIN_RISE * ShaderMath.SmoothStep(-0.05f, 0.15f, s)
                + BROOK_STEP * ShaderMath.SmoothStep(margin, margin + BROOK_STEP_RUN, s);

        /// <summary>
        /// The brook's swale at a point of the field <paramref name="field"/> high, <paramref name="lateral"/> off the
        /// centreline (signed), <paramref name="d"/> from the arena, its water at <paramref name="water"/>: the bed and margin
        /// (<see cref="BrookBed"/>) blended into the field across the bank, plus the lip at its top.
        /// </summary>
        public static float BrookSwale(float field, float lateral, float d, float water, MeadowSceneConfig config)
        {
            float side = lateral < 0f ? -1f : 1f;
            float s = MathF.Abs(lateral) - BrookWaterHalf(d, config);
            float margin = BrookMargin(d, side);
            float bed = water + BrookBed(s, margin);
            float foot = margin + BROOK_STEP_RUN;
            float bank = MathF.Min(MathF.Max(BrookBank(d, side), (field - water - BROOK_MARGIN_RISE - BROOK_STEP) * BROOK_BANK_PER_RISE), BROOK_BANK_MAX);
            float up = ShaderMath.SmoothStep(foot, foot + bank, s);
            float lip = ShaderMath.SmoothStep(0.35f, 0.8f, up) * (1f - ShaderMath.SmoothStep(0.85f, 1f, up));
            return CarveMin(field, bed) + (field - CarveMin(field, bed)) * up + BrookLevee(d, side) * lip;
        }

        /// <summary>How far past the water's edge the bank counts as the brook's own at distance <paramref name="d"/> on side
        /// <paramref name="side"/>: the margin and the earth step, where a stone or a reed belongs and a grass tuft does not.</summary>
        public static float BrookShore(float d, float side) => BrookMargin(d, side) + BROOK_STEP_RUN;

        /// <summary>Whether the point is over the brook's water, its margin or its earth step (the water's wavering width,
        /// not the mean one), where a tuft would stand with its base in the water or on the gravel.</summary>
        public static bool OnBrookBank(float x, float z, MeadowSceneConfig config)
        {
            float lateral = MeadowPath.BrookLateral(x, z, config);
            if (MathF.Abs(lateral) >= BROOK_CARVE_HALF) return false;
            float d = MathF.Sqrt(x * x + z * z);
            return MathF.Abs(lateral) < BrookWaterHalf(d, config) + BrookShore(d, lateral < 0f ? -1f : 1f) + 0.2f;
        }

        /// <summary>How much of the swale there is at distance <paramref name="d"/>: 1, shallowing to 0 past
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

        /// <summary>The level the pond's ground is eased to (<c>PondLevel</c> in the shader).</summary>
        public static float PondLevel(MeadowSceneConfig config)
        {
            (float px, float pz) = MeadowPath.PondCentre(config);
            return TerrainMirror.MeadowNatural(px, pz, config);
        }

        /// <summary>The pond's water level.</summary>
        public static float PondWaterLevel(MeadowSceneConfig config) => PondLevel(config) - POND_DROP;

        /// <summary>The distance from the arena at which the brook's centreline crosses the pond's shore: its mouth.</summary>
        public static float BrookMouth(MeadowSceneConfig config)
        {
            float inside = config.ClearingRadius * MeadowPath.BROOK_START, outside = inside + POND_CARVE_RADIUS;
            for (int i = 0; i < 40; i++)
            {
                float d = (inside + outside) * 0.5f;
                (float x, float z) = MeadowPath.BrookPoint(d, 0f, config);
                if (MeadowPath.PondShore(x, z, config) < 0f) inside = d;
                else outside = d;
            }
            return (inside + outside) * 0.5f;
        }

        /// <summary>
        /// What the water at (x, z) is shaded by (<c>Meadow.fx</c>'s <c>MeadowWaterPS</c>): how still it is (0 the brook's
        /// running ripples, 1 the pond's calm), how deep over the bed it is (what its cover is worked out from), and how much of
        /// the swale's shade on the sun (<c>GrooveSun</c>) it keeps. The pond's mesh draws it inside the shore and the brook's
        /// outside; <paramref name="mouth"/> is <see cref="BrookMouth"/>.
        /// </summary>
        public static (float Still, float Depth, float GrooveShade) WaterShading(float x, float z, float mouth, MeadowSceneConfig config)
        {
            float shore = MeadowPath.PondShore(x, z, config);
            (float px, float pz) = MeadowPath.PondCentre(config);
            float bank = PondBankWidth(MathF.Atan2(z - pz, x - px));
            float d = MathF.Sqrt(x * x + z * z);
            float lateral = MeadowPath.BrookLateral(x, z, config);
            float still, depth;
            if (shore < 0f)
            {
                //The pond's: still but for the brook's current near the mouth, over the deeper of its crater and the brook's
                //channel running on into its shallows
                still = 1f;
                depth = -PondCrater(shore, bank);
                if (d > mouth - MOUTH_INTO)
                {
                    still = 1f - MouthCurrent(d, lateral, mouth);
                    float s = MathF.Abs(lateral) - BrookWaterHalf(d, config);
                    if (s < 0f) depth = MathF.Max(depth, -BrookBed(s, BROOK_MARGIN_MAX));
                }
                return (still, MathF.Max(depth, 0f), 0f);
            }

            //The brook's: running, over its bed and, at the mouth, the pond's crater where that is the deeper
            still = 0f;
            depth = -BrookBed(MathF.Abs(lateral) - BrookWaterHalf(d, config), BROOK_MARGIN_MAX);
            if (d < mouth + MOUTH_UP)
            {
                still = 1f - MouthCurrent(d, lateral, mouth);
                depth = MathF.Max(depth, BrookWaterLevel(d, config) - PondWaterLevel(config) - PondCrater(shore, bank));
            }
            return (still, MathF.Max(depth, 0f), GrooveShadeShare(shore));
        }

        /// <summary>How much of the brook's current there is at distance <paramref name="d"/> from the arena,
        /// <paramref name="lateral"/> off its centreline, its mouth at <paramref name="mouth"/>: all of it on the brook,
        /// slackening over its last <see cref="MOUTH_UP"/> units, and carried <see cref="MOUTH_INTO"/> units into the pond in a
        /// tongue that fans out and fades; none on the rest of the pond.</summary>
        public static float MouthCurrent(float d, float lateral, float mouth)
        {
            float fan = MOUTH_CURRENT_HALF + MOUTH_SPREAD * MathF.Max(mouth - d, 0f);
            return ShaderMath.SmoothStep(mouth - MOUTH_INTO, mouth + MOUTH_UP, d)
                * (1f - ShaderMath.SmoothStep(fan, fan + MOUTH_SOFT, MathF.Abs(lateral)));
        }

        /// <summary>How much of the swale's shade on the sun (<c>GrooveSun</c>) is kept <paramref name="shore"/> units outside
        /// the pond's shore: all of it from <see cref="MOUTH_UP"/> out, none at the shore, where the swale's bank has opened
        /// into the crater's and the pond's water, lit whole, takes over.</summary>
        public static float GrooveShadeShare(float shore) => ShaderMath.SmoothStep(0f, MOUTH_UP, shore);

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
        /// The meadow's ground with the brook's swale and the pond's crater cut into it: <see cref="TerrainMirror.Meadow"/>,
        /// what everything planted stands on. <c>Meadow.fx</c>'s <c>CarvedHeight</c>.
        /// </summary>
        public static float Height(float x, float z, MeadowSceneConfig config) => Height(x, z, config, out _);

        /// <summary>
        /// <see cref="Height(float, float, MeadowSceneConfig)"/>, and the level of the water standing over the point
        /// (<see cref="float.MinValue"/> where there is none): the pond's where the crater is the cut, the brook's where its
        /// swale is. <c>Meadow.fx</c>'s <c>CarvedHeight</c> hands the same down to the pixel shader.
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

            //The brook: the field eased down into its swale, less as it shallows towards its spring
            float lateral = MeadowPath.BrookLateral(x, z, config);
            if (MathF.Abs(lateral) < BROOK_CARVE_HALF)
            {
                float d = MathF.Sqrt(x * x + z * z);
                float presence = BrookPresence(d);
                float reference = BrookReference(d, config);
                float cut = presence * (height - BrookSwale(height, lateral, d, reference - BROOK_DROP, config));
                height -= cut;
                if (cut > 0f && height < crater) water = reference - BROOK_DROP * presence;
            }

            return CarveMin(height, crater);
        }
    }
}

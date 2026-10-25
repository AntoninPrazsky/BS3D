using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The cavern's own shots for a chapter intro's prologue (#559, re-authored for the rebuilt hall in #958): the hall from
    /// high on one side, a glide low over the black lake onto a glowing crystal cluster, an arc round a shaft of light
    /// falling through the vault, and the island from underneath over its own reflection — cut together, then cut to the tour's last
    /// leg. One concrete thing a shot, as the owner asked of every prologue.
    /// <para>
    /// <b>Everything is placed against the hall itself</b> (<see cref="CavernChamber"/>, the CPU geometry the backdrop
    /// draws), and every lens path is checked against it before it is used: a shot rolls its place and keeps it only if
    /// every point of the path stands clear of rock, water, formations and crystals by its margin
    /// (<see cref="CavernChamber.Clearance"/>), with a fallback near the axis where the hall is open air. So no shot can
    /// start inside a column or graze a stalactite, whatever hall the seed builds. Built once when the intro begins;
    /// nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class CavernIntroShots
    {
        //The hall: an arc this far round, at this share of the wall's distance along its bearing and this high over the lake,
        //looking across the hall at a point low over the far water - the lake, the far shore's crystals and the vault over
        //them in one frame
        private const float HALL_RADIUS_SHARE = 0.55f;
        private const float HALL_HEIGHT = 52f;
        private const float HALL_DROP = 8f;
        private const float HALL_ARC_DEGREES = 16f;
        private const float HALL_SECONDS = 3.4f;
        internal const float HALL_MARGIN = 8f;

        //The crystals: a glide this high over the lake towards a cluster, from this far out of it to this far, off to one
        //side of the line straight in by this angle so the cluster turns as it nears
        private const float CRYSTAL_GLIDE_HEIGHT = 4f;
        private const float CRYSTAL_FROM = 85f;
        private const float CRYSTAL_TO = 34f;
        private const float CRYSTAL_SIDE_DEGREES = 22f;
        private const float CRYSTAL_SECONDS = 3.2f;
        internal const float CRYSTAL_MARGIN = 2.5f;
        private const float CRYSTAL_LOW = 8f;

        //The shaft: an arc this far round the beam's landing, this far out and this high over the lake, rising a little, the
        //look on the beam a little over halfway up it
        private const float SHAFT_STAND_OFF = 48f;
        private const float SHAFT_HEIGHT = 8f;
        private const float SHAFT_RISE = 7f;
        private const float SHAFT_ARC_DEGREES = 20f;
        private const float SHAFT_SECONDS = 3.2f;
        internal const float SHAFT_MARGIN = 4f;

        //The underside (#656): a crane up under the island's funnel, over the lake, looking at the glass cone under the rim
        //- the cone, the drum's underside and the lake's light on it, which is what the scene's light being below is for
        private const float UNDER_OUT_FROM = 38f, UNDER_OUT_TO = 31f;
        private const float UNDER_FROM_ABOVE_WATER = 2.6f, UNDER_TO_ABOVE_WATER = 9f;
        private const float UNDER_LOOK_Y = -19f;
        private const float UNDER_FOV_DEGREES = 62f;
        private const float UNDER_SECONDS = 3.0f;

        //THE PASSAGE (#984). The owner: "the cave also has access tunnels at the edges - the first shot could be from such a
        //tunnel, the camera slowly approaching through it and taking in the crystals and the island in the background". A
        //slow push out of a passage, low over the water, from this share of the way between its mouth and its dead end to
        //this share of the mouth's distance inside the hall, the look on the island; the passage is the one whose look takes
        //in the most crystal clusters (within PASSAGE_CONE of it), and the height the first of these that clears
        private const float PASSAGE_FROM = 0.45f, PASSAGE_TO = 0.72f;
        private static readonly float[] PASSAGE_HEIGHTS = { 5f, 8f, 3.5f, 12f, 16f };
        private const float PASSAGE_CONE_DEGREES = 28f;
        private const float PASSAGE_SECONDS = 4.6f;
        internal const float PASSAGE_MARGIN = 2.5f;
        private const float ISLAND_LOOK_Y = 12f;

        //The glide from beyond (#984): "shots low by the surface, ideally so that the island is still visible in the
        //background". The lens comes in from the cluster's far side, this far out of it to this far, so the island stands
        //behind the cluster and its reflection; the look leans this share of the way from the cluster towards the island.
        //Tried first, on the clusters with room behind them; the glide from the island's side is what is left
        private const float BEYOND_FROM = 50f, BEYOND_TO = 25f;
        private const float BEYOND_ISLAND_LEAN = 0.18f;

        private const int ATTEMPTS = 60;
        private const int PATH_POINTS = IntroPaths.FINE_POINTS;

        /// <summary>
        /// The prologue for the cavern being drawn, or null when the renderer has no cavern config.
        /// <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Cavern) is not CavernSceneConfig) return null;
            return Build(scenes.CavernChamber, fieldOfView, random);
        }

        /// <summary>The prologue against a hall, with no renderer: what the tests fly.</summary>
        internal static IntroShot[] Build(CavernChamber chamber, float fieldOfView, Random random) => new[]
        {
            Passage(chamber, fieldOfView) ?? Hall(chamber, fieldOfView, random),
            Crystals(chamber, fieldOfView, random),
            Shaft(chamber, fieldOfView, random),
            Underside(chamber, random),
        };

        /// <summary>Whether every point of <paramref name="path"/> stands clear of the hall by <paramref name="margin"/>.</summary>
        internal static bool Clear(CavernChamber chamber, Vector3[] path, float margin)
        {
            foreach (Vector3 p in path)
                if (chamber.Clearance(p) < margin) return false;
            return true;
        }

        /// <summary>
        /// Out of a passage into the hall (#984): the lens starts in the dark of a passage off the hall, low over its water, and
        /// pushes slowly out towards the island, the lake and the crystals opening ahead as a bright window. Of the passages
        /// whose path clears, the one whose look takes in the most crystal clusters; null when none clears, and the hall's
        /// establishing view opens instead.
        /// </summary>
        private static IntroShot Passage(CavernChamber chamber, float fieldOfView)
        {
            float waterY = chamber.WaterY;
            Vector3 island = new(0f, waterY + ISLAND_LOOK_Y, 0f);
            float cone = MathF.Cos(MathHelper.ToRadians(PASSAGE_CONE_DEGREES));
            Vector3[] best = null;
            int bestScore = -1;

            for (int t = 0; t < chamber.PassageCount; t++)
            {
                (Vector3 axis, float mouth, float end) = chamber.Passage(t);
                Vector2 along = Vector2.Normalize(new Vector2(axis.X, axis.Z));
                Vector2 centre = new(chamber.Centre.X, chamber.Centre.Z);
                float from = MathHelper.Lerp(mouth, end, PASSAGE_FROM), to = mouth * PASSAGE_TO;

                foreach (float height in PASSAGE_HEIGHTS)
                {
                    var path = new Vector3[PATH_POINTS];
                    for (int i = 0; i < PATH_POINTS; i++)
                    {
                        //Eased in, so the push starts slow in the dark and gathers as the hall opens
                        float s = i / (float)(PATH_POINTS - 1);
                        float r = MathHelper.Lerp(from, to, s * s * (3f - 2f * s));
                        Vector2 at = centre + along * r;
                        path[i] = new Vector3(at.X, waterY + height, at.Y);
                    }
                    if (!Clear(chamber, path, PASSAGE_MARGIN)) continue;

                    //Its score: the clusters in the look from where it starts
                    Vector3 look = Vector3.Normalize(island - path[0]);
                    int score = 0;
                    foreach (CavernCluster cluster in chamber.Clusters)
                        if (Vector3.Dot(Vector3.Normalize(cluster.LightPosition - path[0]), look) > cone) score++;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = path;
                    }
                    break;
                }
            }

            return best == null ? null
                : new IntroShot("the passage", best, PASSAGE_SECONDS, fieldOfView * 1.15f, lookAt: island);
        }

        /// <summary>The hall from high on one side, across to the far side: the establishing view.</summary>
        private static IntroShot Hall(CavernChamber chamber, float fieldOfView, Random random)
        {
            float waterY = chamber.WaterY;
            var path = new Vector3[PATH_POINTS];
            Vector3 lookAt = new(0f, waterY + 20f, 0f);

            for (int attempt = 0; attempt <= ATTEMPTS; attempt++)
            {
                bool last = attempt == ATTEMPTS;
                float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                float arc = MathHelper.ToRadians(HALL_ARC_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);
                for (int i = 0; i < PATH_POINTS; i++)
                {
                    float s = i / (float)(PATH_POINTS - 1);
                    float b = bearing + arc * s;
                    float wall = chamber.WallAtWater(MathF.Cos(b), MathF.Sin(b));
                    //The fallback stands nearer the axis and lower, where the hall is open air whatever the seed built
                    float radius = last ? 70f : wall * HALL_RADIUS_SHARE;
                    float height = last ? 34f : HALL_HEIGHT;
                    path[i] = new Vector3(MathF.Cos(b) * radius, waterY + height - HALL_DROP * s, MathF.Sin(b) * radius);
                }

                //Across the hall and low: the look swings with the arc, so the lens pans the far side rather than holding one
                //patch of it
                float middle = bearing + arc * 0.5f + MathHelper.Pi;
                float far = chamber.WallAtWater(MathF.Cos(middle), MathF.Sin(middle));
                lookAt = new Vector3(MathF.Cos(middle) * far * 0.5f, waterY + 22f, MathF.Sin(middle) * far * 0.5f);
                if (last || Clear(chamber, path, HALL_MARGIN)) break;
            }

            return new IntroShot("the hall", path, HALL_SECONDS, fieldOfView * 1.25f, lookAt: lookAt);
        }

        /// <summary>A glide low over the lake onto a crystal cluster, its reflection under it.</summary>
        private static IntroShot Crystals(CavernChamber chamber, float fieldOfView, Random random)
        {
            float waterY = chamber.WaterY;
            var path = new Vector3[PATH_POINTS];

            //From beyond the cluster first, the island behind it (#984)
            for (int attempt = 0; attempt < ATTEMPTS && chamber.Clusters.Count > 0; attempt++)
            {
                CavernCluster cluster = chamber.Clusters[random.Next(chamber.Clusters.Count)];
                if (cluster.Base.Y > waterY + CRYSTAL_LOW) continue;
                Vector3 at = cluster.LightPosition;
                Vector2 outward = Vector2.Normalize(new Vector2(at.X, at.Z));
                float side = MathHelper.ToRadians(CRYSTAL_SIDE_DEGREES) * 0.6f * (random.Next(2) == 0 ? 1f : -1f);
                Vector2 approach = IntroPaths.Rotate(outward, side);
                for (int i = 0; i < PATH_POINTS; i++)
                {
                    float d = MathHelper.Lerp(BEYOND_FROM, BEYOND_TO, i / (float)(PATH_POINTS - 1));
                    path[i] = new Vector3(at.X + approach.X * d, waterY + CRYSTAL_GLIDE_HEIGHT, at.Z + approach.Y * d);
                }
                Vector3 look = Vector3.Lerp(at + new Vector3(0f, 1f, 0f), new Vector3(0f, waterY + ISLAND_LOOK_Y, 0f), BEYOND_ISLAND_LEAN);
                if (Clear(chamber, path, CRYSTAL_MARGIN))
                    return new IntroShot("the crystals", path, CRYSTAL_SECONDS, fieldOfView * 1.15f, lookAt: look);
            }

            for (int attempt = 0; attempt < ATTEMPTS && chamber.Clusters.Count > 0; attempt++)
            {
                //Only the clusters at the water (on the islands and the beaches): one on a ledge up the wall, seen from four
                //units over the lake, is a glow behind the curtain hanging in front of it, and its reflection is lost
                CavernCluster cluster = chamber.Clusters[random.Next(chamber.Clusters.Count)];
                if (cluster.Base.Y > waterY + CRYSTAL_LOW && attempt < ATTEMPTS - 10) continue;
                Vector3 at = cluster.LightPosition;
                Vector2 inward = -Vector2.Normalize(new Vector2(at.X, at.Z));
                float side = MathHelper.ToRadians(CRYSTAL_SIDE_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);
                Vector2 approach = IntroPaths.Rotate(inward, side);
                for (int i = 0; i < PATH_POINTS; i++)
                {
                    float d = MathHelper.Lerp(CRYSTAL_FROM, CRYSTAL_TO, i / (float)(PATH_POINTS - 1));
                    path[i] = new Vector3(at.X + approach.X * d, waterY + CRYSTAL_GLIDE_HEIGHT, at.Z + approach.Y * d);
                }
                if (Clear(chamber, path, CRYSTAL_MARGIN))
                    return new IntroShot("the crystals", path, CRYSTAL_SECONDS, fieldOfView * 1.1f, lookAt: at + new Vector3(0f, 1f, 0f));
            }

            //No cluster could be reached clear: a skim out over the lake from the island towards the nearest cluster
            Vector3 towards = chamber.Clusters.Count > 0 ? chamber.Clusters[0].LightPosition : new Vector3(100f, waterY + 6f, 0f);
            Vector2 dir = Vector2.Normalize(new Vector2(towards.X, towards.Z));
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float d = MathHelper.Lerp(36f, 56f, i / (float)(PATH_POINTS - 1));
                path[i] = new Vector3(dir.X * d, waterY + 5f, dir.Y * d);
            }
            return new IntroShot("the crystals", path, CRYSTAL_SECONDS, fieldOfView * 1.1f, lookAt: towards);
        }

        /// <summary>
        /// A slow arc round a shaft of light, low over the lake, the look on the beam halfway up: the dust in it, the opening
        /// blazing in the vault over it, the pool it lights on the water and the hall dark round it. (A look up a stalactite
        /// curtain into the vault was the first cut: a fan of grey sticks against dark rock, which the owner had already
        /// found the ceiling to be, #656.)
        /// </summary>
        private static IntroShot Shaft(CavernChamber chamber, float fieldOfView, Random random)
        {
            var path = new Vector3[PATH_POINTS];
            float waterY = chamber.WaterY;

            for (int attempt = 0; attempt < ATTEMPTS && chamber.Shafts.Count > 0; attempt++)
            {
                CavernShaft shaft = chamber.Shafts[random.Next(chamber.Shafts.Count)];
                Vector2 landing = new(shaft.Landing.X, shaft.Landing.Z);
                float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                float arc = MathHelper.ToRadians(SHAFT_ARC_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);
                for (int i = 0; i < PATH_POINTS; i++)
                {
                    float t = i / (float)(PATH_POINTS - 1);
                    float b = bearing + arc * t;
                    path[i] = new Vector3(landing.X + MathF.Cos(b) * SHAFT_STAND_OFF, waterY + SHAFT_HEIGHT + SHAFT_RISE * t,
                        landing.Y + MathF.Sin(b) * SHAFT_STAND_OFF);
                }
                Vector3 lookAt = shaft.Source + shaft.Direction * (shaft.Length * 0.6f);
                if (Clear(chamber, path, SHAFT_MARGIN))
                    return new IntroShot("the shaft", path, SHAFT_SECONDS, fieldOfView * 1.35f, lookAt: lookAt);
            }

            //No shaft could be circled clear: a rise beside the island looking up into the vault over it
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float t = i / (float)(PATH_POINTS - 1);
                path[i] = new Vector3(46f, waterY + 20f + SHAFT_RISE * t, 0f);
            }
            return new IntroShot("the shaft", path, SHAFT_SECONDS, fieldOfView * 1.3f,
                lookAt: new Vector3(0f, chamber.VaultHeight(0f, 0f) - 10f, 0f));
        }

        /// <summary>
        /// The island from underneath (#656): a crane rising a little as it closes on the funnel from one side, the look on
        /// the funnel's glass under the rim, the lake mirroring it.
        /// </summary>
        private static IntroShot Underside(CavernChamber chamber, Random random)
        {
            float waterY = chamber.WaterY;
            float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
            Vector2 outward = new(MathF.Cos(bearing), MathF.Sin(bearing));

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float s = i / (float)(PATH_POINTS - 1);
                float out1 = MathHelper.Lerp(UNDER_OUT_FROM, UNDER_OUT_TO, s);
                path[i] = new Vector3(outward.X * out1, waterY + MathHelper.Lerp(UNDER_FROM_ABOVE_WATER, UNDER_TO_ABOVE_WATER, s), outward.Y * out1);
            }

            return new IntroShot("the underside", path, UNDER_SECONDS, MathHelper.ToRadians(UNDER_FOV_DEGREES),
                lookAt: new Vector3(0f, UNDER_LOOK_Y, 0f));
        }
    }
}

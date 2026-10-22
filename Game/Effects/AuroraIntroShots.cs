using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The aurora's own shots for a chapter intro's prologue (#531, re-authored for the rebuilt scene in #963): a low pass
    /// between the spruces towards the curtains, a glide over the open snow into the Moon with the wood's shadows running
    /// at the lens, and the curtains over the treeline from the far side of the clearing — then the tour's last leg, which
    /// looks the same way. Every shot is on something the scene is for: the wood, the moonlit snow, the aurora.
    /// <para>
    /// <b>The path between the trunks is found, not laid out</b> — the city's street shot's trick with the trees in place of
    /// the towers. The wood is a clumped scatter (<see cref="ForestScatter"/>), so a few hundred straight runs
    /// (<c>WOOD_CANDIDATES</c>) heading towards the curtains are rolled, each measured against every conifer and snag the planting holds (the horizontal distance
    /// from the trunk to the run must clear the crown at its scale), and of those that clear, the one with the most trees
    /// standing near it on both hands is taken. Heights come off the snow as drawn
    /// (<see cref="SceneRenderer.AuroraGroundHeight"/>, the drifts round the trunks included). Built once when the intro
    /// begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class AuroraIntroShots
    {
        //The wood: a straight run this long through the ring between the clearing and the wood's edge, this high over the
        //snow (an eye between the trunks, under the crowns' skirts), heading within WOOD_HEADING of the curtains' bearing
        //and pitched UP so they stand over the treeline in the upper half of the frame
        private const float WOOD_RUN = 70f;
        private const float WOOD_HEIGHT = 3.2f;
        private const float WOOD_PITCH_UP_DEGREES = 8f;
        private const float WOOD_SECONDS = 3.4f;
        private const float WOOD_HEADING_DEGREES = 35f;

        //How close a trunk's axis may come to a run: the crown's radius at the largest scale the planting rolls, plus a
        //margin for the lean and the whorls' raggedness. Under this the lens passes through needles.
        private const float TRUNK_MARGIN = 1.4f;

        //How near a tree has to stand to count towards a run's density. ⚠ A bare count chose a grove's edge (#531):
        //the thinner hand counts twice, so a run wants trunks on BOTH hands.
        private const float DENSITY_REACH = 10f;

        //The Moon: a glide this long, this high over the open snow between the island and the wood, beside the island
        //rather than behind it, the lens a little under the Moon so the disc sits in the upper third and the wood on its
        //side stands against the sky, every shadow running towards the lens
        private const float MOON_RADIUS = 40f;
        private const float MOON_DRIFT = 14f;
        private const float MOON_HEIGHT = 1.6f;
        private const float MOON_SECONDS = 3.2f;
        private const float MOON_LOOK_BELOW_DEGREES = 7f;

        //The curtains: a slow drift across the snow on the far side of the clearing from them, the lens up this far over
        //the horizontal towards their bearing, so the island and the treeline cross the bottom of the frame and the great
        //curtain fills the rest
        private const float CURTAINS_RADIUS = 80f;
        private const float CURTAINS_DRIFT = 12f;
        private const float CURTAINS_HEIGHT = 2.2f;
        private const float CURTAINS_LOOK_ELEVATION_DEGREES = 14f;
        private const float CURTAINS_SECONDS = 3.4f;
        private const float TRUNK_CLEARANCE = 7f;   //four put a crown ON the lens for the first two seconds once (#531)

        //A far point to look at: far enough that the drift moves the frame only a little
        private const float LOOK_DISTANCE = 300f;

        //How many candidates each roll weighs: each is measured against every tree and snag, so some forty thousand
        //distance tests per shot, once per intro. The wood's runs are held to a heading, and most that head towards the
        //curtains through a dense stand pass through a crown, so it rolls more of them.
        private const int CANDIDATES = 120;
        private const int WOOD_CANDIDATES = 600;

        private const int PATH_POINTS = IntroPaths.FINE_POINTS;

        /// <summary>
        /// The prologue for the aurora being drawn, or null when there is nothing to build it from.
        /// <paramref name="ground"/> is the snow as drawn; <paramref name="fieldOfView"/> is the intro's own gameplay frame,
        /// which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(ForestScatterRenderer wood, AuroraSceneConfig aurora, Func<float, float, float> ground,
            float fieldOfView, Random random)
        {
            if (wood?.Scatter == null || aurora == null || ground == null) return null;

            float bearing = MathHelper.ToRadians(aurora.Curtains.BearingDegrees);
            Vector2 towardsCurtains = new(MathF.Sin(bearing), MathF.Cos(bearing));
            float moonAzimuth = MathHelper.ToRadians(aurora.Sky.MoonAzimuthDegrees);
            Vector2 towardsMoon = new(MathF.Sin(moonAzimuth), MathF.Cos(moonAzimuth));

            IntroShot pass = Wood(wood.Scatter, aurora, ground, towardsCurtains, fieldOfView, random);
            IntroShot moon = Moon(wood.Scatter, aurora, ground, towardsMoon, fieldOfView, random);
            IntroShot sky = Curtains(wood.Scatter, ground, towardsCurtains, fieldOfView, random);

            int count = (pass != null ? 1 : 0) + (moon != null ? 1 : 0) + (sky != null ? 1 : 0);
            if (count == 0) return null;

            var shots = new IntroShot[count];
            int at = 0;
            if (pass != null) shots[at++] = pass;
            if (moon != null) shots[at++] = moon;
            if (sky != null) shots[at] = sky;
            return shots;
        }

        /// <summary>
        /// The low pass between the spruces: of a few dozen rolled straight runs through the wood's ring heading towards the
        /// curtains, the densest one that clears every trunk. Null when none of them does.
        /// </summary>
        private static IntroShot Wood(ForestScatter scatter, AuroraSceneConfig aurora, Func<float, float, float> ground,
            Vector2 towardsCurtains, float fieldOfView, Random random)
        {
            //Inside the far stand's ring: its spires are a distant silhouette, never in the planting this measures, and a
            //run among them flew through one (the first cut of #963 opened on a black frame)
            ForestSceneConfig terrain = aurora.Terrain;
            float inner = terrain.ClearingRadius * 0.6f;
            float outer = MathF.Max(MathF.Min(terrain.Trees.MaxRadius, aurora.FarStand.InnerRadius - 12f), inner + WOOD_RUN);
            float clearance = terrain.Trees.ConiferCrownRadius * terrain.Trees.MaxScale + TRUNK_MARGIN;
            float heading = MathF.Atan2(towardsCurtains.Y, towardsCurtains.X);
            float spread = MathHelper.ToRadians(WOOD_HEADING_DEGREES);

            int bestScore = -1;
            Vector2 bestFrom = Vector2.Zero, bestTo = Vector2.Zero;

            for (int attempt = 0; attempt < WOOD_CANDIDATES; attempt++)
            {
                float angle = (float)random.NextDouble() * MathHelper.TwoPi;
                float radius = MathHelper.Lerp(inner, outer - WOOD_RUN, (float)random.NextDouble());
                Vector2 from = new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);

                float runHeading = heading + IntroPaths.RandomRange(random, -spread, spread);
                Vector2 to = from + new Vector2(MathF.Cos(runHeading), MathF.Sin(runHeading)) * WOOD_RUN;

                //In the wood's ring the whole way, and never over the island
                float endRadius = to.Length();
                if (endRadius < inner || endRadius > outer) continue;
                if (IntroPaths.DistanceToSegment(Vector2.Zero, from, to) < ArenaIsland.RADIUS + clearance) continue;

                int score = ScoreRun(scatter, from, to, clearance);
                if (score <= bestScore) continue;

                bestScore = score;
                bestFrom = from;
                bestTo = to;
            }

            if (bestScore < 0) return null;

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                Vector2 plan = Vector2.Lerp(bestFrom, bestTo, i / (float)(PATH_POINTS - 1));
                path[i] = new Vector3(plan.X, ground(plan.X, plan.Y) + WOOD_HEIGHT, plan.Y);
            }

            return new IntroShot("the wood", path, WOOD_SECONDS, fieldOfView * 1.15f,
                lookAhead: 18f, pitchDownDegrees: -WOOD_PITCH_UP_DEGREES, pitchDamping: 0.5f);
        }

        /// <summary>
        /// Into the Moon over the open snow: a glide beside the island, clear of every trunk, the lens a little under the
        /// Moon so the disc and the wood on its side are in the frame and the wood's long shadows run towards the lens.
        /// </summary>
        private static IntroShot Moon(ForestScatter scatter, AuroraSceneConfig aurora, Func<float, float, float> ground,
            Vector2 towardsMoon, float fieldOfView, Random random)
        {
            //Beside the island across the Moon's bearing, either side; the drift runs towards the Moon
            Vector2 side = new(-towardsMoon.Y, towardsMoon.X);
            if (random.Next(2) == 0) side = -side;

            if (!ClearGlide(scatter, side, towardsMoon, out Vector2 from, out Vector2 to))
            {
                side = -side;
                if (!ClearGlide(scatter, side, towardsMoon, out from, out to)) return null;
            }

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                Vector2 plan = Vector2.Lerp(from, to, i / (float)(PATH_POINTS - 1));
                path[i] = new Vector3(plan.X, ground(plan.X, plan.Y) + MOON_HEIGHT, plan.Y);
            }

            float elevation = MathHelper.ToRadians(Math.Max(aurora.Sky.MoonElevationDegrees - MOON_LOOK_BELOW_DEGREES, 2f));
            Vector3 centre = path[PATH_POINTS / 2];
            Vector3 lookAt = centre + new Vector3(towardsMoon.X * MathF.Cos(elevation), MathF.Sin(elevation), towardsMoon.Y * MathF.Cos(elevation)) * LOOK_DISTANCE;

            return new IntroShot("the moon", path, MOON_SECONDS, fieldOfView * 1.2f, lookAt: lookAt);
        }

        //The Moon's glide on one side of the island, and whether it is clear of every trunk
        private static bool ClearGlide(ForestScatter scatter, Vector2 side, Vector2 towardsMoon, out Vector2 from, out Vector2 to)
        {
            Vector2 middle = side * MOON_RADIUS;
            from = middle - towardsMoon * (MOON_DRIFT * 0.5f);
            to = middle + towardsMoon * (MOON_DRIFT * 0.5f);
            return ScoreRun(scatter, from, to, TRUNK_CLEARANCE) >= 0;
        }

        /// <summary>
        /// The curtains over the treeline: a slow drift across the snow on the far side of the clearing from them, on the
        /// spot with the fewest trunks close round it, the lens up towards them so the island and the treeline cross the
        /// bottom of the frame.
        /// </summary>
        private static IntroShot Curtains(ForestScatter scatter, Func<float, float, float> ground, Vector2 towardsCurtains,
            float fieldOfView, Random random)
        {
            Vector2 across = new(-towardsCurtains.Y, towardsCurtains.X);

            int bestScore = int.MaxValue;
            Vector2 bestAt = Vector2.Zero;
            bool found = false;

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                //Behind the island from the curtains, a little to either side
                float off = IntroPaths.RandomRange(random, -0.35f, 0.35f);
                Vector2 at = (-towardsCurtains + across * off) * CURTAINS_RADIUS;
                Vector2 from = at - across * (CURTAINS_DRIFT * 0.5f), to = at + across * (CURTAINS_DRIFT * 0.5f);

                int score = ScoreRun(scatter, from, to, TRUNK_CLEARANCE);
                if (score < 0 || score >= bestScore) continue;

                bestScore = score;
                bestAt = at;
                found = true;
            }

            if (!found) return null;

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                Vector2 plan = bestAt + across * ((i / (float)(PATH_POINTS - 1) - 0.5f) * CURTAINS_DRIFT);
                path[i] = new Vector3(plan.X, ground(plan.X, plan.Y) + CURTAINS_HEIGHT, plan.Y);
            }

            float elevation = MathHelper.ToRadians(CURTAINS_LOOK_ELEVATION_DEGREES);
            Vector3 centre = path[PATH_POINTS / 2];
            Vector3 lookAt = centre + new Vector3(towardsCurtains.X * MathF.Cos(elevation), MathF.Sin(elevation), towardsCurtains.Y * MathF.Cos(elevation)) * LOOK_DISTANCE;

            return new IntroShot("the curtains", path, CURTAINS_SECONDS, fieldOfView * 1.2f, lookAt: lookAt);
        }

        //How many trees stand near a straight run, weighted so that trees on BOTH hands count for more than the same number
        //on one — the thinner side twice, the whole once — or -1 when one stands IN it: every conifer and snag of the
        //planting is measured against the segment, and the first within <clearance> of it rejects the run.
        private static int ScoreRun(ForestScatter scatter, Vector2 from, Vector2 to, float clearance)
        {
            int left = 0, right = 0;

            if (!Count(scatter.Conifers, from, to, clearance, ref left, ref right)) return -1;
            if (!Count(scatter.Snags, from, to, clearance, ref left, ref right)) return -1;

            return left + right + 2 * Math.Min(left, right);
        }

        private static bool Count(ModelInstance[][] buckets, Vector2 from, Vector2 to, float clearance, ref int left, ref int right)
        {
            Vector2 run = to - from;
            float length2 = MathF.Max(run.LengthSquared(), 1e-4f);

            foreach (ModelInstance[] bucket in buckets)
            {
                foreach (ModelInstance tree in bucket)
                {
                    Vector2 trunk = new(tree.World.M41, tree.World.M43);
                    Vector2 offset = trunk - from;
                    float t = MathHelper.Clamp(Vector2.Dot(offset, run) / length2, 0f, 1f);
                    float distance = Vector2.Distance(trunk, from + run * t);

                    if (distance < clearance) return false;
                    if (distance >= DENSITY_REACH) continue;

                    //Which hand the tree stands on, by the sign of the cross product with the run.
                    if (run.X * offset.Y - run.Y * offset.X > 0f) left++;
                    else right++;
                }
            }

            return true;
        }
    }
}

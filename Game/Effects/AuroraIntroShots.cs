using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The aurora's own shots for a chapter intro's prologue (#531): a low pass between the spruces with the
    /// curtains over the treeline, then a slow drift over the snow looking up at the sky through the trunks —
    /// cut together, then cut to the tour's last leg. The owner's verdict on #462 was that the tour "looks
    /// far too high up at the start, never flies through the forest, and there are no cuts": the tour is a
    /// spline round the arena at stand-off height, and the wood #462 built stands outside the clearing,
    /// ninety-five units out, where no such spline goes.
    /// <para>
    /// <b>The path between the trunks is found, not laid out</b> — the city's street shot's trick with the
    /// trees in place of the towers. The wood is a clumped scatter (<see cref="ForestScatter"/>), so there is no
    /// centre line to run down; instead a few dozen straight runs through the ring the trees stand in are
    /// rolled, each is measured against every conifer and snag the planting holds (the horizontal distance
    /// from the trunk to the run must clear the crown at its scale), and of the runs that clear, the one with
    /// the most trees standing near it is taken — the densest wood the lens can thread. Heights come off
    /// <see cref="TerrainMirror.Forest"/> with the aurora's own terrain, the same mirror the trees
    /// were planted on. Built once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class AuroraIntroShots
    {
        //The wood: a straight run this long through the ring between the clearing and the wood's edge, this
        //high over the snow (an eye between the trunks, under the crowns' skirts), pitched UP so the curtains
        //stand over the treeline in the upper half of the frame, at a lens a little wider than the game's.
        private const float WOOD_RUN = 70f;
        private const float WOOD_HEIGHT = 3.2f;
        private const float WOOD_PITCH_UP_DEGREES = 6f;
        private const float WOOD_SECONDS = 3.4f;

        //How close a trunk's axis may come to the run: the crown's radius at the largest scale the planting
        //rolls, plus a margin for the lean and the whorls' raggedness. Under this the lens passes through
        //needles; the eye reads that as a green wipe, not as a tree.
        private const float TRUNK_MARGIN = 1.4f;

        //How near a tree has to stand to count towards a run's density — the width of the corridor the lens
        //reads as "among the trees" at this height. ⚠ Sixteen, and the count alone, chose a run along the
        //edge of a grove: a slope of open snow with the trees all on one hand, which photographed as a
        //snowfield with a treeline rather than a pass through a wood. Ten, and a score that weighs the
        //thinner side twice (see ScoreRun), wants trunks on BOTH hands.
        private const float DENSITY_REACH = 10f;

        //The snow: a slow drift this long at this height over the snow at the wood's inner edge, the lens on
        //a fixed point in the sky this high above the horizontal and this far off, out over the wood, so the
        //treeline crosses the bottom of the frame and the curtains fill the rest.
        private const float SNOW_DRIFT = 14f;
        private const float SNOW_HEIGHT = 1.7f;
        private const float SNOW_LOOK_ELEVATION_DEGREES = 44f;
        private const float SNOW_LOOK_DISTANCE = 220f;
        private const float SNOW_SECONDS = 3.4f;

        //How far into the wood from the clearing's edge the snow shot stands, and how clear of trunks it wants
        //to be: a lens on the ground among the trunks, but not against one.
        private const float SNOW_IN_FROM_CLEARING = 22f;
        private const float SNOW_TRUNK_CLEARANCE = 7f;   //four put a crown ON the lens for the first two seconds once

        //How many candidates each roll weighs; the best by its own measure is taken. Each candidate is
        //measured against all 890 trees and snags, so this is about forty thousand distance tests per shot,
        //once per intro — nothing.
        private const int CANDIDATES = 120;

        //Points per path. The heights are read at every one, so the runs follow the snow's own rise and fall.
        private const int PATH_POINTS = 96;

        /// <summary>
        /// The prologue for the aurora being drawn, or null when there is no wood to thread.
        /// <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(ForestScatterRenderer wood, AuroraSceneConfig aurora, float fieldOfView, Random random)
        {
            if (wood?.Scatter == null || aurora == null) return null;

            IntroShot pass = Wood(wood.Scatter, aurora.Terrain, fieldOfView, random);
            IntroShot sky = Snow(wood.Scatter, aurora.Terrain, fieldOfView, random);

            if (pass == null && sky == null) return null;
            if (pass == null) return new[] { sky };
            if (sky == null) return new[] { pass };

            return new[] { pass, sky };
        }

        /// <summary>
        /// The low pass between the spruces: of a few dozen rolled straight runs through the wood's ring, the
        /// densest one that clears every trunk. Null when none of them does — a wood planted so thick that
        /// no seventy-unit line threads it, which the aurora's own config does not do.
        /// </summary>
        private static IntroShot Wood(ForestScatter scatter, ForestSceneConfig terrain, float fieldOfView, Random random)
        {
            float inner = terrain.ClearingRadius + 12f;
            float outer = MathF.Max(terrain.Trees.MaxRadius, inner + WOOD_RUN);
            float clearance = terrain.Trees.ConiferCrownRadius * terrain.Trees.MaxScale + TRUNK_MARGIN;

            int bestScore = -1;
            Vector2 bestFrom = Vector2.Zero, bestTo = Vector2.Zero;

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                //A start somewhere in the ring, and a heading that keeps the run in it: mostly round the
                //arena, leaning in or out a little, so the run neither dives into the clearing nor leaves the
                //wood for the hills.
                float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                float radius = MathHelper.Lerp(inner, outer - WOOD_RUN, (float)random.NextDouble());
                Vector2 from = new(MathF.Cos(bearing) * radius, MathF.Sin(bearing) * radius);

                float turn = random.Next(2) == 0 ? 1f : -1f;
                float heading = bearing + turn * MathHelper.PiOver2 + MathHelper.ToRadians(Lerp(random, -30f, 30f));
                Vector2 to = from + new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * WOOD_RUN;

                float endRadius = to.Length();
                if (endRadius < inner || endRadius > outer) continue;

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
                path[i] = new Vector3(plan.X, TerrainMirror.Forest(plan.X, plan.Y, terrain) + WOOD_HEIGHT, plan.Y);
            }

            return new IntroShot("the wood", path, WOOD_SECONDS, fieldOfView * 1.15f,
                lookAhead: 18f, pitchDownDegrees: -WOOD_PITCH_UP_DEGREES);
        }

        /// <summary>
        /// From the snow looking up: a slow drift at knee height just inside the wood's edge, on a spot clear
        /// of trunks, the lens pinned on a point high over the wood so the treeline crosses the bottom of the
        /// frame under the curtains.
        /// </summary>
        private static IntroShot Snow(ForestScatter scatter, ForestSceneConfig terrain, float fieldOfView, Random random)
        {
            float radius = terrain.ClearingRadius + SNOW_IN_FROM_CLEARING;

            int bestScore = -1;
            Vector2 bestAt = Vector2.Zero, bestOut = Vector2.Zero;

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                Vector2 outward = new(MathF.Cos(bearing), MathF.Sin(bearing));
                Vector2 at = outward * radius;

                //The drift runs across the outward direction, so the lens slides along the wood's edge.
                Vector2 across = new(-outward.Y, outward.X);
                Vector2 from = at - across * (SNOW_DRIFT * 0.5f);
                Vector2 to = at + across * (SNOW_DRIFT * 0.5f);

                //Clear of trunks along the drift, and scored by the trees standing out beyond it — the
                //treeline the shot looks over.
                int score = ScoreRun(scatter, from, to, SNOW_TRUNK_CLEARANCE);
                if (score <= bestScore) continue;

                bestScore = score;
                bestAt = at;
                bestOut = outward;
            }

            if (bestScore < 0) return null;

            Vector2 driftAcross = new(-bestOut.Y, bestOut.X);
            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                Vector2 plan = bestAt + driftAcross * ((i / (float)(PATH_POINTS - 1) - 0.5f) * SNOW_DRIFT);
                path[i] = new Vector3(plan.X, TerrainMirror.Forest(plan.X, plan.Y, terrain) + SNOW_HEIGHT, plan.Y);
            }

            float elevation = MathHelper.ToRadians(SNOW_LOOK_ELEVATION_DEGREES);
            Vector3 middle = path[PATH_POINTS / 2];
            Vector3 lookAt = middle + new Vector3(bestOut.X * MathF.Cos(elevation), MathF.Sin(elevation), bestOut.Y * MathF.Cos(elevation)) * SNOW_LOOK_DISTANCE;

            return new IntroShot("the snow", path, SNOW_SECONDS, fieldOfView * 1.2f, lookAt: lookAt);
        }

        //How many trees stand near a straight run, weighted so that trees on BOTH hands count for more than
        //the same number on one — the thinner side twice, the whole once — or -1 when one stands IN it: every
        //conifer and snag of the planting is measured against the segment, and the first within <clearance>
        //of it rejects the run.
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

        private static float Lerp(Random random, float from, float to) => from + (to - from) * (float)random.NextDouble();
    }
}

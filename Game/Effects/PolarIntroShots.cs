using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The icesheet's own shots for a chapter intro's prologue (#559): a crane up behind the island looking out
    /// over the plain to the pressure front, a low pass over a crevasse field with its slots glowing cyan, a slow
    /// truck along the front's tilted plates, and an arc past the research camp (#1128) — cut together, then cut to
    /// the tour's last leg. The tour is a spline round the arena; the crevasses open thirty-odd units out and the
    /// front stands three hundred out, and from stand-off height both are a line on a white sheet.
    /// <para>
    /// <b>Every height is read off the sheet itself</b> (<see cref="TerrainMirror.Polar"/> on the renderer's own
    /// config, the host copy of <c>Polar.fx</c>'s <c>PolarHeight</c>), and the crevasses, the front and the camp are
    /// found through the same mirror (<see cref="TerrainMirror.PolarCrevasse(float, float, PolarSceneConfig)"/>,
    /// <see cref="TerrainMirror.PolarRidge(float, float, PolarSceneConfig)"/>, <see cref="PolarPlanting.Landmarks"/>):
    /// the crevasse pass is the rolled run that looks down on the most slot, the truck stands off the front's crest
    /// wherever its wander has put it on that bearing, and the camp's arc looks at the camp as it is planted. Each lens
    /// rides a stated clearance over the highest ground under its whole path, so it never bobs over a swell and never
    /// clips a plate, and every lens keeps off the camp. Built once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class PolarIntroShots
    {
        //The sheet: a crane from this far behind the island (on the far side from the front) and this high
        //over the ice, out and up to the second pair, the lens on a point out at the front — the island in the
        //foreground, the plain beyond it, the front on the skyline.
        private const float SHEET_FROM_RADIUS = 58f;
        private const float SHEET_TO_RADIUS = 72f;
        private const float SHEET_FROM_HEIGHT = 7f;
        private const float SHEET_TO_HEIGHT = 30f;
        internal const float SHEET_SECONDS = 3.0f;

        //The crevasses: a run this long, this high over the highest ice under it, pitched this far down, and
        //scored by how much slot lies in the ground it looks at — between these two distances ahead of the lens.
        private const float CREVASSE_RUN = 32f;
        private const float CREVASSE_HEIGHT = 7f;
        private const float CREVASSE_PITCH_DOWN_DEGREES = 22f;
        private const float CREVASSE_LOOK_NEAR = 10f;
        private const float CREVASSE_LOOK_FAR = 32f;
        private const float CREVASSE_FROM_RADIUS = 50f;
        private const float CREVASSE_TO_RADIUS = 190f;
        internal const float CREVASSE_SECONDS = 2.6f;

        //The front: a truck this long across the bearing, this far inside the crest and this high over the ice,
        //the look carried along beside the lens at the crest, part-way up the plates.
        private const float FRONT_TRUCK = 46f;
        private const float FRONT_STAND_OFF = 130f;
        private const float FRONT_HEIGHT = 5f;
        internal const float FRONT_SECONDS = 2.8f;

        //The camp (#1128): an arc of CAMP_SWEEP_DEGREES at CAMP_RADIUS from the camp's middle, CAMP_HEIGHT over the highest ice
        //under it, its middle CAMP_TOWARD_FRONT of the way round from the arena's side of the camp to the side its front faces
        //(the dome's sun is on that side), the lens on the camp's middle all the way. Far enough out that the whole camp, from
        //the turbine to the vehicle, stands in the frame; high enough to look down on the snow on its roofs.
        internal const float CAMP_RADIUS = 76f;
        internal const float CAMP_HEIGHT = 9f;
        private const float CAMP_SWEEP_DEGREES = 26f;
        private const float CAMP_TOWARD_FRONT = 0.45f;
        private const float CAMP_FOV = 1.15f;
        internal const float CAMP_SECONDS = 3.2f;

        /// <summary>How far, on the ground, every lens of the prologue keeps from the camp's farthest object (#1128).</summary>
        internal const float CAMP_AIR = 6f;

        private const int CANDIDATES = 150;
        private const int PATH_POINTS = IntroPaths.POINTS;

        /// <summary>
        /// The prologue for the icesheet being drawn, or null when the renderer has no polar config.
        /// <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, float fieldOfView, Random random) =>
            scenes?.GetSceneConfig(SceneKind.Polar) is PolarSceneConfig polar ? Build(polar, fieldOfView, random) : null;

        /// <summary>The prologue over <paramref name="polar"/>'s sheet and its camp, through the ground's mirror alone.</summary>
        internal static IntroShot[] Build(PolarSceneConfig polar, float fieldOfView, Random random) => new[]
        {
            Sheet(polar, fieldOfView, random),
            Crevasses(polar, fieldOfView, random),
            Front(polar, fieldOfView, random),
            Camp(polar, fieldOfView, random),
        };

        /// <summary>Up and out behind the island, over the plain to the front: the establishing view.</summary>
        private static IntroShot Sheet(PolarSceneConfig polar, float fieldOfView, Random random)
        {
            float front = MathHelper.ToRadians(polar.RidgeBearingDegrees + MathHelper.Lerp(-25f, 25f, (float)random.NextDouble()));
            float behind = front + MathHelper.Pi;

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float s = i / (float)(PATH_POINTS - 1);
                float r = MathHelper.Lerp(SHEET_FROM_RADIUS, SHEET_TO_RADIUS, s);
                float x = MathF.Cos(behind) * r, z = MathF.Sin(behind) * r;
                path[i] = new Vector3(x, 0f, z);
            }

            //Over the highest ice the whole crane passes, so the rise is the crane's own and not the ground's.
            float highest = Highest(polar, path);
            for (int i = 0; i < PATH_POINTS; i++)
                path[i].Y = highest + MathHelper.Lerp(SHEET_FROM_HEIGHT, SHEET_TO_HEIGHT, i / (float)(PATH_POINTS - 1));

            Vector3 lookAt = new(MathF.Cos(front) * polar.RidgeRadius * 0.8f, polar.LevelY + polar.RidgeHeight * 0.4f,
                MathF.Sin(front) * polar.RidgeRadius * 0.8f);

            return new IntroShot("the icesheet", path, SHEET_SECONDS, fieldOfView * 1.25f, lookAt: lookAt);
        }

        /// <summary>Low over a crevasse field, looking down into its slots.</summary>
        private static IntroShot Crevasses(PolarSceneConfig polar, float fieldOfView, Random random)
        {
            Vector2 camp = polar.CampCentre;
            float bestScore = -1f;
            Vector2 bestFrom = Vector2.Zero, bestTo = Vector2.Zero;

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                float radius = MathHelper.Lerp(CREVASSE_FROM_RADIUS, CREVASSE_TO_RADIUS, (float)random.NextDouble());
                Vector2 from = new(MathF.Cos(bearing) * radius, MathF.Sin(bearing) * radius);
                float heading = (float)random.NextDouble() * MathHelper.TwoPi;
                Vector2 forward = new(MathF.Cos(heading), MathF.Sin(heading));
                Vector2 to = from + forward * CREVASSE_RUN;

                //The run must not head in over the island: the look ahead would frame the arena, not the ice.
                if (IntroPaths.DistanceToSegment(Vector2.Zero, from, to + forward * CREVASSE_LOOK_FAR) < ArenaIsland.RADIUS + 24f) continue;

                //Nor fly through the research camp (#747), seven units over the ice past a mast many times that: it stands on
                //ice cleared of crevasses, so the score would seldom pick it, but a run can cross it on the way to some
                if (IntroPaths.DistanceToSegment(camp, from, to) < PolarPlanting.Reach + CAMP_AIR) continue;

                //How much slot the lens looks down on over the run: a grid of taps in the ground ahead of it.
                float score = 0f;
                for (int i = 0; i <= 4; i++)
                {
                    Vector2 lens = Vector2.Lerp(from, to, i / 4f);
                    for (int j = 0; j <= 4; j++)
                    {
                        Vector2 at = lens + forward * MathHelper.Lerp(CREVASSE_LOOK_NEAR, CREVASSE_LOOK_FAR, j / 4f);
                        score += TerrainMirror.PolarCrevasse(at.X, at.Y, polar);
                    }
                }

                if (score <= bestScore) continue;

                bestScore = score;
                bestFrom = from;
                bestTo = to;
            }

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                Vector2 plan = Vector2.Lerp(bestFrom, bestTo, i / (float)(PATH_POINTS - 1));
                path[i] = new Vector3(plan.X, 0f, plan.Y);
            }

            float y = Highest(polar, path) + CREVASSE_HEIGHT;
            for (int i = 0; i < PATH_POINTS; i++) path[i].Y = y;

            return new IntroShot("the crevasses", path, CREVASSE_SECONDS, fieldOfView * 1.2f,
                lookAhead: 20f, pitchDownDegrees: CREVASSE_PITCH_DOWN_DEGREES);
        }

        /// <summary>A slow truck along the pressure front, the look carried beside the lens at the crest.</summary>
        private static IntroShot Front(PolarSceneConfig polar, float fieldOfView, Random random)
        {
            //A bearing well inside the front's sector whose truck keeps off the camp (#1128): the camp stands in front of the
            //front's southern half, and a truck on its bearing would run through it five units over the ice
            float span = MathHelper.ToRadians(polar.RidgeSpanDegrees);
            Vector2 camp = polar.CampCentre;
            Vector2 outward = Vector2.UnitX, across = Vector2.UnitY;
            float crest = polar.RidgeRadius;

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                float bearing = MathHelper.ToRadians(polar.RidgeBearingDegrees) + span * MathHelper.Lerp(-0.3f, 0.3f, (float)random.NextDouble());
                outward = new Vector2(MathF.Cos(bearing), MathF.Sin(bearing));
                across = new Vector2(-outward.Y, outward.X) * (random.Next(2) == 0 ? 1f : -1f);
                crest = Crest(polar, outward, across);

                Vector2 middle = outward * (crest - FRONT_STAND_OFF);
                if (IntroPaths.DistanceToSegment(camp, middle - across * (FRONT_TRUCK * 0.5f), middle + across * (FRONT_TRUCK * 0.5f))
                    >= PolarPlanting.Reach + CAMP_AIR)
                    break;
            }

            var path = new Vector3[PATH_POINTS];
            var look = new Vector3[PATH_POINTS];
            float lookY = polar.LevelY + polar.RidgeHeight * 0.45f;

            for (int i = 0; i < PATH_POINTS; i++)
            {
                float d = (i / (float)(PATH_POINTS - 1) - 0.5f) * FRONT_TRUCK;
                Vector2 lens = outward * (crest - FRONT_STAND_OFF) + across * d;
                Vector2 at = outward * crest + across * d;

                path[i] = new Vector3(lens.X, 0f, lens.Y);
                look[i] = new Vector3(at.X, lookY, at.Y);
            }

            float y = Highest(polar, path) + FRONT_HEIGHT;
            for (int i = 0; i < PATH_POINTS; i++) path[i].Y = y;

            return new IntroShot("the pressure ridge", path, FRONT_SECONDS, fieldOfView * 1.1f, lookAtPath: look);
        }

        //Where the plates stand highest on a bearing: the ridge's envelope, smoothed over a slab or two so one tall plate does
        //not decide it.
        private static float Crest(PolarSceneConfig polar, Vector2 outward, Vector2 across)
        {
            float crest = polar.RidgeRadius, most = -1f;
            for (float r = polar.RidgeRadius - polar.RidgeWidth * 2f; r <= polar.RidgeRadius + polar.RidgeWidth * 2f; r += 2f)
            {
                float sum = 0f;
                for (int k = -2; k <= 2; k++)
                {
                    Vector2 at = outward * r + across * (k * polar.SlabSize);
                    sum += TerrainMirror.PolarRidge(at.X, at.Y, polar);
                }

                if (sum > most) { most = sum; crest = r; }
            }

            return crest;
        }

        /// <summary>
        /// The research camp (#1128): an arc past it from the arena's side towards the side its front faces, the lens on the
        /// camp's middle, so the modules, the mast with its flag and the vehicle turn against each other and against the
        /// pressure front behind them. The owner: "the intro does not deal with the camp".
        /// </summary>
        internal static IntroShot Camp(PolarSceneConfig polar, float fieldOfView, Random random)
        {
            Func<float, float, float> ground = (x, z) => TerrainMirror.Polar(x, z, polar);
            Vector2 centre = polar.CampCentre;

            //The lens's mark: the middle of the camp's landmarks, a third of the way up from their feet to their tops
            PolarPlanting.Landmark[] marks = PolarPlanting.Landmarks(centre, polar.CampTurn, ground);
            Vector3 middle = Vector3.Zero;
            foreach (PolarPlanting.Landmark mark in marks) middle += Vector3.Lerp(mark.Foot, mark.Top, 1f / 3f);
            middle /= marks.Length;

            //From the arena's side of the camp a share of the way round to the side its front faces, either way across that. A
            //turn of the camp's yaw (PolarPlanting) takes its front's bearing the other way round
            float towardsArena = MathF.Atan2(-centre.Y, -centre.X);
            float facing = towardsArena - polar.CampTurn;
            float mid = MathHelper.Lerp(towardsArena, facing, CAMP_TOWARD_FRONT);
            float half = MathHelper.ToRadians(CAMP_SWEEP_DEGREES) * 0.5f * (random.Next(2) == 0 ? 1f : -1f);

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float bearing = mid - half + 2f * half * i / (PATH_POINTS - 1);
                path[i] = new Vector3(middle.X + MathF.Cos(bearing) * CAMP_RADIUS, 0f, middle.Z + MathF.Sin(bearing) * CAMP_RADIUS);
            }

            float y = Highest(polar, path) + CAMP_HEIGHT;
            for (int i = 0; i < PATH_POINTS; i++) path[i].Y = y;

            return new IntroShot("the camp", path, CAMP_SECONDS, fieldOfView * CAMP_FOV, lookAt: middle);
        }

        //The highest ice under a path and a few units round each point of it — the lens's footprint, since a
        //camera's near plane has a width too.
        private static float Highest(PolarSceneConfig polar, Vector3[] path)
        {
            float highest = float.MinValue;

            foreach (Vector3 p in path)
                for (int k = 0; k < 5; k++)
                {
                    float dx = k == 1 ? 3f : k == 2 ? -3f : 0f;
                    float dz = k == 3 ? 3f : k == 4 ? -3f : 0f;
                    highest = MathF.Max(highest, TerrainMirror.Polar(p.X + dx, p.Z + dz, polar));
                }

            return highest;
        }
    }
}

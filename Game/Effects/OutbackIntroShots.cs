using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The outback's own shots for a chapter intro's prologue (#559): the plain from high over it with a
    /// monolith standing beyond the island, a low run over the spinifex towards that monolith's foot, and a
    /// crane up its wall to the crown — cut together, then cut to the tour's last leg. The monoliths are the
    /// scene's subject (the tour's stand already names them), and the old spline saw them only as bumps on a
    /// hazed skyline 300 units off.
    /// <para>
    /// <b>The monolith is found, not placed</b>: <see cref="TerrainMirror.OutbackMonoliths"/> rolls the same
    /// cells <c>Outback.fx</c>'s <c>RockLayer</c> does and hands back each formation's centre, the reach of its
    /// talus apron and a bound on its crown, and every height a lens flies at is read off
    /// <see cref="TerrainMirror.Outback"/>, the mirror of the whole field, boulders included. The plain is not
    /// seeded by <c>sceneseed=</c>, so every roll chooses among the same rocks. Built once when the intro begins;
    /// nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class OutbackIntroShots
    {
        //Which monolith: the tallest crown among those whose centres stand within this far of the arena — near
        //enough to have been built as geometry the lens can go to, and the far ring draws past it anyway.
        private const float MONOLITH_SEARCH_RADIUS = 480f;

        //The plain: an arc round the arena on the side AWAY from the monolith, dollying in and craning up, the
        //lens on the island, so the island stands in the plain with the monolith on the skyline behind it.
        private const float WIDE_FROM_RADIUS = 230f;
        private const float WIDE_TO_RADIUS = 195f;
        private const float WIDE_SWEEP_RADIANS = 0.30f;
        private const float WIDE_FROM_HEIGHT = 30f;
        private const float WIDE_TO_HEIGHT = 52f;
        private const float WIDE_CLEARANCE = 10f;
        private const float WIDE_SECONDS = 3.1f;

        //The approach: a run this long ending this far outside the formation's apron, this low over the ground
        //(spinifex and the odd boulder), the lens pinned on the rock at this fraction of its crown's rise — so the
        //wall grows in the frame as the lens closes on it.
        private const float APPROACH_RUN = 110f;
        private const float APPROACH_STOP_PAST_APRON = 22f;
        private const float APPROACH_CLEARANCE = 2.4f;
        private const float APPROACH_LOOK_FRACTION = 0.45f;
        private const float APPROACH_ARENA_CLEARANCE = 90f;
        private const float APPROACH_SECONDS = 3.2f;

        //The crown: a crane from near the foot to over the crown, this far outside the apron and turning this
        //much round the rock as it rises, the lens on a point that climbs the wall with it — up the wall and over
        //the rounded top.
        private const float CRANE_PAST_APRON = 16f;
        private const float CRANE_SWEEP_RADIANS = 0.45f;
        private const float CRANE_FROM_HEIGHT = 4f;
        private const float CRANE_OVER_CROWN = 18f;
        private const float CRANE_CLEARANCE = 4f;
        private const float CRANE_SECONDS = 3.2f;

        /// <summary>The prologue for the outback being drawn, or null when it has no outback config or no monolith.</summary>
        public static IntroShot[] Build(SceneRenderer scenes, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Outback) is not OutbackSceneConfig outback) return null;

            List<TerrainMirror.Formation> monoliths = TerrainMirror.OutbackMonoliths(outback, MONOLITH_SEARCH_RADIUS);
            if (monoliths.Count == 0) return null;

            //The tallest crown; among near-equals (within a tenth of the rock height) the nearer one, so the
            //choice is not a monolith on the horizon that happens to be two units taller.
            TerrainMirror.Formation rock = monoliths[0];
            foreach (TerrainMirror.Formation candidate in monoliths)
            {
                float taller = candidate.CrownY - rock.CrownY;
                bool nearer = candidate.Centre.Length() < rock.Centre.Length();
                if (taller > outback.Terrain.RockHeight * 0.1f || (taller > -outback.Terrain.RockHeight * 0.1f && nearer)) rock = candidate;
            }

            float Ground(float x, float z) => TerrainMirror.Outback(x, z, outback);

            return new[]
            {
                Plain(rock, Ground, fieldOfView, random),
                Approach(outback, rock, Ground, fieldOfView, random),
                Crown(outback, rock, Ground, fieldOfView, random),
            };
        }

        /// <summary>The establishing view: the island on its plain, the monolith on the skyline behind it.</summary>
        private static IntroShot Plain(TerrainMirror.Formation rock, Func<float, float, float> ground, float fieldOfView, Random random)
        {
            float away = MathF.Atan2(-rock.Centre.Y, -rock.Centre.X) + AridIntroPaths.Roll(random, -0.3f, 0.3f);
            float sign = random.Next(2) == 0 ? 1f : -1f;
            float from = away - sign * WIDE_SWEEP_RADIANS * 0.5f;
            Vector2[] plan = AridIntroPaths.Arc(Vector2.Zero, from, from + sign * WIDE_SWEEP_RADIANS, WIDE_FROM_RADIUS, WIDE_TO_RADIUS);

            float level = ground(0f, 0f);
            Vector3[] path = AridIntroPaths.Hug(plan, ground, WIDE_CLEARANCE, 4f, 6,
                i => level + MathHelper.Lerp(WIDE_FROM_HEIGHT, WIDE_TO_HEIGHT, Smooth(i, plan.Length)));

            return new IntroShot("the plain", path, WIDE_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(0f, ArenaIsland.TOP_Y, 0f));
        }

        /// <summary>
        /// Low over the plain towards the rock: from the arena's side of it, a little off the direct line so the
        /// wall is seen at an angle, the lens on the wall.
        /// </summary>
        private static IntroShot Approach(OutbackSceneConfig outback, TerrainMirror.Formation rock, Func<float, float, float> ground,
            float fieldOfView, Random random)
        {
            Vector2 towardsArena = rock.Centre.LengthSquared() > 1e-4f ? -Vector2.Normalize(rock.Centre) : Vector2.UnitX;
            float arenaBearing = MathF.Atan2(towardsArena.Y, towardsArena.X);

            //In from the rock's SIDE, never from the arena: a run laid on the line from the island starts beside
            //it, a few units under its rim, and the first cut photographed the island's edge and the hanging
            //field sliding past the lens. So the run comes in 50-110 degrees off that line, and a roll whose run
            //still passes within APPROACH_ARENA_CLEARANCE of the arena is refused; if every roll is, the run comes
            //in from the rock's far side, where the arena is behind it.
            Vector2 to = Vector2.Zero, from = Vector2.Zero;
            bool found = false;

            for (int attempt = 0; attempt < 12 && !found; attempt++)
            {
                float side = random.Next(2) == 0 ? 1f : -1f;
                Vector2 outward = AridIntroPaths.Bearing(arenaBearing + side * AridIntroPaths.Roll(random, 0.87f, 1.92f));
                to = rock.Centre + outward * (rock.Reach + APPROACH_STOP_PAST_APRON);
                from = to + outward * APPROACH_RUN;
                found = DistanceToOrigin(from, to) >= APPROACH_ARENA_CLEARANCE;
            }

            if (!found)
            {
                to = rock.Centre - towardsArena * (rock.Reach + APPROACH_STOP_PAST_APRON);
                from = to - towardsArena * APPROACH_RUN;
            }

            Vector3[] path = AridIntroPaths.Hug(AridIntroPaths.Line(from, to), ground, APPROACH_CLEARANCE, 2f, 4);

            float level = outback.Terrain.LevelY;
            Vector3 lookAt = new(rock.Centre.X, level + (rock.CrownY - level) * APPROACH_LOOK_FRACTION, rock.Centre.Y);

            return new IntroShot("the approach", path, APPROACH_SECONDS, fieldOfView * 1.15f, lookAt: lookAt);
        }

        /// <summary>
        /// Up the wall to the crown: a crane from near the foot to over the top, turning a little round the
        /// rock, the lens's aim climbing the wall with it.
        /// </summary>
        private static IntroShot Crown(OutbackSceneConfig outback, TerrainMirror.Formation rock, Func<float, float, float> ground,
            float fieldOfView, Random random)
        {
            //Which side of the rock, and which way round, are rolled: every side of a bornhardt is a wall.
            float from = AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi);
            float sign = random.Next(2) == 0 ? 1f : -1f;
            float radius = rock.Reach + CRANE_PAST_APRON;
            Vector2[] plan = AridIntroPaths.Arc(rock.Centre, from, from + sign * CRANE_SWEEP_RADIANS, radius, radius);

            float level = outback.Terrain.LevelY;
            float top = rock.CrownY + CRANE_OVER_CROWN;
            Vector3[] path = AridIntroPaths.Hug(plan, ground, CRANE_CLEARANCE, 3f, 5,
                i => MathHelper.Lerp(level + CRANE_FROM_HEIGHT, top, Smooth(i, plan.Length)));

            //The look climbs from the lower wall to the crown: pinned at the path's middle height it keeps the
            //wall in frame at the foot and the top at the crest. IntroShot takes one look-at, so it is the point
            //of the rock at the crane's mean height, which the lens passes on the way.
            Vector3 lookAt = new(rock.Centre.X, level + (rock.CrownY - level) * 0.7f, rock.Centre.Y);

            return new IntroShot("the crown", path, CRANE_SECONDS, fieldOfView * 1.15f, lookAt: lookAt);
        }

        //The least distance from the arena's axis to a straight run.
        private static float DistanceToOrigin(Vector2 from, Vector2 to)
        {
            Vector2 run = to - from;
            float t = MathHelper.Clamp(-Vector2.Dot(from, run) / MathF.Max(run.LengthSquared(), 1e-4f), 0f, 1f);
            return (from + run * t).Length();
        }

        private static float Smooth(int i, int count)
        {
            float s = i / (float)(count - 1);
            return s * s * (3f - 2f * s);
        }
    }
}

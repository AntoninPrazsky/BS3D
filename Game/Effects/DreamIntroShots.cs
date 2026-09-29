using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The dream's own shots for a chapter intro's prologue (#559): the island adrift in the marbled sky, a
    /// slow turn round one of the glass solids as it tumbles and melts, and the island seen from underneath,
    /// through its drain — cut together, then cut to the tour's last leg. The dream has no ground and no horizon;
    /// what it has to show are its glass and the island floating among it, and neither stands where the tour's
    /// spline round the arena goes near enough to see what it is.
    /// <para>
    /// <b>The last shot was a dolly into one of the soft orbs (#655)</b>, "the glowing light", and the owner asked for
    /// the view of the island from below in its place, as the Space prologue has one: "that looks good". The dream
    /// island floats as Space's does, so the same crane up under the funnel fits it (see
    /// <see cref="SpaceIntroShots"/>). The orbs are still in the scene and in the tour's own flight; they are only
    /// no longer a shot of their own.
    /// </para>
    /// <para>
    /// <b>The subjects move, so the shots are laid out on the wall clock.</b> The solids orbit on the dream's
    /// own clock (<c>Dream.fx</c>, read here through <see cref="SceneRenderer.DreamSolidCenter"/>, which is the
    /// renderer's wall clock too), so each shot is built for the seconds it will actually play in: the lens is
    /// laid out relative to where its subject will be at each point of its path, the look rides the subject
    /// rather than a point it has left, and a path that has to stay clear of the solids is checked against where
    /// they will be as the lens passes.
    /// </para>
    /// <para>
    /// <b>⚠ The glass is drawn BEHIND everything</b> (the whole pass writes no depth — see "The dream" in
    /// <c>docs/scenes.md</c>), so a solid with the island behind it from the lens would be covered by the
    /// island. The glass shot therefore stands off the solid on a side where the arena is well away from the
    /// line of sight, and never on the far side of it looking in. Built once when the intro begins; nothing
    /// here runs per frame.
    /// </para>
    /// </summary>
    internal static class DreamIntroShots
    {
        //The sky: a slow arc round the arena this far out, rising gently, the lens on the island — so it hangs
        //in the marbling with glass and orbs drifting behind it, the one frame that says where the player is.
        private const float SKY_RADIUS = 78f;
        private const float SKY_FROM_HEIGHT = -2f;
        private const float SKY_TO_HEIGHT = 14f;
        private const float SKY_ARC_DEGREES = 26f;
        private const float SKY_SECONDS = 3.2f;

        //The glass: an arc this far round one solid, at this many of its sizes out and this far above its
        //middle, the look riding the solid.
        private const float GLASS_DISTANCE_IN_SIZES = 2.3f;
        private const float GLASS_ARC_DEGREES = 40f;
        private const float GLASS_RISE = 6f;
        private const float GLASS_SECONDS = 3.4f;

        //The underside (#655): a crane up from this far under the funnel's hole to this far, this far out from the
        //axis, the look on the funnel's middle — the glass, the gold beads and the machined underside against the
        //marbling, which is Space's own drain shot (SpaceIntroShots.Drain) with the dream's figures.
        private const float UNDER_FROM_BELOW = 34f, UNDER_TO_BELOW = 22f;
        private const float UNDER_OUT = 24f;
        private const float UNDER_FOV_DEGREES = 60f;
        private const float UNDER_SECONDS = 3.0f;

        //How far from the arena's axis a lens must keep, and how far off the line of sight the arena must
        //stand (in the glass shot because the island covers whatever solid it stands in front of, in the orb
        //shot because an orb is the subject and the island is not).
        private const float ARENA_CLEARANCE = 48f;
        private const float ARENA_OFF_SIGHT_DEGREES = 50f;

        //How close to any solid's bounding sphere a lens may come: inside it, the march starts at the lens.
        private const float SOLID_MARGIN = 10f;

        private const int CANDIDATES = 48;
        private const int PATH_POINTS = IntroPaths.POINTS;

        /// <summary>
        /// The prologue for the dream being drawn, starting at <paramref name="time"/> on the renderer's wall
        /// clock. <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, float time, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Dream) is not DreamSceneConfig dream) return null;

            IntroShot sky = Sky(scenes, time, fieldOfView, random);
            IntroShot glass = Glass(scenes, dream, time + SKY_SECONDS, fieldOfView, random);
            IntroShot under = Underside(scenes, time + SKY_SECONDS + GLASS_SECONDS, random);

            if (glass == null || under == null)
            {
                //A roll with no clear line to a solid or to the underside (never seen, but the solids wander): the
                //sky alone still says where the player is.
                return glass != null ? new[] { sky, glass } : under != null ? new[] { sky, under } : new[] { sky };
            }

            return new[] { sky, glass, under };
        }

        /// <summary>The island adrift in the marbling: the establishing view, on a bearing clear of the glass.</summary>
        private static IntroShot Sky(SceneRenderer scenes, float time, float fieldOfView, Random random)
        {
            Vector3 island = new(0f, ArenaIsland.TOP_Y + 6f, 0f);
            float arc = MathHelper.ToRadians(SKY_ARC_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);

            Vector3[] best = null;
            for (int attempt = 0; attempt < CANDIDATES && best == null; attempt++)
            {
                float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                var path = new Vector3[PATH_POINTS];

                for (int i = 0; i < PATH_POINTS; i++)
                {
                    float s = i / (float)(PATH_POINTS - 1);
                    float b = bearing + arc * s;
                    path[i] = new Vector3(MathF.Cos(b) * SKY_RADIUS, MathHelper.Lerp(SKY_FROM_HEIGHT, SKY_TO_HEIGHT, s), MathF.Sin(b) * SKY_RADIUS);
                }

                if (ClearOfSolids(scenes, path, time, SKY_SECONDS)) best = path;
            }

            //Every roll crossing a solid would take eight solids crowded onto one ring; keep the last roll.
            best ??= new[] { new Vector3(SKY_RADIUS, SKY_FROM_HEIGHT, 0f), new Vector3(SKY_RADIUS, SKY_TO_HEIGHT, 0f) };

            return new IntroShot("the marbled sky", best, SKY_SECONDS, fieldOfView * 1.2f, lookAt: island);
        }

        /// <summary>A slow turn round one glass solid as it tumbles, the look riding it.</summary>
        private static IntroShot Glass(SceneRenderer scenes, DreamSceneConfig dream, float start, float fieldOfView, Random random)
        {
            float distance = dream.Shapes.Size * GLASS_DISTANCE_IN_SIZES;
            float arc = MathHelper.ToRadians(GLASS_ARC_DEGREES);
            float offSight = MathF.Cos(MathHelper.ToRadians(ARENA_OFF_SIGHT_DEGREES));

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                int solid = random.Next(SceneRenderer.DREAM_SOLID_COUNT);
                float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                float sign = random.Next(2) == 0 ? 1f : -1f;

                var path = new Vector3[PATH_POINTS];
                var look = new Vector3[PATH_POINTS];
                bool clear = true;

                for (int i = 0; i < PATH_POINTS && clear; i++)
                {
                    float s = i / (float)(PATH_POINTS - 1);
                    Vector3 centre = scenes.DreamSolidCenter(solid, start + s * GLASS_SECONDS, out _);
                    float b = bearing + sign * arc * s;

                    Vector3 lens = centre + new Vector3(MathF.Cos(b) * distance, GLASS_RISE, MathF.Sin(b) * distance);
                    path[i] = lens;
                    look[i] = centre;

                    //Off the arena's axis, and the arena well off the line of sight: the island is drawn over
                    //the glass, never behind it.
                    if (new Vector2(lens.X, lens.Z).Length() < ARENA_CLEARANCE) clear = false;

                    Vector3 toSolid = Vector3.Normalize(centre - lens);
                    Vector3 toArena = Vector3.Normalize(new Vector3(0f, ArenaIsland.TOP_Y, 0f) - lens);
                    if (Vector3.Dot(toSolid, toArena) > offSight) clear = false;
                }

                if (!clear || !ClearOfSolids(scenes, path, start, GLASS_SECONDS, solid)) continue;

                return new IntroShot("the glass", path, GLASS_SECONDS, fieldOfView * 1.1f, lookAtPath: look);
            }

            return null;
        }

        /// <summary>
        /// The island from underneath (#655): a crane rising towards the drain's hole from below and to one side, the
        /// look on the funnel's middle. ⚠ The whole glass pass writes no depth (see the class comment), so a solid
        /// standing between the lens and the island would be drawn BEHIND the island it is in front of: the path is
        /// kept clear of every solid, and so is the line of sight to the funnel.
        /// </summary>
        private static IntroShot Underside(SceneRenderer scenes, float start, Random random)
        {
            float hole = ArenaIsland.FUNNEL_BOTTOM_Y;
            Vector3 funnel = new(0f, (hole + ArenaIsland.TOP_Y) * 0.5f, 0f);

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                Vector3 outward = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));

                Vector3[] path = IntroPaths.Line(outward * UNDER_OUT + Vector3.Up * (hole - UNDER_FROM_BELOW),
                    outward * (UNDER_OUT * 0.85f) + Vector3.Up * (hole - UNDER_TO_BELOW), PATH_POINTS);

                if (!ClearOfSolids(scenes, path, start, UNDER_SECONDS) || !LineOfSightClear(scenes, path, funnel, start, UNDER_SECONDS)) continue;

                return new IntroShot("the underside", path, UNDER_SECONDS, MathHelper.ToRadians(UNDER_FOV_DEGREES), lookAt: funnel);
            }

            return null;
        }

        //True when no solid stands (within its bound and the margin) on the line from the lens to the look-at, at the
        //moment the lens is there — sampled along the line, since the solids are big and slow
        private static bool LineOfSightClear(SceneRenderer scenes, Vector3[] path, Vector3 target, float start, float seconds)
        {
            const int SAMPLES = 8;

            for (int i = 0; i < path.Length; i++)
            {
                float t = start + seconds * i / (path.Length - 1f);

                for (int solid = 0; solid < SceneRenderer.DREAM_SOLID_COUNT; solid++)
                {
                    Vector3 centre = scenes.DreamSolidCenter(solid, t, out float bound);

                    for (int k = 1; k < SAMPLES; k++)
                        if (Vector3.Distance(centre, Vector3.Lerp(path[i], target, k / (float)SAMPLES)) < bound + SOLID_MARGIN) return false;
                }
            }

            return true;
        }

        //True when no point of the path, at the moment the lens passes it, stands inside any solid's bounding
        //sphere (bar the subject's own, which the glass shot keeps its distance from by construction).
        private static bool ClearOfSolids(SceneRenderer scenes, Vector3[] path, float start, float seconds, int except = -1)
        {
            for (int i = 0; i < path.Length; i++)
            {
                float t = start + seconds * i / (path.Length - 1f);

                for (int solid = 0; solid < SceneRenderer.DREAM_SOLID_COUNT; solid++)
                {
                    if (solid == except) continue;

                    Vector3 centre = scenes.DreamSolidCenter(solid, t, out float bound);
                    if (Vector3.Distance(centre, path[i]) < bound + SOLID_MARGIN) return false;
                }
            }

            return true;
        }
    }
}

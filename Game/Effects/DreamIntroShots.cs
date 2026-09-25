using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The dream's own shots for a chapter intro's prologue (#559): the island adrift in the marbled sky, a
    /// slow turn round one of the glass solids as it tumbles and melts, and a dolly into one of the soft orbs —
    /// cut together, then cut to the tour's last leg. The dream has no ground and no horizon; what it has to
    /// show are its two kinds of thing, the sharp glass and the blurred light, and neither stands where the
    /// tour's spline round the arena goes near enough to see what it is.
    /// <para>
    /// <b>The subjects move, so the shots are laid out on the wall clock.</b> Both kinds orbit on the dream's
    /// own clock (<c>Dream.fx</c>, read here through <see cref="SceneRenderer.DreamSolidCenter"/> and
    /// <see cref="SceneRenderer.DreamOrbCenter"/>, which is the renderer's wall clock too), so each shot is
    /// built for the seconds it will actually play in: the lens is laid out relative to where its subject
    /// will be at each point of its path, and the look rides the subject rather than a point it has left.
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

        //The orb: a dolly in from this far to this far, the look on the orb. It has no surface — the closest
        //the lens comes is still well outside its glow's own radius, where the light swells rather than fills.
        private const float ORB_FROM = 170f;
        private const float ORB_TO = 90f;
        private const float ORB_SECONDS = 3.0f;

        //How far from the arena's axis a lens must keep, and how far off the line of sight the arena must
        //stand (in the glass shot because the island covers whatever solid it stands in front of, in the orb
        //shot because an orb is the subject and the island is not).
        private const float ARENA_CLEARANCE = 48f;
        private const float ARENA_OFF_SIGHT_DEGREES = 50f;

        //How close to any solid's bounding sphere a lens may come: inside it, the march starts at the lens.
        private const float SOLID_MARGIN = 10f;

        private const int CANDIDATES = 48;
        private const int PATH_POINTS = 64;

        /// <summary>
        /// The prologue for the dream being drawn, starting at <paramref name="time"/> on the renderer's wall
        /// clock. <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, float time, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Dream) is not DreamSceneConfig dream) return null;

            IntroShot sky = Sky(scenes, time, fieldOfView, random);
            IntroShot glass = Glass(scenes, dream, time + SKY_SECONDS, fieldOfView, random);
            IntroShot orb = Orb(scenes, time + SKY_SECONDS + GLASS_SECONDS, fieldOfView, random);

            if (glass == null || orb == null)
            {
                //A roll with no clear line to a solid or an orb (never seen, but the solids wander): the sky
                //alone still says where the player is.
                return glass != null ? new[] { sky, glass } : orb != null ? new[] { sky, orb } : new[] { sky };
            }

            return new[] { sky, glass, orb };
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

        /// <summary>A dolly into one of the soft orbs, from the arena's side, the look riding it.</summary>
        private static IntroShot Orb(SceneRenderer scenes, float start, float fieldOfView, Random random)
        {
            float offSight = MathF.Cos(MathHelper.ToRadians(ARENA_OFF_SIGHT_DEGREES));

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                int orb = random.Next(SceneRenderer.DREAM_ORB_COUNT);
                Vector3 first = scenes.DreamOrbCenter(orb, start, out _);

                //In along a line from roughly the arena's side, turned off it so the island is out of frame
                //behind the lens's shoulder, and a little from below: an orb against the marbling above it.
                Vector2 inward = -Vector2.Normalize(new Vector2(first.X, first.Z));
                float turn = MathHelper.ToRadians(MathHelper.Lerp(35f, 70f, (float)random.NextDouble())) * (random.Next(2) == 0 ? 1f : -1f);
                float c = MathF.Cos(turn), sn = MathF.Sin(turn);
                Vector3 away = Vector3.Normalize(new Vector3(inward.X * c - inward.Y * sn, -0.25f, inward.X * sn + inward.Y * c));

                var path = new Vector3[PATH_POINTS];
                var look = new Vector3[PATH_POINTS];
                bool clear = true;

                for (int i = 0; i < PATH_POINTS && clear; i++)
                {
                    float s = i / (float)(PATH_POINTS - 1);
                    Vector3 centre = scenes.DreamOrbCenter(orb, start + s * ORB_SECONDS, out _);
                    Vector3 lens = centre + away * MathHelper.Lerp(ORB_FROM, ORB_TO, s);

                    path[i] = lens;
                    look[i] = centre;

                    if (new Vector2(lens.X, lens.Z).Length() < ARENA_CLEARANCE && lens.Y > ArenaIsland.TOP_Y - 40f) clear = false;

                    //And the arena out of the shot: an orb that has drifted in near the axis puts the start of
                    //the dolly past the arena, looking back across it. ⚠ The first cut had only the line from
                    //the arena to steer by and photographed the island at the frame's edge, seen from below.
                    Vector3 toOrb = Vector3.Normalize(centre - lens);
                    Vector3 toArena = Vector3.Normalize(new Vector3(0f, ArenaIsland.TOP_Y, 0f) - lens);
                    if (Vector3.Dot(toOrb, toArena) > offSight) clear = false;
                }

                if (!clear || !ClearOfSolids(scenes, path, start, ORB_SECONDS)) continue;

                return new IntroShot("the orb", path, ORB_SECONDS, fieldOfView * 1.1f, lookAtPath: look);
            }

            return null;
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

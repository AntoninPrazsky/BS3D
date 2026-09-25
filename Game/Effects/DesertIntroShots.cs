using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The desert's own shots for a chapter intro's prologue (#559): the erg round the island from high over the
    /// dunes, a low run downwind that climbs a dune's windward slope and crests it over the slip face, and the
    /// birds wheeling over the sand from down among the dunes — cut together, then cut to the tour's last leg.
    /// <para>
    /// <b>The dunes are read off <see cref="TerrainMirror.Desert"/></b>, the CPU mirror of <c>Desert.fx</c>'s
    /// own height field, so the crest run knows where the crests are and the lens clears each one by a stated
    /// height (<see cref="AridIntroPaths.Hug"/>); the flock is the scene's own
    /// (<see cref="DesertSceneConfig.Birds"/>), whose circles are laid round a fixed centre. The dunes are not
    /// seeded by <c>sceneseed=</c>, so every roll here chooses among the same land. Built once when the intro
    /// begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class DesertIntroShots
    {
        //The erg: a slow arc round the arena this far out, dollying in and craning up over the dunes, the lens on
        //the island. The ground is cleared by at least WIDE_CLEARANCE wherever the arc crosses a crest.
        private const float WIDE_FROM_RADIUS = 250f;
        private const float WIDE_TO_RADIUS = 210f;
        private const float WIDE_SWEEP_RADIANS = 0.28f;
        private const float WIDE_FROM_HEIGHT = 32f;
        private const float WIDE_TO_HEIGHT = 56f;
        private const float WIDE_CLEARANCE = 12f;
        private const float WIDE_SECONDS = 3.2f;

        //The crest: a run this long DOWNWIND — up a windward slope, over the knife edge, down the slip face — this
        //high over the highest sand within the dilation window, somewhere in the band of full dunes past the
        //clearing's ramp. Of CREST_CANDIDATES rolled runs the one with the most relief under it is taken, since a
        //run along an interdune flat is a shot of nothing. Pitched down a little so the sand is the frame.
        private const float CREST_RUN = 95f;
        private const float CREST_CLEARANCE = 2.6f;
        private const float CREST_LATERAL = 2.5f;
        private const int CREST_WINDOW = 5;
        private const float CREST_INNER = 215f;
        private const float CREST_OUTER = 360f;
        private const int CREST_CANDIDATES = 48;
        private const float CREST_PITCH_DOWN_DEGREES = 7f;
        private const float CREST_SECONDS = 3.3f;

        //The flock: a drift this long, low over the sand, this far out from the flock's centre (its birds circle
        //at 28-62 units round it, so the lens stands outside every circle), the lens on the flock. The birds fly
        //over the arena's end of the sand, so the stand is turned off the line to the arena by FLOCK_OFF_ARENA:
        //the island is at the edge of the frame rather than behind the birds.
        private const float FLOCK_STAND = 88f;
        private const float FLOCK_DRIFT = 16f;
        private const float FLOCK_CLEARANCE = 2.2f;
        private const float FLOCK_OFF_ARENA_RADIANS = 1.35f;
        private const float FLOCK_SECONDS = 2.9f;

        /// <summary>The prologue for the desert being drawn, or null when there is no desert config.</summary>
        public static IntroShot[] Build(SceneRenderer scenes, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Desert) is not DesertSceneConfig desert) return null;

            float Ground(float x, float z) => TerrainMirror.Desert(x, z, desert);

            IntroShot erg = Erg(Ground, fieldOfView, random);
            IntroShot crest = Crest(desert, Ground, fieldOfView, random);
            IntroShot flock = Flock(desert, Ground, fieldOfView, random);

            return flock == null ? new[] { erg, crest } : new[] { erg, crest, flock };
        }

        /// <summary>The establishing view: over the dunes looking back at the island standing in its clearing.</summary>
        private static IntroShot Erg(Func<float, float, float> ground, float fieldOfView, Random random)
        {
            float from = AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi);
            float sign = random.Next(2) == 0 ? 1f : -1f;
            Vector2[] plan = AridIntroPaths.Arc(Vector2.Zero, from, from + sign * WIDE_SWEEP_RADIANS, WIDE_FROM_RADIUS, WIDE_TO_RADIUS);

            //The crane is the designed height; the dunes can only lift it.
            float level = ground(0f, 0f);
            Vector3[] path = AridIntroPaths.Hug(plan, ground, WIDE_CLEARANCE, 4f, 6,
                i => level + Crane(i, plan.Length, WIDE_FROM_HEIGHT, WIDE_TO_HEIGHT));

            return new IntroShot("the erg", path, WIDE_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(0f, ArenaIsland.TOP_Y, 0f));
        }

        /// <summary>
        /// Low and downwind over the dunes: the lens climbs a windward slope and tips over the crest, so the next
        /// dune's slip face opens under it. Of a few dozen runs rolled in the band of full dunes, the one with the
        /// most relief under it.
        /// </summary>
        private static IntroShot Crest(DesertSceneConfig desert, Func<float, float, float> ground, float fieldOfView, Random random)
        {
            Vector2 wind = TerrainMirror.DesertWind(desert);

            float bestRelief = -1f;
            Vector2 bestFrom = Vector2.Zero, bestTo = Vector2.Zero;

            for (int attempt = 0; attempt < CREST_CANDIDATES; attempt++)
            {
                float bearing = AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi);
                Vector2 middle = AridIntroPaths.Bearing(bearing) * AridIntroPaths.Roll(random, CREST_INNER + CREST_RUN * 0.5f, CREST_OUTER - CREST_RUN * 0.5f);
                Vector2 from = middle - wind * (CREST_RUN * 0.5f);
                Vector2 to = middle + wind * (CREST_RUN * 0.5f);

                //Both ends in the band, so the run never drops into the clearing's ramp.
                if (from.Length() < CREST_INNER || to.Length() < CREST_INNER) continue;

                float low = float.MaxValue, high = float.MinValue;
                for (int k = 0; k <= 16; k++)
                {
                    Vector2 at = Vector2.Lerp(from, to, k / 16f);
                    float h = ground(at.X, at.Y);
                    low = MathF.Min(low, h);
                    high = MathF.Max(high, h);
                }

                if (high - low <= bestRelief) continue;

                bestRelief = high - low;
                bestFrom = from;
                bestTo = to;
            }

            //Every candidate refused only if the band is thinner than the run, which the shipped config is not;
            //then the run goes straight downwind through the band's middle.
            if (bestRelief < 0f)
            {
                Vector2 across = new(-wind.Y, wind.X);
                bestFrom = across * ((CREST_INNER + CREST_OUTER) * 0.5f) - wind * (CREST_RUN * 0.5f);
                bestTo = bestFrom + wind * CREST_RUN;
            }

            Vector3[] path = AridIntroPaths.Hug(AridIntroPaths.Line(bestFrom, bestTo), ground, CREST_CLEARANCE, CREST_LATERAL, CREST_WINDOW);

            return new IntroShot("the crest", path, CREST_SECONDS, fieldOfView * 1.15f,
                lookAhead: 16f, pitchDownDegrees: CREST_PITCH_DOWN_DEGREES);
        }

        /// <summary>
        /// The birds from the sand: a slow drift low among the dunes outside the flock's circles, the lens on the
        /// flock's centre so the birds wheel through the sky over the crests. Null when the desert has no birds.
        /// </summary>
        private static IntroShot Flock(DesertSceneConfig desert, Func<float, float, float> ground, float fieldOfView, Random random)
        {
            if (desert.Birds == null || desert.Birds.Count <= 0) return null;

            Vector3 centre = desert.Birds.FlockCenter.ToVector3();
            Vector2 flock = new(centre.X, centre.Z);

            //Off the flock, turned away from the arena's line to one side or the other.
            Vector2 towardsArena = flock.LengthSquared() > 1e-4f ? -Vector2.Normalize(flock) : Vector2.UnitX;
            float turn = (random.Next(2) == 0 ? 1f : -1f) * (FLOCK_OFF_ARENA_RADIANS + AridIntroPaths.Roll(random, -0.25f, 0.25f));
            float bearing = MathF.Atan2(towardsArena.Y, towardsArena.X) + MathHelper.Pi + turn;
            Vector2 outward = AridIntroPaths.Bearing(bearing);
            Vector2 across = new(-outward.Y, outward.X);

            Vector2 stand = flock + outward * FLOCK_STAND;
            Vector2[] plan = AridIntroPaths.Line(stand - across * (FLOCK_DRIFT * 0.5f), stand + across * (FLOCK_DRIFT * 0.5f));
            Vector3[] path = AridIntroPaths.Hug(plan, ground, FLOCK_CLEARANCE, 2f, 6);

            return new IntroShot("the flock", path, FLOCK_SECONDS, fieldOfView * 1.2f, lookAt: centre);
        }

        //A crane's height at point i: smoothstepped between the two.
        private static float Crane(int i, int count, float from, float to)
        {
            float s = i / (float)(count - 1);
            return MathHelper.Lerp(from, to, s * s * (3f - 2f * s));
        }
    }
}

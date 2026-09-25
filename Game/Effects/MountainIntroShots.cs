using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The mountains' own shots for a chapter intro's prologue (#559): the range round the basin from high over
    /// the arena's end of it, a flight up a valley and over the lowest saddle the range offers, and a slow turn
    /// round the highest summit — cut together, then cut to the tour's last leg.
    /// <para>
    /// <b>Every height is read off <see cref="TerrainMirror.Mountain"/></b>, the CPU mirror of
    /// <c>Mountain.fx</c>'s <c>TerrainHeight</c> — the ridged field, the massifs and the basin — so the summit is
    /// the field's own highest point, the saddle its own lowest crossing, and the lens clears the rock by a stated
    /// height along every path (<see cref="AridIntroPaths.Hug"/>). The range is not seeded by
    /// <c>sceneseed=</c>, so every roll chooses among the same peaks. Built once when the intro begins: the
    /// summit search is some thirteen thousand samples of a five-octave field, once — nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class MountainIntroShots
    {
        //The range: the lens inside the basin, high over one side of the arena, trucking sideways, looking
        //across the island at the range on the far side — the island in the lower frame, the peaks behind it.
        private const float WIDE_STAND = 80f;
        private const float WIDE_TRUCK = 40f;
        private const float WIDE_FROM_HEIGHT = 48f;
        private const float WIDE_TO_HEIGHT = 62f;
        private const float WIDE_LOOK_OUT = 300f;
        private const float WIDE_LOOK_HEIGHT = 30f;
        private const float WIDE_CLEARANCE = 12f;
        private const float WIDE_SECONDS = 3.1f;

        //The pass: of PASS_BEARINGS radial runs from the basin out into the range, the one whose highest ground
        //is LOWEST — the saddle — flown outward this high over the rock, looking along the travel.
        private const int PASS_BEARINGS = 36;
        private const float PASS_FROM = 150f;
        private const float PASS_TO = 255f;
        private const float PASS_CLEARANCE = 14f;
        private const int PASS_WINDOW = 5;
        private const float PASS_PITCH_DOWN_DEGREES = 3f;
        private const float PASS_SECONDS = 3.3f;

        //The summit: the highest ground within SUMMIT_INNER..SUMMIT_OUTER of the arena, sampled every
        //SUMMIT_STEP units; the lens turns round it this far out, this far UNDER the summit (the ground only
        //lifts it), the look on it — so the peak stands over the lens against the sky. ⚠ Ten units OVER it, the
        //first cut looked level across a row of crests of one height, and which of them was the summit could
        //not be told.
        private const float SUMMIT_INNER = 170f;
        private const float SUMMIT_OUTER = 420f;
        private const float SUMMIT_STEP = 6f;
        private const float SUMMIT_ORBIT = 70f;
        private const float SUMMIT_UNDER = 28f;
        private const float SUMMIT_SWEEP_RADIANS = 0.55f;
        private const float SUMMIT_CLEARANCE = 10f;
        private const float SUMMIT_SECONDS = 3.2f;

        /// <summary>The prologue for the range being drawn, or null when there is no mountain config.</summary>
        public static IntroShot[] Build(SceneRenderer scenes, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Mountain) is not MountainSceneConfig mountain) return null;

            float Ground(float x, float z) => TerrainMirror.Mountain(x, z, mountain);

            return new[]
            {
                Range(mountain, Ground, fieldOfView, random),
                Pass(Ground, fieldOfView, random),
                Summit(Ground, fieldOfView, random),
            };
        }

        /// <summary>The establishing view: across the island at the range on the basin's far side.</summary>
        private static IntroShot Range(MountainSceneConfig mountain, Func<float, float, float> ground, float fieldOfView, Random random)
        {
            float bearing = AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi);
            Vector2 stand = AridIntroPaths.Bearing(bearing) * WIDE_STAND;
            Vector2 across = AridIntroPaths.Bearing(bearing + MathHelper.PiOver2) * (random.Next(2) == 0 ? 1f : -1f);

            Vector2[] plan = AridIntroPaths.Line(stand - across * (WIDE_TRUCK * 0.5f), stand + across * (WIDE_TRUCK * 0.5f));
            Vector3[] path = AridIntroPaths.Hug(plan, ground, WIDE_CLEARANCE, 4f, 6,
                i => mountain.LevelY + MathHelper.Lerp(WIDE_FROM_HEIGHT, WIDE_TO_HEIGHT, i / (float)(plan.Length - 1)));

            Vector2 far = -AridIntroPaths.Bearing(bearing) * WIDE_LOOK_OUT;
            return new IntroShot("the range", path, WIDE_SECONDS, fieldOfView * 1.2f,
                lookAt: new Vector3(far.X, mountain.LevelY + WIDE_LOOK_HEIGHT, far.Y));
        }

        /// <summary>Up a valley and over the lowest saddle: the radial run whose highest ground is least.</summary>
        private static IntroShot Pass(Func<float, float, float> ground, float fieldOfView, Random random)
        {
            float offset = AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi / PASS_BEARINGS);
            float bestBearing = offset, bestHigh = float.MaxValue;

            for (int b = 0; b < PASS_BEARINGS; b++)
            {
                float bearing = offset + b * MathHelper.TwoPi / PASS_BEARINGS;
                Vector2 direction = AridIntroPaths.Bearing(bearing);

                float high = float.MinValue;
                for (float r = PASS_FROM; r <= PASS_TO; r += 5f)
                    high = MathF.Max(high, ground(direction.X * r, direction.Y * r));

                if (high >= bestHigh) continue;

                bestHigh = high;
                bestBearing = bearing;
            }

            Vector2 outward = AridIntroPaths.Bearing(bestBearing);
            Vector3[] path = AridIntroPaths.Hug(AridIntroPaths.Line(outward * PASS_FROM, outward * PASS_TO), ground,
                PASS_CLEARANCE, 4f, PASS_WINDOW);

            return new IntroShot("the pass", path, PASS_SECONDS, fieldOfView * 1.15f,
                lookAhead: 24f, pitchDownDegrees: PASS_PITCH_DOWN_DEGREES);
        }

        /// <summary>Round the highest summit, a little under it, the look on the peak.</summary>
        private static IntroShot Summit(Func<float, float, float> ground, float fieldOfView, Random random)
        {
            Vector2 peak = new(SUMMIT_INNER, 0f);
            float peakY = float.MinValue;

            for (float x = -SUMMIT_OUTER; x <= SUMMIT_OUTER; x += SUMMIT_STEP)
            {
                for (float z = -SUMMIT_OUTER; z <= SUMMIT_OUTER; z += SUMMIT_STEP)
                {
                    float r2 = x * x + z * z;
                    if (r2 < SUMMIT_INNER * SUMMIT_INNER || r2 > SUMMIT_OUTER * SUMMIT_OUTER) continue;

                    float h = ground(x, z);
                    if (h <= peakY) continue;

                    peakY = h;
                    peak = new Vector2(x, z);
                }
            }

            //From the arena's side of the peak, turning to one side, so the summit stands against the sky beyond.
            float towardsArena = MathF.Atan2(-peak.Y, -peak.X);
            float sign = random.Next(2) == 0 ? 1f : -1f;
            float from = towardsArena - sign * SUMMIT_SWEEP_RADIANS * 0.5f;
            Vector2[] plan = AridIntroPaths.Arc(peak, from, from + sign * SUMMIT_SWEEP_RADIANS, SUMMIT_ORBIT, SUMMIT_ORBIT);

            Vector3[] path = AridIntroPaths.Hug(plan, ground, SUMMIT_CLEARANCE, 4f, 5, _ => peakY - SUMMIT_UNDER);

            return new IntroShot("the summit", path, SUMMIT_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(peak.X, peakY, peak.Y));
        }
    }
}

using Microsoft.Xna.Framework;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The path arithmetic the open-ground prologues share (#559: the sea, the desert, the outback and the
    /// mountains) — a plan laid out on the ground (a straight run, an arc round a point), and the height a lens
    /// needs over it to stay out of the land. The volcano's and the aurora's shots read their ground point by
    /// point with a clearance, which is enough on a smooth cone or under a wood's crowns; a dune's crest and a
    /// monolith's wall are corners, and a lens that follows the ground point by point either clips the corner
    /// between two points or leaps over it in one frame.
    /// <para>
    /// <b>So the ground is dilated before it is followed</b> (<see cref="Hug"/>): each point takes the highest
    /// ground within a few points either side — and a little way off the line on both hands — plus the
    /// clearance, and that is then averaged over no more than the same span. An average over a window no wider
    /// than the dilation's cannot fall below any point's own clearance (every value it averages was already
    /// raised over that point), so the lens rises BEFORE a crest and settles after it, never through it.
    /// Built once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class AridIntroPaths
    {
        /// <summary>Points per path. Evenly spaced along the plan, which is what <see cref="IntroShot"/> walks.</summary>
        public const int POINTS = 96;

        /// <summary>A straight run from one ground point to another, evenly spaced.</summary>
        public static Vector2[] Line(Vector2 from, Vector2 to)
        {
            var plan = new Vector2[POINTS];
            for (int i = 0; i < POINTS; i++) plan[i] = Vector2.Lerp(from, to, i / (float)(POINTS - 1));

            return plan;
        }

        /// <summary>
        /// An arc round <paramref name="centre"/> from one bearing to another, its radius running from
        /// <paramref name="fromRadius"/> to <paramref name="toRadius"/> — a gentle spiral when they differ.
        /// </summary>
        public static Vector2[] Arc(Vector2 centre, float fromBearing, float toBearing, float fromRadius, float toRadius)
        {
            var plan = new Vector2[POINTS];
            for (int i = 0; i < POINTS; i++)
            {
                float s = i / (float)(POINTS - 1);
                float bearing = MathHelper.Lerp(fromBearing, toBearing, s);
                float radius = MathHelper.Lerp(fromRadius, toRadius, s);
                plan[i] = centre + new Vector2(MathF.Cos(bearing), MathF.Sin(bearing)) * radius;
            }

            return plan;
        }

        /// <summary>
        /// The lens's heights over a plan: <paramref name="clearance"/> over the highest ground within
        /// <paramref name="window"/> points either side and <paramref name="lateral"/> units off the line,
        /// averaged over the same window — see the class remarks for why that can never dip under a crest.
        /// Where <paramref name="floor"/> is given, a point is never lower than it (a designed crane or a fixed
        /// height the ground only ever lifts).
        /// </summary>
        public static Vector3[] Hug(Vector2[] plan, Func<float, float, float> ground, float clearance, float lateral,
            int window, Func<int, float> floor = null)
        {
            int n = plan.Length;
            var raised = new float[n];

            for (int i = 0; i < n; i++)
            {
                Vector2 along = plan[Math.Min(i + 1, n - 1)] - plan[Math.Max(i - 1, 0)];
                along = along.LengthSquared() > 1e-8f ? Vector2.Normalize(along) : Vector2.UnitX;
                Vector2 across = new(-along.Y, along.X);

                float highest = ground(plan[i].X, plan[i].Y);
                for (int k = 1; k <= 2; k++)
                {
                    float offset = lateral * k * 0.5f;
                    highest = MathF.Max(highest, Sample(ground, plan[i] + across * offset));
                    highest = MathF.Max(highest, Sample(ground, plan[i] - across * offset));
                    highest = MathF.Max(highest, Sample(ground, plan[i] + along * offset));
                    highest = MathF.Max(highest, Sample(ground, plan[i] - along * offset));
                }

                raised[i] = highest + clearance;
                if (floor != null) raised[i] = MathF.Max(raised[i], floor(i));
            }

            var dilated = new float[n];
            for (int i = 0; i < n; i++)
            {
                float top = float.MinValue;
                for (int j = Math.Max(0, i - window); j <= Math.Min(n - 1, i + window); j++) top = MathF.Max(top, raised[j]);
                dilated[i] = top;
            }

            var path = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int j = Math.Max(0, i - window); j <= Math.Min(n - 1, i + window); j++)
                {
                    sum += dilated[j];
                    count++;
                }

                path[i] = new Vector3(plan[i].X, sum / count, plan[i].Y);
            }

            return path;
        }

        /// <summary>A unit vector on the ground plane at a bearing (radians, from +X towards +Z).</summary>
        public static Vector2 Bearing(float radians) => new(MathF.Cos(radians), MathF.Sin(radians));

        /// <summary>A roll in [from, to).</summary>
        public static float Roll(Random random, float from, float to) => from + (to - from) * (float)random.NextDouble();


        private static float Sample(Func<float, float, float> ground, Vector2 at) => ground(at.X, at.Y);
    }
}

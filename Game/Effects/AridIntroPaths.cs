using Microsoft.Xna.Framework;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The path arithmetic the open-ground prologues share (#559: the sea, the desert, the outback and the
    /// mountains) — a plan laid out on the ground (a straight run, an arc round a point), and the height a lens
    /// needs over it to stay out of the land. A dune's crest and a monolith's wall are corners, and a lens that
    /// follows the ground point by point either clips the corner between two points or leaps over it in one frame.
    /// <para>
    /// <b>So the lens flies the ground the way a drone does</b> (<see cref="Hug"/>, #1109, #1112): its height is the
    /// ground's own, clearance added, rounded over <see cref="ROUNDING_SECONDS"/> of the shot's travel, and lifted
    /// back over the clearance by a swell no sharper than <see cref="LIFT_SECONDS"/> wherever the rounding cut under
    /// it. Built once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class AridIntroPaths
    {
        /// <summary>Points per path. Evenly spaced along the plan, which is what <see cref="IntroShot"/> walks.</summary>
        public const int POINTS = 96;

        /// <summary>
        /// How much of the shot's travel the lens's height is rounded over (#1109, #1112): the sigma of a Gaussian, in
        /// seconds at the shot's peak speed, so a fast run is rounded over as much time as a slow one rather than as much
        /// ground. The ride's smoothness is set here: a dune crossed in under about two of these is flown over rather
        /// than into, and the pyramids' run at 44 u/s and the crest's at 22 are rounded alike.
        /// </summary>
        public const float ROUNDING_SECONDS = 0.7f;

        /// <summary>
        /// How sharply the lens is lifted back where the rounding cut a crest under the clearance (#1109): the sigma of
        /// the lift's Gaussian, in seconds of travel. The crest's cut is first held over three of these either side of
        /// it and then averaged over three more, so the lift is full at the crest and tapers to nothing six of these
        /// either side of it, in travel at the shot's peak speed: the lens starts to rise that long before a crest it was
        /// cut under and comes down as long after it. ⚠ On a short low run that is most of the shot: one 3-unit outcrop
        /// near the end of one roll of the outback's approach lifts the lens from a sixth of the way in, and the run ends
        /// five units over its clearance rather than one.
        /// </summary>
        public const float LIFT_SECONDS = 0.4f;

        /// <summary>A straight run from one ground point to another, evenly spaced.</summary>
        public static Vector2[] Line(Vector2 from, Vector2 to)
        {
            var plan = new Vector2[POINTS];
            for (int i = 0; i < POINTS; i++) plan[i] = Vector2.Lerp(from, to, i / (float)(POINTS - 1));

            return plan;
        }

        /// <summary>
        /// A line read off something curved (the volcano's river, #1220), laid evenly along its own length in
        /// <see cref="POINTS"/> points: what <see cref="IntroShot"/> walks, whose clock is a uniform speed only over evenly
        /// spaced points. Straight between the line's own points, so they must be close enough for that not to show.
        /// </summary>
        public static Vector2[] Even(Vector2[] line, int count = POINTS)
        {
            var lengths = new float[line.Length];
            for (int i = 1; i < line.Length; i++) lengths[i] = lengths[i - 1] + Vector2.Distance(line[i], line[i - 1]);

            var plan = new Vector2[count];
            int j = 0;
            for (int k = 0; k < count; k++)
            {
                float s = lengths[^1] * k / (count - 1);
                while (j < line.Length - 2 && lengths[j + 1] < s) j++;
                float f = (s - lengths[j]) / MathF.Max(lengths[j + 1] - lengths[j], 1e-6f);
                plan[k] = Vector2.Lerp(line[j], line[j + 1], MathHelper.Clamp(f, 0f, 1f));
            }

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
        /// The lens's heights over a plan flown in <paramref name="seconds"/> (#1109, #1112): never under
        /// <paramref name="clearance"/> over the highest ground at a point and on all four hands of it out to
        /// <paramref name="lateral"/>, laid on <paramref name="floor"/> where one is given (a designed crane, a fixed
        /// height or the crater's ramp, which the ground only ever lifts), and changing height the way a drone does —
        /// over a second, never from one frame to the next. Two steps:
        /// <list type="number">
        /// <item><b>Rounded</b>: each point is the Gaussian average of those heights over <see cref="ROUNDING_SECONDS"/>
        /// of travel either side. A Gaussian keeps a straight climb and a level height exactly as they are and takes the
        /// ground's roughness off, which the box averages it replaced could not: a box has a corner at each end, and the
        /// lens's vertical acceleration jumped at every one (#645). A curve it rounds: a crane's smoothstep comes out
        /// nearer a straight climb, over the smoothstep in its lower half and under it in its upper half by about a
        /// fourteenth of its rise on a 3.2 s shot, its foot and its top where they were laid (the ends, below).</item>
        /// <item><b>Lifted</b> where the rounding cut a crest under the ground's clearance: by the deepest cut within the
        /// lift's reach, averaged by a Gaussian of <see cref="LIFT_SECONDS"/> no wider than that reach. Every value
        /// averaged was taken over the point itself, so every point is lifted by at least its own cut and the lens keeps
        /// the clearance at every point of the path by construction; and the lift is a smooth swell, not a step. ⚠ What
        /// the rounding takes off a floor's own curve is NOT lifted back: the lift's reach carries a cut on to points
        /// that had none, so lifting a crane's upper half carried its top over the design (the outback crown 4.5 units
        /// over its crown, every roll).</item>
        /// </list>
        /// <para>
        /// ⚠ <b>The heights are carried on past both ends</b> for the Gaussian's whole reach, so the first and last points
        /// are rounded like any other: the box average simply stopped at the ends, and the first point where its window
        /// fitted whole was a kink: the worst vertical acceleration of seven of the eleven shots, 36-159 u/s², a quarter
        /// to half a second after the cut. Open ground
        /// is read on along the plan's first and last directions; over a floor the heights are turned over their end
        /// point (2 h(end) - h(end ∓ k)) instead, which keeps a ramp running and a crane's foot where it was laid (read
        /// on, the level plain beside the outback crane's foot rounded the foot four units up).
        /// </para>
        /// <para>
        /// <paramref name="rise"/> is for a shot meant to climb well before the ground does (the volcano's crater run):
        /// the heights rounded are then the highest within that many seconds of travel either side, which carries the
        /// lens high over the flank ahead of it; the lift still answers to each point's own clearance.
        /// </para>
        /// </summary>
        public static Vector3[] Hug(Vector2[] plan, Func<float, float, float> ground, float clearance, float lateral,
            float seconds, Func<int, float> floor = null, float rise = 0f)
        {
            int n = plan.Length;
            float length = 0f;
            for (int i = 1; i < n; i++) length += Vector2.Distance(plan[i], plan[i - 1]);
            float spacing = MathF.Max(length / (n - 1), 1e-3f);

            //Seconds of travel in points of the plan, at the shot's peak speed (its clock eases, IntroShot.PEAK_PACE).
            float pointsPerSecond = IntroShot.PEAK_PACE * length / MathF.Max(seconds, 1e-3f) / spacing;
            float sigma = MathF.Max(ROUNDING_SECONDS * pointsPerSecond, 0.5f);
            float liftSigma = MathF.Max(LIFT_SECONDS * pointsPerSecond, 0.5f);
            int reach = (int)MathF.Ceiling(3f * sigma), liftReach = (int)MathF.Ceiling(3f * liftSigma);

            //The lowest the ground lets each point be (the clearance the lift keeps), and the height laid there (the
            //floor, where higher, which is rounded with the ground and not lifted back: see the remarks).
            var clear = new float[n];
            var least = new float[n];
            for (int i = 0; i < n; i++)
            {
                clear[i] = Over(ground, plan[i], plan[Math.Min(i + 1, n - 1)] - plan[Math.Max(i - 1, 0)], lateral) + clearance;
                least[i] = floor != null ? MathF.Max(clear[i], floor(i)) : clear[i];
            }

            //The same carried on past both ends for the rounding's whole reach (index k is point k - reach).
            int m = n + 2 * reach;
            var heights = new float[m];
            Vector2 head = Direction(plan[0] - plan[1]), tail = Direction(plan[n - 1] - plan[n - 2]);
            for (int k = 0; k < m; k++)
            {
                int i = k - reach;
                if (i >= 0 && i < n) heights[k] = least[i];
                else if (floor != null)
                    heights[k] = i < 0 ? 2f * least[0] - least[Math.Min(-i, n - 1)] : 2f * least[n - 1] - least[Math.Max(2 * (n - 1) - i, 0)];
                else if (i < 0) heights[k] = Over(ground, plan[0] + head * (spacing * -i), head, lateral) + clearance;
                else heights[k] = Over(ground, plan[n - 1] + tail * (spacing * (i - n + 1)), tail, lateral) + clearance;
            }

            if (rise > 0f) heights = Highest(heights, (int)MathF.Round(rise * pointsPerSecond));

            //1. Rounded, and how far under the ground's clearance that left each point.
            float[] weights = Gaussian(sigma, reach);
            var rounded = new float[n];
            var cut = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int d = -reach; d <= reach; d++) sum += heights[i + reach + d] * weights[Math.Abs(d)];
                rounded[i] = sum;
                cut[i] = MathF.Max(0f, clear[i] - sum);
            }

            //2. Lifted: the deepest cut within the lift's reach, averaged over no more than that reach, so a cut reaches
            //twice that far (LIFT_SECONDS). Past the ends the end's own value runs on, which is still a cut taken over
            //every point it is averaged into.
            float[] deepest = Highest(cut, liftReach);
            float[] swell = Gaussian(liftSigma, liftReach);
            var path = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int d = -liftReach; d <= liftReach; d++) sum += deepest[Math.Clamp(i + d, 0, n - 1)] * swell[Math.Abs(d)];
                path[i] = new Vector3(plan[i].X, rounded[i] + sum, plan[i].Y);
            }

            return path;
        }

        /// <summary>A unit vector on the ground plane at a bearing (radians, from +X towards +Z).</summary>
        public static Vector2 Bearing(float radians) => new(MathF.Cos(radians), MathF.Sin(radians));

        /// <summary>A roll in [from, to).</summary>
        public static float Roll(Random random, float from, float to) => from + (to - from) * (float)random.NextDouble();


        private static float Sample(Func<float, float, float> ground, Vector2 at) => ground(at.X, at.Y);

        //The highest ground at a point and at half and all of `lateral` out from it on all four hands, `along` the plan.
        private static float Over(Func<float, float, float> ground, Vector2 point, Vector2 along, float lateral)
        {
            along = Direction(along);
            Vector2 across = new(-along.Y, along.X);

            float highest = Sample(ground, point);
            for (int s = 1; s <= 2; s++)
            {
                float offset = lateral * s * 0.5f;
                highest = MathF.Max(highest, Sample(ground, point + across * offset));
                highest = MathF.Max(highest, Sample(ground, point - across * offset));
                highest = MathF.Max(highest, Sample(ground, point + along * offset));
                highest = MathF.Max(highest, Sample(ground, point - along * offset));
            }

            return highest;
        }

        //The highest value within `reach` either side of each, the windows cut short at the ends.
        private static float[] Highest(float[] values, int reach)
        {
            var highest = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float top = float.MinValue;
                for (int j = Math.Max(0, i - reach); j <= Math.Min(values.Length - 1, i + reach); j++) top = MathF.Max(top, values[j]);
                highest[i] = top;
            }

            return highest;
        }

        /// <summary>
        /// <paramref name="values"/> averaged by a Gaussian of <paramref name="sigma"/> places (#1187), the one rounding every
        /// prologue path shares: past each end, <paramref name="turnOver"/> turns the values over the end (the place k past it
        /// is twice the end less the place k before it), so a rounded path starts and ends where it was laid and leaves and
        /// arrives the way it was going; otherwise the end's own value is carried on, which a direction averaged for the look
        /// wants. Built once when the intro begins.
        /// </summary>
        internal static float[] Rounded(float[] values, float sigma, bool turnOver)
        {
            int reach = Math.Max(1, (int)MathF.Ceiling(3f * sigma)), last = values.Length - 1;
            float[] weights = Gaussian(sigma, reach);

            var rounded = new float[values.Length];
            for (int i = 0; i <= last; i++)
            {
                float sum = 0f;
                for (int d = -reach; d <= reach; d++)
                {
                    int k = i + d;
                    float v = k < 0 ? (turnOver ? 2f * values[0] - values[Math.Min(-k, last)] : values[0])
                        : k > last ? (turnOver ? 2f * values[last] - values[Math.Max(2 * last - k, 0)] : values[last])
                        : values[k];
                    sum += v * weights[Math.Abs(d)];
                }
                rounded[i] = sum;
            }

            return rounded;
        }

        /// <summary>Half of a Gaussian's weights, 0..<paramref name="reach"/>, normalised over the whole of it (-reach..reach).</summary>
        internal static float[] Gaussian(float sigma, int reach)
        {
            var weights = new float[reach + 1];
            float total = 0f;
            for (int d = 0; d <= reach; d++)
            {
                weights[d] = MathF.Exp(-d * d / (2f * sigma * sigma));
                total += d == 0 ? weights[d] : 2f * weights[d];
            }

            for (int d = 0; d <= reach; d++) weights[d] /= total;
            return weights;
        }

        private static Vector2 Direction(Vector2 v) => v.LengthSquared() > 1e-8f ? Vector2.Normalize(v) : Vector2.UnitX;
    }
}

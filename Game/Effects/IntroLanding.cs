using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>A last shot that lands on the gameplay pose</b> (#972, #1107): the shared half of a chapter intro's flight home,
    /// the part that does not depend on what the scene is. A scene lays its own route (the city's staircase of streets,
    /// <see cref="CitySwing"/>; the meadow's brook, pond and turn round the island, <see cref="MeadowLanding"/>; the big
    /// top's run in through its entrance and round inside its king poles, <see cref="CircusLanding"/>; the savanna's run in
    /// low over the grass, <see cref="SavannaLanding"/>) as a dense path that ends on the pose; this times it, points it and
    /// banks it, and the shot it returns (<see cref="IntroShot.Lands"/>) is followed by no tour leg, so the intro hands the
    /// camera over without a cut.
    /// <para>
    /// <b>What a landing needs</b>, and every scene's landing gets it from here:
    /// <list type="bullet">
    /// <item>a <b>release</b> onto the pose (<see cref="Release"/>): a cubic off the line the route is flying along, arriving
    /// along a direction the scene chooses (<see cref="Arrival"/>: the pose's own facing, halfway, or still along the route
    /// with the view swivelling onto the pose); or, for a route round the arena, <see cref="TurnIn"/> off its circle down the
    /// pose's facing; never tighter than a radius the scene states (<see cref="NoTighterThan"/>);</item>
    /// <item><b>timing</b> (<see cref="Fly"/>): the route walked at the speed the scene gives each of its points, then a
    /// cubic in time that decelerates to rest exactly on the pose, so the last half second is slow;</item>
    /// <item>a <b>look</b> along the travel (its direction averaged over a moment either side, which turns the view into a
    /// corner before the lens), turned in towards the pose's target where the route asks it and raised towards its height
    /// where the scene asks it (<see cref="Style.LookRise"/>), that eases onto the pose's own look over the last seconds and
    /// ends on it exactly;</item>
    /// <item>a <b>bank</b> in proportion to the heading's rate of turn, smoothed, level by the landing; and the lens easing
    /// from the scene's wide one to the game's own;</item>
    /// <item>a <b>search</b> (<see cref="Best"/>): rolled candidates each laid in full, the ones a clearance predicate
    /// refuses dropped, the best of the rest flown; null when none is clear, and the scene then plays the reel it had
    /// before, its tour leg included.</item>
    /// </list>
    /// </para>
    /// <para>
    /// ⚠ <b>The arena's keep-out cannot be a scene's clearance near the pose.</b> <see cref="IntroGround.ARENA_KEEP_OUT"/>
    /// refuses any lens within 44 of the axis, and the game's lens stands 28 to 45 out, so a predicate that kept it would
    /// refuse every landing's last leg. A landing keeps the lens off the arena by never letting it nearer the axis than the
    /// pose itself (<see cref="KeepOutFor"/>): everything the arena holds — the island's drum, the gun, the glass, the
    /// cluster — stands nearer than the gameplay lens does.
    /// </para>
    /// <para>
    /// Built once when the intro begins, never per frame.
    /// </para>
    /// </summary>
    internal static class IntroLanding
    {
        /// <summary>How a landing is flown: the lens, the look, the bank and the settle. The route is the scene's own.</summary>
        /// <param name="WideLens">The field of view it opens at, times the game's: it eases to the game's own by the landing.</param>
        /// <param name="ReleaseTimeScale">How long the deceleration takes against its length over the speed it enters with: 2
        /// is a steady deceleration to rest, more settles slower, and under 3 the cubic never runs backwards.</param>
        /// <param name="LookPitchFollow">How much of the path's own rise and fall the look follows.</param>
        /// <param name="LookDistance">How far out along the travel the look is held.</param>
        /// <param name="LookSmoothSeconds">How long either side of a moment the travel's direction is averaged over for the look.
        /// ⚠ Along the path's own direction, not at a point some way ahead: a chord to a point 0.35 s on cut every corner of
        /// the city's swing and pitched the view 140 degrees a second over each.</param>
        /// <param name="LookLandSeconds">The last seconds, over which the look eases from along the travel onto the pose's own
        /// and the bank levels out.</param>
        /// <param name="LookInward">How far the look turns in plan from the travel towards the pose's target, as a share of the
        /// angle between them (where the route asks it: <see cref="Route.Lean"/>): 0 looks along the travel; 0.7 on a circle
        /// round the arena looks 63 degrees in from it, the cluster 27 degrees off the middle of a 16:9 frame, where a lean of
        /// 31 degrees left the island at the frame's edge.</param>
        /// <param name="RollPerTurn">Degrees of bank per degree a second of the heading's turn.</param>
        /// <param name="MaxRollDegrees">The most it banks.</param>
        /// <param name="RollSmoothSeconds">How long either side the bank is smoothed over (three passes).</param>
        /// <param name="Points">How many evenly timed points the shot carries.</param>
        /// <param name="LookRise">How far the point the look is held on is raised from the travel's own height towards the
        /// pose's target's, as a share of the way, where the route asks it (<see cref="Route.Lean"/>, as
        /// <paramref name="LookInward"/> is): 0 keeps the travel's, as the cities and the meadow do. For a route that runs
        /// round the arena close under the cluster (the big top's, #1118), whose look along a level circle put the cluster
        /// in the frame's top corner and the island's cap across its lower half.</param>
        internal readonly record struct Style(float WideLens, float ReleaseTimeScale, float LookPitchFollow, float LookDistance,
            float LookSmoothSeconds, float LookLandSeconds, float LookInward, float RollPerTurn, float MaxRollDegrees,
            float RollSmoothSeconds, int Points, float LookRise = 0f);

        /// <summary>
        /// One candidate route, laid in full: the dense path from the shot's first point to the pose, the speed at each point
        /// up to <see cref="DecelerateFrom"/>, where the deceleration onto the pose begins, and how good it is.
        /// </summary>
        internal sealed class Route
        {
            public List<Vector3> Path;
            public float[] Speed;
            public int DecelerateFrom;
            public float Score;

            /// <summary>How much of <see cref="Style.LookInward"/> the look takes at each point, 0-1, or null for all of it
            /// everywhere: a route that runs along something before it goes round the arena looks along it there.</summary>
            public float[] Lean;
        }

        /// <summary>How the lens arrives on the pose, off one roll: along the pose's
        /// <paramref name="facing"/> half the time, so it comes in towards what it will look at; halfway between that and the
        /// route's own <paramref name="lead"/> three times in ten; and the rest still along the route, the view swivelling onto
        /// the pose as it slows — the one way onto a pose that stands across the route's line.</summary>
        internal static Vector2 Arrival(double roll, Vector2 facing, Vector2 lead) =>
            roll < 0.5 ? facing : roll < 0.8 ? Vector2.Normalize(lead + facing) : lead;

        /// <summary>
        /// The nearest the axis a landing's lens may come (see the class's note): the pose's own distance, less
        /// <paramref name="slack"/> for the curve's last points, and no nearer than the scene's keep-out
        /// <paramref name="keepOut"/> requires elsewhere.
        /// </summary>
        internal static float KeepOutFor(Vector3 pose, float keepOut, float slack) =>
            MathF.Min(keepOut, new Vector2(pose.X, pose.Z).Length() - slack);

        /// <summary>
        /// The best of <paramref name="candidates"/> laid routes (<paramref name="lay"/> returns null for one it could not
        /// lay), by <see cref="Route.Score"/>, the first of equals kept. A route with any point <paramref name="clear"/>
        /// refuses is dropped; null takes every route <paramref name="lay"/> returns (the city's checks its towers itself).
        /// Null when none is left.
        /// </summary>
        internal static Route Best(int candidates, Func<Route> lay, Func<Vector3, bool> clear = null)
        {
            Route best = null;
            for (int attempt = 0; attempt < candidates; attempt++)
            {
                Route route = lay();
                if (route == null || (clear != null && !Clear(route.Path, clear))) continue;
                if (best == null || route.Score > best.Score) best = route;
            }

            return best;
        }

        /// <summary>Whether <paramref name="clear"/> takes every point of <paramref name="path"/>.</summary>
        internal static bool Clear(List<Vector3> path, Func<Vector3, bool> clear)
        {
            foreach (Vector3 p in path)
                if (!clear(p)) return false;

            return true;
        }

        /// <summary>
        /// The curve off the route's line onto the pose, in plan: a cubic Bezier from <paramref name="from"/> leaving along
        /// <paramref name="heading"/> to <paramref name="to"/> arriving along <paramref name="arrive"/>, cut finely and thinned
        /// to <paramref name="step"/> so the plan's spacing stays even. <paramref name="plan"/>'s last point is where the
        /// spacing is counted from; the curve's own first point is not added.
        /// </summary>
        internal static void Release(List<Vector2> plan, Vector2 from, Vector2 heading, Vector2 to, Vector2 arrive, float step)
        {
            float reach = Vector2.Distance(from, to);
            Vector2 c1 = from + heading * (reach * 0.45f), c2 = to - arrive * (reach * 0.35f);

            Vector2 last = plan[^1];
            for (int i = 1; i <= 400; i++)
            {
                float u = i / 400f, v = 1f - u;
                Vector2 p = v * v * v * from + 3f * v * v * u * c1 + 3f * v * u * u * c2 + u * u * u * to;
                if (Vector2.Distance(p, last) >= step || i == 400) { plan.Add(p); last = p; }
            }
        }

        /// <summary>
        /// The release of a route that goes ROUND the arena (#1107), in plan: off a circle of <paramref name="radius"/> about
        /// the axis at bearing <paramref name="from"/>, going round the way <paramref name="sign"/> says (positive is the bearing
        /// growing), onto <paramref name="to"/>, its bearing and its distance from the axis reached together — the distance
        /// easing in from the circle's, so the curve leaves along the circle, and the bearing easing out, so it arrives down the
        /// pose's own radial line, which for a pose looking at the axis is its facing. <paramref name="slant"/> 0 arrives
        /// straight down that line; towards 1 more and more along the turn, the view swivelling onto the pose as it slows. For a
        /// pose inside the circle the distance only ever shrinks, so the lens is never nearer the axis than the pose; for one
        /// outside it the distance grows and every point is nearer, so a scene lays its circle outside the poses it lands on
        /// (the meadow's is 48 to 60 out, the game's lens 28 to 45). Cut finely and thinned to <paramref name="step"/>, as
        /// <see cref="Release"/> is.
        /// <para>
        /// ⚠ A <see cref="Release"/> off a circle onto a pose inside it cannot arrive down the radial line: the circle's tangent
        /// at a release 30-50 degrees short of the pose points away from it, and the cubic turned 130 degrees in 36 units, at a
        /// radius of 5. This makes the same turn (90 degrees and the sweep, so 120 to 140 for the meadow's 30 to 50) over the
        /// whole sweep.
        /// </para>
        /// </summary>
        internal static void TurnIn(List<Vector2> plan, float from, float radius, int sign, Vector2 to, float slant, float step)
        {
            float toRadius = to.Length();
            float sweep = (MathF.Atan2(to.Y, to.X) - from) * sign % MathHelper.TwoPi;
            if (sweep < 0f) sweep += MathHelper.TwoPi;

            Vector2 last = plan[^1];
            for (int i = 1; i <= 400; i++)
            {
                float u = i / 400f;
                float turned = (1f - slant) * (2f * u - u * u) + slant * u;
                float bearing = from + sign * sweep * turned, distance = MathHelper.Lerp(radius, toRadius, u * u);
                Vector2 p = new Vector2(MathF.Cos(bearing), MathF.Sin(bearing)) * distance;
                if (Vector2.Distance(p, last) >= step || i == 400) { plan.Add(p); last = p; }
            }
        }

        /// <summary>
        /// Whether the plan from <paramref name="from"/> on (up to <paramref name="to"/>, or its end) turns no tighter than
        /// <paramref name="radius"/>, read over every other point. A turn's rate is the speed over its radius, so a scene states
        /// the radius its speed there can take.
        /// </summary>
        internal static bool NoTighterThan(List<Vector2> plan, int from, float radius, int to = int.MaxValue)
        {
            int count = Math.Min(to, plan.Count);
            for (int i = from + 2; i + 2 < count; i += 2)
            {
                Vector2 a = plan[i] - plan[i - 2], b = plan[i + 2] - plan[i];
                float turned = MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(b.Y, b.X) - MathF.Atan2(a.Y, a.X)));
                if (turned > 1e-4f && (a.Length() + b.Length()) * 0.5f / turned < radius) return false;
            }

            return true;
        }

        /// <summary>A straight run, cut to <paramref name="step"/>; the first point only if the plan is empty. Returns the
        /// plan's length after it.</summary>
        internal static float Straight(List<Vector2> plan, Vector2 from, Vector2 to, float step)
        {
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(from, to) / step));
            for (int i = plan.Count == 0 ? 0 : 1; i <= steps; i++) plan.Add(Vector2.Lerp(from, to, i / (float)steps));
            return PlanLength(plan);
        }

        /// <summary>The length of a plan, point to point.</summary>
        internal static float PlanLength(List<Vector2> plan)
        {
            float length = 0f;
            for (int i = 1; i < plan.Count; i++) length += Vector2.Distance(plan[i - 1], plan[i]);
            return length;
        }

        /// <summary>
        /// The shot off a laid route: timed by the route's own speeds, then a cubic to rest over the deceleration; the look
        /// along the travel easing onto the pose's; the bank off the heading's rate of turn, level by the landing; the lens
        /// from <see cref="Style.WideLens"/> times the game's to the game's. Its last point and look are the pose exactly.
        /// </summary>
        internal static IntroShot Fly(string name, Route route, Vector3 endPosition, Vector3 endTarget, float fieldOfView, Style style)
        {
            List<Vector3> path = route.Path;
            int releaseFrom = route.DecelerateFrom;

            //The time at each point: the route's speed, then a cubic to rest over the deceleration
            var length = new float[path.Count];
            for (int i = 1; i < path.Count; i++) length[i] = length[i - 1] + Vector3.Distance(path[i - 1], path[i]);

            var time = new float[path.Count];
            for (int i = 1; i <= releaseFrom; i++)
                time[i] = time[i - 1] + (length[i] - length[i - 1]) / route.Speed[i];

            //The deceleration: distance along it on the cubic x(τ) = v0·T·h10 + L·h01, entering at the route's speed there,
            //at rest on the pose
            float v0 = route.Speed[releaseFrom];
            float releaseLength = length[^1] - length[releaseFrom];
            float releaseSeconds = style.ReleaseTimeScale * releaseLength / v0;
            float seconds = time[releaseFrom] + releaseSeconds;

            int points = style.Points;
            var positions = new Vector3[points];
            float[] lean = route.Lean == null ? null : new float[points];
            for (int k = 0; k < points; k++)
            {
                float t = seconds * k / (points - 1);
                float at;
                if (t <= time[releaseFrom]) at = LengthAtTime(time, length, releaseFrom, t);
                else
                {
                    float u = (t - time[releaseFrom]) / releaseSeconds;
                    float h10 = u * u * u - 2f * u * u + u, h01 = -2f * u * u * u + 3f * u * u;
                    at = length[releaseFrom] + v0 * releaseSeconds * h10 + releaseLength * h01;
                }

                positions[k] = PointAtLength(path, length, at);
                if (lean != null) lean[k] = ValueAtLength(route.Lean, length, at);
            }
            positions[^1] = endPosition;

            //The look: along the travel, a little ahead and following some of its rise and fall, easing onto the pose's own
            //look over the last seconds; the bank off the heading's rate of turn, levelling out as the look lands
            float dt = seconds / (points - 1);
            var looks = new Vector3[points];
            var heading = new float[points];
            var land = new float[points];

            //The path's own direction at each point, some of its climb kept, then averaged over the window
            var direction = new Vector3[points];
            for (int k = 0; k < points; k++)
            {
                Vector3 tangent = positions[Math.Min(k + 1, points - 1)] - positions[Math.Max(k - 1, 0)];
                if (tangent.LengthSquared() < 1e-8f) tangent = k > 0 ? direction[k - 1] : endTarget - positions[k];
                tangent.Normalize();
                tangent.Y *= style.LookPitchFollow;
                direction[k] = tangent;
            }

            int window = Math.Max(1, (int)MathF.Round(style.LookSmoothSeconds / dt));
            direction = BoxBlur(direction, window);

            for (int k = 0; k < points; k++)
            {
                Vector3 travel = direction[k];
                if (travel.LengthSquared() < 1e-8f) travel = endTarget - positions[k];
                travel.Normalize();

                heading[k] = MathF.Atan2(travel.Z, travel.X);
                land[k] = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((k * dt - (seconds - style.LookLandSeconds)) / style.LookLandSeconds, 0f, 1f));
                Vector3 ahead = positions[k] + Leaned(travel, positions[k], endTarget, style.LookInward * (lean?[k] ?? 1f)) * style.LookDistance;
                if (style.LookRise > 0f) ahead.Y = MathHelper.Lerp(ahead.Y, endTarget.Y, style.LookRise * (lean?[k] ?? 1f));
                looks[k] = Vector3.Lerp(ahead, endTarget, land[k]);
            }
            looks[^1] = endTarget;

            var roll = new float[points];
            float maxRoll = MathHelper.ToRadians(style.MaxRollDegrees);
            for (int k = 0; k < points; k++)
            {
                int a = Math.Max(k - 1, 0), b = Math.Min(k + 1, points - 1);
                float turn = MathHelper.WrapAngle(heading[b] - heading[a]) / ((b - a) * dt);
                roll[k] = MathHelper.Clamp(style.RollPerTurn * turn, -maxRoll, maxRoll);
            }

            int rollWindow = Math.Max(1, (int)MathF.Round(style.RollSmoothSeconds / dt));
            for (int pass = 0; pass < 3; pass++) roll = BoxBlur(roll, rollWindow);
            for (int k = 0; k < points; k++) roll[k] *= 1f - land[k];
            roll[^1] = 0f;

            return new IntroShot(name, positions, seconds, fieldOfView * style.WideLens, lookAtPath: looks,
                rollPath: roll, endFieldOfView: fieldOfView, lands: true);
        }

        /// <summary>
        /// The look's direction off the travel's: turned in plan towards <paramref name="target"/> through
        /// <paramref name="inward"/> of the angle between them (see <see cref="Style.LookInward"/>), the travel's own rise and
        /// fall kept. 0 returns the travel untouched.
        /// </summary>
        private static Vector3 Leaned(Vector3 travel, Vector3 position, Vector3 target, float inward)
        {
            if (inward <= 0f) return travel;

            float flat = MathF.Sqrt(travel.X * travel.X + travel.Z * travel.Z);
            Vector2 toTarget = new(target.X - position.X, target.Z - position.Z);
            if (flat < 1e-4f || toTarget.LengthSquared() < 1e-8f) return travel;

            float heading = MathF.Atan2(travel.Z, travel.X);
            heading += inward * MathHelper.WrapAngle(MathF.Atan2(toTarget.Y, toTarget.X) - heading);
            return new Vector3(MathF.Cos(heading) * flat, travel.Y, MathF.Sin(heading) * flat);
        }

        private static float LengthAtTime(float[] time, float[] length, int last, float t)
        {
            int lo = 0, hi = last;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (time[mid] <= t) lo = mid; else hi = mid;
            }

            float span = time[hi] - time[lo];
            return span > 1e-6f ? MathHelper.Lerp(length[lo], length[hi], (t - time[lo]) / span) : length[lo];
        }

        private static float ValueAtLength(float[] values, float[] length, float at)
        {
            int lo = 0, hi = length.Length - 1;
            if (at <= 0f) return values[0];
            if (at >= length[hi]) return values[hi];

            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (length[mid] <= at) lo = mid; else hi = mid;
            }

            float span = length[hi] - length[lo];
            return span > 1e-6f ? MathHelper.Lerp(values[lo], values[hi], (at - length[lo]) / span) : values[lo];
        }

        private static Vector3 PointAtLength(List<Vector3> path, float[] length, float at)
        {
            int lo = 0, hi = length.Length - 1;
            if (at <= 0f) return path[0];
            if (at >= length[hi]) return path[hi];

            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (length[mid] <= at) lo = mid; else hi = mid;
            }

            float span = length[hi] - length[lo];
            return span > 1e-6f ? Vector3.Lerp(path[lo], path[hi], (at - length[lo]) / span) : path[lo];
        }

        //A running sum over ±half: the look's directions are normalized after, so they are not divided
        private static Vector3[] BoxBlur(Vector3[] values, int half)
        {
            var result = new Vector3[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                Vector3 sum = Vector3.Zero;
                for (int j = Math.Max(0, i - half); j <= Math.Min(values.Length - 1, i + half); j++) sum += values[j];
                result[i] = sum;
            }

            return result;
        }

        private static float[] BoxBlur(float[] values, int half)
        {
            var result = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int j = Math.Max(0, i - half); j <= Math.Min(values.Length - 1, i + half); j++) { sum += values[j]; count++; }
                result[i] = sum / count;
            }

            return result;
        }
    }
}

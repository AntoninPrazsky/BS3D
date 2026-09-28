using Microsoft.Xna.Framework;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The small geometry the chapter-intro prologues share (#596): a straight path, a turn in plan, a path held
    /// over the ground, the distance from a point to a segment, a random value in a range. Each builder searches
    /// its own scene for its shots — a real crater, a gap in the spruces, a storm cell where it will be when the
    /// shot plays — and that search is theirs; what was copied between them was only this, written out again in
    /// up to four files each, identically.
    /// </summary>
    internal static class IntroPaths
    {
        /// <summary>How many points a prologue path is sampled at — the scenes whose shots fly over open ground.</summary>
        public const int POINTS = 64;

        /// <summary>
        /// The denser sampling of the scenes whose paths bend close past things (the cities' streets, the wood,
        /// the cavern, the volcano's rim, the open ground's planting), where 64 points turned a curve into chords.
        /// </summary>
        public const int FINE_POINTS = 96;

        /// <summary>A straight path from <paramref name="from"/> to <paramref name="to"/>, <paramref name="points"/> evenly spaced points.</summary>
        public static Vector3[] Line(Vector3 from, Vector3 to, int points)
        {
            var path = new Vector3[points];
            for (int i = 0; i < points; i++) path[i] = Vector3.Lerp(from, to, i / (float)(points - 1));

            return path;
        }

        /// <summary>A plan vector turned by <paramref name="radians"/>, counter-clockwise.</summary>
        public static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = MathF.Cos(radians), s = MathF.Sin(radians);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        /// <summary>
        /// Holds every point of a path <paramref name="clearance"/> over the highest ground anywhere under it, as
        /// <b>one</b> height change for the whole of it: a constant lift, so a path laid level stays level and a crane
        /// stays a crane. <paramref name="ceiling"/> is the scene's own highest surface at a plan point.
        /// </summary>
        public static void KeepOver(Vector3[] path, Func<float, float, float> ceiling, float clearance)
        {
            float lift = 0f;
            foreach (Vector3 point in path)
                lift = MathF.Max(lift, ceiling(point.X, point.Z) + clearance - point.Y);

            for (int i = 0; i < path.Length; i++) path[i].Y += lift;
        }

        /// <summary>How far <paramref name="point"/> is from the segment <paramref name="from"/>–<paramref name="to"/>, in plan.</summary>
        public static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 run = to - from;
            float t = MathHelper.Clamp(Vector2.Dot(point - from, run) / MathF.Max(run.LengthSquared(), 1e-4f), 0f, 1f);
            return Vector2.Distance(point, from + run * t);
        }

        /// <summary>How far <paramref name="p"/> is from the segment <paramref name="a"/>–<paramref name="b"/>, in space.</summary>
        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float length2 = ab.LengthSquared();
            if (length2 < 1e-6f) return Vector3.Distance(p, a);

            float t = MathHelper.Clamp(Vector3.Dot(p - a, ab) / length2, 0f, 1f);
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>A value drawn evenly between <paramref name="from"/> and <paramref name="to"/>.</summary>
        public static float RandomRange(Random random, float from, float to) => from + (float)random.NextDouble() * (to - from);

        /// <summary>Clamped to 0–1.</summary>
        public static float Saturate(float value) => MathHelper.Clamp(value, 0f, 1f);

        /// <summary>The smoothstep polynomial on an already-saturated <paramref name="t"/>.</summary>
        public static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}

using Microsoft.Xna.Framework;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The tick of <see cref="CheckGlyph"/> as numbers (#816): which part of each row of pixels it covers, worked out from
    /// two strokes with round ends. Apart from the widget so the tests can check the shape against a brute-force cover
    /// without a device or Myra: plain geometry, compiled into <c>Tests/BS3D.Tests</c>.
    /// </summary>
    internal static class CheckTick
    {
        //The check mark most interfaces draw: a short stroke down to the right, then a long one up to the right, in
        //fractions of the box's side
        internal static readonly Vector2 START = new(0.25f, 0.52f);
        internal static readonly Vector2 CORNER = new(0.43f, 0.71f);
        internal static readonly Vector2 END = new(0.76f, 0.30f);

        /// <summary>The stroke's width as a fraction of the side.</summary>
        internal const float STROKE = 0.15f;

        /// <summary>
        /// The tick's spans for a box of <paramref name="side"/> pixels, four numbers a row (two spans of from and to, in
        /// pixels from the box's left edge), NaN for a span the row does not have. Where the two strokes overlap in a row
        /// they are one span, so a row's two spans never overlap.
        /// </summary>
        public static float[] Spans(int side)
        {
            float[] spans = new float[side * 4];
            Array.Fill(spans, float.NaN);
            float radius = side * STROKE / 2f;
            Vector2 start = START * side, corner = CORNER * side, end = END * side;

            for (int row = 0; row < side; row++)
            {
                float y = row + 0.5f;
                bool a = StrokeSpan(y, start, corner, radius, out float a0, out float a1);
                bool b = StrokeSpan(y, corner, end, radius, out float b0, out float b1);

                //Where the two strokes overlap in a row they are one span, so no pixel is drawn twice
                if (a && b && a0 <= b1 && b0 <= a1) (a0, a1, b) = (MathF.Min(a0, b0), MathF.Max(a1, b1), false);
                else if (!a && b) (a0, a1, a, b) = (b0, b1, true, false);

                if (a) (spans[row * 4], spans[row * 4 + 1]) = (Math.Max(a0, 0f), Math.Min(a1, side));
                if (b) (spans[row * 4 + 2], spans[row * 4 + 3]) = (Math.Max(b0, 0f), Math.Min(b1, side));
            }

            return spans;
        }

        /// <summary>
        /// Where the row at height <paramref name="y"/> crosses a stroke from <paramref name="p0"/> to <paramref name="p1"/>
        /// with round ends of <paramref name="radius"/>: a stroke is convex, so the row crosses it in one span, the widest
        /// of the spans it crosses its two end discs and its straight body in.
        /// </summary>
        private static bool StrokeSpan(float y, Vector2 p0, Vector2 p1, float radius, out float from, out float to)
        {
            from = float.MaxValue;
            to = float.MinValue;

            EndSpan(y, p0, radius, ref from, ref to);
            EndSpan(y, p1, radius, ref from, ref to);

            //The body: the rectangle the stroke sweeps, crossed at its four edges
            Vector2 along = Vector2.Normalize(p1 - p0);
            Vector2 across = new Vector2(-along.Y, along.X) * radius;
            Span<Vector2> corners = stackalloc[] { p0 + across, p1 + across, p1 - across, p0 - across };
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i], b = corners[(i + 1) % 4];
                if (a.Y == b.Y || y < MathF.Min(a.Y, b.Y) || y > MathF.Max(a.Y, b.Y)) continue;
                float x = a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                from = MathF.Min(from, x);
                to = MathF.Max(to, x);
            }

            return from <= to;
        }

        private static void EndSpan(float y, Vector2 centre, float radius, ref float from, ref float to)
        {
            float dy = y - centre.Y;
            if (MathF.Abs(dy) > radius) return;
            float dx = MathF.Sqrt(radius * radius - dy * dy);
            from = MathF.Min(from, centre.X - dx);
            to = MathF.Max(to, centre.X + dx);
        }
    }
}

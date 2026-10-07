using BS3D.Screens;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The checkbox's tick (#816): the spans <c>CheckTick.Spans</c> works out in closed form for each row of pixels,
    /// against the same two round-ended strokes covered by brute force, a row sampled every hundredth of a pixel.
    /// <c>CheckTick</c> is compiled in from the Game.
    /// </summary>
    public class CheckTickTests
    {
        private const float STEP = 0.01f;

        /// <summary>The spans a row crosses the tick in, found by walking it in steps of <see cref="STEP"/>.</summary>
        private static List<(float From, float To)> Walked(int side, int row)
        {
            float radius = side * CheckTick.STROKE / 2f, y = row + 0.5f;
            Vector2 start = CheckTick.START * side, corner = CheckTick.CORNER * side, end = CheckTick.END * side;
            List<(float, float)> spans = new();
            float? from = null;
            for (float x = 0f; x <= side; x += STEP)
            {
                Vector2 p = new(x, y);
                bool inside = Distance(p, start, corner) <= radius || Distance(p, corner, end) <= radius;
                if (inside && from == null) from = x;
                if (!inside && from != null) { spans.Add((from.Value, x)); from = null; }
            }
            if (from != null) spans.Add((from.Value, side));
            return spans;
        }

        private static float Distance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
            return Vector2.Distance(p, a + t * ab);
        }

        [Theory]
        [InlineData(12)]
        [InlineData(27)]
        [InlineData(58)]
        [InlineData(117)]
        public void Each_rows_spans_are_the_strokes_own_cover(int side)
        {
            float[] spans = CheckTick.Spans(side);
            int rowsWithTick = 0;

            for (int row = 0; row < side; row++)
            {
                List<(float From, float To)> walked = Walked(side, row);
                List<(float From, float To)> computed = new();
                for (int k = 0; k < 2; k++)
                    if (!float.IsNaN(spans[row * 4 + k * 2])) computed.Add((spans[row * 4 + k * 2], spans[row * 4 + k * 2 + 1]));

                Assert.True(walked.Count == computed.Count, $"side {side}, row {row}: {walked.Count} spans walked, {computed.Count} computed");
                for (int k = 0; k < walked.Count; k++)
                {
                    Assert.InRange(computed[k].From, walked[k].From - 2 * STEP, walked[k].From + 2 * STEP);
                    Assert.InRange(computed[k].To, walked[k].To - 2 * STEP, walked[k].To + 2 * STEP);
                }

                //Left to right, inside the box, and never overlapping, so no row is drawn over itself
                if (computed.Count == 2) Assert.True(computed[0].To < computed[1].From, $"side {side}, row {row}: spans overlap");
                foreach ((float from, float to) in computed) Assert.True(from >= 0f && to <= side && from <= to);
                if (computed.Count > 0) rowsWithTick++;
            }

            //The tick stands in the box's middle half and more, from its long stroke's top to its corner's foot
            Assert.InRange(rowsWithTick, side / 2, side);
        }
    }
}

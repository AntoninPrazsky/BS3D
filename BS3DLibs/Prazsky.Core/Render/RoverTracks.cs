using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>A rover's tracks across a plain</b> (#746, the Moon; #745 is the same on Mars): the polyline a backdrop hands
    /// <c>RoverTracks.fxh</c>, which prints the two ruts, their berms and the wheels' chevron cleats into the ground the
    /// scene's own shader draws. A track is designed as a few control points; <see cref="Resample"/> turns them into the
    /// shader's <see cref="POINTS"/> along a Catmull-Rom curve, and <see cref="Push"/> hands them over with the distance along
    /// the track and the bounds the shader's branch tests. <see cref="Distance"/> answers the same question on the CPU, for a
    /// planting that keeps its rocks off the ruts.
    /// </summary>
    public static class RoverTracks
    {
        /// <summary>The points the shader reads (<c>RoverTracks.fxh</c>'s <c>TRACK_POINTS</c>): keep the two in one change.</summary>
        public const int POINTS = 24;

        //The runs the shader tests by their own boxes: RoverTracks.fxh's TRACK_CHUNK and TRACK_CHUNKS, keep the two in one
        //change as POINTS is
        private const int CHUNK = 4, CHUNKS = (POINTS - 1 + CHUNK - 1) / CHUNK;

        /// <summary>Half the track's width over both ruts and their berms, world units: what the bounds are padded by and
        /// what a planting keeps clear of (<c>TRACK_GAUGE</c> / 2 + a tyre and a berm).</summary>
        public const float HALF_WIDTH = 1.4f;

        /// <summary>
        /// <see cref="POINTS"/> points along the Catmull-Rom curve through <paramref name="controls"/> (its ends doubled, so
        /// it starts and ends on them), evenly spaced in the curve's parameter: a smooth track the shader's straight segments
        /// follow closely, since its controls are tens of units apart and its bends gentle.
        /// </summary>
        public static Vector2[] Resample(IReadOnlyList<Vector2> controls)
        {
            var points = new Vector2[POINTS];
            int spans = controls.Count - 1;
            for (int i = 0; i < POINTS; i++)
            {
                float s = i / (float)(POINTS - 1) * spans;
                int k = Math.Min((int)s, spans - 1);
                float t = s - k;
                Vector2 p0 = controls[Math.Max(k - 1, 0)], p1 = controls[k], p2 = controls[k + 1], p3 = controls[Math.Min(k + 2, spans)];
                points[i] = Vector2.CatmullRom(p0, p1, p2, p3, t);
            }
            return points;
        }

        /// <summary>
        /// Hands <paramref name="path"/> (at most <see cref="POINTS"/> points) to <paramref name="effect"/>'s <c>TrackPath</c>,
        /// <c>TrackCount</c>, <c>TrackBounds</c> and <c>TrackChunks</c>: each point's world XZ and the distance along the track
        /// to it, the box round it all and the box round each run of <c>CHUNK</c> segments, padded by <see cref="HALF_WIDTH"/>.
        /// Once at load; the track does not move.
        /// </summary>
        public static void Push(Effect effect, IReadOnlyList<Vector2> path)
        {
            var packed = new Vector4[POINTS];
            Vector2 min = new(float.MaxValue), max = new(float.MinValue);
            float along = 0f;
            int count = Math.Min(path.Count, POINTS);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) along += Vector2.Distance(path[i - 1], path[i]);
                packed[i] = new Vector4(path[i].X, path[i].Y, along, 0f);
                min = Vector2.Min(min, path[i]);
                max = Vector2.Max(max, path[i]);
            }

            //Each run of CHUNK segments in its own padded box (RoverTracks.fxh's TrackChunks)
            var chunks = new Vector4[CHUNKS];
            for (int c = 0; c < CHUNKS; c++)
            {
                Vector2 lo = new(float.MaxValue), hi = new(float.MinValue);
                for (int i = c * CHUNK; i <= Math.Min(c * CHUNK + CHUNK, count - 1); i++)
                {
                    lo = Vector2.Min(lo, path[i]);
                    hi = Vector2.Max(hi, path[i]);
                }
                chunks[c] = lo.X > hi.X ? new Vector4(1f, 1f, -1f, -1f)
                    : new Vector4(lo.X - HALF_WIDTH, lo.Y - HALF_WIDTH, hi.X + HALF_WIDTH, hi.Y + HALF_WIDTH);
            }

            effect.Parameters["TrackPath"].SetValue(packed);
            effect.Parameters["TrackChunks"].SetValue(chunks);
            effect.Parameters["TrackCount"].SetValue((float)count);
            effect.Parameters["TrackBounds"].SetValue(new Vector4(min.X - HALF_WIDTH, min.Y - HALF_WIDTH, max.X + HALF_WIDTH, max.Y + HALF_WIDTH));
        }

        /// <summary>How far the point (<paramref name="x"/>, <paramref name="z"/>) is from <paramref name="path"/>'s middle line.</summary>
        public static float Distance(IReadOnlyList<Vector2> path, float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector2 a = path[i], ab = path[i + 1] - a;
                float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / MathF.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
                best = MathF.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }
    }
}

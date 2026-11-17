using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;

namespace BS3D.Screens
{
    /// <summary>
    /// The power-ups' pictograms (#991): one picture a power-up, so the stock reads at a glance anywhere it is shown, drawn
    /// procedurally as a signed distance at whatever pixel size it is asked for, so a chip at 40 pixels and a card at 400 are
    /// both crisp and are the same drawing. Each tells its kind by shape before colour: the Swap two balls trading places
    /// round a pair of arrows, the Brake the glass plate lifted off the balls under it, the Cut the Cutter's own saw blade.
    /// <para>
    /// Coordinates run 0 to 1 across the picture and 0 to 1 down it; a distance is in the same units, negative inside.
    /// </para>
    /// <para>
    /// <b>Written for cold code</b> (#1146): plain floats and <see cref="MathF"/> only, every per-arc figure worked out once,
    /// and the loops <see cref="MethodImplOptions.AggressiveOptimization"/>, so they are compiled optimised at their first call.
    /// The bake runs once a size, so it never runs warm: the first version, written with MonoGame's <see cref="Vector2"/>,
    /// took 47-50 ms for the chips' three pairs at 3840x1581 at tier 0 against 7-9 ms warm, and 23 ms from ReadyToRun code,
    /// which cannot inline MonoGame's methods (the Pi's). This one takes about 5 ms in all three, and draws the same pixels:
    /// the same float operations in the same order (<c>PowerupPictogramTests</c> holds it against the first version).
    /// </para>
    /// </summary>
    internal static class PowerupPictograms
    {
        /// <summary>
        /// The drawing's version, which names every pictogram kept on disk (<see cref="PictogramCache"/>): <b>raise it with any
        /// change to what the raster draws</b> (a shape, a constant, the falloff, the sampling), or a player's cache keeps
        /// showing the old pictures. <c>PowerupPictogramTests</c> pins the drawing's coverage to it and fails on such a change.
        /// </summary>
        internal const int RASTER_VERSION = 1;

        //How thick a stroke is (the arrows, the chevron), and the balls' radius
        private const float STROKE = 0.055f;
        private const float BALL = 0.12f;

        //Half an arrow's stroke
        private const float ARC_HALF = STROKE * 0.85f;

        //Samples a pixel, a side
        private const int SUB = 4;

        /// <summary>The signed distance from (<paramref name="x"/>, <paramref name="y"/>) to <paramref name="kind"/>'s picture.</summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal static float Distance(PowerupKind kind, float x, float y) => kind switch
        {
            PowerupKind.Swap => Swap(x, y),
            PowerupKind.Brake => Brake(x, y),
            PowerupKind.Cut => Cut(x, y),
            _ => 1f,
        };

        /// <summary>
        /// The picture as premultiplied white coverage, <paramref name="size"/> pixels across with <paramref name="pad"/>
        /// clear pixels round it. <paramref name="soft"/> pixels of falloff outside the outline make the backing a chip's text
        /// stands on (its blur), 0 the crisp picture itself. Sampled four by four inside every pixel.
        /// </summary>
        internal static Color[] Raster(PowerupKind kind, int size, int pad, float soft)
        {
            int side = size + 2 * pad;
            var pixels = new Color[side * side];
            if (soft > 0f) Bake(kind, size, pad, soft, null, pixels);
            else Bake(kind, size, pad, 0f, pixels, null);
            return pixels;
        }

        /// <summary>
        /// The crisp picture and its backing of <paramref name="soft"/> pixels at once (#1095), each exactly what
        /// <see cref="Raster"/> gives: they are drawn from the same distances, which are the whole of the cost.
        /// </summary>
        internal static (Color[] Picture, Color[] Backing) RasterWithBacking(PowerupKind kind, int size, int pad, float soft)
        {
            int side = size + 2 * pad;
            var picture = new Color[side * side];
            var backing = new Color[side * side];
            Bake(kind, size, pad, soft, picture, backing);
            return (picture, backing);
        }

        //The picture into `sharp` and the backing into `blurred`, either of them null for none: rows in parallel, each pixel's
        //samples in the same order as ever, so the result does not depend on how the rows were shared out
        private static void Bake(PowerupKind kind, int size, int pad, float soft, Color[] sharp, Color[] blurred) =>
            System.Threading.Tasks.Parallel.For(0, size + 2 * pad, y => BakeRow(kind, size, pad, soft, sharp, blurred, y));

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void BakeRow(PowerupKind kind, int size, int pad, float soft, Color[] sharp, Color[] blurred, int y)
        {
            int side = size + 2 * pad;
            for (int x = 0; x < side; x++)
            {
                float sharpCover = 0f, blurredCover = 0f;
                for (int sy = 0; sy < SUB; sy++)
                    for (int sx = 0; sx < SUB; sx++)
                    {
                        float px = (x - pad + (sx + 0.5f) / SUB) / size, py = (y - pad + (sy + 0.5f) / SUB) / size;
                        float d = Distance(kind, px, py) * size;
                        sharpCover += Clamp01(0.5f - d);
                        if (blurred != null) blurredCover += 1f - Smooth(-0.5f, soft, d);
                    }
                if (sharp != null) sharp[x + side * y] = Coverage(sharpCover / (SUB * SUB));
                if (blurred != null) blurred[x + side * y] = Coverage(blurredCover / (SUB * SUB));
            }
        }

        private static Color Coverage(float cover)
        {
            byte a = (byte)MathF.Round(cover * 255f);
            return new Color(a, a, a, a);
        }

        //--- The three pictures -------------------------------------------------------------------------------------------

        //Two balls side by side, an arrow arching over them from the left one to the right, another under them back. The
        //arcs well clear of the balls: drawn through them, the three ran together into a ring with a hole in it
        private static readonly Arc UPPER_ARROW = new(0.5f, 0.66f, 0.46f, 148f, 42f);
        private static readonly Arc LOWER_ARROW = new(0.5f, 0.34f, 0.46f, -32f, -138f);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static float Swap(float x, float y)
        {
            float d = MathF.Min(Disc(x, y, 0.25f, 0.5f, BALL), Disc(x, y, 0.75f, 0.5f, BALL));
            d = MathF.Min(d, ArrowArc(x, y, UPPER_ARROW));
            d = MathF.Min(d, ArrowArc(x, y, LOWER_ARROW));
            return d;
        }

        //The glass plate, a chevron lifting it, the balls it lifts off hanging under it
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static float Brake(float x, float y)
        {
            float d = RoundedBar(x, y, 0.5f, 0.42f, 0.4f, 0.055f);
            d = MathF.Min(d, Segment(x, y, 0.32f, 0.27f, 0.5f, 0.1f) - STROKE);
            d = MathF.Min(d, Segment(x, y, 0.5f, 0.1f, 0.68f, 0.27f) - STROKE);
            const float r = 0.095f;
            d = MathF.Min(d, Disc(x, y, 0.29f, 0.66f, r));
            d = MathF.Min(d, Disc(x, y, 0.5f, 0.66f, r));
            d = MathF.Min(d, Disc(x, y, 0.71f, 0.66f, r));
            d = MathF.Min(d, Disc(x, y, 0.395f, 0.84f, r));
            d = MathF.Min(d, Disc(x, y, 0.605f, 0.84f, r));
            return d;
        }

        //A saw blade: a disc with ten hooked teeth and a hole for its arbor
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static float Cut(float x, float y)
        {
            float qx = x - 0.5f, qy = y - 0.5f;
            float r = MathF.Sqrt((qx * qx) + (qy * qy));
            float angle = MathF.Atan2(qy, qx);
            const int TEETH = 10;
            float tooth = angle / MathHelper.TwoPi * TEETH;
            tooth -= MathF.Floor(tooth);
            float outer = 0.33f + 0.11f * tooth;
            //The outline's distance, shrunk where it slopes, so the antialiasing is even along the teeth's long edges
            float d = (r - outer) / MathF.Sqrt(1f + MathF.Pow(0.11f * TEETH / MathHelper.TwoPi / MathF.Max(r, 0.05f), 2f) * 0.35f);
            return MathF.Max(d, 0.1f - r);
        }

        //--- Primitives ---------------------------------------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Disc(float x, float y, float cx, float cy, float radius) => Length(x - cx, y - cy) - radius;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Length(float x, float y) => MathF.Sqrt((x * x) + (y * y));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Clamp01((((x - ax) * abx) + ((y - ay) * aby)) / ((abx * abx) + (aby * aby)));
            return Length(x - (ax + abx * t), y - (ay + aby * t));
        }

        //A bar `hx` by `hy` half-extents round `cx`, `cy` with fully rounded ends
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float RoundedBar(float x, float y, float cx, float cy, float hx, float hy)
        {
            float qx = MathF.Abs(x - cx) - (hx - hy), qy = MathF.Abs(y - cy) - hy + hy;
            return Length(qx > 0f ? qx : 0f, qy > 0f ? qy : 0f) + MathF.Min(MathF.Max(qx, qy), 0f) - hy;
        }

        /// <summary>
        /// An arc of a circle round its centre from one angle to another (counted up from the right, y up), a stroke thick,
        /// with an arrowhead at the end it runs to (<see cref="Arc"/>).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static float ArrowArc(float x, float y, Arc a)
        {
            float qx = x - a.CentreX, qy = a.CentreY - y;
            float angle = MathF.Atan2(qy, qx);
            //Outside the arc's span the distance is to its nearer end
            float arc = angle >= a.From && angle <= a.To
                ? MathF.Abs(Length(qx, qy) - a.Radius) - ARC_HALF
                : MathF.Min(Length(qx - a.FromX, qy - a.FromY), Length(qx - a.ToX, qy - a.ToY)) - ARC_HALF;
            return MathF.Min(arc, Triangle(qx, qy, a.TipX, a.TipY, a.LeftX, a.LeftY, a.RightX, a.RightY));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d = MathF.Min(Segment(x, y, ax, ay, bx, by), MathF.Min(Segment(x, y, bx, by, cx, cy), Segment(x, y, cx, cy, ax, ay)));
            float s0 = Cross(bx - ax, by - ay, x - ax, y - ay), s1 = Cross(cx - bx, cy - by, x - bx, y - by), s2 = Cross(ax - cx, ay - cy, x - cx, y - cy);
            bool inside = (s0 >= 0f && s1 >= 0f && s2 >= 0f) || (s0 <= 0f && s1 <= 0f && s2 <= 0f);
            return inside ? -d : d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Cross(float ax, float ay, float bx, float by) => ax * by - ay * bx;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Clamp01(float value)
        {
            value = value > 1f ? 1f : value;
            return value < 0f ? 0f : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// An arrow's figures, worked out once rather than per sample: the span's angles and its two ends, and the head, a
        /// triangle at the end the arrow runs to, pointing along the circle's tangent there.
        /// </summary>
        private sealed class Arc
        {
            internal readonly float CentreX, CentreY, Radius, From, To, FromX, FromY, ToX, ToY;
            internal readonly float TipX, TipY, LeftX, LeftY, RightX, RightY;

            internal Arc(float centreX, float centreY, float radius, float from, float to)
            {
                CentreX = centreX;
                CentreY = centreY;
                Radius = radius;
                From = MathHelper.ToRadians(MathF.Min(from, to));
                To = MathHelper.ToRadians(MathF.Max(from, to));
                FromX = MathF.Cos(From) * radius;
                FromY = MathF.Sin(From) * radius;
                ToX = MathF.Cos(To) * radius;
                ToY = MathF.Sin(To) * radius;

                float end = MathHelper.ToRadians(to);
                float sense = to > from ? 1f : -1f;
                float tipX = MathF.Cos(end) * radius, tipY = MathF.Sin(end) * radius;
                float tangentX = -MathF.Sin(end) * sense, tangentY = MathF.Cos(end) * sense;
                float normalX = tipX / radius, normalY = tipY / radius;
                TipX = tipX + tangentX * 0.09f;
                TipY = tipY + tangentY * 0.09f;
                LeftX = tipX - tangentX * 0.06f + normalX * 0.1f;
                LeftY = tipY - tangentY * 0.06f + normalY * 0.1f;
                RightX = tipX - tangentX * 0.06f - normalX * 0.1f;
                RightY = tipY - tangentY * 0.06f - normalY * 0.1f;
            }
        }
    }
}

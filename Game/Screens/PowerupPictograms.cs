using Microsoft.Xna.Framework;
using System;

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
    /// </summary>
    internal static class PowerupPictograms
    {
        //How thick a stroke is (the arrows, the chevron), and the balls' radius
        private const float STROKE = 0.055f;
        private const float BALL = 0.12f;

        /// <summary>The signed distance from <paramref name="p"/> to <paramref name="kind"/>'s picture.</summary>
        internal static float Distance(PowerupKind kind, Vector2 p) => kind switch
        {
            PowerupKind.Swap => Swap(p),
            PowerupKind.Brake => Brake(p),
            PowerupKind.Cut => Cut(p),
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
            const int SUB = 4;
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float cover = 0f;
                    for (int sy = 0; sy < SUB; sy++)
                        for (int sx = 0; sx < SUB; sx++)
                        {
                            var p = new Vector2((x - pad + (sx + 0.5f) / SUB) / size, (y - pad + (sy + 0.5f) / SUB) / size);
                            float d = Distance(kind, p) * size;
                            cover += soft > 0f ? 1f - Smooth(-0.5f, soft, d) : MathHelper.Clamp(0.5f - d, 0f, 1f);
                        }
                    cover /= SUB * SUB;
                    byte a = (byte)MathF.Round(cover * 255f);
                    pixels[x + side * y] = new Color(a, a, a, a);
                }
            return pixels;
        }

        //--- The three pictures -------------------------------------------------------------------------------------------

        //Two balls side by side, an arrow arching over them from the left one to the right, another under them back
        private static float Swap(Vector2 p)
        {
            //The arcs well clear of the balls: drawn through them, the three ran together into a ring with a hole in it
            float d = MathF.Min(Disc(p, new Vector2(0.25f, 0.5f), BALL), Disc(p, new Vector2(0.75f, 0.5f), BALL));
            d = MathF.Min(d, ArrowArc(p, new Vector2(0.5f, 0.66f), 0.46f, 148f, 42f));
            d = MathF.Min(d, ArrowArc(p, new Vector2(0.5f, 0.34f), 0.46f, -32f, -138f));
            return d;
        }

        //The glass plate, a chevron lifting it, the balls it lifts off hanging under it
        private static float Brake(Vector2 p)
        {
            float d = RoundedBar(p, new Vector2(0.5f, 0.42f), new Vector2(0.4f, 0.055f));
            d = MathF.Min(d, Segment(p, new Vector2(0.32f, 0.27f), new Vector2(0.5f, 0.1f)) - STROKE);
            d = MathF.Min(d, Segment(p, new Vector2(0.5f, 0.1f), new Vector2(0.68f, 0.27f)) - STROKE);
            const float r = 0.095f;
            d = MathF.Min(d, Disc(p, new Vector2(0.29f, 0.66f), r));
            d = MathF.Min(d, Disc(p, new Vector2(0.5f, 0.66f), r));
            d = MathF.Min(d, Disc(p, new Vector2(0.71f, 0.66f), r));
            d = MathF.Min(d, Disc(p, new Vector2(0.395f, 0.84f), r));
            d = MathF.Min(d, Disc(p, new Vector2(0.605f, 0.84f), r));
            return d;
        }

        //A saw blade: a disc with ten hooked teeth and a hole for its arbor
        private static float Cut(Vector2 p)
        {
            Vector2 q = p - new Vector2(0.5f, 0.5f);
            float r = q.Length();
            float angle = MathF.Atan2(q.Y, q.X);
            const int TEETH = 10;
            float tooth = angle / MathHelper.TwoPi * TEETH;
            tooth -= MathF.Floor(tooth);
            float outer = 0.33f + 0.11f * tooth;
            //The outline's distance, shrunk where it slopes, so the antialiasing is even along the teeth's long edges
            float d = (r - outer) / MathF.Sqrt(1f + MathF.Pow(0.11f * TEETH / MathHelper.TwoPi / MathF.Max(r, 0.05f), 2f) * 0.35f);
            return MathF.Max(d, 0.1f - r);
        }

        //--- Primitives ---------------------------------------------------------------------------------------------------

        private static float Disc(Vector2 p, Vector2 centre, float radius) => Vector2.Distance(p, centre) - radius;

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float RoundedBar(Vector2 p, Vector2 centre, Vector2 half)
        {
            Vector2 q = new(MathF.Abs(p.X - centre.X) - (half.X - half.Y), MathF.Abs(p.Y - centre.Y) - half.Y + half.Y);
            Vector2 outside = Vector2.Max(q, Vector2.Zero);
            return outside.Length() + MathF.Min(MathF.Max(q.X, q.Y), 0f) - half.Y;
        }

        /// <summary>
        /// An arc of a circle round <paramref name="centre"/> from <paramref name="from"/> degrees to <paramref name="to"/>
        /// (counted up from the right, y up), a stroke thick, with an arrowhead at its <paramref name="to"/> end.
        /// </summary>
        private static float ArrowArc(Vector2 p, Vector2 centre, float radius, float from, float to)
        {
            Vector2 q = new(p.X - centre.X, centre.Y - p.Y);
            float a0 = MathHelper.ToRadians(MathF.Min(from, to)), a1 = MathHelper.ToRadians(MathF.Max(from, to));
            float angle = MathF.Atan2(q.Y, q.X);
            float ring = MathF.Abs(q.Length() - radius) - STROKE * 0.85f;
            //Outside the arc's span the distance is to its nearer end
            Vector2 e0 = new(MathF.Cos(a0) * radius, MathF.Sin(a0) * radius), e1 = new(MathF.Cos(a1) * radius, MathF.Sin(a1) * radius);
            float arc = angle >= a0 && angle <= a1 ? ring : MathF.Min(Vector2.Distance(q, e0), Vector2.Distance(q, e1)) - STROKE * 0.85f;

            //The head: a triangle at the end the arrow runs to, pointing along the circle's tangent there
            float end = MathHelper.ToRadians(to);
            float sense = to > from ? 1f : -1f;
            Vector2 tip0 = new(MathF.Cos(end) * radius, MathF.Sin(end) * radius);
            Vector2 tangent = new Vector2(-MathF.Sin(end), MathF.Cos(end)) * sense;
            Vector2 normal = new(tip0.X / radius, tip0.Y / radius);
            Vector2 tip = tip0 + tangent * 0.09f;
            Vector2 left = tip0 - tangent * 0.06f + normal * 0.1f, right = tip0 - tangent * 0.06f - normal * 0.1f;
            return MathF.Min(arc, Triangle(q, tip, left, right));
        }

        private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d = MathF.Min(Segment(p, a, b), MathF.Min(Segment(p, b, c), Segment(p, c, a)));
            float s0 = Cross(b - a, p - a), s1 = Cross(c - b, p - b), s2 = Cross(a - c, p - c);
            bool inside = (s0 >= 0f && s1 >= 0f && s2 >= 0f) || (s0 <= 0f && s1 <= 0f && s2 <= 0f);
            return inside ? -d : d;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = MathHelper.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}

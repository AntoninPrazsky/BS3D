using Microsoft.Xna.Framework;
using System;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// The colour cue's figures (#737) on the CPU: a line-for-line port of the signed field in
    /// <c>BallColourCue.fxh</c>, positive inside a figure, for what has to draw or measure a figure without the ball
    /// shader. Two readers: the HUD's queue, whose flat discs carry the figure their round carries
    /// (<see cref="BakeDisc"/>), and <c>ColourCueTests</c>, which measures every figure's share of what the play
    /// camera sees with it and holds <see cref="ColourCues.Figure"/>'s coverage to that.
    /// <para>
    /// <b>A change to a figure in the shader is a change here</b>, in the same commit: the coverage test fails on
    /// the numbers when the two disagree on a figure's size, but not on a figure that moved without changing size.
    /// </para>
    /// </summary>
    public static class ColourCueField
    {
        //BallColourCue.fxh's solids, as axes (both ends of each are a vertex) except the tetrahedron's
        private static readonly Vector3[] Tetrahedron =
        {
            new(0.577350f, 0.577350f, 0.577350f),
            new(0.577350f, -0.577350f, -0.577350f),
            new(-0.577350f, 0.577350f, -0.577350f),
            new(-0.577350f, -0.577350f, 0.577350f),
        };

        private static readonly Vector3[] Cube =
        {
            new(0.577350f, 0.577350f, 0.577350f),
            new(0.577350f, 0.577350f, -0.577350f),
            new(0.577350f, -0.577350f, 0.577350f),
            new(-0.577350f, 0.577350f, 0.577350f),
        };

        private static readonly Vector3[] Icosahedron =
        {
            new(0.000000f, 0.525731f, 0.850651f),
            new(0.000000f, 0.525731f, -0.850651f),
            new(0.525731f, 0.850651f, 0.000000f),
            new(0.525731f, -0.850651f, 0.000000f),
            new(0.850651f, 0.000000f, 0.525731f),
            new(-0.850651f, 0.000000f, 0.525731f),
        };

        private static readonly Vector3[] Dodecahedron =
        {
            new(0.577350f, 0.577350f, 0.577350f),
            new(0.577350f, 0.577350f, -0.577350f),
            new(0.577350f, -0.577350f, 0.577350f),
            new(-0.577350f, 0.577350f, 0.577350f),
            new(0.000000f, 0.356822f, 0.934172f),
            new(0.000000f, 0.356822f, -0.934172f),
            new(0.356822f, 0.934172f, 0.000000f),
            new(0.356822f, -0.934172f, 0.000000f),
            new(0.934172f, 0.000000f, 0.356822f),
            new(-0.934172f, 0.000000f, 0.356822f),
        };

        /// <summary>
        /// A direction in object space read in a figure's frame, as the shader's <c>mul(cue.frame, direction)</c>: each
        /// component is one of the frame's rows dotted with the direction.
        /// </summary>
        public static Vector3 ToFrame(Matrix frame, Vector3 direction) => new(
            frame.M11 * direction.X + frame.M12 * direction.Y + frame.M13 * direction.Z,
            frame.M21 * direction.X + frame.M22 * direction.Y + frame.M23 * direction.Z,
            frame.M31 * direction.X + frame.M32 * direction.Y + frame.M33 * direction.Z);

        /// <summary>The figure's signed field at <paramref name="q"/>, a unit direction already in its frame; positive inside.</summary>
        public static float Field(ColourCues.Figure figure, Vector3 q)
        {
            Vector4 a = figure.A, b = figure.B, c = figure.C;

            if (a.X < 1.5f) return BandFamily(q.Y, MathF.Atan2(q.Z, q.X), a, b, c);

            if (a.X < 2.5f)
            {
                float nearest = Nearest(q, a.Y);

                //A spot, or a ring where an inner radius is given (its cosine is 2, out of reach, where none is)
                float field = MathF.Min(nearest - a.Z, a.W - nearest);

                //A second solid's spots, the other size of a two-size pattern
                if (b.X != 0f) field = MathF.Max(field, Nearest(q, b.X) - b.Y);

                return field;
            }

            if (a.X < 3.5f) return MathF.Sin(q.X * a.Y) * MathF.Sin(q.Y * a.Y) * MathF.Sin(q.Z * a.Y);

            //The face of the frame's cube this direction is on, and where on it (a gnomonic projection)
            Vector3 m = Abs(q);
            Vector2 uv = m.X >= MathF.Max(m.Y, m.Z) ? new Vector2(q.Z, q.Y) / m.X
                : m.Y >= m.Z ? new Vector2(q.X, q.Z) / m.Y : new Vector2(q.X, q.Y) / m.Z;

            return Emblem(uv / a.Z, a.Y);
        }

        /// <summary>Which face of the frame's cube <paramref name="q"/> is on (0 X, 1 Y, 2 Z), the select the emblems' seam test reads.</summary>
        public static int Face(Vector3 q)
        {
            Vector3 m = Abs(q);
            return m.X >= MathF.Max(m.Y, m.Z) ? 0 : m.Y >= m.Z ? 1 : 2;
        }

        /// <summary>
        /// The figure painted on a flat disc of <paramref name="radius"/> pixels for the HUD's queue: the ball seen
        /// face-on along its frame's Z, its frame's Y up, so an emblem stands the way it was drawn and a band lies
        /// level. Laid out like <c>PlayHud.DiscTexture</c>'s discs - even-sided, (2r + 2) texels a side, the centre on
        /// the corner where the four middle texels meet - as premultiplied white RGBA whose every channel is the
        /// figure's coverage, so the HUD tints it with the figure's ink. The edge is the shader's: a smoothstep over the
        /// field's change across a texel, and an emblem's texel that straddles two faces of its cube is ground.
        /// </summary>
        public static byte[] BakeDisc(ColourCues.Figure figure, int radius)
        {
            if (radius < 1) throw new ArgumentOutOfRangeException(nameof(radius), radius, null);

            int size = 2 * radius + 2;
            float centre = radius + 1f;
            float step = 1f / radius;
            bool emblem = figure.A.X > 3.5f;
            byte[] data = new byte[size * size * 4];

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - centre) * step;
                    float v = (centre - (y + 0.5f)) * step;

                    if (!TryFaceOn(u, v, out Vector3 q) || !TryFaceOn(u + step, v, out Vector3 qx) || !TryFaceOn(u, v - step, out Vector3 qy))
                        continue;

                    if (emblem && (Face(qx) != Face(q) || Face(qy) != Face(q))) continue;

                    float field = Field(figure, q);
                    float width = MathF.Max(MathF.Abs(Field(figure, qx) - field) + MathF.Abs(Field(figure, qy) - field), 1e-5f);
                    float coverage = SmoothStep(-width, width, field);

                    byte alpha = (byte)MathF.Round(coverage * 255f);
                    int o = (y * size + x) * 4;
                    data[o] = alpha;
                    data[o + 1] = alpha;
                    data[o + 2] = alpha;
                    data[o + 3] = alpha;
                }

            return data;
        }

        //The point of the unit sphere seen at (u, v) on its disc from along +Z; none past the outline
        private static bool TryFaceOn(float u, float v, out Vector3 q)
        {
            float rest = 1f - u * u - v * v;
            q = rest > 0f ? new Vector3(u, v, MathF.Sqrt(rest)) : Vector3.Zero;
            return rest > 0f;
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        //ColourCueNearest: the cosine of the angle to the nearest vertex of a solid, `solid` its vertex count (-4 the
        //tetrahedron turned inside out)
        private static float Nearest(Vector3 q, float solid)
        {
            float nearest = -1f;

            if (solid < -3.5f)
                foreach (Vector3 axis in Tetrahedron) nearest = MathF.Max(nearest, -Vector3.Dot(q, axis));
            else if (solid < 4.5f)
                foreach (Vector3 axis in Tetrahedron) nearest = MathF.Max(nearest, Vector3.Dot(q, axis));
            else if (solid < 6.5f)
            {
                Vector3 m = Abs(q);
                nearest = MathF.Max(m.X, MathF.Max(m.Y, m.Z));
            }
            else if (solid < 8.5f)
                foreach (Vector3 axis in Cube) nearest = MathF.Max(nearest, MathF.Abs(Vector3.Dot(q, axis)));
            else if (solid < 12.5f)
                foreach (Vector3 axis in Icosahedron) nearest = MathF.Max(nearest, MathF.Abs(Vector3.Dot(q, axis)));
            else
                foreach (Vector3 axis in Dodecahedron) nearest = MathF.Max(nearest, MathF.Abs(Vector3.Dot(q, axis)));

            return nearest;
        }

        //ColourCueBandFamily: one family of bands round an axis, `along` the height along it, `around` the angle round it
        private static float BandFamily(float along, float around, Vector4 a, Vector4 b, Vector4 c)
        {
            float t = a.Y * along + a.Z + b.X * MathF.Sin(b.Y * around);
            float wave = MathF.Cos(MathF.PI * t);
            float field = wave - MathF.Cos(MathF.PI * a.W);

            //A hollow band is its two edges: a double line
            if (b.Z > 0f) field = MathF.Min(field, MathF.Cos(MathF.PI * b.Z) - wave);

            //A dashed one is cut round the ball
            if (b.W > 0f) field = MathF.Min(field, MathF.Cos(b.W * around) - MathF.Cos(MathF.PI * c.X));

            return field;
        }

        //ColourCueHeart: a heart, point down
        private static float Heart(Vector2 p)
        {
            p.Y -= 0.08f;
            float corner = (0.672f - (MathF.Abs(p.X) + MathF.Abs(p.Y + 0.15f))) * 0.707107f;
            float lobe = 0.475f - (new Vector2(MathF.Abs(p.X), p.Y) - new Vector2(0.336f, 0.186f)).Length();

            return MathF.Max(corner, lobe);
        }

        //ColourCueStem: a spade's or a club's stem
        private static float Stem(Vector2 p) =>
            MathF.Min(0.06f + 0.25f * (-0.30f - p.Y) - MathF.Abs(p.X), MathF.Min(p.Y + 0.90f, -0.20f - p.Y));

        //ColourCueEmblem: the emblems' signed fields, in the face's units over the emblem's size, positive inside
        private static float Emblem(Vector2 p, float shape)
        {
            if (shape < 1.5f)
            {
                //A five-pointed star, a point up. HLSL's frac is x - floor(x), also for a negative x
                float turns = (MathF.Atan2(p.Y, p.X) - 1.5707963f) * (5f / 6.2831853f);
                float spoke = MathF.Abs((turns - MathF.Floor(turns)) * 2f - 1f);
                return MathHelper.Lerp(0.44f, 1.08f, spoke * spoke) - p.Length();
            }

            if (shape < 2.5f) return Heart(p);

            if (shape < 3.5f) return MathF.Max(Heart(new Vector2(p.X, 0.15f - p.Y) * 1.2f), Stem(p));

            if (shape < 4.5f)
            {
                float leaves = MathF.Max(0.34f - (p - new Vector2(0f, 0.40f)).Length(),
                    MathF.Max(0.34f - (p - new Vector2(0.40f, -0.08f)).Length(), 0.34f - (p - new Vector2(-0.40f, -0.08f)).Length()));
                return MathF.Max(MathF.Max(leaves, 0.20f - (p - new Vector2(0f, 0.08f)).Length()), Stem(p));
            }

            if (shape < 5.5f) return 1f - (MathF.Abs(p.X) / 0.66f + MathF.Abs(p.Y));

            if (shape < 6.5f) return 0.62f - MathF.Max(MathF.Abs(p.X), MathF.Abs(p.Y));

            if (shape < 7.5f)
            {
                float d = p.Length();
                return MathF.Min(0.85f - d, d - 0.50f);
            }

            if (shape < 8.5f) return MathF.Min(0.45f - (MathF.Abs(p.X) * 0.866025f + p.Y * 0.5f), p.Y + 0.45f);

            if (shape < 9.5f) return MathF.Min(0.85f - p.Length(), (p - new Vector2(0.42f, 0.18f)).Length() - 0.68f);

            if (shape < 10.5f)
                return MathF.Max(0.55f - (p - new Vector2(0f, -0.25f)).Length(),
                    MathF.Min(-(MathF.Abs(p.X) * 0.888f + (p.Y - 0.95f) * 0.458f), p.Y + 0.25f));

            if (shape < 11.5f)
            {
                float petals = MathF.Max(MathF.Max(0.40f - (p - new Vector2(0.42f, 0f)).Length(), 0.40f - (p + new Vector2(0.42f, 0f)).Length()),
                    MathF.Max(0.40f - (p - new Vector2(0f, 0.42f)).Length(), 0.40f - (p + new Vector2(0f, 0.42f)).Length()));
                return MathF.Max(petals, 0.30f - p.Length());
            }

            if (shape < 12.5f) return MathF.Min(0.28f - MathF.Abs(p.Y - 0.35f + MathF.Abs(p.X) * 0.9f), 0.85f - MathF.Abs(p.X));

            if (shape < 13.5f)
            {
                Vector2 r = new Vector2(p.X + p.Y, p.X - p.Y) * 0.707107f;
                return MathF.Max(MathF.Min(0.24f - MathF.Abs(r.X), 0.95f - MathF.Abs(r.Y)), MathF.Min(0.24f - MathF.Abs(r.Y), 0.95f - MathF.Abs(r.X)));
            }

            Vector2 h = new(MathF.Abs(p.X), MathF.Abs(p.Y));
            return 0.75f - MathF.Max(h.X * 0.866025f + h.Y * 0.5f, h.Y);
        }

        private static Vector3 Abs(Vector3 v) => new(MathF.Abs(v.X), MathF.Abs(v.Y), MathF.Abs(v.Z));
    }
}

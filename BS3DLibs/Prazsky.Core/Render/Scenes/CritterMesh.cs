using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The desert's creatures' bodies (#966): a darkling beetle and a scorpion, built of ellipsoids and tubes one body length
    /// long (the nose at +Z, standing on Y = 0), each vertex carrying in its texture coordinate what <c>Critter.fx</c> moves it
    /// by: X the leg's gait phase in radians (a tripod, the middle leg of a side out of step with its front and back), or
    /// <see cref="BODY"/> for the body and <see cref="TAIL"/> for a scorpion's tail; Y how far along the leg or the tail the
    /// vertex is (0 at the hip, 1 at the foot or the sting). Every triangle is wound clockwise seen from outside, against its
    /// vertices' own normals, as every procedural mesh in the repository is.
    /// </summary>
    internal static class CritterMesh
    {
        internal const float BODY = -1f, TAIL = -2f;

        internal static (VertexPositionNormalTexture[] Vertices, short[] Indices) Beetle()
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            //The shell: long and domed, the head in front of it, the pronotum between
            Ellipsoid(v, idx, new Vector3(0f, 0.2f, -0.05f), new Vector3(0.24f, 0.17f, 0.36f), BODY);
            Ellipsoid(v, idx, new Vector3(0f, 0.17f, 0.32f), new Vector3(0.15f, 0.11f, 0.1f), BODY);
            Ellipsoid(v, idx, new Vector3(0f, 0.15f, 0.44f), new Vector3(0.08f, 0.07f, 0.07f), BODY);
            //Antennae
            Leg(v, idx, new Vector3(0.04f, 0.18f, 0.48f), new Vector3(0.12f, 0.24f, 0.62f), new Vector3(0.16f, 0.22f, 0.72f), 0.012f, BODY);
            Leg(v, idx, new Vector3(-0.04f, 0.18f, 0.48f), new Vector3(-0.12f, 0.24f, 0.62f), new Vector3(-0.16f, 0.22f, 0.72f), 0.012f, BODY);
            //Six legs, long and thin, in a tripod: front and back of one side with the middle of the other
            float[] along = { 0.26f, 0.05f, -0.18f };
            for (int i = 0; i < 3; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float phase = ((i == 1) == (side > 0)) ? 0f : MathF.PI;
                    float z = along[i];
                    Leg(v, idx, new Vector3(0.15f * side, 0.14f, z), new Vector3(0.36f * side, 0.24f, z + 0.06f * (1 - i)),
                        new Vector3(0.48f * side, 0f, z + 0.12f * (1 - i)), 0.022f, phase);
                }
            return (v.ToArray(), idx.ToArray());
        }

        internal static (VertexPositionNormalTexture[] Vertices, short[] Indices) Scorpion()
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            //The carapace and the abdomen, low and flat
            Ellipsoid(v, idx, new Vector3(0f, 0.1f, 0.12f), new Vector3(0.14f, 0.07f, 0.16f), BODY);
            Ellipsoid(v, idx, new Vector3(0f, 0.1f, -0.12f), new Vector3(0.13f, 0.075f, 0.2f), BODY);
            //The tail: six thick segments overlapping into one curl up and over the back, the bulb and the sting at its tip.
            //⚠ Five thin ones, each a short ellipsoid, read as a stack of balls
            //On a circle behind the body: from straight down at the abdomen's end, back, up over the top and forwards, a two
            //thirds turn. ⚠ Raised straight up and dropped back, it read from the side as a stack of balls
            var tail = new Vector3[7];
            for (int s = 0; s < tail.Length; s++)
            {
                float a = MathHelper.ToRadians(-90f - 235f * s / 6f);
                tail[s] = new Vector3(0f, 0.34f + MathF.Sin(a) * 0.24f, -0.33f + MathF.Cos(a) * 0.24f);
            }
            for (int s = 0; s < tail.Length - 1; s++)
            {
                Vector3 mid = (tail[s] + tail[s + 1]) * 0.5f;
                Vector3 axis = tail[s + 1] - tail[s];
                Segment(v, idx, mid, axis * 1.3f, 0.07f - 0.006f * s, TAIL, s / 6f);
            }
            Ellipsoid(v, idx, tail[6] + new Vector3(0f, -0.03f, 0.05f), new Vector3(0.055f, 0.06f, 0.075f), TAIL, 1f);
            Leg(v, idx, tail[6] + new Vector3(0f, -0.06f, 0.1f), tail[6] + new Vector3(0f, -0.1f, 0.13f), tail[6] + new Vector3(0f, -0.15f, 0.12f), 0.018f, TAIL);
            //The pincers: an arm out and forwards, a big claw at its end opening into two fingers, the references' chelae
            for (int side = -1; side <= 1; side += 2)
            {
                Leg(v, idx, new Vector3(0.08f * side, 0.1f, 0.24f), new Vector3(0.24f * side, 0.15f, 0.34f), new Vector3(0.22f * side, 0.11f, 0.48f), 0.04f, TAIL);
                Ellipsoid(v, idx, new Vector3(0.22f * side, 0.11f, 0.56f), new Vector3(0.085f, 0.055f, 0.12f), TAIL, 0.6f);
                Leg(v, idx, new Vector3(0.24f * side, 0.11f, 0.64f), new Vector3(0.25f * side, 0.11f, 0.72f), new Vector3(0.21f * side, 0.1f, 0.77f), 0.025f, TAIL);
                Leg(v, idx, new Vector3(0.19f * side, 0.11f, 0.64f), new Vector3(0.17f * side, 0.11f, 0.71f), new Vector3(0.2f * side, 0.1f, 0.76f), 0.02f, TAIL);
            }
            //Eight legs in two tripods and a pair
            float[] along = { 0.14f, 0.03f, -0.08f, -0.19f };
            for (int i = 0; i < 4; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float phase = ((i % 2 == 1) == (side > 0)) ? 0f : MathF.PI;
                    float z = along[i];
                    Leg(v, idx, new Vector3(0.11f * side, 0.09f, z), new Vector3(0.3f * side, 0.17f, z + 0.03f * (1.5f - i)),
                        new Vector3(0.4f * side, 0f, z + 0.07f * (1.5f - i)), 0.018f, phase);
                }
            return (v.ToArray(), idx.ToArray());
        }

        //An ellipsoid: a sphere of 12 x 8 scaled by the radii, normals from the scaled surface's own gradient
        private static void Ellipsoid(List<VertexPositionNormalTexture> v, List<short> idx, Vector3 centre, Vector3 radii,
            float marker, float weight = 0f)
        {
            const int SLICES = 12, STACKS = 8;
            int start = v.Count;
            for (int st = 0; st <= STACKS; st++)
            {
                float phi = MathF.PI * st / STACKS;
                for (int sl = 0; sl <= SLICES; sl++)
                {
                    float theta = MathHelper.TwoPi * sl / SLICES;
                    var unit = new Vector3(MathF.Sin(phi) * MathF.Cos(theta), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(theta));
                    Vector3 p = centre + unit * radii;
                    Vector3 n = Vector3.Normalize(unit / radii);
                    v.Add(new VertexPositionNormalTexture(p, n, new Vector2(marker, weight)));
                }
            }
            for (int st = 0; st < STACKS; st++)
                for (int sl = 0; sl < SLICES; sl++)
                {
                    int a = start + st * (SLICES + 1) + sl, b = a + SLICES + 1;
                    Triangle(v, idx, a, b, a + 1);
                    Triangle(v, idx, a + 1, b, b + 1);
                }
        }

        //A tail segment: a short ellipsoid along its own axis
        private static void Segment(List<VertexPositionNormalTexture> v, List<short> idx, Vector3 centre, Vector3 axis, float radius,
            float marker, float weight)
        {
            int start = v.Count;
            Ellipsoid(v, idx, Vector3.Zero, new Vector3(radius, radius, axis.Length() * 0.62f), marker, weight);
            //Turned from +Z onto the axis and moved to its place
            Vector3 forward = Vector3.Normalize(axis);
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, forward));
            Vector3 up = Vector3.Cross(forward, right);
            for (int i = start; i < v.Count; i++)
            {
                VertexPositionNormalTexture x = v[i];
                Vector3 p = right * x.Position.X + up * x.Position.Y + forward * x.Position.Z + centre;
                Vector3 n = right * x.Normal.X + up * x.Normal.Y + forward * x.Normal.Z;
                v[i] = new VertexPositionNormalTexture(p, Vector3.Normalize(n), x.TextureCoordinate);
            }
        }

        //A leg: a thin tube from the hip over the knee to the foot, five-sided, its far end closed by the foot
        private static void Leg(List<VertexPositionNormalTexture> v, List<short> idx, Vector3 hip, Vector3 knee, Vector3 foot,
            float radius, float phase)
        {
            Vector3[] points = { hip, knee, foot };
            const int SIDES = 5;
            int start = v.Count;
            for (int k = 0; k < points.Length; k++)
            {
                Vector3 along = k == 0 ? points[1] - points[0] : k == points.Length - 1 ? points[k] - points[k - 1] : points[k + 1] - points[k - 1];
                along = Vector3.Normalize(along);
                Vector3 side = Vector3.Normalize(Vector3.Cross(MathF.Abs(along.Y) > 0.9f ? Vector3.UnitX : Vector3.Up, along));
                Vector3 up = Vector3.Cross(along, side);
                float r = radius * (k == points.Length - 1 ? 0.5f : 1f);
                float weight = k / (float)(points.Length - 1);
                for (int s = 0; s <= SIDES; s++)
                {
                    float a = MathHelper.TwoPi * s / SIDES;
                    Vector3 n = side * MathF.Cos(a) + up * MathF.Sin(a);
                    v.Add(new VertexPositionNormalTexture(points[k] + n * r, n, new Vector2(phase, weight)));
                }
            }
            for (int k = 0; k < points.Length - 1; k++)
                for (int s = 0; s < SIDES; s++)
                {
                    int a = start + k * (SIDES + 1) + s, b = a + SIDES + 1;
                    Triangle(v, idx, a, b, a + 1);
                    Triangle(v, idx, a + 1, b, b + 1);
                }
        }

        //A triangle wound clockwise seen from outside: its face normal ((b - a) x (c - a)) points into the surface, against the
        //vertices' own normals; turned over when it does not
        private static void Triangle(List<VertexPositionNormalTexture> v, List<short> idx, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(v[b].Position - v[a].Position, v[c].Position - v[a].Position);
            Vector3 normal = v[a].Normal + v[b].Normal + v[c].Normal;
            if (Vector3.Dot(face, normal) > 0f) (b, c) = (c, b);
            idx.Add((short)a);
            idx.Add((short)b);
            idx.Add((short)c);
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One small fish for the sea's school (#760), the shape the references drew (<c>C:\Users\panrd\AI\sd\out\760-zimage</c>
    /// and <c>760-klein</c>): a slim torpedo, deeper than it is wide, a forked tail and a small dorsal fin, in flat facets
    /// so it reads as the stylised fish the concept sheets showed rather than as a smooth blob at the distance it is seen.
    /// <para>
    /// The local frame is the one <c>Fish.fx</c> swims it in: <b>+X forward</b> (the nose at +0.5, the tail's tips at
    /// −0.78), +Y up, Z across. One unit long from nose to the body's end, so the school's own size is a scale. Every
    /// triangle carries its own flat normal, turned outward from the body's axis, and the school is drawn
    /// <c>CullNone</c> (the fins are single sheets), so the winding rule of the procedural meshes does not apply here.
    /// </para>
    /// </summary>
    internal static class FishMesh
    {
        //The body's stations from the end of the tail stalk (0) to the last ring before the nose (0.9), with the
        //half-depth and half-width of each: deepest a little ahead of the middle, laterally pressed to about half
        private static readonly (float U, float Depth, float Width)[] RINGS =
        {
            (0.00f, 0.035f, 0.020f),
            (0.15f, 0.070f, 0.035f),
            (0.35f, 0.110f, 0.055f),
            (0.55f, 0.130f, 0.065f),
            (0.75f, 0.115f, 0.058f),
            (0.90f, 0.070f, 0.040f),
        };

        //The belly sits a little lower than the back is high, as a fish's does
        private const float BELLY_DROP = 0.012f;

        //Six sides: back, two flanks above, two below, belly - the fewest that still read as a fish and not a tube
        private const int SIDES = 6;

        /// <summary>How many vertices one fish is, as a triangle list.</summary>
        internal static int VertexCount => Build().Count;

        /// <summary>One fish's triangles in its local frame: position, outward normal, and whether it is a fin
        /// (1) or the body (0). A triangle list, three entries a triangle.</summary>
        internal static List<(Vector3 Position, Vector3 Normal, float Fin)> Build()
        {
            List<(Vector3, Vector3, float)> triangles = new();

            Vector3[][] rings = new Vector3[RINGS.Length][];
            for (int k = 0; k < RINGS.Length; k++)
            {
                (float u, float depth, float width) = RINGS[k];
                float x = u - 0.5f;
                rings[k] = new Vector3[SIDES];
                for (int j = 0; j < SIDES; j++)
                {
                    //From the back round the right flank to the belly and up the left
                    float angle = MathHelper.PiOver2 - j * MathHelper.TwoPi / SIDES;
                    float y = MathF.Sin(angle) * depth;
                    if (y < 0f) y -= BELLY_DROP * MathF.Sin(-angle);
                    rings[k][j] = new Vector3(x, y, MathF.Cos(angle) * width);
                }
            }

            //The body between rings
            for (int k = 0; k + 1 < RINGS.Length; k++)
                for (int j = 0; j < SIDES; j++)
                {
                    Vector3 a = rings[k][j], b = rings[k][(j + 1) % SIDES];
                    Vector3 c = rings[k + 1][(j + 1) % SIDES], d = rings[k + 1][j];
                    Body(triangles, a, b, c);
                    Body(triangles, a, c, d);
                }

            //The nose, a point a little low, and the tail stalk's end closed
            Vector3 nose = new(0.5f, -0.012f, 0f);
            Vector3 stalk = new(-0.5f, 0f, 0f);
            for (int j = 0; j < SIDES; j++)
            {
                Body(triangles, rings[^1][j], rings[^1][(j + 1) % SIDES], nose);
                Body(triangles, rings[0][(j + 1) % SIDES], rings[0][j], stalk);
            }

            //The forked tail: two lobes from the stalk to their tips, the fork's notch between them
            Vector3 notch = new(-0.66f, 0.005f, 0f);
            Fin(triangles, new Vector3(-0.5f, 0.03f, 0f), new Vector3(-0.78f, 0.17f, 0f), notch);
            Fin(triangles, new Vector3(-0.5f, -0.03f, 0f), notch, new Vector3(-0.78f, -0.15f, 0f));

            //The dorsal fin, a small swept triangle on the back just behind the deepest ring
            float back = RINGS[3].Depth;
            Fin(triangles, new Vector3(0.06f, back - 0.005f, 0f), new Vector3(-0.14f, back - 0.02f, 0f), new Vector3(-0.05f, back + 0.09f, 0f));

            return triangles;
        }

        //A body facet, its normal turned away from the body's axis
        private static void Body(List<(Vector3, Vector3, float)> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.LengthSquared() < 1e-12f) return;
            normal.Normalize();

            Vector3 middle = (a + b + c) / 3f;
            Vector3 outward = new(0f, middle.Y, middle.Z);
            if (outward.LengthSquared() < 1e-8f) outward = new Vector3(Math.Sign(middle.X), 0f, 0f);
            if (Vector3.Dot(normal, outward) < 0f) normal = -normal;

            triangles.Add((a, normal, 0f));
            triangles.Add((b, normal, 0f));
            triangles.Add((c, normal, 0f));
        }

        //A fin: a sheet in the fish's own plane of symmetry, its normal across it (the shader lights it from either side)
        private static void Fin(List<(Vector3, Vector3, float)> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            triangles.Add((a, Vector3.UnitZ, 1f));
            triangles.Add((b, Vector3.UnitZ, 1f));
            triangles.Add((c, Vector3.UnitZ, 1f));
        }
    }

    /// <summary>A fish vertex: its place and normal in the fish's own frame, and (which fish, fin or body, 0, 0).</summary>
    internal struct FishVertex : IVertexType
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector4 Data;

        public FishVertex(Vector3 position, Vector3 normal, Vector4 data)
        {
            Position = position;
            Normal = normal;
            Data = data;
        }

        public static readonly VertexDeclaration Declaration = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0));

        readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
    }
}

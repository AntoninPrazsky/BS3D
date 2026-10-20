using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The aurora's far stand's one spruce (#963): a spire of four skirts of jagged bough tips over a short stem, about 170
    /// triangles, wound clockwise from outside like every procedural mesh (<c>Tools/WindingCheck</c> judges it). The far
    /// stand draws a few thousand of it instanced on the hills past the planted wood, where a tree is a silhouette and
    /// a silhouette is its bough tips. Its vertices carry how much of a tier's top the snow may lie on.
    /// </summary>
    public sealed class AuroraStandMesh : IProceduralMesh, IDisposable
    {
        /// <inheritdoc/>
        public VertexBuffer VertexBuffer { get; private set; }

        /// <inheritdoc/>
        public IndexBuffer IndexBuffer { get; private set; }

        /// <inheritdoc/>
        public int PrimitiveCount { get; }

        /// <inheritdoc/>
        public BoundingSphere BoundingSphere { get; }

        /// <summary>A vertex of the far stand's spire: position, normal, and how much of a tier's top the snow may lie on.</summary>
        private readonly struct StandVertex : IVertexType
        {
            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly float Snow;

            public StandVertex(Vector3 position, Vector3 normal, float snow)
            {
                Position = position;
                Normal = normal;
                Snow = snow;
            }

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
                new VertexElement(24, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 0));

            VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }

        //The spire's tiers: four skirts of jagged bough tips, each a cone up to a collar inside the next, over a short
        //stem, and a leader. A far tree is a silhouette, and a silhouette is its bough tips: each skirt's ring alternates
        //long and short tips and droops at them.
        private const int STAND_TIERS = 4, STAND_SIDES = 10;

        public AuroraStandMesh(GraphicsDevice device, AuroraFarStandConfig stand)
        {
            var vertices = new List<StandVertex>();
            var indices = new List<int>();
            float height = stand.Height, radius = stand.Radius;

            void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, float snowA, float snowB, float snowC, Vector3 na, Vector3 nb, Vector3 nc)
            {
                //Clockwise seen from outside (CLAUDE.md's winding): the face's (b - a) x (c - a) points into the mesh
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) > 0f)
                {
                    (b, c) = (c, b);
                    (snowB, snowC) = (snowC, snowB);
                    (nb, nc) = (nc, nb);
                }

                int at = vertices.Count;
                vertices.Add(new StandVertex(a, na, snowA));
                vertices.Add(new StandVertex(b, nb, snowB));
                vertices.Add(new StandVertex(c, nc, snowC));
                indices.Add(at);
                indices.Add(at + 1);
                indices.Add(at + 2);
            }

            static Vector3 Ring(float r, float y, float angle) => new(MathF.Cos(angle) * r, y, MathF.Sin(angle) * r);

            //The stem
            float stemRadius = radius * 0.09f, stemTop = height * 0.2f;
            for (int i = 0; i < 6; i++)
            {
                float a0 = i * MathHelper.TwoPi / 6f, a1 = (i + 1) * MathHelper.TwoPi / 6f;
                Vector3 b0 = Ring(stemRadius, -1f, a0), b1 = Ring(stemRadius, -1f, a1);
                Vector3 t0 = Ring(stemRadius * 0.7f, stemTop, a0), t1 = Ring(stemRadius * 0.7f, stemTop, a1);
                Vector3 n0 = Vector3.Normalize(new Vector3(b0.X, 0f, b0.Z)), n1 = Vector3.Normalize(new Vector3(b1.X, 0f, b1.Z));
                Vector3 outward = Vector3.Normalize(n0 + n1);
                Triangle(b0, b1, t1, outward, 0f, 0f, 0f, n0, n1, n1);
                Triangle(b0, t1, t0, outward, 0f, 0f, 0f, n0, n1, n0);

                //Shut at both ends (#974's gate): the foot is under the snow and the top inside the first tier, but a mesh
                //with an open ring is a mesh the seam audit cannot vouch for
                Triangle(b0, new Vector3(0f, -1f, 0f), b1, -Vector3.UnitY, 0f, 0f, 0f, -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY);
                Triangle(t0, t1, new Vector3(0f, stemTop, 0f), Vector3.UnitY, 0f, 0f, 0f, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY);
            }

            float topCollar = 0f;
            for (int tier = 0; tier < STAND_TIERS; tier++)
            {
                float t = tier / (float)STAND_TIERS;
                float skirtY = height * (0.12f + 0.62f * t);
                float collarY = skirtY + height * 0.3f;
                float skirtR = radius * (1f - 0.68f * t);
                float collarR = skirtR * 0.22f;
                float twist = tier * 0.7f;
                topCollar = collarY;

                for (int i = 0; i < STAND_SIDES; i++)
                {
                    float a0 = twist + i * MathHelper.TwoPi / STAND_SIDES, a1 = twist + (i + 1) * MathHelper.TwoPi / STAND_SIDES;
                    float r0 = skirtR * (i % 2 == 0 ? 1f : 0.74f), r1 = skirtR * ((i + 1) % 2 == 0 ? 1f : 0.74f);
                    Vector3 s0 = Ring(r0, skirtY - (r0 - skirtR * 0.74f) * 0.35f, a0), s1 = Ring(r1, skirtY - (r1 - skirtR * 0.74f) * 0.35f, a1);
                    Vector3 c0 = Ring(collarR, collarY, a0), c1 = Ring(collarR, collarY, a1);

                    //The upper face: a cone's normal, out and up
                    float slope = (skirtR - collarR) / (collarY - skirtY);
                    Vector3 n0 = Vector3.Normalize(new Vector3(MathF.Cos(a0), slope, MathF.Sin(a0)));
                    Vector3 n1 = Vector3.Normalize(new Vector3(MathF.Cos(a1), slope, MathF.Sin(a1)));
                    Vector3 outward = Vector3.Normalize(n0 + n1);
                    Triangle(s0, s1, c1, outward, 1f, 1f, 0.6f, n0, n1, n1);
                    Triangle(s0, c1, c0, outward, 1f, 0.6f, 0.6f, n0, n1, n0);

                    //The underside, back to the stem: dark, no snow
                    Vector3 hub = new(0f, skirtY + height * 0.03f, 0f);
                    float mid = (a0 + a1) * 0.5f;
                    Vector3 down = Vector3.Normalize(new Vector3(MathF.Cos(mid) * 0.3f, -1f, MathF.Sin(mid) * 0.3f));
                    Triangle(s0, hub, s1, down, 0f, 0f, 0f, down, down, down);

                    //The top tier's collar shut under the leader (the lower ones are buried in the tier above them)
                    if (tier == STAND_TIERS - 1)
                        Triangle(c0, c1, new Vector3(0f, collarY, 0f), Vector3.UnitY, 0.6f, 0.6f, 0.6f, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY);
                }
            }

            //The leader, out of the top collar
            Vector3 tip = new(0f, height, 0f);
            float leaderR = radius * 0.08f;
            for (int i = 0; i < 6; i++)
            {
                float a0 = i * MathHelper.TwoPi / 6f, a1 = (i + 1) * MathHelper.TwoPi / 6f;
                Vector3 b0 = Ring(leaderR, topCollar - height * 0.04f, a0), b1 = Ring(leaderR, topCollar - height * 0.04f, a1);
                Vector3 n0 = Vector3.Normalize(new Vector3(MathF.Cos(a0), 0.4f, MathF.Sin(a0)));
                Vector3 n1 = Vector3.Normalize(new Vector3(MathF.Cos(a1), 0.4f, MathF.Sin(a1)));
                Triangle(b0, b1, tip, Vector3.Normalize(n0 + n1), 0.5f, 0.5f, 0.3f, n0, n1, Vector3.Up);
                Triangle(b0, new Vector3(0f, topCollar - height * 0.04f, 0f), b1, -Vector3.UnitY, 0f, 0f, 0f, -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY);
            }

            VertexBuffer = new VertexBuffer(device, StandVertex.Declaration, vertices.Count, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices.ToArray());
            IndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices.ToArray());
            PrimitiveCount = indices.Count / 3;
            BoundingSphere = new BoundingSphere(new Vector3(0f, height * 0.5f, 0f), height * 0.5f + radius);
        }

        /// <summary>Frees the buffers.</summary>
        public void Dispose()
        {
            VertexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer?.Dispose();
            IndexBuffer = null;
        }
    }
}

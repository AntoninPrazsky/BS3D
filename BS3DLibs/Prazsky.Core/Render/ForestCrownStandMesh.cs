using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The daytime forest's far stand's broadleaf (#962): a rounded crown of a few swelling lobes over a short stem, about
    /// 130 triangles at the near detail and 60 at the far, for the hills past the planted wood, where a beech is a soft dome
    /// among the spruces' spires (<see cref="AuroraStandMesh"/>, which the stand's spruces are). Wound clockwise from outside
    /// like every procedural mesh, every piece shut (<c>Tools/WindingCheck</c> and its <c>--openness</c> gate judge it). Its
    /// vertices carry the vertex layout the spire's do, so one technique (<c>Forest.fx</c>'s <c>ForestStand</c>) draws both:
    /// position, normal, and how much of the crown's upper face the vertex is (the tips catching the sky).
    /// </summary>
    public sealed class ForestCrownStandMesh : IProceduralMesh, IDisposable
    {
        /// <inheritdoc/>
        public VertexBuffer VertexBuffer { get; private set; }

        /// <inheritdoc/>
        public IndexBuffer IndexBuffer { get; private set; }

        /// <inheritdoc/>
        public int PrimitiveCount { get; }

        /// <inheritdoc/>
        public BoundingSphere BoundingSphere { get; }

        /// <summary>A vertex of the stand: position, normal, and how much of an upper face it is (<see cref="AuroraStandMesh"/>'s layout).</summary>
        private readonly struct StandVertex : IVertexType
        {
            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly float Tip;

            public StandVertex(Vector3 position, Vector3 normal, float tip)
            {
                Position = position;
                Normal = normal;
                Tip = tip;
            }

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
                new VertexElement(24, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 0));

            VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }

        //The crown's centre and half-height as shares of the tree's height: a beech's crown from a quarter of the way up to
        //the top, a little wider than it is tall from far away
        private const float CROWN_CENTRE = 0.6f, CROWN_HALF = 0.38f;

        //The lobes the crown swells into, how far, and how tightly each gathers round its own direction
        private const int LOBES = 6;
        private const float LOBE_SWELL = 0.22f, LOBE_POWER = 3f;

        /// <param name="device">The device the buffers go on.</param>
        /// <param name="height">The tree's height, ground to the crown's top, at scale 1.</param>
        /// <param name="radius">The crown's radius at scale 1.</param>
        /// <param name="slices">Facets round the crown (ten near, seven far).</param>
        /// <param name="stacks">Rings from pole to pole (six near, four far).</param>
        /// <param name="seed">Rolls the lobes.</param>
        public ForestCrownStandMesh(GraphicsDevice device, float height, float radius, int slices = 10, int stacks = 6, int seed = 96202)
        {
            slices = Math.Max(slices, 5);
            stacks = Math.Max(stacks, 3);
            var vertices = new List<StandVertex>();
            var indices = new List<int>();

            //--- The crown: a sphere of `stacks` rings between two poles, squashed to the crown's half-height and swollen
            //into lobes, its normals the summed faces' so it shades as one soft mass
            var rng = new Random(seed);
            var lobes = new Vector3[LOBES];
            for (int k = 0; k < LOBES; k++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float y = -0.1f + 0.9f * (float)rng.NextDouble();
                float r = MathF.Sqrt(1f - y * y);
                lobes[k] = new Vector3(MathF.Cos(a) * r, y, MathF.Sin(a) * r);
            }

            Vector3 centre = new(0f, height * CROWN_CENTRE, 0f);
            float half = height * CROWN_HALF;
            var crownAt = new List<Vector3>();
            var crownUp = new List<float>();
            for (int i = 0; i <= stacks; i++)
            {
                float polar = MathHelper.Pi * i / stacks;
                for (int j = 0; j < (i == 0 || i == stacks ? 1 : slices); j++)
                {
                    float azimuth = MathHelper.TwoPi * j / slices;
                    Vector3 direction = new(MathF.Sin(polar) * MathF.Cos(azimuth), MathF.Cos(polar), MathF.Sin(polar) * MathF.Sin(azimuth));
                    float swell = 1f;
                    foreach (Vector3 lobe in lobes)
                        swell += LOBE_SWELL * MathF.Pow(MathF.Max(Vector3.Dot(direction, lobe), 0f), LOBE_POWER);

                    //The underside is flatter than the top: a crown is a dome over the shade it casts on itself
                    float down = direction.Y < 0f ? 0.7f : 1f;
                    crownAt.Add(centre + new Vector3(direction.X * radius, direction.Y * half * down, direction.Z * radius) * swell);
                    crownUp.Add(MathF.Max(direction.Y, 0f));
                }
            }

            int RingStart(int i) => i == 0 ? 0 : 1 + (i - 1) * slices;
            var crownTriangles = new List<(int, int, int)>();
            for (int j = 0; j < slices; j++)
                crownTriangles.Add((0, RingStart(1) + j, RingStart(1) + (j + 1) % slices));
            for (int i = 1; i < stacks - 1; i++)
            {
                int a = RingStart(i), b = RingStart(i + 1);
                for (int j = 0; j < slices; j++)
                {
                    int j1 = (j + 1) % slices;
                    crownTriangles.Add((a + j, b + j, a + j1));
                    crownTriangles.Add((a + j1, b + j, b + j1));
                }
            }
            int bottom = crownAt.Count - 1, lastRing = RingStart(stacks - 1);
            for (int j = 0; j < slices; j++)
                crownTriangles.Add((bottom, lastRing + (j + 1) % slices, lastRing + j));

            //The summed outward face normals, then each triangle wound clockwise from outside
            var normals = new Vector3[crownAt.Count];
            for (int t = 0; t < crownTriangles.Count; t++)
            {
                (int a, int b, int c) = crownTriangles[t];
                Vector3 n = Vector3.Cross(crownAt[b] - crownAt[a], crownAt[c] - crownAt[a]);
                Vector3 outward = (crownAt[a] + crownAt[b] + crownAt[c]) / 3f - centre;
                if (Vector3.Dot(n, outward) < 0f) n = -n;
                normals[a] += n;
                normals[b] += n;
                normals[c] += n;
            }
            int first = vertices.Count;
            for (int v = 0; v < crownAt.Count; v++)
                vertices.Add(new StandVertex(crownAt[v], Vector3.Normalize(normals[v]), crownUp[v]));
            foreach ((int a, int b, int c) in crownTriangles)
                AddWound(vertices, indices, first + a, first + b, first + c, centre);

            //--- The stem: from under the ground into the crown, six sides, shut at both ends
            float stemRadius = radius * 0.07f, stemTop = height * (CROWN_CENTRE - CROWN_HALF * 0.3f);
            Vector3 stemAxis = new(0f, (stemTop - 1f) * 0.5f, 0f);
            const int STEM_SIDES = 6;

            //The flank: a ring at the foot and one at the top, the side normals shared round it
            int flank = vertices.Count;
            for (int k = 0; k < STEM_SIDES; k++)
            {
                float a = MathHelper.TwoPi * k / STEM_SIDES;
                Vector3 side = new(MathF.Cos(a), 0f, MathF.Sin(a));
                vertices.Add(new StandVertex(side * stemRadius + new Vector3(0f, -1f, 0f), side, 0f));
                vertices.Add(new StandVertex(side * stemRadius * 0.7f + new Vector3(0f, stemTop, 0f), side, 0f));
            }
            for (int k = 0; k < STEM_SIDES; k++)
            {
                int b0 = flank + 2 * k, t0 = b0 + 1, b1 = flank + 2 * ((k + 1) % STEM_SIDES), t1 = b1 + 1;
                AddWound(vertices, indices, b0, b1, t1, stemAxis);
                AddWound(vertices, indices, b0, t1, t0, stemAxis);
            }

            //Both ends shut, each a fan of vertices of its own facing out of the end (#974's gate): the foot is under the
            //ground and the top inside the crown, but a mesh with an open ring is one the seam audit cannot vouch for
            foreach ((float y, float r, Vector3 n) in new[] { (-1f, stemRadius, -Vector3.UnitY), (stemTop, stemRadius * 0.7f, Vector3.UnitY) })
            {
                int hub = vertices.Count;
                vertices.Add(new StandVertex(new Vector3(0f, y, 0f), n, 0f));
                for (int k = 0; k < STEM_SIDES; k++)
                {
                    float a = MathHelper.TwoPi * k / STEM_SIDES;
                    vertices.Add(new StandVertex(new Vector3(MathF.Cos(a) * r, y, MathF.Sin(a) * r), n, 0f));
                }
                for (int k = 0; k < STEM_SIDES; k++)
                    AddWound(vertices, indices, hub, hub + 1 + k, hub + 1 + (k + 1) % STEM_SIDES, stemAxis);
            }

            VertexBuffer = new VertexBuffer(device, StandVertex.Declaration, vertices.Count, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices.ToArray());
            IndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices.ToArray());
            PrimitiveCount = indices.Count / 3;
            BoundingSphere = new BoundingSphere(new Vector3(0f, height * 0.5f, 0f), height * 0.5f + radius * (1f + LOBE_SWELL));
        }

        //One triangle wound clockwise seen from outside (CLAUDE.md's winding): its (b - a) x (c - a) points towards `inside`
        private static void AddWound(List<StandVertex> vertices, List<int> indices, int a, int b, int c, Vector3 inside)
        {
            Vector3 pa = vertices[a].Position, pb = vertices[b].Position, pc = vertices[c].Position;
            Vector3 outward = (pa + pb + pc) / 3f - inside;
            if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), outward) > 0f) (b, c) = (c, b);
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
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

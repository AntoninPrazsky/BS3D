using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A savanna acacia as real 3D geometry: a trunk that forks into several boughs (<see cref="Wood"/>)
    /// holding a wide, billowing, flat-topped canopy of foliage (<see cref="Canopy"/>). Two meshes, two
    /// materials (bark, leaves), drawn as one instance. The boughs are the point — a single tapering stick
    /// under a disc reads as a lollipop, where a fork spreading into the canopy reads as an acacia.
    /// </summary>
    public sealed class AcaciaMesh : IDisposable
    {
        public IProceduralMesh Wood { get; }
        public IProceduralMesh Canopy { get; }

        public AcaciaMesh(GraphicsDevice device, float trunkRadius, float treeHeight, float canopyRadius, int seed)
        {
            Random rng = new(seed);
            float forkY = treeHeight * (0.34f + 0.1f * (float)rng.NextDouble());
            float canopyBase = treeHeight * 0.86f;

            Wood = new WoodMesh(device, trunkRadius, treeHeight, canopyRadius, forkY, canopyBase, rng);
            Canopy = new FoliageMesh(device, canopyRadius, treeHeight * 0.19f,
                centreY: canopyBase + treeHeight * 0.12f, seed: seed * 31 + 7);
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Canopy as IDisposable)?.Dispose();
        }

        /// <summary>Trunk + boughs: a flared trunk to the fork, then several tapered boughs fanning up and out
        /// to the canopy's underside, each with a slight upward bend so the crown sits on spread arms.</summary>
        private sealed class WoodMesh : IProceduralMesh, IDisposable
        {
            public VertexBuffer VertexBuffer { get; private set; }
            public IndexBuffer IndexBuffer { get; private set; }
            public int PrimitiveCount { get; }
            public BoundingSphere BoundingSphere { get; }

            public WoodMesh(GraphicsDevice device, float trunkRadius, float treeHeight, float canopyRadius,
                float forkY, float canopyBase, Random rng)
            {
                var v = new List<VertexPositionNormalTexture>();
                var idx = new List<short>();
                const int SEG = 7;

                //The trunk, flared at the root and holding most of its girth to the fork.
                AddTube(v, idx, SEG, new Vector3(0f, 0f, 0f), trunkRadius * 1.5f,
                    new Vector3(0f, forkY, 0f), trunkRadius * 0.9f);

                //The boughs: 3..5, evenly spread with a jittered bearing, each rising in two bent segments to a
                //point out under the canopy. They taper hard, so the wood thins into the leaves it carries.
                int boughs = 3 + rng.Next(3);
                float baseAngle = (float)rng.NextDouble() * MathHelper.TwoPi;
                Vector3 fork = new(0f, forkY, 0f);
                for (int b = 0; b < boughs; b++)
                {
                    float a = baseAngle + MathHelper.TwoPi * b / boughs + (float)(rng.NextDouble() - 0.5) * 0.6f;
                    float spread = canopyRadius * (0.5f + 0.35f * (float)rng.NextDouble());
                    float tipY = canopyBase + treeHeight * (0.02f + 0.08f * (float)rng.NextDouble());
                    Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));
                    Vector3 mid = fork + dir * (spread * 0.5f) + Vector3.Up * ((tipY - forkY) * 0.55f);
                    Vector3 tip = fork + dir * spread + Vector3.Up * (tipY - forkY);
                    AddTube(v, idx, SEG, fork, trunkRadius * 0.6f, mid, trunkRadius * 0.42f);
                    AddTube(v, idx, SEG, mid, trunkRadius * 0.42f, tip, trunkRadius * 0.14f);
                }

                PrimitiveCount = idx.Count / 3;
                BoundingSphere = new BoundingSphere(new Vector3(0f, treeHeight * 0.5f, 0f), canopyRadius * 1.2f + treeHeight * 0.5f);

                VertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration, v.Count, BufferUsage.WriteOnly);
                VertexBuffer.SetData(v.ToArray());
                IndexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, idx.Count, BufferUsage.WriteOnly);
                IndexBuffer.SetData(idx.ToArray());
            }

            //A straight tapered cylinder a(ra) -> b(rb), side faces only (both ends are buried, in the ground or
            //the canopy). Normals are radial; wound clockwise seen from outside, MonoGame's front face.
            private static void AddTube(List<VertexPositionNormalTexture> v, List<short> idx, int seg,
                Vector3 a, float ra, Vector3 b, float rb)
            {
                Vector3 axis = b - a;
                float len = axis.Length();
                if (len < 1e-4f) return;
                axis /= len;

                Vector3 ref0 = MathF.Abs(axis.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
                Vector3 u = Vector3.Normalize(Vector3.Cross(ref0, axis));
                Vector3 w = Vector3.Cross(axis, u);

                short baseIdx = (short)v.Count;
                for (int s = 0; s <= seg; s++)
                {
                    float ang = MathHelper.TwoPi * s / seg;
                    Vector3 dir = u * MathF.Cos(ang) + w * MathF.Sin(ang);
                    v.Add(new VertexPositionNormalTexture(a + dir * ra, dir, new Vector2(s / (float)seg, 0f)));
                    v.Add(new VertexPositionNormalTexture(b + dir * rb, dir, new Vector2(s / (float)seg, 1f)));
                }
                for (int s = 0; s < seg; s++)
                {
                    int i0 = baseIdx + s * 2;
                    idx.Add((short)i0); idx.Add((short)(i0 + 1)); idx.Add((short)(i0 + 2));
                    idx.Add((short)(i0 + 2)); idx.Add((short)(i0 + 1)); idx.Add((short)(i0 + 3));
                }
            }

            public void Dispose()
            {
                VertexBuffer?.Dispose(); VertexBuffer = null;
                IndexBuffer?.Dispose(); IndexBuffer = null;
            }
        }
    }

    /// <summary>
    /// A billowing mass of foliage: a UV sphere pushed out into a cluster of overlapping lobes over a base
    /// unevenness, scaled wide and low so it reads as an acacia's flat-topped crown (or, small, a bush). The
    /// lobes are what make it a cluster of leaf masses rather than one smooth ball. Normals stay spherical so
    /// the light wraps the lobes softly the way lit foliage does. Rolled from a seed, so no two are alike.
    /// </summary>
    public sealed class FoliageMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        public FoliageMesh(GraphicsDevice device, float radius, float halfHeight, float centreY, int seed)
        {
            Random rng = new(seed);
            float phase = seed * 2.39996f;

            int lobeCount = 7 + rng.Next(4);
            var lobeDir = new Vector3[lobeCount];
            var lobeWeight = new float[lobeCount];
            var lobeSharp = new float[lobeCount];
            for (int k = 0; k < lobeCount; k++)
            {
                float ly = -0.1f + 1.0f * (float)rng.NextDouble();
                float la = (float)rng.NextDouble() * MathHelper.TwoPi;
                float lr = MathF.Sqrt(MathF.Max(0f, 1f - ly * ly));
                lobeDir[k] = new Vector3(MathF.Cos(la) * lr, ly, MathF.Sin(la) * lr);
                lobeWeight[k] = 0.14f + 0.28f * (float)rng.NextDouble();
                lobeSharp[k] = 2.2f + 3.5f * (float)rng.NextDouble();
            }

            float Bulge(Vector3 d)
            {
                float a = MathF.Atan2(d.Z, d.X) + phase;
                float n = 0.5f * LatheMesh.Irregularity(a, d.Y * 3f + phase)
                    + 0.3f * LatheMesh.Irregularity(a + 2.1f, d.Y * 3f + 1.7f + phase)
                    + 0.2f * LatheMesh.Irregularity(a + 4.3f, d.Y * 3f + 3.4f + phase);
                float swell = 0.18f * n;
                for (int k = 0; k < lobeCount; k++)
                    swell += lobeWeight[k] * MathF.Pow(MathF.Max(0f, Vector3.Dot(d, lobeDir[k])), lobeSharp[k]);
                return swell;
            }

            const int SLICES = 16, STACKS = 11;
            int vertexCount = (STACKS - 1) * SLICES + 2;
            var vertices = new VertexPositionNormalTexture[vertexCount];
            Vector3 centre = new(0f, centreY, 0f);
            float reach = 0f;

            VertexPositionNormalTexture Build(Vector3 dir)
            {
                float swell = 1f + Bulge(dir);
                float tuck = BottomTuck(dir.Y);
                float rXZ = radius * swell * (1f - tuck);
                //Flatten the very top a little so the crown reads flat-topped, not domed.
                float yScale = dir.Y > 0f ? halfHeight * (1f - 0.35f * dir.Y) : halfHeight;
                Vector3 pos = new(dir.X * rXZ, centreY + dir.Y * yScale * swell, dir.Z * rXZ);
                reach = MathF.Max(reach, (pos - centre).Length());
                return new VertexPositionNormalTexture(pos, dir, new Vector2(dir.X * 0.5f + 0.5f, dir.Z * 0.5f + 0.5f));
            }

            vertices[0] = Build(Vector3.Up);
            int index = 1;
            for (int stack = 1; stack < STACKS; stack++)
            {
                float phi = MathF.PI * stack / STACKS;
                float y = MathF.Cos(phi);
                float ringRadius = MathF.Sin(phi);
                for (int slice = 0; slice < SLICES; slice++)
                {
                    float theta = MathHelper.TwoPi * slice / SLICES;
                    vertices[index++] = Build(new Vector3(ringRadius * MathF.Cos(theta), y, ringRadius * MathF.Sin(theta)));
                }
            }
            int bottomPole = index;
            vertices[bottomPole] = Build(Vector3.Down);

            PrimitiveCount = SLICES * 2 + (STACKS - 2) * SLICES * 2;
            var indices = new short[PrimitiveCount * 3];
            int i = 0;
            for (int slice = 0; slice < SLICES; slice++)
            {
                indices[i++] = 0;
                indices[i++] = (short)(1 + slice);
                indices[i++] = (short)(1 + (slice + 1) % SLICES);
            }
            for (int stack = 0; stack < STACKS - 2; stack++)
            {
                int upper = 1 + stack * SLICES;
                int lower = upper + SLICES;
                for (int slice = 0; slice < SLICES; slice++)
                {
                    int next = (slice + 1) % SLICES;
                    indices[i++] = (short)(upper + slice);
                    indices[i++] = (short)(lower + slice);
                    indices[i++] = (short)(upper + next);
                    indices[i++] = (short)(upper + next);
                    indices[i++] = (short)(lower + slice);
                    indices[i++] = (short)(lower + next);
                }
            }
            int lastRing = 1 + (STACKS - 2) * SLICES;
            for (int slice = 0; slice < SLICES; slice++)
            {
                indices[i++] = (short)bottomPole;
                indices[i++] = (short)(lastRing + (slice + 1) % SLICES);
                indices[i++] = (short)(lastRing + slice);
            }

            VertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration, vertexCount, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);
            IndexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);
            BoundingSphere = new BoundingSphere(centre, reach);
        }

        //The crown narrows towards its underside so it settles over the branches instead of balancing on them.
        private static float BottomTuck(float y)
        {
            float t = MathHelper.Clamp((-y - 0.15f) / 0.85f, 0f, 1f);
            return 0.5f * t * t;
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose(); VertexBuffer = null;
            IndexBuffer?.Dispose(); IndexBuffer = null;
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Accumulates triangles for a procedural mesh and turns them into GPU buffers — the small builder the
    /// gun's carriage and wheels are made with. It carries <see cref="CannonMesh"/>'s one load-bearing trick
    /// as a service: every triangle is added with the <b>face normal it is meant to show</b>, and the winding
    /// is corrected against it, so a composite of boxes, tubes and tori cannot come out inside-out piece by
    /// piece (the trap CLAUDE.md's winding convention documents — nothing disappears, the far side is drawn,
    /// and only the shading says so).
    /// <para>
    /// Internal on purpose: it is a construction detail of the meshes in this namespace, not a modelling API.
    /// <see cref="CannonMesh"/> predates it and keeps its own copy of the correction; the shapes here serve
    /// the newer composite meshes.
    /// </para>
    /// </summary>
    internal sealed class MeshBuilder
    {
        private readonly List<VertexPositionNormalTexture> _vertices = new();
        private readonly List<short> _indices = new();

        /// <summary>
        /// Adds a triangle wound so <paramref name="faceNormal"/> is the front side under MonoGame's default
        /// culling — the same convention as <see cref="CannonMesh"/> and <see cref="BoxMesh"/>: the front face
        /// is the one whose <c>(b - a) × (c - a)</c> points <b>opposite</b> the outward normal (the viewport
        /// transform flips Y), so the vertices are swapped when the geometric winding comes out on the wrong
        /// side. Per-vertex normals are kept for shading regardless of the winding.
        /// </summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector3 faceNormal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), faceNormal) > 0f)
            {
                (b, c) = (c, b);
                (nb, nc) = (nc, nb);
            }

            short baseIndex = (short)_vertices.Count;
            _vertices.Add(new VertexPositionNormalTexture(a, Vector3.Normalize(na), Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(b, Vector3.Normalize(nb), Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(c, Vector3.Normalize(nc), Vector2.Zero));

            _indices.Add(baseIndex);
            _indices.Add((short)(baseIndex + 1));
            _indices.Add((short)(baseIndex + 2));
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Vector3 faceNormal)
        {
            AddTriangle(a, b, c, na, nb, nc, faceNormal);
            AddTriangle(a, c, d, na, nc, nd, faceNormal);
        }

        /// <summary>
        /// A box about <paramref name="center"/>, spanned by three half-extent vectors that carry both the
        /// orientation and the size — hand in an orthogonal basis scaled to the half-sizes and the box comes
        /// out oriented, which is how the carriage's angled trail beams are laid without a matrix. Flat-shaded:
        /// each face takes its own normal.
        /// </summary>
        public void AddBox(Vector3 center, Vector3 halfA, Vector3 halfB, Vector3 halfC)
        {
            AddBoxFace(center, halfA, halfB, halfC);
            AddBoxFace(center, -halfA, halfC, halfB); //mirrored winding rides on the face normal, not on order
            AddBoxFace(center, halfB, halfC, halfA);
            AddBoxFace(center, -halfB, halfA, halfC);
            AddBoxFace(center, halfC, halfA, halfB);
            AddBoxFace(center, -halfC, halfB, halfA);
        }

        private void AddBoxFace(Vector3 center, Vector3 outHalf, Vector3 uHalf, Vector3 vHalf)
        {
            Vector3 normal = Vector3.Normalize(outHalf);
            Vector3 faceCentre = center + outHalf;

            AddQuad(
                faceCentre - uHalf - vHalf,
                faceCentre + uHalf - vHalf,
                faceCentre + uHalf + vHalf,
                faceCentre - uHalf + vHalf,
                normal, normal, normal, normal, normal);
        }

        /// <summary>
        /// A cylinder along the local X axis about <paramref name="centre"/>, smooth-shaded around its wall,
        /// with flat end caps — the carriage's axle and the wheel's hub.
        /// </summary>
        public void AddTubeX(Vector3 centre, float halfLength, float radius, int segments)
        {
            Vector3 left = centre + new Vector3(-halfLength, 0f, 0f);
            Vector3 right = centre + new Vector3(halfLength, 0f, 0f);

            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * MathHelper.TwoPi;
                float a1 = (i + 1) / (float)segments * MathHelper.TwoPi;

                Vector3 d0 = new(0f, MathF.Cos(a0), MathF.Sin(a0));
                Vector3 d1 = new(0f, MathF.Cos(a1), MathF.Sin(a1));
                Vector3 mid = Vector3.Normalize(d0 + d1);

                AddQuad(
                    d0 * radius + left,
                    d1 * radius + left,
                    d1 * radius + right,
                    d0 * radius + right,
                    d0, d1, d1, d0, mid);

                //End caps, one triangle of the fan each: normals along -X and +X
                AddTriangle(left, d0 * radius + left, d1 * radius + left,
                    Vector3.Left, Vector3.Left, Vector3.Left, Vector3.Left);
                AddTriangle(right, d0 * radius + right, d1 * radius + right,
                    Vector3.Right, Vector3.Right, Vector3.Right, Vector3.Right);
            }
        }

        /// <summary>
        /// Uploads what was added. The 16-bit index buffer holds because every consumer here is a prop of a
        /// few hundred triangles; the builder throws rather than silently wrapping if one ever is not
        /// (the mountain grid's 16-bit wrap cost a long hunt — see docs/rendering.md).
        /// </summary>
        public (VertexBuffer Vertices, IndexBuffer Indices, int PrimitiveCount) Build(GraphicsDevice device)
        {
            if (_vertices.Count > short.MaxValue)
                throw new InvalidOperationException($"MeshBuilder overflowed 16-bit indices ({_vertices.Count} vertices).");

            var vertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration, _vertices.Count, BufferUsage.WriteOnly);
            vertexBuffer.SetData(_vertices.ToArray());

            var indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, _indices.Count, BufferUsage.WriteOnly);
            indexBuffer.SetData(_indices.ToArray());

            return (vertexBuffer, indexBuffer, _indices.Count / 3);
        }
    }
}

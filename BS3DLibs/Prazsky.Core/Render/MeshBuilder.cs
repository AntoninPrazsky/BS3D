using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Accumulates triangles for a procedural mesh and turns them into GPU buffers — the small builder the
    /// gun's barrel, carriage, wheels and the flock's birds are made with. It carries one load-bearing trick
    /// as a service: every triangle is added with the <b>face normal it is meant to show</b>, and the winding
    /// is corrected against it, so a composite of boxes, tubes and tori cannot come out inside-out piece by
    /// piece (the trap CLAUDE.md's winding convention documents — nothing disappears, the far side is drawn,
    /// and only the shading says so).
    /// <para>
    /// Internal on purpose: it is a construction detail of the meshes in this namespace, not a modelling API.
    /// The trick began as <see cref="CannonMesh"/>'s own; that mesh predates the builder and moved onto it
    /// when the breech dome reshaped it.
    /// </para>
    /// </summary>
    internal sealed class MeshBuilder
    {
        private readonly List<VertexPositionNormalTexture> _vertices = new();
        private readonly List<int> _indices = new();

        /// <summary>
        /// Adds a triangle wound so <paramref name="faceNormal"/> is the front side under MonoGame's default
        /// culling — the same convention as <see cref="CannonMesh"/> and <see cref="BoxMesh"/>: the front face
        /// is the one whose <c>(b - a) × (c - a)</c> points <b>opposite</b> the outward normal (the viewport
        /// transform flips Y), so the vertices are swapped when the geometric winding comes out on the wrong
        /// side. Per-vertex normals are kept for shading regardless of the winding.
        /// </summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector3 faceNormal)
            => AddTriangle(a, b, c, na, nb, nc, Vector2.Zero, Vector2.Zero, Vector2.Zero, faceNormal);

        /// <summary>
        /// The same, carrying a per-vertex texture coordinate. <see cref="BirdMesh"/> is what wants it: a bird's
        /// vertices ride a pair its vertex shader animates from — the signed spanwise station and the chordwise
        /// distance from the wing's mean line — so the flap cannot be read off position alone. The coordinates
        /// follow the winding swap, or a corrected triangle would hand its neighbours' values to the shader.
        /// </summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc,
            Vector2 ta, Vector2 tb, Vector2 tc, Vector3 faceNormal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), faceNormal) > 0f)
            {
                (b, c) = (c, b);
                (nb, nc) = (nc, nb);
                (tb, tc) = (tc, tb);
            }

            int baseIndex = _vertices.Count;
            _vertices.Add(new VertexPositionNormalTexture(a, Vector3.Normalize(na), ta));
            _vertices.Add(new VertexPositionNormalTexture(b, Vector3.Normalize(nb), tb));
            _vertices.Add(new VertexPositionNormalTexture(c, Vector3.Normalize(nc), tc));

            _indices.Add(baseIndex);
            _indices.Add(baseIndex + 1);
            _indices.Add(baseIndex + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Vector3 faceNormal)
        {
            AddTriangle(a, b, c, na, nb, nc, faceNormal);
            AddTriangle(a, c, d, na, nc, nd, faceNormal);
        }

        /// <summary>The same, carrying per-vertex texture coordinates (see the triangle overload).</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
            Vector2 ta, Vector2 tb, Vector2 tc, Vector2 td, Vector3 faceNormal)
        {
            AddTriangle(a, b, c, na, nb, nc, ta, tb, tc, faceNormal);
            AddTriangle(a, c, d, na, nc, nd, ta, tc, td, faceNormal);
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
        public void AddTubeX(Vector3 centre, float halfLength, float radius, int segments) =>
            AddTube(centre, Vector3.UnitX, Vector3.UnitY, halfLength, radius, segments);

        /// <summary>
        /// A cylinder along any <paramref name="axis"/> (unit length) about <paramref name="centre"/>, smooth-shaded
        /// around its wall, with flat end caps — <see cref="AddTubeX"/> is this along X, and the trail legs'
        /// hinge knuckles stand it upright (#403). <paramref name="reference"/> is any unit vector square to the
        /// axis, where the ring's angle starts; along X from Y it lays down exactly the vertices
        /// <see cref="AddTubeX"/> always did.
        /// </summary>
        public void AddTube(Vector3 centre, Vector3 axis, Vector3 reference, float halfLength, float radius, int segments)
        {
            Vector3 across = Vector3.Cross(axis, reference);
            Vector3 left = centre - axis * halfLength;
            Vector3 right = centre + axis * halfLength;

            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * MathHelper.TwoPi;
                float a1 = (i + 1) / (float)segments * MathHelper.TwoPi;

                Vector3 d0 = reference * MathF.Cos(a0) + across * MathF.Sin(a0);
                Vector3 d1 = reference * MathF.Cos(a1) + across * MathF.Sin(a1);
                Vector3 mid = Vector3.Normalize(d0 + d1);

                AddQuad(
                    d0 * radius + left,
                    d1 * radius + left,
                    d1 * radius + right,
                    d0 * radius + right,
                    d0, d1, d1, d0, mid);

                //End caps, one triangle of the fan each: normals along -axis and +axis
                AddTriangle(left, d0 * radius + left, d1 * radius + left, -axis, -axis, -axis, -axis);
                AddTriangle(right, d0 * radius + right, d1 * radius + right, axis, axis, axis, axis);
            }
        }

        /// <summary>
        /// A round bar swept along <paramref name="path"/> — the trail legs' lifting handles (#403). The path has
        /// to lie in one plane and <paramref name="binormal"/> is that plane's normal: each ring is laid square to
        /// the path's local direction from it, so a planar bend never twists. Smooth round the bar; the ends are
        /// open, because the only caller buries both in the part the bar is welded to.
        /// </summary>
        public void AddSweptTube(Vector3[] path, Vector3 binormal, float radius, int segments)
        {
            int last = path.Length - 1;

            for (int k = 0; k < last; k++)
            {
                Vector3 n0 = RingNormal(path, k, binormal);
                Vector3 n1 = RingNormal(path, k + 1, binormal);

                for (int i = 0; i < segments; i++)
                {
                    float a0 = i / (float)segments * MathHelper.TwoPi;
                    float a1 = (i + 1) / (float)segments * MathHelper.TwoPi;

                    Vector3 d00 = binormal * MathF.Cos(a0) + n0 * MathF.Sin(a0);
                    Vector3 d01 = binormal * MathF.Cos(a1) + n0 * MathF.Sin(a1);
                    Vector3 d10 = binormal * MathF.Cos(a0) + n1 * MathF.Sin(a0);
                    Vector3 d11 = binormal * MathF.Cos(a1) + n1 * MathF.Sin(a1);

                    AddQuad(path[k] + d00 * radius, path[k] + d01 * radius,
                        path[k + 1] + d11 * radius, path[k + 1] + d10 * radius,
                        d00, d01, d11, d10, d00 + d01 + d11 + d10);
                }
            }
        }

        //The ring's in-plane axis at one station of a swept path: square to the binormal and to the path's
        //direction there, taken across the neighbouring stations so a bend is shared by the rings either side
        private static Vector3 RingNormal(Vector3[] path, int k, Vector3 binormal)
        {
            Vector3 tangent = path[Math.Min(k + 1, path.Length - 1)] - path[Math.Max(k - 1, 0)];

            return Vector3.Normalize(Vector3.Cross(tangent, binormal));
        }

        /// <summary>
        /// A flat convex plate — the trail's spades and gussets (#403). <paramref name="outline"/> is its
        /// mid-surface, a convex polygon given in order round its edge, thickened to either side along
        /// <paramref name="normal"/> by each corner's own half-thickness, so an edge can be ground thinner than
        /// the rest. Every facet is shaded by its own geometric normal, which is what makes a ground edge read as
        /// a bevel rather than merely as a thinner plate.
        /// </summary>
        public void AddPlate(Vector3[] outline, float[] halfThickness, Vector3 normal)
        {
            int count = outline.Length;
            Vector3 centre = Vector3.Zero;
            float centreThickness = 0f;

            for (int i = 0; i < count; i++)
            {
                centre += outline[i];
                centreThickness += halfThickness[i];
            }

            centre /= count;
            centreThickness /= count;

            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;

                Vector3 frontI = outline[i] + normal * halfThickness[i];
                Vector3 frontJ = outline[j] + normal * halfThickness[j];
                Vector3 backI = outline[i] - normal * halfThickness[i];
                Vector3 backJ = outline[j] - normal * halfThickness[j];

                //The two broad faces as fans from the centre, so a ground edge tilts only the facets that reach it
                AddFacet(centre + normal * centreThickness, frontI, frontJ, normal);
                AddFacet(centre - normal * centreThickness, backI, backJ, -normal);

                //And the edge between them, facing away from the plate's centre
                Vector3 edge = Vector3.Cross(frontJ - frontI, backI - frontI);
                if (Vector3.Dot(edge, (outline[i] + outline[j]) * 0.5f - centre) < 0f) edge = -edge;
                edge = Vector3.Normalize(edge);

                AddQuad(frontI, frontJ, backJ, backI, edge, edge, edge, edge, edge);
            }
        }

        //One flat triangle, shaded by its own geometric normal turned to the side `outward` says is out
        private void AddFacet(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, outward) < 0f) normal = -normal;
            normal = Vector3.Normalize(normal);

            AddTriangle(a, b, c, normal, normal, normal, normal);
        }

        /// <summary>
        /// Uploads what was added, choosing the index width the way <see cref="LatheMesh"/> does: sixteen bits
        /// while every vertex is addressable by them, thirty-two past that. A 16-bit index silently wraps and
        /// sends far triangles to the wrong corners of the mesh (the mountain grid's long hunt — see
        /// docs/rendering.md), so the width is chosen rather than assumed.
        /// <para>
        /// The ceiling used to be <c>short.MaxValue</c> and a throw. The indices were stored SIGNED, which
        /// halved a sixteen-bit buffer that the GPU reads unsigned, and the builder charges six vertices a quad,
        /// so the trophy cup sat at 30 000 of those 32 767 and every profile ring it could have had was spent
        /// on that bookkeeping (#429). Every executable runs the HiDef profile, which takes 32-bit indices.
        /// </para>
        /// </summary>
        public (VertexBuffer Vertices, IndexBuffer Indices, int PrimitiveCount) Build(GraphicsDevice device)
        {
            var vertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration, _vertices.Count, BufferUsage.WriteOnly);
            vertexBuffer.SetData(_vertices.ToArray());

            IndexBuffer indexBuffer;

            if (_vertices.Count <= ushort.MaxValue)
            {
                var narrow = new short[_indices.Count];
                for (int n = 0; n < narrow.Length; n++) narrow[n] = unchecked((short)_indices[n]);

                indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, narrow.Length, BufferUsage.WriteOnly);
                indexBuffer.SetData(narrow);
            }
            else
            {
                indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, _indices.Count, BufferUsage.WriteOnly);
                indexBuffer.SetData(_indices.ToArray());
            }

            return (vertexBuffer, indexBuffer, _indices.Count / 3);
        }
    }
}

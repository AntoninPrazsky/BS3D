using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A procedurally generated cannon barrel: a thick-walled tube along the Z axis with a long window cut
    /// in the top, so the balls loaded in a row inside show through it. The bore is open at both ends
    /// (annular rims), the cut edges of the window are closed by rim faces so the wall does not read as
    /// paper-thin, and the walls are smooth-shaded (per-vertex radial normals).
    /// <para>
    /// It is the last modeled asset made procedural. The barrel is built along +Z from the muzzle; the
    /// caller orients it with <see cref="Matrix.CreateWorld(Vector3, Vector3, Vector3)"/> so the muzzle
    /// (local -Z) points down the aim direction and the window (local +Y) faces up, and draws the queued
    /// balls in a line along the same axis. Wound clockwise seen from outside, like <see cref="BoxMesh"/>,
    /// so the outward face is the front one under MonoGame's default back-face culling.
    /// </para>
    /// </summary>
    public class CannonMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        private readonly List<VertexPositionNormalTexture> _vertices = new();
        private readonly List<short> _indices = new();

        /// <param name="boreRadius">Inner radius of the tube; a little over the ball radius so a ball nests inside.</param>
        /// <param name="wallThickness">Radial thickness of the wall (outer radius = bore + this).</param>
        /// <param name="frontZ">Z of the muzzle rim, and <paramref name="backZ"/> of the breech rim.</param>
        /// <param name="slotHalfAngle">Half-width of the top window, in radians, measured from straight up.</param>
        /// <param name="segments">Angular segments across the solid wall arc.</param>
        public CannonMesh(GraphicsDevice graphicsDevice, float boreRadius, float wallThickness, float frontZ, float backZ,
            float slotHalfAngle, int segments)
        {
            float inner = boreRadius;
            float outer = boreRadius + wallThickness;

            //The window is centred on straight up (+Y). The solid wall covers everything else: from just past
            //the window's near edge, around the bottom, to just before its far edge.
            float top = Constants.HALF_PI;
            float start = top + slotHalfAngle;
            float sweep = MathHelper.TwoPi - 2f * slotHalfAngle;
            float step = sweep / segments;

            for (int i = 0; i < segments; i++)
            {
                float a0 = start + i * step;
                float a1 = start + (i + 1) * step;

                Vector3 dir0 = new(MathF.Cos(a0), MathF.Sin(a0), 0f);
                Vector3 dir1 = new(MathF.Cos(a1), MathF.Sin(a1), 0f);
                Vector3 midOut = Vector3.Normalize(dir0 + dir1);

                //Outer wall: visible from outside, normals point outward (radial+)
                AddQuad(
                    dir0 * outer + new Vector3(0, 0, frontZ),
                    dir1 * outer + new Vector3(0, 0, frontZ),
                    dir1 * outer + new Vector3(0, 0, backZ),
                    dir0 * outer + new Vector3(0, 0, backZ),
                    dir0, dir1, dir1, dir0, midOut);

                //Inner wall: visible from the bore, normals point inward (radial-)
                AddQuad(
                    dir0 * inner + new Vector3(0, 0, frontZ),
                    dir1 * inner + new Vector3(0, 0, frontZ),
                    dir1 * inner + new Vector3(0, 0, backZ),
                    dir0 * inner + new Vector3(0, 0, backZ),
                    -dir0, -dir1, -dir1, -dir0, -midOut);

                //Muzzle rim (front, faces -Z) and breech rim (back, faces +Z): the annular ends of the wall
                AddQuad(
                    dir0 * inner + new Vector3(0, 0, frontZ),
                    dir1 * inner + new Vector3(0, 0, frontZ),
                    dir1 * outer + new Vector3(0, 0, frontZ),
                    dir0 * outer + new Vector3(0, 0, frontZ),
                    Vector3.Forward, Vector3.Forward, Vector3.Forward, Vector3.Forward, Vector3.Forward);

                AddQuad(
                    dir0 * inner + new Vector3(0, 0, backZ),
                    dir1 * inner + new Vector3(0, 0, backZ),
                    dir1 * outer + new Vector3(0, 0, backZ),
                    dir0 * outer + new Vector3(0, 0, backZ),
                    Vector3.Backward, Vector3.Backward, Vector3.Backward, Vector3.Backward, Vector3.Backward);
            }

            //The two cut edges of the window, closing the wall's thickness so it does not read as paper-thin.
            //Each faces into the slot (roughly up), so the eye sees a solid rim along the opening.
            AddRim(start, inner, outer, frontZ, backZ, tangentTowardSlot: -1f);
            AddRim(start + sweep, inner, outer, frontZ, backZ, tangentTowardSlot: +1f);

            PrimitiveCount = _indices.Count / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, _vertices.Count, BufferUsage.WriteOnly);
            VertexBuffer.SetData(_vertices.ToArray());

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, _indices.Count, BufferUsage.WriteOnly);
            IndexBuffer.SetData(_indices.ToArray());

            float halfLength = (backZ - frontZ) * Constants.HALF;
            BoundingSphere = new BoundingSphere(new Vector3(0, 0, (frontZ + backZ) * Constants.HALF),
                MathF.Sqrt(outer * outer + halfLength * halfLength));
        }

        /// <summary>One cut edge of the window: a wall-thick quad at a fixed angle, facing into the slot.</summary>
        private void AddRim(float angle, float inner, float outer, float frontZ, float backZ, float tangentTowardSlot)
        {
            Vector3 radial = new(MathF.Cos(angle), MathF.Sin(angle), 0f);
            Vector3 tangent = new(-MathF.Sin(angle), MathF.Cos(angle), 0f);
            Vector3 faceNormal = tangent * tangentTowardSlot;

            AddQuad(
                radial * inner + new Vector3(0, 0, frontZ),
                radial * outer + new Vector3(0, 0, frontZ),
                radial * outer + new Vector3(0, 0, backZ),
                radial * inner + new Vector3(0, 0, backZ),
                faceNormal, faceNormal, faceNormal, faceNormal, faceNormal);
        }

        private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Vector3 faceNormal)
        {
            AddTriangle(a, b, c, na, nb, nc, faceNormal);
            AddTriangle(a, c, d, na, nc, nd, faceNormal);
        }

        /// <summary>
        /// Adds a triangle, winding it so <paramref name="faceNormal"/> is the front side under MonoGame's
        /// default culling. Same convention as <see cref="BoxMesh"/>: the front face is the one whose
        /// <c>(b - a) × (c - a)</c> points <b>opposite</b> the outward normal (the viewport flips Y), so the
        /// vertices are swapped when the geometric winding comes out on the wrong side. Per-vertex normals
        /// are kept for shading regardless of the winding.
        /// </summary>
        private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector3 faceNormal)
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

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer?.Dispose();
            IndexBuffer = null;
        }
    }
}

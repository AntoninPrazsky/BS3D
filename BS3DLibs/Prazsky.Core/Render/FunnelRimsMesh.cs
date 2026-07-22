using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The two metal rims of the drain funnel: a torus (round bead) around the wide top circle and another
    /// around the small bottom hole, in one mesh so a single (gold-metal) renderer draws both. Built in the
    /// funnel's own local space — the top rim on the plane y = 0, the bottom rim on y = -<paramref name="height"/> —
    /// so it shares the funnel's world matrix. Each torus wraps both ways (around the ring and around the tube),
    /// so it is a closed convex-tube surface; meant to be drawn opaque, before the translucent funnel glass.
    /// </summary>
    public class FunnelRimsMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <param name="topRadius">Major radius of the top rim (the funnel mouth).</param>
        /// <param name="holeRadius">Major radius of the bottom rim (the drain hole).</param>
        /// <param name="height">Vertical drop between the two rims (top at y = 0, hole at y = -height).</param>
        /// <param name="topTube">Tube (bead) radius of the top rim.</param>
        /// <param name="holeTube">Tube (bead) radius of the bottom rim.</param>
        /// <param name="majorSegments">Facets around each ring (more = rounder circle).</param>
        /// <param name="tubeSegments">Facets around each tube cross-section (more = rounder bead).</param>
        public FunnelRimsMesh(GraphicsDevice graphicsDevice, float topRadius, float holeRadius, float height,
            float topTube, float holeTube, int majorSegments, int tubeSegments)
        {
            int vertsPerTorus = majorSegments * tubeSegments;
            int quadsPerTorus = majorSegments * tubeSegments; //full wrap both ways: one quad per (ring, tube) cell

            var vertices = new VertexPositionNormalTexture[vertsPerTorus * 2];
            var indices = new short[quadsPerTorus * 6 * 2];
            int v = 0, i = 0;

            AddTorus(0f, topRadius, topTube);
            AddTorus(-height, holeRadius, holeTube);

            void AddTorus(float centerY, float major, float tube)
            {
                int baseV = v;

                for (int s = 0; s < majorSegments; s++)
                {
                    float u = (float)(s / (double)majorSegments * Math.PI * 2.0);
                    float cosU = (float)Math.Cos(u), sinU = (float)Math.Sin(u);

                    for (int t = 0; t < tubeSegments; t++)
                    {
                        float w = (float)(t / (double)tubeSegments * Math.PI * 2.0);
                        float cosW = (float)Math.Cos(w), sinW = (float)Math.Sin(w);

                        //Point on the tube: the ring centre (major * radial) pushed out along the radial by
                        //tube*cosW and up by tube*sinW; the normal is that same offset direction, already unit.
                        Vector3 normal = new(cosW * cosU, sinW, cosW * sinU);
                        Vector3 pos = new((major + tube * cosW) * cosU, centerY + tube * sinW, (major + tube * cosW) * sinU);
                        vertices[v++] = new VertexPositionNormalTexture(pos, normal, new Vector2(s / (float)majorSegments, t / (float)tubeSegments));
                    }
                }

                for (int s = 0; s < majorSegments; s++)
                {
                    int sNext = (s + 1) % majorSegments;
                    for (int t = 0; t < tubeSegments; t++)
                    {
                        int tNext = (t + 1) % tubeSegments;
                        int i00 = baseV + s * tubeSegments + t;
                        int i10 = baseV + sNext * tubeSegments + t;
                        int i11 = baseV + sNext * tubeSegments + tNext;
                        int i01 = baseV + s * tubeSegments + tNext;
                        indices[i++] = (short)i00; indices[i++] = (short)i10; indices[i++] = (short)i11;
                        indices[i++] = (short)i00; indices[i++] = (short)i11; indices[i++] = (short)i01;
                    }
                }
            }

            PrimitiveCount = indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);

            float outer = topRadius + topTube;
            BoundingSphere = new BoundingSphere(new Vector3(0f, -height * Constants.HALF, 0f),
                new Vector2(outer, height * Constants.HALF).Length());
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

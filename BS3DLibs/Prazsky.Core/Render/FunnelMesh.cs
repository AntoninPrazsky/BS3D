using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Procedurally generated drain funnel: a truncated cone with a wide top rim and a small hole at the
    /// bottom (its wall faces inward and up, so shot balls dropped into it read as resting on the inside and
    /// rolling down to the hole), plus a flat top <b>collar</b> that fills the ring from the round rim out to
    /// a square boundary. The collar is what closes the corner gaps between the round funnel and the square
    /// opening cut in the arena floor. Origin is the centre of the top rim (y = 0); the wall descends to
    /// y = -<paramref name="height"/>. Meant to be drawn translucent (glass) with back-face culling off.
    /// </summary>
    public class FunnelMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <param name="topRadius">Radius of the round top rim (the mouth balls fall into).</param>
        /// <param name="holeRadius">Radius of the hole at the bottom.</param>
        /// <param name="height">Vertical drop from the top rim down to the hole.</param>
        /// <param name="segments">Number of facets around the cone (more = smoother).</param>
        /// <param name="squareHalf">
        /// Half-width of the square opening the funnel sits in. The flat collar fills from the rim out to this
        /// square (thin at the edge midpoints, widest at the corners). Pass 0 (or &lt;= topRadius) for no collar.
        /// </param>
        public FunnelMesh(GraphicsDevice graphicsDevice, float topRadius, float holeRadius, float height, int segments, float squareHalf = 0f)
        {
            bool collar = squareHalf > topRadius;
            int quads = collar ? segments * 2 : segments;

            var vertices = new VertexPositionNormalTexture[quads * 4];
            var indices = new short[quads * 6];
            int v = 0, i = 0;

            //The cone wall normal, in the radial/y plane, points to the concave (inner, upper) side the balls
            //rest on: inward by the height and up by how far the rim overhangs the hole.
            float radialN = -height;
            float upN = topRadius - holeRadius;

            for (int s = 0; s < segments; s++)
            {
                float a0 = (float)(s / (double)segments * Math.PI * 2.0);
                float a1 = (float)((s + 1) / (double)segments * Math.PI * 2.0);
                float u0 = s / (float)segments;
                float u1 = (s + 1) / (float)segments;

                //Cone wall: rim (radius topRadius, y 0) down to the hole (radius holeRadius, y -height)
                Vector3 coneNormal0 = WallNormal(a0), coneNormal1 = WallNormal(a1);
                AddVert(Ring(a0, topRadius, 0f), coneNormal0, new Vector2(u0, 0f));
                AddVert(Ring(a1, topRadius, 0f), coneNormal1, new Vector2(u1, 0f));
                AddVert(Ring(a1, holeRadius, -height), coneNormal1, new Vector2(u1, 1f));
                AddVert(Ring(a0, holeRadius, -height), coneNormal0, new Vector2(u0, 1f));
                Quad();

                if (collar)
                {
                    //Flat collar: from the round rim out to the square boundary, at the rim's height. Normal up.
                    AddVert(Ring(a0, topRadius, 0f), Vector3.Up, new Vector2(u0, 0f));
                    AddVert(Ring(a1, topRadius, 0f), Vector3.Up, new Vector2(u1, 0f));
                    AddVert(SquareEdge(a1, squareHalf), Vector3.Up, new Vector2(u1, 0f));
                    AddVert(SquareEdge(a0, squareHalf), Vector3.Up, new Vector2(u0, 0f));
                    Quad();
                }
            }

            void AddVert(Vector3 pos, Vector3 normal, Vector2 uv) => vertices[v++] = new VertexPositionNormalTexture(pos, normal, uv);

            void Quad()
            {
                int b = v - 4;
                indices[i++] = (short)b; indices[i++] = (short)(b + 1); indices[i++] = (short)(b + 2);
                indices[i++] = (short)b; indices[i++] = (short)(b + 2); indices[i++] = (short)(b + 3);
            }

            static Vector3 Ring(float angle, float radius, float y) =>
                new(radius * (float)Math.Cos(angle), y, radius * (float)Math.Sin(angle));

            //The point on the square of half-width h in the direction of the angle (radius grows to the corners)
            static Vector3 SquareEdge(float angle, float h)
            {
                float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
                float r = h / Math.Max(Math.Abs(cos), Math.Abs(sin));
                return new Vector3(r * cos, 0f, r * sin);
            }

            Vector3 WallNormal(float angle) =>
                Vector3.Normalize(new Vector3(radialN * (float)Math.Cos(angle), upN, radialN * (float)Math.Sin(angle)));

            PrimitiveCount = indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);

            float outer = Math.Max(topRadius, collar ? squareHalf * 1.4143f : topRadius);
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

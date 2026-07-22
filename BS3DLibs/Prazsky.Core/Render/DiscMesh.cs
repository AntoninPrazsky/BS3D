using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Procedurally generated round platform: a solid stone annulus (a washer) with a flat top and bottom, an
    /// outer vertical wall - the hard edge the scene falls away past - and an inner wall around the central
    /// hole the drain funnel sits in. Origin is the centre of the top face (y = 0); it descends to
    /// y = -<paramref name="height"/>. Meant to be drawn opaque with <c>CullNone</c> (the nearest face wins on
    /// depth, so the result matches back-face culling and the winding is moot, exactly like the funnel rims),
    /// so no winding convention is imposed here. Normals are per-vertex (up, down, radial) for smooth shading.
    /// </summary>
    public class DiscMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <param name="innerRadius">Radius of the central hole, where the funnel's rim meets the stone.</param>
        /// <param name="outerRadius">Radius of the platform's outer edge.</param>
        /// <param name="height">Vertical drop from the top face down to the underside (the edge's height).</param>
        /// <param name="segments">Number of facets around the ring (more = smoother).</param>
        public DiscMesh(GraphicsDevice graphicsDevice, float innerRadius, float outerRadius, float height, int segments)
        {
            int quads = segments * 4; //top ring, bottom ring, outer wall, inner wall
            var vertices = new VertexPositionNormalTexture[quads * 4];
            var indices = new short[quads * 6];
            int v = 0, i = 0;

            Vector3 down = new(0f, height, 0f);

            for (int s = 0; s < segments; s++)
            {
                float a0 = (float)(s / (double)segments * Math.PI * 2.0);
                float a1 = (float)((s + 1) / (double)segments * Math.PI * 2.0);
                Vector3 d0 = new((float)Math.Cos(a0), 0f, (float)Math.Sin(a0));
                Vector3 d1 = new((float)Math.Cos(a1), 0f, (float)Math.Sin(a1));

                Vector3 ti0 = d0 * innerRadius, ti1 = d1 * innerRadius;   //top inner
                Vector3 to0 = d0 * outerRadius, to1 = d1 * outerRadius;   //top outer
                Vector3 bi0 = ti0 - down, bi1 = ti1 - down;              //bottom inner
                Vector3 bo0 = to0 - down, bo1 = to1 - down;              //bottom outer

                Quad(ti0, to0, to1, ti1, Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up);       //top face
                Quad(bi0, bi1, bo1, bo0, Vector3.Down, Vector3.Down, Vector3.Down, Vector3.Down); //bottom face
                Quad(to0, bo0, bo1, to1, d0, d0, d1, d1);                                        //outer wall (radial out)
                Quad(ti0, ti1, bi1, bi0, -d0, -d1, -d1, -d0);                                    //inner wall (radial in)
            }

            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n0, Vector3 n1, Vector3 n2, Vector3 n3)
            {
                int b = v;
                vertices[v++] = new VertexPositionNormalTexture(p0, n0, new Vector2(0f, 0f));
                vertices[v++] = new VertexPositionNormalTexture(p1, n1, new Vector2(1f, 0f));
                vertices[v++] = new VertexPositionNormalTexture(p2, n2, new Vector2(1f, 1f));
                vertices[v++] = new VertexPositionNormalTexture(p3, n3, new Vector2(0f, 1f));
                indices[i++] = (short)b; indices[i++] = (short)(b + 1); indices[i++] = (short)(b + 2);
                indices[i++] = (short)b; indices[i++] = (short)(b + 2); indices[i++] = (short)(b + 3);
            }

            PrimitiveCount = indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);

            BoundingSphere = new BoundingSphere(new Vector3(0f, -height * Constants.HALF, 0f),
                new Vector2(outerRadius, height * Constants.HALF).Length());
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

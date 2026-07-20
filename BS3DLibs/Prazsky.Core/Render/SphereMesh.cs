using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Procedurally generated UV sphere (positions, normals and texture coordinates), replacing
    /// a modeled sphere asset. Building several of these at different resolutions gives LOD levels:
    /// per-pixel lighting shades even a coarse sphere smoothly, only the silhouette reveals the polygons.
    /// </summary>
    public class SphereMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public int VertexCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <param name="radius">Sphere radius.</param>
        /// <param name="slices">Segments around the vertical axis (minimum 3).</param>
        /// <param name="stacks">Segments from pole to pole (minimum 2).</param>
        public SphereMesh(GraphicsDevice graphicsDevice, float radius, int slices, int stacks)
        {
            if (slices < 3) throw new ArgumentOutOfRangeException(nameof(slices));
            if (stacks < 2) throw new ArgumentOutOfRangeException(nameof(stacks));

            VertexCount = (stacks - 1) * slices + 2;
            var vertices = new VertexPositionNormalTexture[VertexCount];

            vertices[0] = new(new Vector3(0f, radius, 0f), Vector3.Up, new Vector2(0.5f, 0f));

            int index = 1;
            for (int stack = 1; stack < stacks; stack++)
            {
                float phi = MathF.PI * stack / stacks;
                float y = MathF.Cos(phi);
                float ringRadius = MathF.Sin(phi);

                for (int slice = 0; slice < slices; slice++)
                {
                    float theta = MathHelper.TwoPi * slice / slices;
                    Vector3 normal = new(ringRadius * MathF.Cos(theta), y, ringRadius * MathF.Sin(theta));

                    vertices[index++] = new(normal * radius, normal, new Vector2((float)slice / slices, (float)stack / stacks));
                }
            }

            int bottomPole = index;
            vertices[bottomPole] = new(new Vector3(0f, -radius, 0f), Vector3.Down, new Vector2(0.5f, 1f));

            PrimitiveCount = slices * 2 + (stacks - 2) * slices * 2;
            var indices = new short[PrimitiveCount * 3];
            int i = 0;

            //Top cap
            for (int slice = 0; slice < slices; slice++)
            {
                indices[i++] = 0;
                indices[i++] = (short)(1 + (slice + 1) % slices);
                indices[i++] = (short)(1 + slice);
            }

            //Bands between rings
            for (int stack = 0; stack < stacks - 2; stack++)
            {
                int upperRing = 1 + stack * slices;
                int lowerRing = upperRing + slices;

                for (int slice = 0; slice < slices; slice++)
                {
                    int next = (slice + 1) % slices;

                    indices[i++] = (short)(upperRing + slice);
                    indices[i++] = (short)(upperRing + next);
                    indices[i++] = (short)(lowerRing + slice);

                    indices[i++] = (short)(upperRing + next);
                    indices[i++] = (short)(lowerRing + next);
                    indices[i++] = (short)(lowerRing + slice);
                }
            }

            //Bottom cap
            int lastRing = 1 + (stacks - 2) * slices;
            for (int slice = 0; slice < slices; slice++)
            {
                indices[i++] = (short)bottomPole;
                indices[i++] = (short)(lastRing + slice);
                indices[i++] = (short)(lastRing + (slice + 1) % slices);
            }

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, VertexCount, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);

            BoundingSphere = new BoundingSphere(Vector3.Zero, radius);
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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The strip a ball's rim is drawn on (#804): the anti-aliasing of a sphere's outline on the Raspberry Pi's Potato
    /// path, which has no multisampled target. It is not a shape - a vertex is a point of the unit circle and a row
    /// (<c>x = cos t, y = sin t, z = 0</c> inside the outline or <c>1</c> outside it), and
    /// <c>PotatoModel.fx</c>'s <c>PotatoBallRimVS</c> puts it on the circle where the eye's rays touch the ball, one
    /// instance a ball, from the ball draw's own instance stream. See that shader for what the ring is and why its
    /// depth needs no bias.
    /// <para>
    /// The segment count decides only how much of the strip is wasted: the ring's alpha is measured per pixel from
    /// the circle itself, so its edge is round whatever the count, and the outer row is pushed out by
    /// <see cref="Secant"/> so that the chord between two vertices still covers the ramp.
    /// </para>
    /// <para>
    /// Not an <see cref="IProceduralMesh"/>: it has no surface, no normals and no facing, and is drawn with no
    /// culling, so the winding rule the procedural meshes are held to does not apply to it.
    /// </para>
    /// </summary>
    public sealed class BallRimMesh : IDisposable
    {
        public VertexBuffer VertexBuffer { get; }
        public IndexBuffer IndexBuffer { get; }
        public int PrimitiveCount { get; }

        /// <summary><c>1 / cos(pi / segments)</c>: how far out of a circle a polygon's corners must stand for its
        /// edges to clear it.</summary>
        public float Secant { get; }

        private static readonly VertexDeclaration DECLARATION = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0));

        /// <param name="segments">Segments round the ring, 8 at least.</param>
        public BallRimMesh(GraphicsDevice graphicsDevice, int segments)
        {
            if (segments < 8) throw new ArgumentOutOfRangeException(nameof(segments));

            Secant = 1f / MathF.Cos(MathF.PI / segments);
            PrimitiveCount = segments * 2;

            //Two vertices a step, the inner then the outer, and no seam: the last quad closes on the first pair
            Vector3[] vertices = new Vector3[segments * 2];
            for (int i = 0; i < segments; i++)
            {
                float angle = MathHelper.TwoPi * i / segments;
                float x = MathF.Cos(angle), y = MathF.Sin(angle);

                vertices[i * 2] = new Vector3(x, y, 0f);
                vertices[i * 2 + 1] = new Vector3(x, y, 1f);
            }

            short[] indices = new short[PrimitiveCount * 3];
            int at = 0;
            for (int i = 0; i < segments; i++)
            {
                int inner = i * 2, outer = inner + 1;
                int nextInner = (i + 1) % segments * 2, nextOuter = nextInner + 1;

                indices[at++] = (short)inner;
                indices[at++] = (short)outer;
                indices[at++] = (short)nextOuter;

                indices[at++] = (short)inner;
                indices[at++] = (short)nextOuter;
                indices[at++] = (short)nextInner;
            }

            VertexBuffer = new VertexBuffer(graphicsDevice, DECLARATION, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);
        }

        public void Dispose()
        {
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
        }
    }
}

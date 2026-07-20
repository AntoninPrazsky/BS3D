using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Procedurally generated outline of a box centerd at the origin: the twelve edges rendered as thin
    /// beams, with per-face normals. Unlike <see cref="BoxMesh"/> it leaves the faces open, so it can mark
    /// out a volume (e.g. the play field of a map) without hiding what is inside it.
    /// </summary>
    public class WireBoxMesh : IProceduralMesh, IDisposable
    {
        private const int BEAM_COUNT = 12;
        private const int VERTICES_PER_BEAM = 24;
        private const int INDICES_PER_BEAM = 36;

        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        private readonly VertexPositionNormalTexture[] _vertices = new VertexPositionNormalTexture[BEAM_COUNT * VERTICES_PER_BEAM];
        private readonly short[] _indices = new short[BEAM_COUNT * INDICES_PER_BEAM];
        private int _vertexCount;
        private int _indexCount;

        /// <param name="sizeX">Full box size along the X axis.</param>
        /// <param name="sizeY">Full box size along the Y axis.</param>
        /// <param name="sizeZ">Full box size along the Z axis.</param>
        /// <param name="thickness">Edge of the square cross section of each beam. Beams grow inwards from the box surface.</param>
        public WireBoxMesh(GraphicsDevice graphicsDevice, float sizeX, float sizeY, float sizeZ, float thickness)
        {
            if (thickness <= 0f) throw new ArgumentOutOfRangeException(nameof(thickness));

            Vector3 half = new(sizeX * Constants.HALF, sizeY * Constants.HALF, sizeZ * Constants.HALF);

            //A beam may not eat up more than half of the box in any direction, otherwise opposite beams would
            //grow through each other and the outline would collapse into a solid block
            float t = Math.Min(thickness, Math.Min(half.X, Math.Min(half.Y, half.Z)));

            float[] xs = { -half.X, half.X };
            float[] ys = { -half.Y, half.Y };
            float[] zs = { -half.Z, half.Z };

            //The four uprights run the full height, the beams along X and Z are shortened at both ends
            //so that they butt against the uprights instead of overlapping them
            foreach (float x in xs)
            {
                foreach (float z in zs) AddBeam(new Vector3(x, -half.Y, z), new Vector3(x, half.Y, z), t, 0f);
                foreach (float y in ys) AddBeam(new Vector3(x, y, -half.Z), new Vector3(x, y, half.Z), t, t);
            }

            foreach (float y in ys)
            {
                foreach (float z in zs) AddBeam(new Vector3(-half.X, y, z), new Vector3(half.X, y, z), t, t);
            }

            PrimitiveCount = _indexCount / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, _vertexCount, BufferUsage.WriteOnly);
            VertexBuffer.SetData(_vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, _indexCount, BufferUsage.WriteOnly);
            IndexBuffer.SetData(_indices);

            BoundingSphere = new BoundingSphere(Vector3.Zero, half.Length());
        }

        /// <summary>
        /// Adds one beam running along an edge of the box, given by the two endpoints of that edge. The beam grows
        /// from the edge towards the inside of the box and is shortened at both ends by <paramref name="shrink"/>,
        /// so that beams meeting in a corner butt against each other instead of overlapping (overlapping ones would
        /// be blended twice and show up as darker corners).
        /// </summary>
        private void AddBeam(Vector3 from, Vector3 to, float thickness, float shrink)
        {
            Vector3 min = Vector3.Min(from, to);
            Vector3 max = Vector3.Max(from, to);

            //Along the axis the beam runs on it spans the whole edge (less the shrink at both ends);
            //across the other two it grows from the surface of the box towards the inside
            (float Min, float Max) Extent(float low, float high)
            {
                if (low != high) return (low + shrink, high - shrink);
                return low > 0f ? (low - thickness, low) : (low, low + thickness);
            }

            (float x0, float x1) = Extent(min.X, max.X);
            (float y0, float y1) = Extent(min.Y, max.Y);
            (float z0, float z1) = Extent(min.Z, max.Z);

            AddBox(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));
        }

        /// <summary>
        /// Adds an axis-aligned box given by its two opposite corners, with the same counter-clockwise
        /// (viewed from outside) winding as <see cref="BoxMesh"/>.
        /// </summary>
        private void AddBox(Vector3 min, Vector3 max)
        {
            Vector3 center = (min + max) * Constants.HALF;
            Vector3 half = (max - min) * Constants.HALF;

            AddFace(center, Vector3.Right, Vector3.Forward, half.X, half.Z, half.Y * Vector3.Up);       //Top (+Y)
            AddFace(center, Vector3.Right, Vector3.Backward, half.X, half.Z, half.Y * Vector3.Down);    //Bottom (-Y)
            AddFace(center, Vector3.Forward, Vector3.Up, half.Z, half.Y, half.X * Vector3.Right);       //+X
            AddFace(center, Vector3.Backward, Vector3.Up, half.Z, half.Y, half.X * Vector3.Left);       //-X
            AddFace(center, Vector3.Right, Vector3.Up, half.X, half.Y, half.Z * Vector3.Backward);      //+Z
            AddFace(center, Vector3.Left, Vector3.Up, half.X, half.Y, half.Z * Vector3.Forward);        //-Z
        }

        private void AddFace(Vector3 origin, Vector3 right, Vector3 up, float halfRight, float halfUp, Vector3 center)
        {
            Vector3 normal = Vector3.Cross(right, up);

            Vector3 r = right * halfRight;
            Vector3 u = up * halfUp;
            Vector3 c = origin + center;

            int baseIndex = _vertexCount;

            _vertices[_vertexCount++] = new(c - r - u, normal, new Vector2(0f, 1f));
            _vertices[_vertexCount++] = new(c + r - u, normal, new Vector2(1f, 1f));
            _vertices[_vertexCount++] = new(c + r + u, normal, new Vector2(1f, 0f));
            _vertices[_vertexCount++] = new(c - r + u, normal, new Vector2(0f, 0f));

            _indices[_indexCount++] = (short)baseIndex;
            _indices[_indexCount++] = (short)(baseIndex + 1);
            _indices[_indexCount++] = (short)(baseIndex + 2);

            _indices[_indexCount++] = (short)baseIndex;
            _indices[_indexCount++] = (short)(baseIndex + 2);
            _indices[_indexCount++] = (short)(baseIndex + 3);
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

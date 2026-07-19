using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Render
{
    /// <summary>
    /// Procedurally generated box centred at the origin, with per-face normals (24 vertices).
    /// Replaces modelled slab assets whose size depends on runtime data (e.g. the ceiling plate,
    /// which is sized to the loaded map).
    /// </summary>
    public class BoxMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        private readonly VertexPositionNormalTexture[] _vertices = new VertexPositionNormalTexture[24];
        private readonly short[] _indices = new short[36];
        private int _vertexCount;
        private int _indexCount;

        /// <param name="sizeX">Full box size along the X axis.</param>
        /// <param name="sizeY">Full box size along the Y axis.</param>
        /// <param name="sizeZ">Full box size along the Z axis.</param>
        public BoxMesh(GraphicsDevice graphicsDevice, float sizeX, float sizeY, float sizeZ)
        {
            Vector3 half = new(sizeX * Constants.HALF, sizeY * Constants.HALF, sizeZ * Constants.HALF);

            //Right and up vectors are chosen so that right × up = the outward face normal,
            //giving the same counter-clockwise (viewed from outside) winding as SphereMesh
            AddFace(Vector3.Right, Vector3.Forward, half.X, half.Z, half.Y * Vector3.Up);       //Top (+Y)
            AddFace(Vector3.Right, Vector3.Backward, half.X, half.Z, half.Y * Vector3.Down);    //Bottom (-Y)
            AddFace(Vector3.Forward, Vector3.Up, half.Z, half.Y, half.X * Vector3.Right);       //+X
            AddFace(Vector3.Backward, Vector3.Up, half.Z, half.Y, half.X * Vector3.Left);       //-X
            AddFace(Vector3.Right, Vector3.Up, half.X, half.Y, half.Z * Vector3.Backward);      //+Z
            AddFace(Vector3.Left, Vector3.Up, half.X, half.Y, half.Z * Vector3.Forward);        //-Z

            PrimitiveCount = _indexCount / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, _vertexCount, BufferUsage.WriteOnly);
            VertexBuffer.SetData(_vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, _indexCount, BufferUsage.WriteOnly);
            IndexBuffer.SetData(_indices);

            BoundingSphere = new BoundingSphere(Vector3.Zero, half.Length());
        }

        private void AddFace(Vector3 right, Vector3 up, float halfRight, float halfUp, Vector3 centre)
        {
            Vector3 normal = Vector3.Cross(right, up);

            Vector3 r = right * halfRight;
            Vector3 u = up * halfUp;

            int baseIndex = _vertexCount;

            _vertices[_vertexCount++] = new(centre - r - u, normal, new Vector2(0f, 1f));
            _vertices[_vertexCount++] = new(centre + r - u, normal, new Vector2(1f, 1f));
            _vertices[_vertexCount++] = new(centre + r + u, normal, new Vector2(1f, 0f));
            _vertices[_vertexCount++] = new(centre - r + u, normal, new Vector2(0f, 0f));

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

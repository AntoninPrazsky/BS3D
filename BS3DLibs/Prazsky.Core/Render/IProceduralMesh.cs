using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Render
{
    /// <summary>
    /// A procedurally generated mesh (vertex and index buffer) that <see cref="InstancedModelRenderer"/>
    /// can draw in place of a modelled asset.
    /// </summary>
    public interface IProceduralMesh
    {
        VertexBuffer VertexBuffer { get; }
        IndexBuffer IndexBuffer { get; }
        int PrimitiveCount { get; }
        BoundingSphere BoundingSphere { get; }
    }
}

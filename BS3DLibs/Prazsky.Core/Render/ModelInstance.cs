using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Render
{
    /// <summary>
    /// Per-instance data for <see cref="InstancedModelRenderer"/>: the world matrix (four rows in
    /// TEXCOORD1-TEXCOORD4) plus one custom vector (TEXCOORD5). XYZ of the custom vector carry the
    /// world-space direction towards the instance's occluders (zero = none), W the base ambient
    /// occlusion factor (1 = fully open, towards 0 = occluded).
    /// </summary>
    public struct ModelInstance : IVertexType
    {
        public Matrix World;
        public Vector4 Custom;

        public static readonly VertexDeclaration VertexDeclaration = new(
            new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
            new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
            new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5));

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

        public ModelInstance(Matrix world, Vector4 custom)
        {
            World = world;
            Custom = custom;
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One instance drawn into the motion blur's velocity target (#402): where it is now, and where it stood when
    /// the shutter opened, <see cref="MotionBlur.SHUTTER_SECONDS"/> ago. The velocity shader projects every vertex
    /// through both and writes the difference, so the blur of a turning, travelling object comes out per pixel —
    /// the muzzle of a traversing barrel smears further than its breech, which no single vector for the whole
    /// object could say.
    /// <para>
    /// Rides in the second vertex stream at <c>TEXCOORD1-8</c>, the two matrices' rows in XNA's row-major layout,
    /// exactly as <see cref="ModelInstance"/> carries its one — so <c>float4x4(r1, r2, r3, r4)</c> needs no
    /// transpose. A struct of its own rather than a second matrix bolted onto <see cref="ModelInstance"/>: that one
    /// rides on every instance of every draw in the game, and only this pass wants the second pose.
    /// </para>
    /// </summary>
    public struct MotionInstance : IVertexType
    {
        /// <summary>The pose the object is drawn at this frame.</summary>
        public Matrix World;

        /// <summary>The pose it had when the shutter opened. Equal to <see cref="World"/> for a still object.</summary>
        public Matrix ShutterWorld;

        public static readonly VertexDeclaration VertexDeclaration = new(
            new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
            new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
            new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5),
            new VertexElement(80, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 6),
            new VertexElement(96, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 7),
            new VertexElement(112, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 8));

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

        public MotionInstance(in Matrix world, in Matrix shutterWorld)
        {
            World = world;
            ShutterWorld = shutterWorld;
        }
    }
}

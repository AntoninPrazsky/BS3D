using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Per-instance data for <see cref="InstancedModelRenderer"/>: the world matrix (four rows in
    /// TEXCOORD1-TEXCOORD4), one custom vector (TEXCOORD5) and a dissolve amount (TEXCOORD6).
    /// <para>
    /// XYZ of the custom vector carry the world-space direction towards the instance's occluders
    /// (zero = none), W the base ambient occlusion factor (1 = fully open, towards 0 = occluded).
    /// </para>
    /// </summary>
    public struct ModelInstance : IVertexType
    {
        public Matrix World;
        public Vector4 Custom;

        /// <summary>
        /// How much of this instance has been dithered away, and which way round. <b>Zero — what every
        /// instance that is not mid-transition carries — draws the whole thing</b>, so nothing has to know
        /// this exists in order to opt out of it.
        /// <list type="bullet">
        /// <item>Positive: <i>going</i>. Keeps the pixels whose noise is above the value, so the instance
        /// eats itself away as it climbs to 1.</item>
        /// <item>Negative: <i>arriving</i>. Keeps the pixels whose noise is below the magnitude, so the
        /// instance fills in as that climbs to 1.</item>
        /// </list>
        /// The two are exact complements, which is the point: drawing one object twice, at <c>+t</c> and at
        /// <c>-t</c>, covers every pixel exactly once. That is what makes a cross-fade between two ball
        /// colours possible at all — a colour is a <i>per-draw</i> uniform here, so blending two of them means
        /// drawing the object in both buckets, and two coincident <i>translucent</i> spheres would need depth
        /// sorting and would come out muddy. A dither cut needs neither; both draws stay opaque.
        /// <para>
        /// Only the ball pattern technique reads it. A single float rather than another
        /// <see cref="Vector4"/> because one scalar is all it is, and this rides on every instance in the
        /// scene — the city alone is well over a thousand of them.
        /// </para>
        /// </summary>
        public float Dissolve;

        public static readonly VertexDeclaration VertexDeclaration = new(
            new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
            new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
            new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5),
            new VertexElement(80, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 6));

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

        /// <param name="dissolve">Left at zero by everything not mid-transition — see <see cref="Dissolve"/>.</param>
        public ModelInstance(Matrix world, Vector4 custom, float dissolve = 0f)
        {
            World = world;
            Custom = custom;
            Dissolve = dissolve;
        }
    }
}

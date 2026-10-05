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
        /// <item>Below <c>-1</c>: a <b>ghost</b> (#794) — not dithered at all but drawn <i>smaller</i>, its radius
        /// <c>-Dissolve - 1</c> of a whole ball's, round its own centre; see <see cref="GhostDissolve"/>. Only the
        /// aim preview is one. It lives in this channel because the channel is free for it (the ghost is never
        /// mid-transition), and it needs no change to any pixel shader: the clip above already keeps every pixel of
        /// a value under <c>-1</c>, so only the vertex shader has anything to do.</item>
        /// </list>
        /// The two dithers are exact complements, which is the point: drawing one object twice, at <c>+t</c> and at
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

        /// <summary>
        /// The <see cref="Dissolve"/> that makes an instance a <b>ghost</b> of radius <paramref name="scale"/> (a share of
        /// a whole ball's, kept inside <c>(0, 1]</c>): <c>-(1 + scale)</c>. Below <c>-1</c>, where no dither value is, so
        /// the two uses of the channel cannot be mistaken for each other (see <see cref="IsGhost"/>).
        /// </summary>
        public static float GhostDissolve(float scale) => -(1f + MathHelper.Clamp(scale, GHOST_MIN_SCALE, 1f));

        /// <summary>
        /// Whether a <see cref="Dissolve"/> value is a ghost's, not a dither's: strictly below <c>-1</c>, where the
        /// arriving dither's range ends. The one test the Potato draw makes to tell the balls it can draw without a
        /// <c>clip()</c> from the ones that need one (#794).
        /// </summary>
        public static bool IsGhost(float dissolve) => dissolve < -1f;

        /// <summary>
        /// The radius a <see cref="GhostDissolve"/> value stands for, as a share of a whole ball's; 1 for any value that
        /// is not a ghost's. What the vertex shaders compute (<c>GhostScale</c> in BallCommon.fxh, PotatoModel.fx).
        /// </summary>
        public static float GhostScale(float dissolve) => IsGhost(dissolve) ? -dissolve - 1f : 1f;

        /// <summary>
        /// The smallest ghost <see cref="GhostDissolve"/> encodes. A ghost of radius 0 would be <c>-1</c>, which is
        /// the arriving dither at its end - a whole ball - and so the one value that cannot be told from "not a ghost".
        /// </summary>
        public const float GHOST_MIN_SCALE = 0.01f;

        /// <summary>
        /// How brightly this instance is flaring right now, 0…1 — the light running through the cluster from
        /// wherever the last ball landed. <b>Zero, which is what a ball at rest carries, adds nothing</b>, so
        /// like <see cref="Dissolve"/> nothing has to know it exists in order to opt out of it.
        /// <para>
        /// The curve is evaluated on the CPU and only its result rides here, because <i>when</i> a given ball
        /// takes its turn is a question about the cluster's connectivity — how many balls away from the impact
        /// it is, walking only over balls that touch — and the shader has no way to ask that. What travels is
        /// therefore a number per ball per frame rather than a wave equation in world space, which would run
        /// straight through the holes a played cluster is full of instead of around them.
        /// </para>
        /// <para>
        /// Only the ball pattern technique reads it, and a single float again for the reason
        /// <see cref="Dissolve"/> is one: this rides on every instance in the scene.
        /// </para>
        /// </summary>
        public float Ripple;

        public static readonly VertexDeclaration VertexDeclaration = new(
            new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
            new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
            new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5),
            new VertexElement(80, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 6),
            new VertexElement(84, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 7));

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

        /// <param name="dissolve">Left at zero by everything not mid-transition — see <see cref="Dissolve"/>.</param>
        /// <param name="ripple">Left at zero by everything not flaring — see <see cref="Ripple"/>.</param>
        public ModelInstance(Matrix world, Vector4 custom, float dissolve = 0f, float ripple = 0f)
        {
            World = world;
            Custom = custom;
            Dissolve = dissolve;
            Ripple = ripple;
        }

        /// <summary>
        /// The same instance at a different point of its dissolve — the one place a caller needs to put a ball
        /// out <b>twice</b> and have the two halves partition its pixels (#325's clear-to-colour crossing,
        /// which draws the glass at <c>+d</c> and the new colour at <c>−d</c>).
        /// <para>
        /// A copy rather than a setter: this rides in a vertex buffer and is written once per ball per frame,
        /// so a caller holding one and mutating it is a caller that can change an instance already stored.
        /// </para>
        /// </summary>
        public readonly ModelInstance WithDissolve(float dissolve) =>
            new(World, Custom, dissolve, Ripple);
    }
}

using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Tools
{
    /// <summary>
    /// Conversions between the sRGB encoding colors are authored and stored in and the linear radiance
    /// the renderer does its arithmetic in.
    /// <para>
    /// The distinction matters wherever a color is scaled, tinted or blended. sRGB is a display encoding,
    /// not a quantity of light, so halving an sRGB value does not halve the light it stands for and
    /// <c>SrgbToLinear(a * b) != SrgbToLinear(a) * SrgbToLinear(b)</c>. Any such operation has to happen
    /// on the linear side, which is why the light rig and the sky palette are converted here, once, on
    /// the way into the renderer, rather than per pixel at the far end of the shader.
    /// </para>
    /// </summary>
    public static class ColorSpace
    {
        /// <summary>
        /// Decodes an sRGB color to linear radiance. The exact piecewise curve rather than a 2.2 power:
        /// the toe near black is where the two disagree, and that is where a night scene lives.
        /// </summary>
        public static Vector3 SrgbToLinear(Vector3 color) =>
            new(SrgbToLinear(color.X), SrgbToLinear(color.Y), SrgbToLinear(color.Z));

        public static float SrgbToLinear(float channel) =>
            channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);

        /// <summary>Encodes linear radiance back to sRGB.</summary>
        public static Vector3 LinearToSrgb(Vector3 color) =>
            new(LinearToSrgb(color.X), LinearToSrgb(color.Y), LinearToSrgb(color.Z));

        /// <summary>
        /// Luminance of a <b>linear-radiance</b> color, Rec. 709 weights — how bright the colour reads,
        /// as one figure. This is the repo's weighting for linear values everywhere it takes one (the
        /// forest's pigment shift, Tonemap.fx, Outback.fx); it was written out inline at each site until
        /// #156 added a third copy and made it a helper. Rec. 601 appears once in the codebase and on a
        /// different domain (an sRGB-authored material diffuse, <c>InstancedModelRenderer</c>'s pattern
        /// tint) — do not unify that one into this.
        /// </summary>
        public static float Luminance(Vector3 linear) =>
            linear.X * 0.2126f + linear.Y * 0.7152f + linear.Z * 0.0722f;

        public static float LinearToSrgb(float channel) =>
            channel <= 0.0031308f ? channel * 12.92f : 1.055f * MathF.Pow(channel, 1f / 2.4f) - 0.055f;
    }
}

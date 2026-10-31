using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <c>PotatoOutput.fxh</c>'s end of the frame on the CPU: for a Potato surface whose colours are the same across a draw,
    /// curved here once a frame and handed to the effect display-encoded, so that no pixel pays for the curve. The cloud
    /// deck's three colours (<see cref="PotatoCloudDeck"/>) and the cities' glass and lamps (#1044).
    /// </summary>
    public static class PotatoDisplay
    {
        /// <summary>
        /// <c>ToDisplay</c>, line for line: the exposure, Narkowicz's ACES fit and the exact sRGB curve, from linear
        /// radiance to what the back buffer holds.
        /// </summary>
        public static Vector3 ToDisplay(Vector3 radiance, float exposure)
        {
            Vector3 x = radiance * exposure;
            return new Vector3(Channel(x.X), Channel(x.Y), Channel(x.Z));

            static float Channel(float v)
            {
                float aces = Math.Clamp(v * (2.51f * v + 0.03f) / (v * (2.43f * v + 0.59f) + 0.14f), 0f, 1f);
                float c = MathF.Max(aces, 0f);
                return c < 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;
            }
        }
    }
}

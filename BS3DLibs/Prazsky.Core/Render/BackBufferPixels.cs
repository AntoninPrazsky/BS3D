using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Reads the back buffer as 8-bit colour whatever its format (#859). <c>GraphicsDevice.GetBackBufferData</c> copies
    /// the swap chain's bytes into whatever array it is handed and converts nothing, so a 10-bit back buffer
    /// (<see cref="SurfaceFormat.Rgba1010102"/>) read into <see cref="Color"/>s came back as its packed words misread
    /// as bytes — cornflower blue (100, 149, 237) read as (145, 89, 121), measured on the Game's own path. The two
    /// readers of the back buffer, the screenshot writer and the note's picture, read through this instead: the 8-bit
    /// buffer as before, the 10-bit one as its words, each decoded to the eight bits a PNG or a JPEG holds.
    /// </summary>
    public static class BackBufferPixels
    {
        /// <summary>
        /// The back buffer into <paramref name="pixels"/>, one <see cref="Color"/> a pixel, as the frame was presented.
        /// Allocates the packed words on the 10-bit path: a readback is a long frame anyway, never a per-frame one.
        /// </summary>
        public static void Read(GraphicsDevice device, Color[] pixels)
        {
            if (device.PresentationParameters.BackBufferFormat != SurfaceFormat.Rgba1010102)
            {
                device.GetBackBufferData(pixels);
                return;
            }

            uint[] packed = new uint[pixels.Length];
            device.GetBackBufferData(packed);

            for (int i = 0; i < packed.Length; i++)
                pixels[i] = Decode1010102(packed[i]);
        }

        /// <summary>
        /// One <c>R10G10B10A2_UNORM</c> word to 8-bit colour: red in the low ten bits, then green, then blue, two bits
        /// of alpha on top. Each channel's top eight bits, exact at both ends (0 → 0, 1023 → 255) and never more than a
        /// quarter of a step off between; opaque, because the back buffer's alpha is not a picture's.
        /// </summary>
        public static Color Decode1010102(uint packed) => new(
            (int)((packed & 0x3FF) >> 2),
            (int)(((packed >> 10) & 0x3FF) >> 2),
            (int)(((packed >> 20) & 0x3FF) >> 2),
            255);

        /// <summary>The word a 10-bit back buffer holds for a colour — the decode's inverse, for a check to build from.</summary>
        public static uint Encode1010102(int red10, int green10, int blue10, int alpha2) =>
            (uint)(red10 & 0x3FF) | ((uint)(green10 & 0x3FF) << 10) | ((uint)(blue10 & 0x3FF) << 20) | ((uint)(alpha2 & 0x3) << 30);
    }
}

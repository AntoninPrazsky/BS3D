using Microsoft.Xna.Framework;
using System;
using System.Globalization;

namespace BS3D.Capture
{
    /// <summary>
    /// The size a capture's frames are drawn at when it is not the window's (#1177, <c>capturesize=&lt;w&gt;x&lt;h&gt;</c>): the back buffer is
    /// made that size and the window shows it scaled down, so a 16:9 4K film comes out of the desktop's 21:9 panel and out of a laptop
    /// whose display is smaller than the film. What is pure about it lives here, where the tests reach it: the spelling, the limits and
    /// the preview window's size.
    /// </summary>
    internal static class CaptureSize
    {
        /// <summary>The largest side a Direct3D 11 texture can have, and so the largest a back buffer can.</summary>
        internal const int MAX_SIDE = 16384;

        /// <summary>The smallest side taken: a smaller frame is a typo, not a film.</summary>
        internal const int MIN_SIDE = 64;

        /// <summary>The box the preview window is fitted into when <c>width=</c>/<c>height=</c> do not size it.</summary>
        internal static readonly Point PREVIEW_BOX = new(1280, 720);

        /// <summary>
        /// Reads <c>3840x2160</c> (an <c>x</c> or an <c>X</c> between two whole numbers). Both sides must be even, because the H.264 and VP9
        /// the frames are encoded to store colour at half the resolution in each direction and refuse an odd side, and within
        /// <see cref="MIN_SIDE"/> and <see cref="MAX_SIDE"/>.
        /// </summary>
        internal static bool TryParse(string text, out Point size)
        {
            size = Point.Zero;
            if (string.IsNullOrWhiteSpace(text)) return false;

            int x = text.IndexOfAny(['x', 'X']);
            if (x <= 0 || x == text.Length - 1) return false;
            if (!int.TryParse(text.AsSpan(0, x), NumberStyles.None, CultureInfo.InvariantCulture, out int width)
                || !int.TryParse(text.AsSpan(x + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int height))
                return false;
            if (!Fits(width) || !Fits(height)) return false;

            size = new Point(width, height);
            return true;

            static bool Fits(int side) => side >= MIN_SIDE && side <= MAX_SIDE && side % 2 == 0;
        }

        /// <summary>
        /// The window that shows a capture of <paramref name="size"/>: the frame's aspect, as large as fits in <paramref name="box"/>, never
        /// larger than the frame itself. The window only shows the frames: what is written is the back buffer, whatever this is.
        /// </summary>
        internal static Point Preview(Point size, Point box)
        {
            float scale = Math.Min(1f, Math.Min(box.X / (float)size.X, box.Y / (float)size.Y));
            return new Point(Math.Max(1, (int)MathF.Round(size.X * scale)), Math.Max(1, (int)MathF.Round(size.Y * scale)));
        }

        /// <summary>
        /// Whether the scene target of a frame of <paramref name="size"/> at the supersample factor <paramref name="factor"/> is a texture
        /// Direct3D 11 can make: the scene is drawn at the frame's size times the factor, so a 4K frame takes any factor up to 4 and an 8K
        /// one only 2. Otherwise the run would fail creating its first target.
        /// </summary>
        internal static bool SceneFits(Point size, int factor) =>
            (long)size.X * factor <= MAX_SIDE && (long)size.Y * factor <= MAX_SIDE;
    }
}

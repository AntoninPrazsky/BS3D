using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The rungs of the Game's Resolution row (#801): the heights the 3D may be drawn at on a display of a given size,
    /// each at <b>the display's own aspect ratio</b> - the owner's rule, so a 16:9 panel offers only 16:9 sizes and a
    /// 21:9 one only 21:9, and the picture scaled up to the display is never stretched. Native first, then 5/6, 2/3, 1/2
    /// and 1/3 of its height, none under <see cref="MIN_HEIGHT"/>; on a 1920x1080 panel 1600x900, 1280x720, 960x540 and
    /// 640x360.
    /// <para>
    /// <b>Exact where the display allows it.</b> A rung's height is the nearest whole multiple of the aspect's own unit
    /// (16:9's is 9 pixels, doubled to 18 so both sides come out even), so its width is a whole number at the exact
    /// ratio - 2560x1440 gives 2144x1206 rather than a rounded 2133x1200. A panel whose unit is too coarse for that (1366x768,
    /// whose ratio does not reduce) takes the fraction's own even height instead, and its width rounded to even is then
    /// off the ratio by under 0.2 %. Pure arithmetic, so the tests can state what the row promises without a window.
    /// </para>
    /// </summary>
    public static class RenderResolution
    {
        /// <summary>The lowest height offered: under it the 3D is a smear behind a sharp HUD, whatever the display.</summary>
        public const int MIN_HEIGHT = 360;

        //The fractions of the display's height below native, largest first
        private static readonly float[] FRACTIONS = [5f / 6f, 2f / 3f, 1f / 2f, 1f / 3f];

        //How far from its fraction an exact-aspect height may land before the fraction's own height is taken instead
        private const float EXACT_TOLERANCE = 0.03f;

        /// <summary>
        /// The heights offered on a <paramref name="width"/> x <paramref name="height"/> display, native first and then
        /// downwards, none repeated. Each one's width is <see cref="WidthAt"/>.
        /// </summary>
        public static int[] Heights(int width, int height)
        {
            if (width <= 0 || height <= 0) return [];

            int divisor = GreatestCommonDivisor(width, height);
            int unitWidth = width / divisor, unitHeight = height / divisor;

            //Both sides of every rung even, so an odd unit is doubled
            if (unitWidth % 2 != 0 || unitHeight % 2 != 0) unitHeight *= 2;

            var heights = new List<int> { height };

            foreach (float fraction in FRACTIONS)
            {
                float target = height * fraction;
                int exact = (int)MathF.Round(target / unitHeight) * unitHeight;

                int rung = exact > 0 && MathF.Abs(exact - target) <= target * EXACT_TOLERANCE
                    ? exact
                    : (int)MathF.Round(target * 0.5f) * 2;

                if (rung >= MIN_HEIGHT && rung < height && !heights.Contains(rung)) heights.Add(rung);
            }

            return heights.ToArray();
        }

        /// <summary>The width <paramref name="height"/> has at the display's aspect, rounded to even.</summary>
        public static int WidthAt(int displayWidth, int displayHeight, int height) =>
            (int)MathF.Round(displayWidth * (float)height / displayHeight * 0.5f) * 2;

        /// <summary>
        /// The rung nearest <paramref name="height"/>, the lower of two equally near - what a stored height snaps to on a
        /// display whose ladder does not hold it. Zero for an empty ladder.
        /// </summary>
        public static int Nearest(ReadOnlySpan<int> heights, int height)
        {
            int best = 0;

            foreach (int rung in heights)
            {
                int distance = Math.Abs(rung - height), bestDistance = Math.Abs(best - height);
                if (best == 0 || distance < bestDistance || (distance == bestDistance && rung < best)) best = rung;
            }

            return best;
        }

        /// <summary>
        /// The rung one click on from <paramref name="height"/>: <b>the first rung below it</b>, wrapping from the lowest
        /// back to native - so a height off the ladder walks onto it, and the first click from native is the smallest step
        /// down. A row has no back-step, and the walk goes the way a slow frame wants.
        /// </summary>
        public static int Next(ReadOnlySpan<int> heights, int height)
        {
            if (heights.IsEmpty) return height;

            foreach (int rung in heights)
                if (rung < height) return rung;

            return heights[0];
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0) (a, b) = (b, a % b);

            return a;
        }
    }
}

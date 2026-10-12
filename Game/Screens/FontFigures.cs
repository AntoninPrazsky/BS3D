using FontStashSharp;
using System;
using System.Runtime.CompilerServices;

namespace BS3D.Screens
{
    /// <summary>
    /// <see cref="TabularFigures"/> for a real face (#806): its ten digit advances, read once per font and kept, and the
    /// two things a page asks — a number set in tabular figures for a label, and how wide that comes out. A font is one size
    /// of a face (the menu asks its <c>FontSystem</c>s for a new one at every layout size), so the advances are kept per
    /// <see cref="SpriteFontBase"/>, weakly: a size the menu no longer uses is let go with it. Called when a number changes or
    /// a column is pinned, never per frame.
    /// </summary>
    internal static class FontFigures
    {
        private static readonly ConditionalWeakTable<SpriteFontBase, int[]> Advances = new();

        /// <summary><paramref name="text"/> with its digits in tabular figures for <paramref name="font"/>: a label's text.</summary>
        internal static string Tabular(string text, SpriteFontBase font) => TabularFigures.Set(text, AdvancesOf(font));

        /// <summary>How wide <see cref="Tabular"/>'s text lays out in <paramref name="font"/>, in whole pixels.</summary>
        internal static int Width(string text, SpriteFontBase font) =>
            TabularFigures.Width(text, AdvancesOf(font), run => (int)font.MeasureString(run).X);

        /// <summary>
        /// The ten digits' advances. A single glyph's <c>MeasureString</c> is its advance (the bounds run from the pen's start
        /// to where the advance leaves it), a whole pixel in FontStashSharp 1.5.6, and the rich-text layout sizes a chunk by
        /// the same call truncated to an integer, so the cell and the layout agree to the pixel.
        /// </summary>
        private static int[] AdvancesOf(SpriteFontBase font)
        {
            ArgumentNullException.ThrowIfNull(font);

            if (!Advances.TryGetValue(font, out int[] advances))
            {
                advances = new int[TabularFigures.DIGITS];
                for (int d = 0; d < advances.Length; d++) advances[d] = (int)font.MeasureString(Digits[d]).X;
                Advances.AddOrUpdate(font, advances);
            }

            return advances;
        }

        private static readonly string[] Digits = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
    }
}

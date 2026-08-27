using System.Globalization;

namespace BS3D.Screens
{
    /// <summary>
    /// How a score is written on screen, in one copy — the digits grouped in threes by a space, so a score
    /// reads <c>12 340</c> rather than <c>12,340</c> (#284).
    /// <para>
    /// <b>Every score-like figure the game prints goes through here, and that is the point rather than the
    /// tidiness.</b> The HUD's running total, the award popup that flies off a match and the end-of-level
    /// breakdown are three different screens written at three different times, and they are on screen
    /// together: a popup reading "+2 960" beside a corner score reading "2,960" is one number written two
    /// ways on the same frame. It happened, which is why the popup carried a comment saying so. A shared
    /// formatter is what makes it impossible rather than merely unlikely.
    /// </para>
    /// <para>
    /// <b>It stays independent of the machine's locale</b>, for the same reason the call sites used
    /// <see cref="CultureInfo.InvariantCulture"/> before they used this: what <c>"N0"</c> groups with is a
    /// property of whatever culture the machine happens to run under, so the game would print a different
    /// number in Prague than in London. The separator is now chosen here, deliberately, instead of inherited
    /// from either.
    /// </para>
    /// </summary>
    internal static class ScoreText
    {
        /// <summary>
        /// Invariant grouping with a space in place of the comma. A plain space (U+0020) rather than a
        /// no-break or thin one: these figures are drawn as single-line labels that never wrap, so the
        /// no-break variant would buy nothing, and both are glyphs the HUD's fonts are not guaranteed to
        /// carry — a missing glyph is a hole in a number the player is reading.
        /// </summary>
        private static readonly NumberFormatInfo GROUPED_BY_SPACE = BuildFormat();

        /// <summary>The figure as the player reads it — grouped in threes, no decimals, no currency.</summary>
        internal static string Of(int value) => value.ToString("N0", GROUPED_BY_SPACE);

        private static NumberFormatInfo BuildFormat()
        {
            NumberFormatInfo format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            format.NumberGroupSeparator = " ";
            return format;
        }
    }
}

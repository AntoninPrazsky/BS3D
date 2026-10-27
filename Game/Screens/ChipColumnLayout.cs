namespace BS3D.Screens
{
    /// <summary>
    /// Where the power-up chips' keycaps, pictograms and words stand (#694, #1030): the keycaps in one column, centred in
    /// it, the pictograms in the column after it and every caption starting at one x after that, the widest caption a
    /// chip can show ending on the magazine strip's right edge. A row reads key - picture - word, the key first because
    /// it is what the player presses. A pure function of measured widths, pulled out of <c>PlayHud.DrawChip</c> so its
    /// promise can be pinned in a test the way <see cref="TutorialCardLayout"/> is: one column for every keycap, whatever
    /// the captions say and whichever device's glyphs are drawn.
    /// <para>
    /// Each chip used to be right-aligned as a whole, so its keycap stood at "the right edge minus its own caption",
    /// and "Cut", "Brake" and "Swap" left the three keycaps a ragged column (the owner's playtest, read off a frame at
    /// 1735, 1693 and 1700 px). The column's two widths are the widest a chip can show and not this frame's, so it does
    /// not move when "Swap" becomes "Swap used" or the hand moves to the pad.
    /// </para>
    /// </summary>
    internal readonly struct ChipColumnLayout
    {
        /// <summary>The keycap column's left edge.</summary>
        public readonly float GlyphColumnLeft;

        /// <summary>The keycap column's width: the widest glyph either device draws.</summary>
        public readonly float GlyphColumnWidth;

        /// <summary>Where every pictogram's ink starts: after the keycap column and its gap.</summary>
        public readonly float IconLeft;

        /// <summary>Where every caption starts.</summary>
        public readonly float CaptionLeft;

        private ChipColumnLayout(float glyphColumnLeft, float glyphColumnWidth, float iconLeft, float captionLeft)
        {
            GlyphColumnLeft = glyphColumnLeft;
            GlyphColumnWidth = glyphColumnWidth;
            IconLeft = iconLeft;
            CaptionLeft = captionLeft;
        }

        /// <param name="right">The right edge the widest caption ends on — the magazine strip's own.</param>
        /// <param name="glyphColumnWidth">The widest keycap or button glyph either device draws.</param>
        /// <param name="iconWidth">The pictograms' width (their ink, not the margin round it).</param>
        /// <param name="captionColumnWidth">The widest caption a chip can show.</param>
        /// <param name="gap">Between the keycap column and the pictograms, and between them and the captions.</param>
        public static ChipColumnLayout Compute(float right, float glyphColumnWidth, float iconWidth, float captionColumnWidth, float gap)
        {
            float captionLeft = right - captionColumnWidth;
            float iconLeft = captionLeft - gap - iconWidth;
            return new ChipColumnLayout(iconLeft - gap - glyphColumnWidth, glyphColumnWidth, iconLeft, captionLeft);
        }

        /// <summary>The left edge a glyph <paramref name="glyphWidth"/> wide is drawn at: centred in the column, so
        /// keycaps of different advances (and the pad's arrows) share one centre line.</summary>
        public float GlyphLeft(float glyphWidth) => GlyphColumnLeft + (GlyphColumnWidth - glyphWidth) * 0.5f;
    }
}

using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The power-up chips' columns (#694, #1030): every keycap shares one centre line, then the pictograms stand in one
    /// column and every caption on one left edge, whatever the captions say and whichever device's glyphs are drawn, and
    /// the widest caption ends on the strip's right edge. <c>ChipColumnLayout</c> is compiled in from the Game; the widths
    /// are 1080p figures of the shipped glyphs and captions' order of size, read off a capture (the keycaps about 40 px,
    /// the pad's arrows narrower, a pictogram as tall as the keycap's ink).
    /// </summary>
    public class ChipColumnLayoutTests
    {
        private const float RIGHT = 1875f;
        private const float GAP = 7f;
        private const float EPSILON = 1e-3f;

        //The widest glyph either device draws, the pictograms' width, and the widest caption a chip can show ("Brake used")
        private const float GLYPH_COLUMN = 42f;
        private const float ICON = 30f;
        private const float CAPTION_COLUMN = 120f;

        //Each glyph's own width: the three keycaps and the pad's three arrows
        public static TheoryData<float> Glyphs() => new() { 42f, 40f, 41f, 30f, 34f, 36f };

        [Theory]
        [MemberData(nameof(Glyphs))]
        public void Every_glyph_is_centred_on_one_line(float glyphWidth)
        {
            ChipColumnLayout layout = ChipColumnLayout.Compute(RIGHT, GLYPH_COLUMN, ICON, CAPTION_COLUMN, GAP);

            float centre = layout.GlyphLeft(glyphWidth) + glyphWidth * 0.5f;

            Assert.Equal(layout.GlyphColumnLeft + GLYPH_COLUMN * 0.5f, centre, EPSILON);
        }

        [Fact]
        public void The_columns_do_not_overlap_and_the_widest_caption_ends_on_the_right_edge()
        {
            ChipColumnLayout layout = ChipColumnLayout.Compute(RIGHT, GLYPH_COLUMN, ICON, CAPTION_COLUMN, GAP);

            Assert.Equal(layout.CaptionLeft - GAP, layout.IconLeft + ICON, EPSILON);
            Assert.Equal(RIGHT, layout.CaptionLeft + CAPTION_COLUMN, EPSILON);
        }

        [Fact]
        public void A_row_reads_key_then_picture_then_caption()
        {
            ChipColumnLayout layout = ChipColumnLayout.Compute(RIGHT, GLYPH_COLUMN, ICON, CAPTION_COLUMN, GAP);

            //The keycap column ends a gap before the picture, and the picture a gap before the caption (#1030): the key
            //led the row, the picture came between it and the word
            Assert.Equal(layout.IconLeft - GAP, layout.GlyphColumnLeft + GLYPH_COLUMN, EPSILON);
            Assert.True(layout.GlyphColumnLeft + GLYPH_COLUMN < layout.IconLeft);
            Assert.True(layout.IconLeft + ICON < layout.CaptionLeft);
        }

        [Fact]
        public void The_row_is_no_wider_than_it_was_with_the_picture_ahead_of_the_key()
        {
            ChipColumnLayout layout = ChipColumnLayout.Compute(RIGHT, GLYPH_COLUMN, ICON, CAPTION_COLUMN, GAP);

            //#991 hung the picture left of the keycap column, so the row spanned picture, gap, keycap, gap, caption. The same
            //five parts in the other order must take the same room, or the strip's left neighbour is crowded for nothing
            float before = ICON + GAP + GLYPH_COLUMN + GAP + CAPTION_COLUMN;

            Assert.Equal(before, RIGHT - layout.GlyphColumnLeft, EPSILON);
        }
    }
}

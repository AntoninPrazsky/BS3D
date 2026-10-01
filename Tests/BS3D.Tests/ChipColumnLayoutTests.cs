using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The power-up chips' columns (#694): every keycap shares one centre line and every caption one left edge,
    /// whatever the captions say and whichever device's glyphs are drawn, and the widest caption ends on the strip's
    /// right edge. <c>ChipColumnLayout</c> is compiled in from the Game; the widths are 1080p figures of the shipped
    /// glyphs and captions' order of size, read off a capture (the keycaps about 40 px, the pad's arrows narrower).
    /// </summary>
    public class ChipColumnLayoutTests
    {
        private const float RIGHT = 1875f;
        private const float GAP = 7f;
        private const float EPSILON = 1e-3f;

        //The widest glyph either device draws, and the widest caption a chip can show ("Brake used")
        private const float GLYPH_COLUMN = 42f;
        private const float CAPTION_COLUMN = 120f;

        //Each glyph's own width: the three keycaps and the pad's three arrows
        public static TheoryData<float> Glyphs() => new() { 42f, 40f, 41f, 30f, 34f, 36f };

        [Theory]
        [MemberData(nameof(Glyphs))]
        public void Every_glyph_is_centred_on_one_line(float glyphWidth)
        {
            ChipColumnLayout layout = ChipColumnLayout.Compute(RIGHT, GLYPH_COLUMN, CAPTION_COLUMN, GAP);

            float centre = layout.GlyphLeft(glyphWidth) + glyphWidth * 0.5f;

            Assert.Equal(layout.GlyphColumnLeft + GLYPH_COLUMN * 0.5f, centre, EPSILON);
        }

        [Fact]
        public void The_columns_do_not_overlap_and_the_widest_caption_ends_on_the_right_edge()
        {
            ChipColumnLayout layout = ChipColumnLayout.Compute(RIGHT, GLYPH_COLUMN, CAPTION_COLUMN, GAP);

            Assert.Equal(layout.CaptionLeft - GAP, layout.GlyphColumnLeft + GLYPH_COLUMN, EPSILON);
            Assert.Equal(RIGHT, layout.CaptionLeft + CAPTION_COLUMN, EPSILON);
        }
    }
}

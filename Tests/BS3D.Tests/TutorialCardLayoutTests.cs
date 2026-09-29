using BS3D.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The tutorial card's geometry (#673): the praise word lands under an instruction the player has already
    /// read, and nothing of that instruction may move when it does — not the caption, not the detail line, not
    /// the glyph beside them, whichever of the glyph and the text is the taller, and not while the praise's
    /// spring is swelling it. <c>TutorialCardLayout</c> is compiled in from the Game; the sizes are the shipped
    /// cards' at 1080p, measured off a capture to the nearest few pixels.
    /// </summary>
    public class TutorialCardLayoutTests
    {
        private const float CENTRE_X = 960f;
        private const float TOP = 40f;
        private const float HALF_STRIP = 700f;
        private const float GLYPH_GAP = 24f;
        private const float LINE_GAP = 5f;
        private const float PRAISE_GAP = 13f;
        private const float BOB = 2.5f;
        private const float EPSILON = 1e-3f;

        private static readonly Vector2 PRAISE = new(140f, 62f);

        /// <summary>A shipped card's shape: a glyph, a caption and maybe a detail line.</summary>
        public static TheoryData<string, Vector2, Vector2, Vector2> Cards() => new()
        {
            //"Move the mouse to aim": the glyph is taller than the one line beside it
            { "aim", new Vector2(80f, 96f), new Vector2(620f, 62f), Vector2.Zero },
            //"Click to fire / Space fires too": the text is the taller
            { "fire", new Vector2(60f, 70f), new Vector2(330f, 62f), new Vector2(250f, 36f) },
            //"Three of a colour together fall": no glyph at all
            { "match", Vector2.Zero, new Vector2(780f, 62f), new Vector2(560f, 36f) },
        };

        private static TutorialCardLayout Lay(Vector2 glyph, Vector2 caption, Vector2 detail, Vector2 praise,
            float kick, float arrive = 1f, float halfStrip = HALF_STRIP) =>
            TutorialCardLayout.Compute(glyph, caption, detail, praise,
                glyph.X > 0f ? GLYPH_GAP : 0f, detail.Y > 0f ? LINE_GAP : 0f, praise.Y > 0f ? PRAISE_GAP : 0f,
                CENTRE_X, TOP, BOB, arrive, kick, halfStrip);

        private static void Same(Vector2 expected, Vector2 actual, string what)
        {
            Assert.True(Vector2.Distance(expected, actual) < EPSILON, $"{what}: {expected} before the praise, {actual} with it");
        }

        [Theory]
        [MemberData(nameof(Cards))]
        public void TheInstructionDoesNotMoveWhenThePraiseLands(string card, Vector2 glyph, Vector2 caption, Vector2 detail)
        {
            TutorialCardLayout before = Lay(glyph, caption, detail, Vector2.Zero, kick: 1f);

            //At rest, at the top of the spring's swell, and in its one undershoot
            foreach (float kick in new[] { 1f, 1.3f, 0.94f })
            {
                TutorialCardLayout praised = Lay(glyph, caption, detail, PRAISE, kick);

                Same(before.GlyphAt, praised.GlyphAt, $"{card} glyph at kick {kick}");
                Same(before.CaptionAt, praised.CaptionAt, $"{card} caption at kick {kick}");
                Same(before.DetailAt, praised.DetailAt, $"{card} detail at kick {kick}");
                Assert.Equal(before.Scale, praised.Scale, 4);
            }
        }

        [Theory]
        [MemberData(nameof(Cards))]
        public void ThePraiseHangsBelowTheInstruction(string card, Vector2 glyph, Vector2 caption, Vector2 detail)
        {
            foreach (float kick in new[] { 1f, 1.3f })
            {
                TutorialCardLayout praised = Lay(glyph, caption, detail, PRAISE, kick);
                float textBottom = detail.Y > 0f
                    ? praised.DetailAt.Y + detail.Y * praised.Scale
                    : praised.CaptionAt.Y + caption.Y * praised.Scale;

                Assert.True(praised.PraiseAt.Y >= textBottom,
                    $"{card} at kick {kick}: the praise's top {praised.PraiseAt.Y} is over the text's bottom {textBottom}");
                if (glyph.X > 0f)
                    Assert.True(praised.PraiseAt.X >= praised.GlyphAt.X + glyph.X * praised.Scale,
                        $"{card} at kick {kick}: the praise reaches into the glyph's column");
            }

            //At rest it stands in the caption's column, under the text
            TutorialCardLayout rest = Lay(glyph, caption, detail, PRAISE, kick: 1f);
            Assert.Equal(rest.CaptionAt.X, rest.PraiseAt.X, 3);
            Assert.Equal(rest.Scale, rest.PraiseScale, 4);
        }

        [Fact]
        public void ThePraiseSwellsAboutItsOwnCentre()
        {
            Vector2 glyph = new(60f, 70f), caption = new(330f, 62f), detail = new(250f, 36f);
            TutorialCardLayout rest = Lay(glyph, caption, detail, PRAISE, kick: 1f);
            TutorialCardLayout swollen = Lay(glyph, caption, detail, PRAISE, kick: 1.3f);

            Assert.Equal(1.3f, swollen.PraiseScale, 4);
            Same(rest.PraiseAt + PRAISE * rest.PraiseScale * 0.5f, swollen.PraiseAt + PRAISE * swollen.PraiseScale * 0.5f,
                "the praise's centre");
        }

        [Fact]
        public void ACardWithoutAPraiseRowKeepsItsKick()
        {
            //The send-off (#459) arrives celebrating and is kicked on the frame it lands, with no praise row to
            //carry the spring instead: the whole card swells about its own centre, as it always has
            Vector2 caption = new(900f, 62f), detail = new(600f, 36f);
            TutorialCardLayout rest = Lay(Vector2.Zero, caption, detail, Vector2.Zero, kick: 1f);
            TutorialCardLayout kicked = Lay(Vector2.Zero, caption, detail, Vector2.Zero, kick: 1.3f);

            Assert.Equal(1.3f, kicked.Scale, 4);

            float height = caption.Y + LINE_GAP + detail.Y;
            Assert.Equal(TOP + BOB, rest.CaptionAt.Y, 3);
            Assert.Equal(TOP + BOB + height * 0.5f * (1f - 1.3f), kicked.CaptionAt.Y, 3);
        }

        [Fact]
        public void ThePraiseStaysInsideTheStrip()
        {
            //A narrow window and a praise word wider than the space right of the caption's edge
            Vector2 glyph = new(80f, 96f), caption = new(300f, 62f), wide = new(700f, 62f);
            const float narrow = 260f;
            TutorialCardLayout praised = Lay(glyph, caption, Vector2.Zero, wide, kick: 1.3f, halfStrip: narrow);

            Assert.True(praised.PraiseAt.X >= CENTRE_X - narrow - EPSILON, $"left edge {praised.PraiseAt.X}");
            Assert.True(praised.PraiseAt.X + wide.X * praised.PraiseScale <= CENTRE_X + narrow + EPSILON,
                $"right edge {praised.PraiseAt.X + wide.X * praised.PraiseScale}");
        }
    }
}

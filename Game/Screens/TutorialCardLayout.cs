using Microsoft.Xna.Framework;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// Where the tutorial card's parts stand on the frame: the glyph, the caption, the detail line and the
    /// praise word, out of their measured sizes. A pure function of those sizes, pulled out of
    /// <c>PlayHud.DrawTutorial</c> so its one promise can be pinned in a test (#673).
    /// </summary>
    internal readonly struct TutorialCardLayout
    {
        /// <summary>The top-left of the glyph, at <see cref="Scale"/>.</summary>
        public readonly Vector2 GlyphAt;

        /// <summary>The top-left of the caption, at <see cref="Scale"/>.</summary>
        public readonly Vector2 CaptionAt;

        /// <summary>The top-left of the detail line, at <see cref="Scale"/>.</summary>
        public readonly Vector2 DetailAt;

        /// <summary>The scale the glyph, the caption and the detail are drawn at.</summary>
        public readonly float Scale;

        /// <summary>The top-left of the praise word, at <see cref="PraiseScale"/>.</summary>
        public readonly Vector2 PraiseAt;

        /// <summary>The scale the praise word is drawn at.</summary>
        public readonly float PraiseScale;

        private TutorialCardLayout(Vector2 glyphAt, Vector2 captionAt, Vector2 detailAt, float scale,
            Vector2 praiseAt, float praiseScale)
        {
            GlyphAt = glyphAt;
            CaptionAt = captionAt;
            DetailAt = detailAt;
            Scale = scale;
            PraiseAt = praiseAt;
            PraiseScale = praiseScale;
        }

        /// <param name="glyph">The glyph's measured size, zero for none.</param>
        /// <param name="caption">The caption's measured size.</param>
        /// <param name="detail">The detail line's measured size, zero for none.</param>
        /// <param name="praise">The praise word's measured size, zero while there is none.</param>
        /// <param name="glyphGap">Between the glyph and the text, already zero when there is no glyph.</param>
        /// <param name="lineGap">Between the caption and the detail, already zero when there is no detail.</param>
        /// <param name="praiseGap">Between the detail and the praise, already zero when there is no praise.</param>
        /// <param name="centreX">The frame's horizontal centre, which the card is centred on.</param>
        /// <param name="top">Where the card's top stands at rest.</param>
        /// <param name="bob">The idle bob's offset this frame. It runs through the praise: stopping it there made the
        /// whole card jump by the bob on the frame the praise landed.</param>
        /// <param name="arrive">The pop-in's scale.</param>
        /// <param name="kick">The praise's spring, 1 at rest — the praise word's alone while there is one.</param>
        /// <param name="halfStrip">How far from <paramref name="centreX"/> the card may reach either way (#461).</param>
        public static TutorialCardLayout Compute(Vector2 glyph, Vector2 caption, Vector2 detail, Vector2 praise,
            float glyphGap, float lineGap, float praiseGap, float centreX, float top, float bob, float arrive,
            float kick, float halfStrip)
        {
            //THE INSTRUCTION IS LAID OUT FROM ITS OWN GEOMETRY ALONE: the glyph, the caption and the detail
            //decide the card's width, its height, the centre it is scaled about and the strip clamp, and the
            //praise enters none of it. It was one block with the praise in it until #673, so the frame the
            //praise landed re-centred everything already read — the text rose by half the praise's height
            //beside a glyph taller than it, the glyph sank beside text taller than it — and the praise's
            //spring swelled all of it about a centre that now included the praise row.
            float textWidth = MathF.Max(caption.X, detail.X);
            float textHeight = caption.Y + lineGap + detail.Y;
            float width = glyph.X + glyphGap + textWidth;
            float height = MathF.Max(glyph.Y, textHeight);

            //The spring swells the praise when there is one and the card when there is not: the send-off is
            //kicked on the frame it lands (#459) and has no praise row to carry it
            bool praising = praise.Y > 0f;
            float clamp = width > 0f ? 2f * halfStrip / width : float.MaxValue;
            float scale = MathF.Min(arrive * (praising ? 1f : kick), clamp);

            Vector2 centre = new(centreX, top + height * 0.5f + bob);
            Vector2 origin = centre - new Vector2(width, height) * (0.5f * scale);

            Vector2 glyphAt = origin + new Vector2(0f, (height - glyph.Y) * 0.5f * scale);
            Vector2 captionAt = origin + new Vector2((glyph.X + glyphGap) * scale, (height - textHeight) * 0.5f * scale);
            Vector2 detailAt = captionAt + new Vector2(0f, (caption.Y + lineGap) * scale);

            if (!praising) return new TutorialCardLayout(glyphAt, captionAt, detailAt, scale, detailAt, scale);

            //The praise hangs under the text in the caption's column, and swells about its own centre, so the
            //words over it stay where the eye left them. Its own clamp keeps a word wider than the room right
            //of the caption's edge off the score — the instruction's cannot see it, and must not.
            Vector2 praiseCentre = captionAt + new Vector2(praise.X * 0.5f, textHeight + praiseGap + praise.Y * 0.5f) * scale;
            float praiseScale = scale * kick;
            if (praise.X > 0f)
            {
                float room = MathF.Min(centreX + halfStrip - praiseCentre.X, praiseCentre.X - (centreX - halfStrip));
                praiseScale = MathF.Min(praiseScale, MathF.Max(0f, 2f * room / praise.X));
            }

            Vector2 praiseAt = praiseCentre - praise * (0.5f * praiseScale);

            return new TutorialCardLayout(glyphAt, captionAt, detailAt, scale, praiseAt, praiseScale);
        }
    }
}

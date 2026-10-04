using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// A media player's symbol drawn as shapes (#784): ▶ play, ❚❚ pause and ⏭ next track, for the buttons of the About
    /// page's player and the Jukebox (#704). The owner's call: "the player understands an arrow, two bars side by side,
    /// and going to the next track — no text is needed, one symbol is enough".
    /// <para>
    /// <b>Shapes, not a font.</b> PromptFont carries ⏸ and ⏭ at a good size, but its ▶ (U+25B6) is a small triangle
    /// 0.56 of an em tall against the pause's 0.88 (read off the font's outlines), and its U+23F5 is a keycap; a ▶ set
    /// in a bigger size of the face grows the label's line, so the Play button would stand taller than Next beside it.
    /// Anton and Inter have no pause or next at all, and FontStashSharp drops a glyph a face lacks without a word. So
    /// the three are drawn from rectangles: the bars as two slabs, a triangle as one row of pixels at a time with the
    /// slanted edge's last pixel at the coverage it has, so the edge is smooth at every resolution. Drawn with
    /// <see cref="FlatBrush.Fill"/>, not <c>FillRectangle</c> (#712's stripe).
    /// </para>
    /// <para>
    /// The widget is as tall as the line of the label it stands in for, so a button holding it is exactly as tall as a
    /// worded one, and the shape is the display face's cap height, centred where Anton centres its capitals (the middle
    /// of the line, measured on the face: caps 0.859 of the em in a line of 1.505). Nothing is allocated per frame.
    /// </para>
    /// </summary>
    internal sealed class MediaGlyph : Widget
    {
        internal enum Symbol : byte { Play, Pause, Next }

        /// <summary>Which symbol is drawn; a page sets it as the player's state changes.</summary>
        public Symbol Shape { get; set; }

        /// <summary>The symbol's colour: the menu's text, or its dim aside while the button cannot act yet.</summary>
        public Color Tint { get; set; } = BS3DGame.MENU_TEXT;

        private readonly int _shapeHeight;

        /// <param name="shapeHeight">The symbol's height in pixels — the display face's cap height.</param>
        /// <param name="lineHeight">The height of the line it stands in for, which the widget takes.</param>
        public MediaGlyph(Symbol shape, int shapeHeight, int lineHeight)
        {
            Shape = shape;
            _shapeHeight = Math.Max(4, shapeHeight);
            Width = _shapeHeight * 6 / 5;
            Height = Math.Max(lineHeight, _shapeHeight);
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
        }

        public override void InternalRender(RenderContext context)
        {
            Rectangle bounds = ActualBounds;
            int h = _shapeHeight;
            int top = bounds.Y + (bounds.Height - h) / 2;
            int centreX = bounds.X + bounds.Width / 2;
            Color tint = Tint;

            switch (Shape)
            {
                case Symbol.Play:
                {
                    //An equilateral triangle's width, nudged right by a twelfth of it: a triangle's mass sits at its base,
                    //so one centred by its bounds reads left of centre beside the bars
                    float width = h * 0.87f;
                    int left = (int)MathF.Round(centreX - width / 2f + width / 12f);
                    Triangle(context, left, top, h, width, tint);
                    break;
                }

                case Symbol.Pause:
                {
                    int bar = Math.Max(1, (int)MathF.Round(h * 0.3f));
                    int gap = Math.Max(1, (int)MathF.Round(h * 0.22f));
                    int left = centreX - (2 * bar + gap) / 2;
                    FlatBrush.Fill(context, new Rectangle(left, top, bar, h), tint);
                    FlatBrush.Fill(context, new Rectangle(left + bar + gap, top, bar, h), tint);
                    break;
                }

                case Symbol.Next:
                {
                    //Two triangles nose to tail and a bar against the second's nose: ⏭
                    float width = h * 0.46f;
                    int bar = Math.Max(1, (int)MathF.Round(h * 0.14f));
                    float total = 2f * width + bar;
                    int left = (int)MathF.Round(centreX - total / 2f);
                    Triangle(context, left, top, h, width, tint);
                    Triangle(context, left + (int)MathF.Round(width), top, h, width, tint);
                    FlatBrush.Fill(context, new Rectangle(left + (int)MathF.Round(2f * width), top, bar, h), tint);
                    break;
                }
            }
        }

        /// <summary>
        /// A triangle pointing right, its upright edge at <paramref name="left"/>: one row of pixels at a time, each the
        /// width the row's middle crosses, and the row's last pixel at the coverage left over (the colour premultiplied,
        /// as the menu's batch blends), so the two slanted edges are smooth.
        /// </summary>
        private static void Triangle(RenderContext context, int left, int top, int height, float width, Color tint)
        {
            float half = height / 2f;

            for (int row = 0; row < height; row++)
            {
                float reach = width * (1f - MathF.Abs(row + 0.5f - half) / half);
                int whole = (int)reach;
                float partial = reach - whole;

                if (whole > 0) FlatBrush.Fill(context, new Rectangle(left, top + row, whole, 1), tint);
                if (partial > 0.02f) FlatBrush.Fill(context, new Rectangle(left + whole, top + row, 1, 1), tint * partial);
            }
        }
    }
}

using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// A checkbox's box and tick drawn as shapes (#816), for the two-state rows of the menus: Settings' On/Off rows and the
    /// note page's picture. The owner's question was whether the UI should have a checkbox at all, and the answer is that a
    /// row whose caption already says what is on reads faster as a tick than as the word "On" beside it.
    /// <para>
    /// <b>Shapes, not a font</b>, for <see cref="MediaGlyph"/>'s reason: Anton has no ☐ or ✓, and FontStashSharp drops a
    /// glyph a face lacks without a word. The box is four slabs. The tick is two strokes with round ends, and each row of
    /// pixels is filled across the span the strokes cover there (<see cref="CheckTick"/>), with the span's first and last
    /// pixel at the coverage it has (the colour premultiplied, as the menu's batch blends), so its slanted edges are smooth
    /// at every resolution. The spans are worked out once, when the widget is made, so nothing is computed or allocated
    /// per frame.
    /// </para>
    /// <para>
    /// The box is the display face's cap height, square, and the widget is as tall as the line it stands in for, so a
    /// button holding it is exactly as tall as a worded one, as <see cref="MediaGlyph"/>'s are.
    /// </para>
    /// </summary>
    internal sealed class CheckGlyph : Widget
    {
        /// <summary>Whether the tick is drawn.</summary>
        public bool Checked { get; set; }

        /// <summary>The box's and the tick's colour: the menu's text, or its dim aside while the row cannot act.</summary>
        public Color Tint { get; set; } = BS3DGame.MENU_TEXT;

        /// <summary>The box's frame, as a fraction of its side.</summary>
        private const float FRAME_STROKE = 0.1f;

        private readonly int _side, _frame;

        /// <summary>Per row of the box, the tick's spans across it (<see cref="CheckTick.Spans"/>).</summary>
        private readonly float[] _spans;

        /// <param name="side">The box's side in pixels: the display face's cap height.</param>
        /// <param name="lineHeight">The height of the line it stands in for, which the widget takes.</param>
        public CheckGlyph(int side, int lineHeight)
        {
            _side = Math.Max(8, side);
            _frame = Math.Max(1, (int)MathF.Round(_side * FRAME_STROKE));
            Width = _side;
            Height = Math.Max(lineHeight, _side);
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;

            _spans = CheckTick.Spans(_side);
        }

        public override void InternalRender(RenderContext context)
        {
            Rectangle bounds = ActualBounds;
            int left = bounds.X + (bounds.Width - _side) / 2;
            int top = bounds.Y + (bounds.Height - _side) / 2;
            Color tint = Tint;

            FlatBrush.Fill(context, new Rectangle(left, top, _side, _frame), tint);
            FlatBrush.Fill(context, new Rectangle(left, top + _side - _frame, _side, _frame), tint);
            FlatBrush.Fill(context, new Rectangle(left, top + _frame, _frame, _side - 2 * _frame), tint);
            FlatBrush.Fill(context, new Rectangle(left + _side - _frame, top + _frame, _frame, _side - 2 * _frame), tint);

            if (!Checked) return;

            for (int row = 0; row < _side; row++)
                for (int k = 0; k < 2; k++)
                {
                    float from = _spans[row * 4 + k * 2];
                    if (float.IsNaN(from)) continue;
                    Span(context, left, top + row, from, _spans[row * 4 + k * 2 + 1], tint);
                }
        }

        /// <summary>
        /// One row's span from <paramref name="from"/> to <paramref name="to"/> (in pixels from the box's left edge): the
        /// pixels it covers whole at full strength, and the pixel each end falls in at the share of it the span covers.
        /// </summary>
        private static void Span(RenderContext context, int left, int y, float from, float to, Color tint)
        {
            int first = (int)MathF.Floor(from), last = (int)MathF.Floor(to);
            if (first == last)
            {
                FlatBrush.Fill(context, new Rectangle(left + first, y, 1, 1), tint * (to - from));
                return;
            }

            FlatBrush.Fill(context, new Rectangle(left + first, y, 1, 1), tint * (first + 1 - from));
            if (last - first > 1) FlatBrush.Fill(context, new Rectangle(left + first + 1, y, last - first - 1, 1), tint);
            if (to > last) FlatBrush.Fill(context, new Rectangle(left + last, y, 1, 1), tint * (to - last));
        }
    }
}

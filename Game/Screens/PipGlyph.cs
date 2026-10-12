using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// One of the level picker's chapter pips drawn as a shape (#847): a disc for a chapter whose every level is cleared, a
    /// ring otherwise, bright for the chapter shown and dim for the rest.
    /// <para>
    /// <b>Shapes, not a font</b>, for <see cref="MediaGlyph"/>'s reason and one of the pips' own: Inter's ● and ○ were
    /// set in the menu's largest face so that the two could be told apart at all, which made them a heading-size mark
    /// on a page marker, and a glyph's ink is placed by the font's metrics, not centred on its plate — the owner saw
    /// both, "not centred on their background and needlessly huge". Drawn, the mark is centred on its widget to the
    /// pixel at any size, and the ring's stroke is a share of the diameter, so the two states stay apart however small
    /// the row is drawn.
    /// </para>
    /// <para>
    /// Each row of pixels is filled across the span the shape covers there, its two end pixels at the coverage they
    /// have (the colour premultiplied, as the menu's batch blends). The span is averaged over four sub-rows, so the
    /// rows a circle's top and bottom only clip are drawn as thin as they are covered. Worked out once, when the widget
    /// is made; nothing is computed or allocated per frame.
    /// </para>
    /// </summary>
    internal sealed class PipGlyph : Widget
    {
        /// <summary>A disc (every level of the chapter cleared) rather than a ring.</summary>
        public bool Filled { get; set; }

        /// <summary>The mark's colour: the menu's text for the chapter shown, its dim aside for the rest.</summary>
        public Color Tint { get; set; } = BS3DGame.MENU_TEXT_DIM;

        /// <summary>The ring's stroke as a share of the diameter: wide enough that a ring never reads as a disc.</summary>
        private const float RING_STROKE = 0.2f;

        private const int SUB_ROWS = 4;

        private readonly int _diameter;

        /// <summary>Per row, the outer circle's and the hole's half-widths across it, NaN where the row misses them.</summary>
        private readonly float[] _outer, _inner;

        /// <param name="diameter">The mark's diameter in pixels; the widget is that square.</param>
        public PipGlyph(int diameter)
        {
            _diameter = Math.Max(6, diameter);
            Width = _diameter;
            Height = _diameter;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;

            float radius = _diameter / 2f;
            float hole = radius * (1f - 2f * RING_STROKE);
            _outer = new float[_diameter];
            _inner = new float[_diameter];

            for (int row = 0; row < _diameter; row++)
            {
                _outer[row] = HalfWidth(row, radius);
                _inner[row] = HalfWidth(row, hole);
            }
        }

        /// <summary>
        /// The mean half-width a circle of <paramref name="radius"/>, centred on the widget, spans across a row: averaged
        /// over <see cref="SUB_ROWS"/> sub-rows, NaN where none of them touches it.
        /// </summary>
        private float HalfWidth(int row, float radius)
        {
            float centre = _diameter / 2f, sum = 0f;
            bool touched = false;

            for (int k = 0; k < SUB_ROWS; k++)
            {
                float y = row + (k + 0.5f) / SUB_ROWS - centre;
                float squared = radius * radius - y * y;
                if (squared <= 0f) continue;

                sum += MathF.Sqrt(squared);
                touched = true;
            }

            return touched ? sum / SUB_ROWS : float.NaN;
        }

        public override void InternalRender(RenderContext context)
        {
            Rectangle bounds = ActualBounds;
            int left = bounds.X + (bounds.Width - _diameter) / 2;
            int top = bounds.Y + (bounds.Height - _diameter) / 2;
            float centre = _diameter / 2f;
            Color tint = Tint;

            for (int row = 0; row < _diameter; row++)
            {
                float outer = _outer[row];
                if (float.IsNaN(outer)) continue;

                float inner = _inner[row];
                if (Filled || float.IsNaN(inner))
                {
                    Span(context, left, top + row, centre - outer, centre + outer, tint);
                    continue;
                }

                Span(context, left, top + row, centre - outer, centre - inner, tint);
                Span(context, left, top + row, centre + inner, centre + outer, tint);
            }
        }

        /// <summary>
        /// One row's span from <paramref name="from"/> to <paramref name="to"/> (in pixels from the mark's left edge): the
        /// pixels it covers whole at full strength, and the pixel each end falls in at the share of it the span covers —
        /// <see cref="CheckGlyph"/>'s rule.
        /// </summary>
        private static void Span(RenderContext context, int left, int y, float from, float to, Color tint)
        {
            if (to <= from) return;

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

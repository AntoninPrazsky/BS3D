using FontStashSharp;
using FontStashSharp.RichText;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace BS3D.Screens
{
    /// <summary>
    /// A Myra <see cref="Label"/> that carries its own backing under the type (#521): a second draw of the same
    /// text in a dark colour, through one of FontStashSharp's glyph effects — <see cref="FontSystemEffect.Stroked"/>
    /// dilates every glyph by <see cref="Style.Amount"/> pixels into an outline, <see cref="FontSystemEffect.Blurry"/>
    /// softens it into a halo, <see cref="FontSystemEffect.None"/> is a plain copy — optionally displaced by
    /// <see cref="Style.Offset"/> for a drop shadow. One widget, one <c>Text</c>, one <c>Visible</c>: the backing
    /// is drawn from the same string the type is, so nothing has to copy a line's text and visibility down onto
    /// a second label after the page has written it (which is what <c>ResultPage.SyncShadows</c> used to do).
    /// <para>
    /// <b>Why a subclass and not a font system.</b> #521 was filed against FontStashSharp 1.2's
    /// <c>FontSystemSettings.Effect</c>, which a whole font system was built with. The shipped 1.5.6 has no such
    /// setting: an effect is an argument of each <c>DrawText</c> call, and Myra's <c>RenderContext.DrawString</c>
    /// does not pass one. What Myra does expose is <see cref="RenderContext.DrawRichText"/>, and FontStashSharp's
    /// rich text takes the effect as a command in the string (<c>/es&lt;n&gt;</c> strokes, <c>/eb&lt;n&gt;</c>
    /// blurs), so the backing is one <see cref="RichTextLayout"/> whose text is the label's with that command in
    /// front. The glyph variants are rasterised into the atlas once, so the second draw costs one more string
    /// draw per line and nothing per glyph.
    /// </para>
    /// <para>
    /// The backing is laid out by the same class Myra's own label lays its text out with, at the label's own
    /// origin and width, so the two agree line for line. Meant for the unwrapped, left-aligned lines this game
    /// puts on the result page; a wrapping label hands its width to the backing too, but <c>TextAlign</c>'s
    /// horizontal offsets are not reproduced here.
    /// </para>
    /// </summary>
    internal sealed class ShadowedLabel : Label
    {
        /// <summary>How the backing is drawn: the glyph effect and its size in atlas pixels (a stroke's width, a
        /// blur's radius — already scaled by the caller, since fonts here are sized in pixels), the displacement
        /// of the whole backing under the type, and its colour.</summary>
        internal readonly record struct Style(FontSystemEffect Effect, int Amount, Point Offset, Color Color)
        {
            /// <summary>The rich-text command that asks FontStashSharp for this effect, or nothing for a plain copy.</summary>
            internal string Command => Effect switch
            {
                FontSystemEffect.Stroked => "/es" + Amount,
                FontSystemEffect.Blurry => "/eb" + Amount,
                _ => string.Empty,
            };
        }

        private readonly Style _style;
        private readonly string _command;
        private readonly RichTextLayout _backing = new() { SupportsCommands = true };

        //The Text the backing was last built from, compared by reference: a page writes its lines once per
        //refresh, so this is a pointer compare per frame and a string concatenation per change, never per draw.
        private string _backingSource;

        internal ShadowedLabel(in Style style)
        {
            _style = style;
            _command = style.Command;
        }

        public override void InternalRender(RenderContext context)
        {
            string text = Text;
            if (!string.IsNullOrEmpty(text) && Font != null)
            {
                if (!ReferenceEquals(text, _backingSource))
                {
                    _backingSource = text;
                    _backing.Text = _command + text;
                }

                if (!ReferenceEquals(_backing.Font, Font)) _backing.Font = Font;
                if (_backing.VerticalSpacing != VerticalSpacing) _backing.VerticalSpacing = VerticalSpacing;

                //A wrapping label wraps against the width it was given, and its backing must break the same lines;
                //null is "no wrapping" — a width of 0 wraps at every glyph, which photographed as the heading's
                //backing standing in a column down the page
                int? width = Wrap ? ActualBounds.Width : null;
                if (_backing.Width != width) _backing.Width = width;

                Rectangle bounds = ActualBounds;
                context.DrawRichText(_backing, new Vector2(bounds.X + _style.Offset.X, bounds.Y + _style.Offset.Y),
                    _style.Color, null, 0f, 0f, TextHorizontalAlignment.Left);
            }

            base.InternalRender(context);
        }
    }
}

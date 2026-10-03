using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;

namespace BS3D
{
    /// <summary>
    /// A flat slab of one colour, drawn from a texel of its own. It stands in for Myra's <c>SolidBrush</c> on every
    /// plate, button and tag of the menu, and for <c>RenderContext.FillRectangle</c> in the two widgets that draw bars
    /// (#712).
    /// <para>
    /// <b>Why not Myra's.</b> <c>SolidBrush</c> and <c>FillRectangle</c> both stretch <c>Stylesheet.Current.WhiteRegion</c>,
    /// which is a <b>single texel of Myra's default skin atlas</b> (1×1 at (901, 1) of <c>default_ui_skin.png</c>), and
    /// the atlas does not stop being a picture on either side of it: the texel to its right is
    /// (27, 161, 226). The last columns of a stretched quad sample into that neighbour, so every slab came out with a
    /// dark teal stripe down its right edge — 2 px on a plate, 1 px on a button, on every page — and the stripe is
    /// that blue times the slab's own tone: a plate's grey 42 gives 42/255 × (27, 161, 226) = (4, 26, 37), which is
    /// what the capture measured (premultiplied (5, 27, 38) at the plate's own alpha, #712). A 1×1 texture of our own
    /// has no neighbour to leak in, and the slab then ends exactly where its layout bounds do — two pixels further
    /// right than the plate used to read.
    /// </para>
    /// <para>
    /// The colour arithmetic is <c>SolidBrush.Draw</c>'s, line for line: the brush's colour, scaled channel by channel
    /// by the tint the widget draws with (white unless something fades it), so swapping one for the other changes
    /// nothing but that column.
    /// </para>
    /// </summary>
    internal sealed class FlatBrush : IBrush
    {
        //One texel for every slab, made when the first one is drawn — the brushes are static fields of the menu and
        //exist before the device does, so it cannot be built with them. A texture outlives a device reset.
        private static Texture2D _texel;

        private readonly Color _color;

        public FlatBrush(Color color)
        {
            _color = color;
        }

        public void Draw(RenderContext context, Rectangle dest, Color color)
        {
            Color tinted = color == Color.White
                ? _color
                : new Color(
                    _color.R * color.R / 255,
                    _color.G * color.G / 255,
                    _color.B * color.B / 255,
                    _color.A * color.A / 255);

            Fill(context, dest, tinted);
        }

        /// <summary>
        /// <c>context.FillRectangle</c> without the neighbour: a rectangle of <paramref name="color"/>, which goes to
        /// the render context as it stands, as <c>FillRectangle</c>'s did. For a widget that draws its own bars.
        /// </summary>
        internal static void Fill(RenderContext context, Rectangle rectangle, Color color)
        {
            if (_texel == null)
            {
                _texel = new Texture2D(MyraEnvironment.GraphicsDevice, 1, 1);
                _texel.SetData(new[] { Color.White });
            }

            context.Draw(_texel, rectangle, color);
        }
    }
}

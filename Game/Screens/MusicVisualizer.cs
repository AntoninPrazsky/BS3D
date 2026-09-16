using BS3D.Audio;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The About page's spectrum (#443): one bar per <see cref="ProceduralJukebox"/> band, standing on a common
    /// baseline, drawn straight into Myra's render pass so it lays out in the page's column like any other widget.
    /// <para>
    /// It marks level the way the whole menu marks state — <b>by brightness, never by hue</b> (the palette in
    /// <c>BS3DGame.Menu.cs</c>): a quiet band is a dim stub and a loud one a tall bright bar, so the display
    /// belongs to the same greys as the entries under it. It reads the bands the jukebox computed in its own
    /// update and allocates nothing, since it draws every frame the page is up.
    /// </para>
    /// </summary>
    internal sealed class MusicVisualizer : Widget
    {
        //A floor under every bar, so a silent display still reads as an instrument rather than as a gap
        private const float STUB = 0.035f;

        private readonly ProceduralJukebox _jukebox;
        private readonly int _gap;

        public MusicVisualizer(ProceduralJukebox jukebox, int width, int height, int gap)
        {
            _jukebox = jukebox;
            _gap = gap;

            Width = width;
            Height = height;
        }

        public override void InternalRender(RenderContext context)
        {
            ReadOnlySpan<float> bands = _jukebox.Bands;
            Rectangle bounds = ActualBounds;

            int count = bands.Length;
            int barWidth = Math.Max(1, (bounds.Width - _gap * (count - 1)) / count);
            int used = barWidth * count + _gap * (count - 1);
            int x = bounds.X + (bounds.Width - used) / 2;

            for (int b = 0; b < count; b++)
            {
                float level = MathF.Max(STUB, bands[b]);
                int height = Math.Max(1, (int)(level * bounds.Height));

                context.FillRectangle(new Rectangle(x, bounds.Bottom - height, barWidth, height),
                    BS3DGame.MENU_TEXT * (0.3f + 0.7f * level));

                x += barWidth + _gap;
            }
        }
    }
}

using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The touch screen's controls as the HUD draws them (#1051): the precise-aim toggle, the pause button, the floating
    /// stick under the thumb that moves the gun, and the power-up chips as buttons, which keep where they were drawn so a
    /// finger can press them.
    /// </summary>
    internal sealed partial class PlayHud
    {
        //The touch glyphs the cards and the chips share (TouchControls), and the pad's Menu button (a circle with three bars)
        //for the pause
        internal const string TOUCH_TAP_GLYPH = TouchControls.TAP_GLYPH;
        internal const string TOUCH_PRECISE_GLYPH = TouchControls.PRECISE_GLYPH;
        private const string TOUCH_PAUSE_GLYPH = "⇻";

        //The buttons' rims and fills: white over the scene like the HUD's text, and the precise-aim button's fill amber while
        //it is on, as the HUD marks what is in effect
        private const float TOUCH_RIM_ALPHA = 0.6f;
        private const float TOUCH_FILL_ALPHA = 0.28f;
        private const float TOUCH_RIM_WIDTH = 0.08f;
        private const float TOUCH_STICK_ALPHA = 0.18f;
        private const float TOUCH_KNOB_SHARE = 0.42f;

        //Each chip's rectangle as drawn this frame, by PowerupKind; empty for a chip not drawn
        private readonly Rectangle[] _chipBounds = new Rectangle[4];

        private void ForgetChipBounds() => Array.Clear(_chipBounds);

        private void NoteChipBounds(PowerupKind kind, float left, float top, float right, float bottom)
        {
            int reach = Scaled(HUD_SWAP_ROW_GAP);
            _chipBounds[(int)kind] = new Rectangle((int)left - reach, (int)top - reach,
                (int)(right - left) + 2 * reach, (int)(bottom - top) + 2 * reach);
        }

        /// <summary>Where a power-up's chip was drawn last frame, a margin round it; empty when it was not.</summary>
        internal Rectangle ChipBounds(PowerupKind kind) => _chipBounds[(int)kind];

        /// <summary>The touch controls, over everything else the HUD drew.</summary>
        internal void DrawTouchControls(TouchControls touch, Vector2 preciseCentre, float preciseRadius, bool preciseOn,
            Vector2 pauseCentre, float pauseRadius)
        {
            SpriteBatch batch = _game.OverlayBatch;
            batch.Begin();

            DrawTouchButton(batch, preciseCentre, preciseRadius, TOUCH_PRECISE_GLYPH, preciseOn);
            DrawTouchButton(batch, pauseCentre, pauseRadius, TOUCH_PAUSE_GLYPH, false);

            if (touch.StickActive)
            {
                //The base where the thumb landed, as far as a full push reaches, and the knob under the thumb inside it
                float height = _game.GraphicsDevice.Viewport.Height;
                float reach = (TouchControls.TAP_SLOP + TouchControls.STICK_RADIUS) * height;
                Vector2 origin = touch.StickOrigin;
                Vector2 offset = touch.StickThumb - origin;
                if (offset.Length() > reach) offset = Vector2.Normalize(offset) * reach;

                DrawDisc(batch, origin.X, origin.Y, reach, Color.White * TOUCH_STICK_ALPHA);
                DrawDisc(batch, origin.X, origin.Y, reach, Color.White * TOUCH_RIM_ALPHA, reach * (1f - TOUCH_RIM_WIDTH * 0.5f));
                float knob = reach * TOUCH_KNOB_SHARE;
                DrawDisc(batch, origin.X + offset.X, origin.Y + offset.Y, knob, Color.White * TOUCH_RIM_ALPHA);
            }

            batch.End();
        }

        private void DrawTouchButton(SpriteBatch batch, Vector2 centre, float radius, string glyph, bool on)
        {
            Color fill = on ? HUD_ACCENT * 0.85f : Color.Black * TOUCH_FILL_ALPHA;
            Color rim = on ? HUD_ACCENT : Color.White * TOUCH_RIM_ALPHA;

            DrawDisc(batch, centre.X, centre.Y, radius, fill);
            DrawDisc(batch, centre.X, centre.Y, radius, rim, radius * (1f - TOUCH_RIM_WIDTH));

            SpriteFontBase font = _game.HudFontChipPrompt;
            Vector2 size = font.MeasureString(glyph);
            Vector2 at = new(MathF.Round(centre.X - size.X * 0.5f), MathF.Round(centre.Y - InkCentreY(font, glyph)));
            DrawString(font, glyph, at, on ? Color.Black : BS3DGame.MENU_TEXT, 1f);
        }
    }
}

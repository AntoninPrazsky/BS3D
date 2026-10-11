using FontStashSharp;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The key or pad button that does a thing, drawn in the UI beside the thing it does (#799): B on Back, LB and RB on
    /// the tab row and the page turns, and so on. The owner's ask, with a pad in hand: the binding is part of the UI and
    /// always visible, never a legend somewhere else.
    /// <para>
    /// <b>One widget, two pictures.</b> It holds the pad's glyph and the keyboard's, and draws whichever the menus'
    /// hand is (<see cref="BS3DGame.PromptsForPad"/>), read as it renders. So a player who puts the mouse down and picks
    /// the pad up sees every prompt on the page change on the next frame, and nothing is rebuilt or relaid for it: the
    /// widget is as wide as the wider of the two and as tall as the line it stands beside, whichever it is showing.
    /// </para>
    /// <para>
    /// <b>Set by its ink, not its line box</b>, the way the HUD's chips set theirs (<c>PlayHud</c>): PromptFont centres
    /// the ink of every glyph used here on one line, read off the Y button, whose stored box is its ink. The glyph's ink
    /// centre is put on the widget's middle, which is where Anton centres its capitals, so a glyph beside a word stands
    /// level with it.
    /// </para>
    /// </summary>
    internal sealed class PromptGlyph : Widget
    {
        //PromptFont's own codepoints (Game/Content/Fonts/PromptFont.ttf), each rendered and looked at before it was used:
        //the cmap only says a codepoint exists. The Xbox buttons and the shoulders sit in the arrows block, the keycaps
        //in the control pictures block. PromptFont has no keycap for a single arrow key, only the cluster of four, so the
        //arrow keys are its bare bold arrows.
        internal const string PAD_A = "⇓";
        internal const string PAD_B = "⇒";
        internal const string PAD_X = "⇐";
        internal const string PAD_Y = "⇑";
        //The shoulders' full-height pills, not U+2198/U+2199, the HUD's Cut chip's RB: that pill is half the height of a
        //button's circle, and beside the tab row's words it read as small print (photographed). These stand as tall as
        //the B button and twice as wide.
        internal const string PAD_LB = "⮘";
        internal const string PAD_RB = "⮙";
        internal const string PAD_LT = "↖";
        internal const string PAD_RT = "↗";
        internal const string PAD_VIEW = "⇺";
        internal const string PAD_MENU = "⇻";
        internal const string PAD_RIGHT_STICK = "↻";
        internal const string PAD_LEFT_STICK_SIDEWAYS = "⇄";
        internal const string PAD_LEFT_STICK_UP_DOWN = "⇅";
        internal const string PAD_RIGHT_STICK_UP_DOWN = "⇵";
        internal const string MOUSE_WHEEL = "⟲";
        internal const string KEY_ENTER = "␮";
        internal const string KEY_ESCAPE = "␯";
        internal const string KEY_LEFT = "←";
        internal const string KEY_RIGHT = "→";

        private readonly BS3DGame _game;
        private readonly SpriteFontBase _font;
        private readonly string _pad, _keys;
        private readonly float _padWidth, _keysWidth, _inkCentre;

        /// <summary>The glyph's colour while what it stands beside can act; dimmed with it otherwise.</summary>
        public Color Tint { get; set; } = BS3DGame.MENU_TEXT;

        /// <summary>
        /// The entry the glyph stands beside rather than in, when it stands outside it - Help's LB beside Previous - so it
        /// dims when that entry is disabled, as it would inside it.
        /// </summary>
        public Widget StandsFor { get; set; }

        /// <param name="game">The host, whose <see cref="BS3DGame.PromptsForPad"/> picks the picture.</param>
        /// <param name="font">PromptFont at the size the prompt is drawn at.</param>
        /// <param name="pad">What a pad player presses.</param>
        /// <param name="keys">What a keyboard player presses.</param>
        /// <param name="lineHeight">The height of the line the glyph stands beside, which the widget takes.</param>
        public PromptGlyph(BS3DGame game, SpriteFontBase font, string pad, string keys, int lineHeight)
        {
            _game = game;
            _font = font;
            _pad = pad;
            _keys = keys;
            _padWidth = font.MeasureString(pad).X;
            _keysWidth = font.MeasureString(keys).X;
            _inkCentre = InkCentreY(font);

            Width = (int)MathF.Ceiling(MathF.Max(_padWidth, _keysWidth));
            Height = lineHeight;
            VerticalAlignment = VerticalAlignment.Center;
        }

        /// <summary>
        /// Where PromptFont's ink is centred down its line box at <paramref name="font"/>'s size, read off the Y button
        /// (see the class remarks for why that one glyph stands for all of them).
        /// </summary>
        internal static float InkCentreY(SpriteFontBase font)
        {
            Bounds ink = font.TextBounds(PAD_Y, Vector2.Zero);
            return (ink.Y + ink.Y2) * 0.5f;
        }

        public override void InternalRender(RenderContext context)
        {
            bool pad = _game.PromptsForPad;
            string text = pad ? _pad : _keys;
            float width = pad ? _padWidth : _keysWidth;

            Rectangle bounds = ActualBounds;
            Vector2 at = new(
                MathF.Round(bounds.X + (bounds.Width - width) * 0.5f),
                MathF.Round(bounds.Y + bounds.Height * 0.5f - _inkCentre));

            context.DrawString(_font, text, at, Usable() ? Tint : BS3DGame.MENU_TEXT_DIM, 0f);
        }

        /// <summary>
        /// Whether nothing the glyph stands in is disabled: a page turn with nowhere to go dims its word, and its key
        /// with it. A walk up a few parents, which allocates nothing.
        /// </summary>
        private bool Usable()
        {
            if (StandsFor is { Enabled: false }) return false;

            for (Widget widget = this; widget != null; widget = widget.Parent)
                if (!widget.Enabled) return false;

            return true;
        }
    }

    /// <summary>
    /// The content of a menu button that shows its own prompt (#799, <c>BS3DGame.MenuButton</c>'s prompted form): the word
    /// centred and the <see cref="PromptGlyph"/> at one end, overlaid. Its type is also the mark the focus cursor reads,
    /// so A is not drawn over an entry that already says what presses it.
    /// </summary>
    internal sealed class PromptedContent : Panel
    {
        public PromptedContent()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Center;
        }
    }
}

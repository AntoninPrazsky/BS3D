using FontStashSharp;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Prazsky.BS3D.Scoring;
using Prazsky.Core.Screens;
using System;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// One of the game's Myra screens on the <see cref="ScreenManager"/>'s stack: the splash, the main menu,
    /// the level picker, the pause menu, settings, the scene picker, about, the end-of-level result.
    /// <para>
    /// <b>What the stack buys here is the relationships, not the swapping.</b> Settings opened from a pause
    /// used to return to the pause because the code asked <c>_state == Paused</c>, and it dimmed the frame for
    /// the same reason — the screen had to work out where it had come from. On a stack it does not have to:
    /// the pause is literally underneath it, so backing out is a pop and "am I over a stopped game" is a
    /// question about the stack rather than about a state enum somewhere else.
    /// </para>
    /// <para>
    /// The play loop is a screen too now (<see cref="GameplayScreen"/>, #65), and the two flags below are
    /// what the whole stack was built for: every page shows the world behind it — the orbiting scene, or a
    /// stopped game — so it draws what is underneath; and whether what is underneath still <i>runs</i> is
    /// exactly whether that is a scene or a game.
    /// </para>
    /// </summary>
    internal abstract class MenuPage : Screen
    {
        protected readonly BS3DGame Game;

        /// <summary>
        /// Always: a menu page is an overlay, never the whole picture. The front end shows the scene turning
        /// behind it, and a pause shows the game it stopped — the manager goes on drawing down the stack.
        /// </summary>
        public override bool DrawsUnderlying => true;

        /// <summary>
        /// Whether what is underneath keeps running, and the stack itself answers: over the front end's
        /// backdrop the scene goes on turning, while any page over a <see cref="GameplayScreen"/> — the pause,
        /// the result screen, settings opened over either — freezes it. This is the case the flag exists for:
        /// a pause has to <i>draw</i> the game it stopped while stopping it <i>updating</i>.
        /// </summary>
        public override bool UpdatesUnderlying => Manager != null && !Manager.Contains<GameplayScreen>();

        /// <summary>
        /// The shared menu frame — the pointer, Escape and the display hotkeys, the pad navigation — is the
        /// host's, and exactly one page a frame asks for it: the one the player is actually on. A page under
        /// another (the pause under settings) still updates, but its input has been taken over.
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            if (IsActive) Game.UpdateMenuChrome(gameTime);
        }

        private Widget _root;
        private int _builtForLayout = -1;

        protected MenuPage(BS3DGame game) => Game = game;

        /// <summary>
        /// The widget tree, built on demand and rebuilt when the viewport has changed size enough to matter.
        /// <para>
        /// Lazy rather than eager: every size is a 2160p design figure resolved at build time (see
        /// <see cref="BS3DGame.MenuLayoutGeneration"/>), so a resize invalidates every page — but a page the
        /// player never opens never builds a tree at all, where the old code rebuilt all seven on every step
        /// of a window drag.
        /// </para>
        /// </summary>
        internal Widget Root
        {
            get
            {
                if (_root != null && _builtForLayout == Game.MenuLayoutGeneration) return _root;

                _builtForLayout = Game.MenuLayoutGeneration;
                _root = BuildTree();

                //A fresh tree starts at its authored defaults, and everything the page writes onto it — the
                //resume entry, the setting values, the marked scene, the score — was written onto the tree
                //that has just been thrown away
                Refresh();

                return _root;
            }
        }

        /// <summary>Builds this page's widgets at the current layout size. Called again after a resize.</summary>
        protected abstract Widget BuildTree();

        /// <summary>
        /// Whether Escape and the pad's B leave this page. False where there is nowhere to go back <i>to</i>:
        /// the main menu (quitting is an entry, and a front end that closes on a tapped key closes by
        /// accident) and the result screen (the level has already ended, so "back one level" means nothing).
        /// </summary>
        internal virtual bool CanGoBack => true;

        /// <summary>
        /// Re-reads everything that has to agree with the game's state — whether there is a session to resume,
        /// what each setting says, which scene is marked, the score just earned. Called when the page becomes
        /// the active one, because that is the only time it can change while nobody is looking at it, and
        /// again whenever the tree is rebuilt.
        /// </summary>
        internal virtual void Refresh() { }

        /// <summary>
        /// The pad/arrow focus cursor has moved onto <paramref name="focused"/> (null: it went down — the
        /// pointer is driving, whose hover the page's own events see). Raised by the host's
        /// <c>ApplyNavHighlight</c>, for the page that presents the focused entry somewhere other than on the
        /// entry itself — the level picker's detail line (#91). Most pages need nothing here.
        /// </summary>
        internal virtual void NavFocusChanged(Button focused) { }

        /// <summary>
        /// Whether this page dims the frame behind it. A pause dims hard, because what is behind it is a
        /// stopped game; the front end does not dim at all, because the rotating scene is the point of that
        /// screen. A page shared between the two — settings, scene, about — asks the <b>stack</b> whether a
        /// pause is underneath it, which is the question it was really asking all along.
        /// </summary>
        internal virtual bool DimsFrame => Manager != null && Manager.Contains<PausePage>();

        /// <summary>
        /// How far the scene behind this page has gone <b>out of focus</b>, 0–1 — the frame's own defocus,
        /// mixed in by the tonemap (<see cref="Prazsky.Core.Render.PostProcessPipeline.Resolve"/>). Zero for
        /// every page but the result screen, which is the one moment a page would rather soften what is behind
        /// it than darken it: see "The arena goes out of focus" in <see cref="ResultPage"/>.
        /// <para>
        /// Asked of the <b>active</b> screen only, at resolve time, so it is the value this page's own
        /// <c>Update</c> left this frame — a page that is not on top is not updated, and its answer would be
        /// however long ago it was covered.
        /// </para>
        /// </summary>
        internal virtual float FrameBlur => 0f;

        /// <summary>
        /// Putting the tree into the shared Myra desktop is done here, on <see cref="Screen.CoveredChanged"/>
        /// alone, and that is enough for every transition: the manager raises it on whichever page ends up on
        /// top after a frame's pushes and pops have been applied, so a push, a pop, a replace and a reset all
        /// arrive through the one path. There is one desktop and it holds one root, so only the top page's
        /// tree is ever in it.
        /// </summary>
        public override void CoveredChanged() => Game.ShowPage(this);

        #region The shared look, forwarded so a page's own build reads as its own

        //Every page is built out of the same handful of pieces, and they live on BS3DGame because the fonts,
        //the layout scale and the palette are the frame's rather than any one page's. Forwarding them here is
        //what lets a page's BuildTree read as a description of that page instead of as calls through a host.

        protected int Scaled(int designUnits) => Game.Scaled(designUnits);

        protected Thickness ScaledThickness(int horizontal, int vertical) => Game.ScaledThickness(horizontal, vertical);

        protected Thickness ScaledThickness(int left, int top, int right, int bottom) =>
            Game.ScaledThickness(left, top, right, bottom);

        protected Button MenuButton(string text, Action onClick) => Game.MenuButton(text, onClick);

        protected Button MenuButton(string text, Action onClick, out Label label) => Game.MenuButton(text, onClick, out label);

        protected VerticalStackPanel MenuColumn() => Game.MenuColumn();

        protected ScrollViewer MenuScroll(Widget content, int reservedDesignUnits) =>
            Game.MenuScroll(content, reservedDesignUnits);

        protected Label ScreenHeading(string text) => Game.ScreenHeading(text);

        protected Panel Plate(Widget content) => Game.Plate(content);

        /// <summary>
        /// The page centred over the whole frame. It carries no scrim of its own: whether the frame dims is
        /// still <see cref="DimsFrame"/>'s answer, but the wash itself is the host's own quad since #114 —
        /// Myra's background paint stops short of the viewport's bottom edge, and the strip it left undimmed
        /// was the bug.
        /// </summary>
        protected Panel ScreenRoot(Widget content) => BS3DGame.ScreenRoot(content);

        /// <summary>One level back off the stack — what every sub-page's "Back" entry does.</summary>
        protected void GoBack() => Manager.Pop();

        protected SpriteFontBase FontBody => Game.MenuFontBody;
        protected SpriteFontBase FontSmall => Game.MenuFontSmall;
        protected SpriteFontBase FontTitle => Game.MenuFontTitle;
        protected SpriteFontBase FontStars => Game.MenuFontStars;

        //The rating's two glyphs, shared so the picker's small rows and the result's headline cannot drift
        //apart. They must be drawn with FontStars or FontSmall: the glyphs live in Inter, and the display face
        //the loud type is set in carries neither — FontStashSharp would silently draw blanks.
        protected const char STAR_FILLED = '★';
        protected const char STAR_HOLLOW = '☆';

        /// <summary>
        /// The filled run of a rating — one ★ per star earned. It is drawn <b>separately from the hollow
        /// remainder</b> (<see cref="StarsRemaining"/>) rather than as one string, because since #139 the two
        /// halves are different colours everywhere they appear: the earned run carries the tier's colour
        /// (<see cref="BS3DGame.StarTierColor"/>) and the remainder recedes to <see cref="BS3DGame.STAR_EMPTY"/>.
        /// </summary>
        protected static string StarsEarned(int earned) =>
            new(STAR_FILLED, Math.Clamp(earned, 0, StarRating.MAX));

        /// <summary>The hollow remainder of a rating — what is left of <see cref="StarRating.MAX"/>.</summary>
        protected static string StarsRemaining(int earned) =>
            new(STAR_HOLLOW, StarRating.MAX - Math.Clamp(earned, 0, StarRating.MAX));

        #endregion
    }
}

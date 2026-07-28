using FontStashSharp;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
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
    /// The play loop is deliberately <b>not</b> a screen yet (#65), so the stack is empty for the whole of a
    /// level. That is a legitimate resting state (see <see cref="ScreenManager.Clear"/>).
    /// </para>
    /// </summary>
    internal abstract class MenuPage : Screen
    {
        protected readonly BS3DGame Game;

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
        /// Whether this page dims the frame behind it. A pause dims hard, because what is behind it is a
        /// stopped game; the front end does not dim at all, because the rotating scene is the point of that
        /// screen. A page shared between the two — settings, scene, about — asks the <b>stack</b> whether a
        /// pause is underneath it, which is the question it was really asking all along.
        /// </summary>
        internal virtual bool DimsFrame => Manager != null && Manager.Contains<PausePage>();

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

        protected Label ScreenHeading(string text) => Game.ScreenHeading(text);

        protected Panel Plate(Widget content) => Game.Plate(content);

        /// <summary>
        /// The page centred over the whole frame. The scrim is <b>not</b> passed: it is set per showing by
        /// <see cref="BS3DGame.ShowPage"/> from <see cref="DimsFrame"/>, since the same page dims over a pause
        /// and does not over the front end.
        /// </summary>
        protected Panel ScreenRoot(Widget content) => BS3DGame.ScreenRoot(content, scrim: null);

        /// <summary>One level back off the stack — what every sub-page's "Back" entry does.</summary>
        protected void GoBack() => Manager.Pop();

        protected SpriteFontBase FontBody => Game.MenuFontBody;
        protected SpriteFontBase FontSmall => Game.MenuFontSmall;
        protected SpriteFontBase FontTitle => Game.MenuFontTitle;

        #endregion
    }
}

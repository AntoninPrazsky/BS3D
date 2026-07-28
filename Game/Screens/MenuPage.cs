using Myra.Graphics2D.UI;
using Prazsky.Core.Screens;

namespace BS3D.Screens
{
    /// <summary>
    /// One of the game's Myra screens on the <see cref="ScreenManager"/>'s stack: the main menu, the pause
    /// menu, settings, the scene picker, about, the end-of-level result.
    /// <para>
    /// <b>What the stack buys here is the relationships, not the swapping.</b> Settings opened from a pause
    /// used to return to the pause because the code asked <c>_state == Paused</c>, and it dimmed the frame for
    /// the same reason — the screen had to work out where it had come from. On a stack it does not have to:
    /// the pause is literally underneath it, so backing out is a pop and "am I over a stopped game" is a
    /// question about the stack rather than about a state enum somewhere else.
    /// </para>
    /// <para>
    /// The play loop is deliberately <b>not</b> a screen yet, so the stack is empty for the whole of a level.
    /// That is a legitimate resting state (see <see cref="ScreenManager.Clear"/>) and it is what makes this a
    /// step rather than a rewrite: the pages moved onto the stack first, and the game body follows.
    /// </para>
    /// </summary>
    internal abstract class MenuPage : Screen
    {
        protected readonly BS3DGame Game;

        protected MenuPage(BS3DGame game) => Game = game;

        /// <summary>
        /// The widget tree this page shows. It is rebuilt at the viewport's size by the game's own layout pass
        /// — one tree per page, built once per size rather than per frame — so this reads it rather than
        /// holding it.
        /// </summary>
        internal abstract Widget Root { get; }

        /// <summary>
        /// Whether Escape and the pad's B leave this page. False where there is nowhere to go back <i>to</i>:
        /// the main menu (quitting is an entry, and a front end that closes on a tapped key closes by
        /// accident) and the result screen (the level has already ended, so "back one level" means nothing).
        /// </summary>
        internal virtual bool CanGoBack => true;

        /// <summary>
        /// Re-reads everything that has to agree with the game's state — whether there is a session to resume,
        /// what each setting says, which scene is marked, the score just earned. Called when the page becomes
        /// the active one, because that is the only time it can change while nobody is looking at it.
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
    }
}

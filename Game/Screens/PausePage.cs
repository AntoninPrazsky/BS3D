using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace BS3D.Screens
{
    /// <summary>
    /// Over a stopped game. Its own back is not a pop but a resume — the game has to start running again, and
    /// only the game knows that — so <see cref="BS3DGame.MenuBack"/> routes it.
    /// </summary>
    internal sealed class PausePage : MenuPage
    {
        public PausePage(BS3DGame game) : base(game) { }

        internal override bool DimsFrame => true;

        #region The stopped game goes out of focus

        //The frame behind a pause softens too (#178), and the result screen's argument runs the other way here:
        //that page delays the blur because its ending is worth watching, where a pause has nothing arriving —
        //the game stopped mid-move and the menu IS the subject from the first frame. So it starts at once. It is
        //still a ramp and not a cut: a frame that snapped out of focus reads as a stutter, which is the same
        //reason the title card fades rather than cutting in.
        //
        //The scrim stays (DimsFrame): what is behind this page is a stopped game rather than an ending playing
        //out, and #178 allows the darkening to remain where it is the blur that does the work.

        /// <summary>
        /// Short enough to be over before the hand has left the key, long enough not to be a cut. It is the
        /// whole of the effect's authored time, there being no delay in front of it.
        /// </summary>
        private const float BLUR_SECONDS = 0.35f;

        //Stamped against the WALL clock rather than advanced in Update, which is the LaserGrid's idiom and its
        //reason: a page pushed over this one (Settings, Scene) stops this page updating — MenuPage.UpdatesUnderlying
        //is false while a GameplayScreen is on the stack — so a ramp advanced here would freeze half way if the
        //player opened Settings within the first third of a second. As a function of the clock it simply carries on.
        private float _blurFrom;

        /// <summary>Stamps the ramp. Every arrival, so a second pause starts from a sharp frame again.</summary>
        public override void Enter() => _blurFrom = Game.WallClock;

        /// <inheritdoc/>
        /// <remarks>
        /// Smoothstepped like the result screen's, so the frame leaves focus with its rate at zero rather than
        /// lurching out of a still image — and <see cref="MathHelper.SmoothStep"/> clamps its own input, so this
        /// is flat at 1 for the rest of the pause however long the player leaves it standing.
        /// </remarks>
        internal override float FrameBlur =>
            MathHelper.SmoothStep(0f, 1f, (Game.WallClock - _blurFrom) / BLUR_SECONDS);

        #endregion

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();

            column.Widgets.Add(ScreenHeading("PAUSED"));
            column.Widgets.Add(MenuButton("Resume", Game.ResumeGame));
            column.Widgets.Add(MenuButton("Settings", Game.OpenSettings));
            column.Widgets.Add(MenuButton("Scene", Game.OpenSceneSelect));

            //Two entries clear of Resume rather than under it (#383): Resume is the one a player hits without
            //reading, and the entry right below it must not throw the run away. Grouped instead with Main Menu
            //and Quit below it — all three end the run in progress, where Settings and Scene above do not.
            //No confirm dialog: nothing on this page has one, Main Menu and Quit sit one click away and are
            //already more final than this, and building dialog machinery for one button would be new
            //infrastructure the codebase does not otherwise carry. No key either — WASD belong to the gameplay
            //screen underneath, the result screen's own Retry sets the precedent of menu-only, and no pause
            //entry has ever bound a letter.
            column.Widgets.Add(MenuButton("Restart", Game.RetryLevel));

            column.Widgets.Add(MenuButton("Main Menu", Game.ReturnToMainMenu));
            column.Widgets.Add(MenuButton("Quit", Game.Exit));

            return ScreenRoot(column);
        }
    }
}

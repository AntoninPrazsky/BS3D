using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Prazsky.BS3D.GameStructure;
using System.Collections.Generic;

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
            //With B and Escape on it (#799): the pause's back is a resume, so they are what does what Resume does
            column.Widgets.Add(BackButton("Resume", Game.ResumeGame));
            column.Widgets.Add(MenuButton("Settings", Game.OpenSettings));
            column.Widgets.Add(MenuButton("Scene", Game.OpenSceneSelect));

            //With the entries that leave the run standing (#813): a note is about the moment the game stopped on, and
            //the page comes back here
            column.Widgets.Add(Game.NoteEntry("pause"));

            //Two entries clear of Resume rather than under it (#383): Resume is the one a player hits without
            //reading, and the entry right below it must not throw the run away. Grouped instead with Main Menu
            //and Quit below it — the entries that leave the run, where Settings and Scene above do not (Main
            //Menu keeps the session for Continue; Restart and Quit lose it). Restart asks nothing: the player
            //asked to start again, and the two entries between it and Resume are its guard. No key either —
            //WASD belong to the gameplay screen underneath, the result screen's own Retry sets the precedent of
            //menu-only, and no pause entry has ever bound a letter.
            column.Widgets.Add(MenuButton("Restart", Game.RetryLevel));

            column.Widgets.Add(MenuButton("Main Menu", Game.ReturnToMainMenu));

            //Quit asks first (#826, which reverses #383's "no confirm dialog" for this entry alone): a player wrote from
            //this page that the game should always ask before it quits, and here it throws the run away besides
            column.Widgets.Add(MenuButton("Quit", Game.AskQuit));

            //The dev build's row (#876), under everything the player has: nothing at all in a release, where the kinds are none
            IReadOnlyList<BallKind> debugKinds = DebugKindRow.Kinds(BuildVersion.Release);
            _nextRound = null;
            if (debugKinds.Count > 0) column.Widgets.Add(BuildDebugRow(debugKinds));

            return ScreenRoot(column);
        }

        #region The debug row (#876)

        //Design units. The strip stands a clear gap under Quit so that it is not read as one more entry, and its own parts
        //are close: it is one small thing, a caption, the buttons and a line saying what they did
        private const int DEBUG_GAP = 60;
        private const int DEBUG_SPACING = 16;
        private const int DEBUG_BUTTON_GAP = 26;

        //The "next round" line, written by Refresh from what the paused level's gun holds. Null in a release.
        private Label _nextRound;

        /// <summary>
        /// The row: a caption that says plainly it is debug, one button per kind a shot can carry
        /// (<see cref="DebugKindRow.Kinds"/>: read off the enum, so a kind added there is a button here) and a line saying what
        /// the bore holds, so a press is seen to have done something behind the dimmed frame. The buttons are an ordinary
        /// menu entry's in everything but their width: the same type and the same slab height, which the focus prompt at the
        /// slab's right edge needs (on a shorter slab the key's cap stands out above it). They are cut to the column's width
        /// between them, the way the About page's two are, and at most <see cref="DebugKindRow.MAX_PER_LINE"/> to a line, so the
        /// strip never grows wider than the entries above it. They are added left to right under Quit, which is the order the
        /// pad walks them in; left and right along a line is the host's row rule (#805), as for the Jukebox's controls.
        /// </summary>
        private Widget BuildDebugRow(IReadOnlyList<BallKind> kinds)
        {
            VerticalStackPanel strip = new()
            {
                Spacing = Scaled(DEBUG_SPACING),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, DEBUG_GAP, 0, 0),
            };

            strip.Widgets.Add(new Label
            {
                Text = DebugKindRow.CAPTION,
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            int perLine = DebugKindRow.PerLine(kinds.Count);
            int width = (ColumnWidth - (perLine - 1) * Scaled(DEBUG_BUTTON_GAP)) / perLine;
            HorizontalStackPanel line = null;

            for (int i = 0; i < kinds.Count; i++)
            {
                if (i % perLine == 0)
                {
                    line = new HorizontalStackPanel { Spacing = Scaled(DEBUG_BUTTON_GAP), HorizontalAlignment = HorizontalAlignment.Center };
                    strip.Widgets.Add(line);
                }

                BallKind kind = kinds[i];
                Button button = MenuButton(DebugKindRow.Name(kind), () => Game.LoadDebugKind(kind));
                button.Width = width;
                button.Padding = ScaledThickness(16, 18);
                line.Widgets.Add(button);
            }

            _nextRound = new Label { Font = FontSmall, TextColor = BS3DGame.MENU_TEXT_DIM, HorizontalAlignment = HorizontalAlignment.Center };
            strip.Widgets.Add(_nextRound);
            Refresh();

            return strip;
        }

        /// <inheritdoc/>
        /// <remarks>Writes the line under the debug row: what the gun's next round is, read off the session each time the page
        /// comes up and after a press, so it is never a memory of what was clicked.</remarks>
        internal override void Refresh()
        {
            if (_nextRound != null) _nextRound.Text = "Next round: " + Game.NextRoundDescription;
        }

        #endregion
    }
}

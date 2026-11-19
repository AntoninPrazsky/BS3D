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
            //The dev build's row (#876), under everything the player has: nothing at all in a release, where the kinds are none
            IReadOnlyList<BallKind> debugKinds = DebugKindRow.Kinds(BuildVersion.Release);
            _nextRound = null;
            _card = null;
            _beside = null;

            //On a phone held landscape (#1184), the entries beside the level's card rather than one column over empty sides
            if (UsesTheWidth && Game.HasSession) return BuildBesideTheCard(debugKinds);

            //Stacked: a monitor's, and an enlarged interface's on a screen too square for the card beside the entries, where
            //they are touch targets as everywhere on it (PhoneTarget, nothing on a monitor) and the page's fit brings the
            //column inside the height
            VerticalStackPanel column = MenuColumn();

            column.Widgets.Add(ScreenHeading("PAUSED"));
            //With B and Escape on it (#799): the pause's back is a resume, so they are what does what Resume does
            column.Widgets.Add(PhoneTarget(BackButton("Resume", Game.ResumeGame)));
            column.Widgets.Add(PhoneTarget(MenuButton("Settings", Game.OpenSettings)));
            column.Widgets.Add(PhoneTarget(MenuButton("Scene", Game.OpenSceneSelect)));

            //With the entries that leave the run standing (#813): a note is about the moment the game stopped on, and
            //the page comes back here
            column.Widgets.Add(PhoneTarget(Game.NoteEntry("pause")));

            //Two entries clear of Resume rather than under it (#383): Resume is the one a player hits without
            //reading, and the entry right below it must not throw the run away. Grouped instead with Main Menu
            //and Quit below it — the entries that leave the run, where Settings and Scene above do not (Main
            //Menu keeps the session for Continue; Restart and Quit lose it). Restart asks nothing: the player
            //asked to start again, and the two entries between it and Resume are its guard. No key either —
            //WASD belong to the gameplay screen underneath, the result screen's own Retry sets the precedent of
            //menu-only, and no pause entry has ever bound a letter.
            column.Widgets.Add(PhoneTarget(MenuButton("Restart", Game.RetryLevel)));

            column.Widgets.Add(PhoneTarget(MenuButton("Main Menu", Game.ReturnToMainMenu)));

            //Quit asks first (#826, which reverses #383's "no confirm dialog" for this entry alone): a player wrote from
            //this page that the game should always ask before it quits, and here it throws the run away besides
            column.Widgets.Add(PhoneTarget(MenuButton("Quit", Game.AskQuit)));

            if (debugKinds.Count > 0) column.Widgets.Add(BuildDebugRow(debugKinds));

            return ScreenRoot(column);
        }

        #region The phone's pause: the entries beside the level's card (#1181, #1184)

        //Design units: the card's width (the level picker's side panel's, whose card it is), between the two columns of entries,
        //and above the attempt's lines, which part them from the level's own
        private const int CARD_WIDTH = 960;
        private const int ENTRY_COLUMN_GAP = 26;
        private const int RUN_GAP = 48;

        //The level's card and the attempt's three lines under it, written by Refresh; and the row the entries and the card stand
        //in, whose width the page needs. Null off the phone's layout
        private LevelCard _card;
        private Label _runScore, _runBalls, _runNextStar;
        private Widget _beside;

        /// <summary>
        /// The phone's pause (#1181, #1184): the entries in two columns, each a touch target tall (<see cref="MenuPage.PhoneTarget"/>),
        /// and the level's card beside them: which level it is, the player's stars and best on it, its rules, and the attempt
        /// so far (its score, the balls it has left and what the next star costs). Stacked, the seven entries stood in one
        /// column at a quarter of the width, 5.6 mm tall, over a blurred frame with nothing at the sides; at a finger's height
        /// seven do not fit the phone's.
        /// <para>
        /// The columns part the entries as #383 grouped them: the ones that leave the run standing (Resume, Settings, Scene,
        /// the note) and the ones that leave it (Restart, Main Menu, Quit), so Restart is a column away from Resume rather than
        /// under it, and the second column stands at the foot of the first, Quit lowest. Added in the list's order, so the pad
        /// walks them as it walks the stacked column; left and right cross between the columns by the host's row rule (#805).
        /// The dev build's row stands under the card, clear of the entries.
        /// </para>
        /// </summary>
        private Widget BuildBesideTheCard(IReadOnlyList<BallKind> debugKinds)
        {
            VerticalStackPanel entries = MenuColumn();
            entries.Widgets.Add(ScreenHeading("PAUSED"));

            VerticalStackPanel stay = MenuColumn();
            stay.VerticalAlignment = VerticalAlignment.Top;
            stay.Widgets.Add(PhoneTarget(BackButton("Resume", Game.ResumeGame)));
            stay.Widgets.Add(PhoneTarget(MenuButton("Settings", Game.OpenSettings)));
            stay.Widgets.Add(PhoneTarget(MenuButton("Scene", Game.OpenSceneSelect)));
            stay.Widgets.Add(PhoneTarget(Game.NoteEntry("pause")));

            //At the columns' foot, so Quit stands lowest, under where the Quit confirm's pair stands over this page: a double tap
            //on Quit lands on nothing (the confirm's own guard, #826), where level with the third row it landed on its Quit
            VerticalStackPanel leave = MenuColumn();
            leave.VerticalAlignment = VerticalAlignment.Bottom;
            leave.Widgets.Add(PhoneTarget(MenuButton("Restart", Game.RetryLevel)));
            leave.Widgets.Add(PhoneTarget(MenuButton("Main Menu", Game.ReturnToMainMenu)));
            leave.Widgets.Add(PhoneTarget(MenuButton("Quit", Game.AskQuit)));

            HorizontalStackPanel columns = new() { Spacing = Scaled(ENTRY_COLUMN_GAP), HorizontalAlignment = HorizontalAlignment.Center };
            columns.Widgets.Add(stay);
            columns.Widgets.Add(leave);
            entries.Widgets.Add(columns);

            //The level picker's card, and the attempt's lines under it
            _card = new LevelCard(this, Scaled(CARD_WIDTH));
            VerticalStackPanel card = _card.Root;
            _runScore = new Label { Font = FontBody, TextColor = BS3DGame.MENU_TEXT, Margin = ScaledThickness(0, RUN_GAP, 0, 0) };
            _runBalls = new Label { Font = FontSmall, TextColor = BS3DGame.MENU_TEXT_BODY };
            _runNextStar = new Label { Font = FontSmall, TextColor = BS3DGame.MENU_TEXT_BODY };
            card.Widgets.Add(_runScore);
            card.Widgets.Add(_runBalls);
            card.Widgets.Add(_runNextStar);

            VerticalStackPanel side = new() { VerticalAlignment = VerticalAlignment.Center };
            side.Widgets.Add(Plate(card));
            if (debugKinds.Count > 0) side.Widgets.Add(BuildDebugRow(debugKinds));

            HorizontalStackPanel row = new()
            {
                Spacing = Scaled(WideMenuLayout.RAIL_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            row.Widgets.Add(entries);
            row.Widgets.Add(side);
            _beside = row;

            return ScreenRoot(row);
        }

        /// <summary>
        /// The entries and the card side by side, laid out unbounded (#1184): wider than a stacked page, so on a screen
        /// narrower than the X300 Ultra's 2.2:1 <c>FitPage</c> brings the page down to fit it rather than cut the card off.
        /// </summary>
        internal override int RequiredWidth => _beside != null ? UnboundedWidth(_beside) : 0;

        //The card for the level the session plays and the attempt as it stands: on every showing (Refresh), since the tree is
        //kept between levels and a pause is the one time the attempt is looked at here
        private void WriteCard()
        {
            if (_card == null || !Game.HasSession) return;

            int level = Game.SessionLevel;
            (int? ballsLeft, int score, int levelBalls) = Game.SessionRun;

            _card.Show(level, open: true);
            _runScore.Text = PickerCopy.CardScore(score);
            _runBalls.Text = PickerCopy.CardBallsLeft(ballsLeft);
            _runBalls.Visible = _runBalls.Text.Length > 0;
            _runNextStar.Text = PickerCopy.CardNextStar(Game.LevelStars(level), levelBalls);
            _runNextStar.Visible = _runNextStar.Text.Length > 0;
        }

        #endregion

        #region The debug row (#876)

        //Design units. The strip stands a clear gap under Quit (on the phone's layout, under the level's card) so that it is not
        //read as one more entry, and its own parts are close: it is one small thing, a caption, the buttons and a line saying
        //what they did
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
        /// strip never grows wider than the entries above it. They are added left to right under Quit (on the phone's layout,
        /// under the level's card, <see cref="BuildBesideTheCard"/>), which is the order the pad walks them in; left and right
        /// along a line is the host's row rule (#805), as for the Jukebox's controls.
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
        /// comes up and after a press, so it is never a memory of what was clicked. On the phone, the level's card too (#1184).</remarks>
        internal override void Refresh()
        {
            if (_nextRound != null) _nextRound.Text = "Next round: " + Game.NextRoundDescription;
            WriteCard();
        }

        #endregion
    }
}

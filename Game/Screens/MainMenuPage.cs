using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;
using VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment;

namespace BS3D.Screens
{
    /// <summary>
    /// The front end. No back — quitting is an entry on it, and a menu that closes when a key is tapped is one
    /// that closes by accident. It never dims: the scene turning behind it is the whole point of the screen.
    /// <para>
    /// <b>It is the one page laid out as a composition rather than as a stack (#217)</b>, and that follows from
    /// the line above. Every other page is a centred column, which is right for a panel that has the frame's
    /// attention — but the centre of this frame is where the island, the drain and the turning scene are, and a
    /// centred column of entries with the game's name centred over it covered exactly the thing the page exists
    /// to show. The name is pinned top-right and the entries bottom-left, so the middle is left to the scene
    /// and the two blocks of type balance across the diagonal. The pause and both pickers deliberately stay
    /// centred: what is behind a pause is a stopped game rather than a view worth keeping clear, and a picker is
    /// a grid that wants the whole frame.
    /// </para>
    /// <para>
    /// <b>Half of that composition is no longer in this tree (#248).</b> The name is still pinned top-right at
    /// the same inset, but it is 3D lettering standing in the scene — <see cref="Effects.TitleWordmark"/>, drawn
    /// by <see cref="BackdropScreen"/> — rather than a label laid over it, so what this page holds is the
    /// bottom-left half and the corner the other half is measured from
    /// (<see cref="FRONT_INSET"/>). The composition it describes is unchanged; only which layer draws it is.
    /// </para>
    /// </summary>
    internal sealed class MainMenuPage : MenuPage
    {
        /// <summary>
        /// How far the composition is held off the frame's edges, in the 2160p design units everything else on
        /// the page is authored in. One figure for both corners, so the name's distance from its edges and the
        /// column's from its own are the same measurement rather than two that drifted apart.
        /// <para>
        /// Internal since #248, and for exactly that reason: the game's name left this tree to become 3D
        /// lettering in the scene (<see cref="Effects.TitleWordmark"/>), and it holds the SAME inset off the
        /// top and right edges that the entry column below holds off the bottom and left. The wordmark works
        /// in fractions of the frame rather than in Myra's design units, so the host converts this figure
        /// once when it builds it — it does not restate it.
        /// </para>
        /// </summary>
        internal const int FRONT_INSET = 130;

        /// <summary>
        /// The quality notice's wrap width. Narrower than it was when it sat under a centred column: in the
        /// bottom-left stack it is read against the entries beside it, and a paragraph twice their width read
        /// as the page's subject rather than as a footnote to a change nobody asked for.
        /// </summary>
        private const int NOTICE_WIDTH = 1240;

        private Button _resumeButton;
        private Label _playLabel;
        private Label _qualityNotice;

        //Held across a rebuild: the notice is raised by the adaptive path, which may run long before this page
        //is ever shown and will not run again to re-raise it on the tree built after a resize
        private string _noticeText;

        public MainMenuPage(BS3DGame game) : base(game) { }

        internal override bool CanGoBack => false;
        internal override bool DimsFrame => false;

        //The front end rests its entries almost to nothing — big unplated type needs no slab, and the six grey
        //bars over the turning scene were the point of the complaint. See BS3DGame.MENU_FRONT_BUTTON.
        internal override IBrush EntryRestBrush => BS3DGame.MENU_FRONT_BUTTON_BRUSH;

        protected override Widget BuildTree()
        {
            //THE GAME'S NAME IS NOT IN THIS TREE ANY MORE (#248). It was a Label pinned to this corner in the
            //display face at 240 design units — no plate, no frame, no effect — and the owner's complaint was
            //that at that it whispered: the title has to be the loudest thing on the screen. It is now 3D
            //lettering standing in the scene itself (Effects.TitleWordmark, drawn by BackdropScreen), which
            //Myra cannot do at all: a widget is a sprite over the frame, and a wordmark that turns, drifts and
            //catches the scene's light is geometry in it. The corner and the inset are unchanged, so the
            //composition this class exists to describe is the same one — the name is still pinned top-right
            //and the entries bottom-left, and the middle is still left to the scene.

            VerticalStackPanel column = MenuColumn();

            //MenuColumn is centred, which is what every other page wants; this one hangs off the bottom-left
            //corner instead. Set here rather than as another argument on the shared helper — one page does
            //this, and a knob nobody else turns is a knob that goes stale.
            column.HorizontalAlignment = HorizontalAlignment.Left;
            column.VerticalAlignment = VerticalAlignment.Bottom;
            column.Margin = ScaledThickness(FRONT_INSET, 0, 0, FRONT_INSET);

            //Hidden unless the adaptive path actually lowered something (see TuneQualityToFrameRate). A player
            //whose machine copes never learns this exists, which is the point: it explains a change they did
            //not ask for, and is not itself a setting.
            //
            //ABOVE the entries rather than under them, which it was while the column was centred. The stack
            //hangs off the bottom now, so height added anywhere in it pushes everything above that point up:
            //as the last child this would have shifted all six entries the moment the probe spoke, and a menu
            //that moves under the pointer is worse than a notice in an odd place.
            _qualityNotice = new Label
            {
                Text = string.Empty,
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                Wrap = true,
                Width = Scaled(NOTICE_WIDTH),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = ScaledThickness(0, 0, 0, 40),

                //Its own backing, because the front end deliberately has no scrim — the rotating scene is the
                //point of that screen — and a line of small text over open water or a lit skyline is exactly
                //what a plate exists for. Buttons carry their own; this is the only prose here that does not.
                Background = new SolidBrush(BS3DGame.MENU_PLATE),
                Padding = ScaledThickness(34, 18),

                Visible = false,
            };
            column.Widgets.Add(_qualityNotice);

            _resumeButton = FrontEndEntry("Continue", Game.ContinueGame);
            column.Widgets.Add(_resumeButton);

            column.Widgets.Add(FrontEndEntry("Play", Game.OpenLevelSelect, out _playLabel));
            column.Widgets.Add(FrontEndEntry("Scene", Game.OpenSceneSelect));
            column.Widgets.Add(FrontEndEntry("Settings", Game.OpenSettings));
            column.Widgets.Add(FrontEndEntry("About", Game.OpenAbout));
            column.Widgets.Add(FrontEndEntry("Quit", Game.Exit));

            return ScreenRoot(column);
        }

        internal override void Refresh()
        {
            if (_resumeButton == null) return;

            //Resuming is only offered when there is something to resume, and the play entry says plainly that
            //pressing it again deals a new cluster rather than continuing this one
            _resumeButton.Visible = Game.HasSession;
            _playLabel.Text = Game.HasSession ? "New Game" : "Play";

            _qualityNotice.Text = _noticeText ?? string.Empty;
            _qualityNotice.Visible = _noticeText != null;
        }

        /// <summary>
        /// Explains a quality change the player did not ask for. Kept as text rather than written straight
        /// onto the label, because the adaptive path raises it while the front end may not have been built or
        /// shown yet — and because a resize throws the label away.
        /// </summary>
        internal void ShowQualityNotice(QualityLevel quality)
        {
            //Names the tier rather than the supersample factor it used to name: a tier step turns down more than
            //antialiasing now, and saying "antialiasing" would be telling the player the wrong thing.
            _noticeText = $"Quality lowered to {quality} for a smoother frame rate — change it in Settings.";

            Refresh();
        }

        /// <summary>The notice has been answered: the player has set the dial themselves.</summary>
        internal void ClearQualityNotice()
        {
            _noticeText = null;

            Refresh();
        }
    }
}

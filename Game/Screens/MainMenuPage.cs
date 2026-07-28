using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// The front end. No back — quitting is an entry on it, and a menu that closes when a key is tapped is one
    /// that closes by accident. It never dims: the scene turning behind it is the whole point of the screen.
    /// </summary>
    internal sealed class MainMenuPage : MenuPage
    {
        private const int NOTICE_WIDTH = 1860;

        private Button _resumeButton;
        private Label _playLabel;
        private Label _qualityNotice;

        //Held across a rebuild: the notice is raised by the adaptive path, which may run long before this page
        //is ever shown and will not run again to re-raise it on the tree built after a resize
        private string _noticeText;

        public MainMenuPage(BS3DGame game) : base(game) { }

        internal override bool CanGoBack => false;
        internal override bool DimsFrame => false;

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();

            //The title carries no plate and no frame: at this size the letters are their own mass, and a
            //frame around them would be one more thing competing with whichever scene is turning behind it.
            //"BS3D" is the repository's and the assembly's shorthand; the game's name is spelled out.
            column.Widgets.Add(new Label
            {
                Text = BS3DGame.GAME_TITLE,
                Font = FontTitle,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 60),
            });

            _resumeButton = MenuButton("Continue", Game.ContinueGame);
            column.Widgets.Add(_resumeButton);

            column.Widgets.Add(MenuButton("Play", Game.OpenLevelSelect, out _playLabel));
            column.Widgets.Add(MenuButton("Scene", Game.OpenSceneSelect));
            column.Widgets.Add(MenuButton("Settings", Game.OpenSettings));
            column.Widgets.Add(MenuButton("About", Game.OpenAbout));
            column.Widgets.Add(MenuButton("Quit", Game.Exit));

            //Hidden unless the adaptive path actually lowered something (see TuneQualityToFrameRate). A player
            //whose machine copes never learns this exists, which is the point: it explains a change they did
            //not ask for, and is not itself a setting.
            _qualityNotice = new Label
            {
                Text = string.Empty,
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                Wrap = true,
                Width = Scaled(NOTICE_WIDTH),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 40, 0, 0),

                //Its own backing, because the front end deliberately has no scrim — the rotating scene is the
                //point of that screen — and a line of small text over open water or a lit skyline is exactly
                //what a plate exists for. Buttons carry their own; this is the only prose here that does not.
                Background = new SolidBrush(BS3DGame.MENU_PLATE),
                Padding = ScaledThickness(34, 18),

                Visible = false,
            };
            column.Widgets.Add(_qualityNotice);

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

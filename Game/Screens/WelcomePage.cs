using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// The author's note on the Raspberry Pi build's first launch (#1083): what the game is, that it is early and
    /// one person's, that the score server is a Pi too, and thanks. What it says and when it shows are
    /// <see cref="WelcomeNote"/>'s. It stands on a prose plate over the main menu, as About does.
    /// <para>
    /// <b>Let's play is first and is where the cursor lands</b> (<see cref="NavArrival"/>), so Enter, a click or the
    /// pad's A goes on to the game. Escape and the pad's B do the same, through the stack's back. Write to the Author is
    /// the sea-teal entry the pause page and About carry; the note page it opens comes back here.
    /// </para>
    /// <para>
    /// <b>Nothing pressed in its first <see cref="INPUT_GRACE_SECONDS"/> counts</b>, as on the nickname plate. Any key
    /// skips the title card, and the note comes up on the next frame, so a player tapping through the logo would
    /// otherwise close it unread.
    /// </para>
    /// <para>
    /// <b>Leaving it writes <c>welcomeShown</c></b> (<see cref="Leave"/>), however it is left, so it never comes back. A
    /// run that ends while the note is still up has not shown it, and the next launch shows it again. The nickname
    /// question (#763) waits for the main menu to be the page up, so it follows the note.
    /// </para>
    /// </summary>
    internal sealed class WelcomePage : MenuPage
    {
        //The nickname plate's grace (#763), for the same reason: the press that skipped the title card
        private const float INPUT_GRACE_SECONDS = 0.8f;

        private Button _play;
        private float _arrivedAt;

        public WelcomePage(BS3DGame game) : base(game) { }

        internal override Button NavArrival => _play;

        //Escape and the pad's B are the stack's back, held through the grace like the buttons
        internal override bool CanGoBack => !InGrace;

        /// <summary>Whether the note has been up long enough to be answered - see <see cref="INPUT_GRACE_SECONDS"/>.</summary>
        private bool InGrace => Game.WallClock - _arrivedAt < INPUT_GRACE_SECONDS;

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading(WelcomeNote.HEADING));

            //Paragraphs at the prose measure, as About's are; the entry's words set strong where the sentence names them
            VerticalStackPanel body = new();
            foreach (string paragraph in WelcomeNote.Paragraphs(BS3DGame.GAME_TITLE))
                body.Widgets.Add(Paragraph(paragraph.Replace(BS3DGame.NOTE_ENTRY, Strong(BS3DGame.NOTE_ENTRY)), ProseMeasure));

            //The sign-off in the loud white, flush with the paragraphs, and a double gap before the entries
            Label signature = Prose(WelcomeNote.SIGNATURE, ProseMeasure, BS3DGame.MENU_TEXT, ProseLeading,
                new Thickness(0, 0, 0, ParagraphGap * 2));
            body.Widgets.Add(signature);
            column.Widgets.Add(body);

            _play = BackButton(WelcomeNote.PLAY, Play);
            column.Widgets.Add(_play);
            column.Widgets.Add(Game.NoteEntry("welcome"));

            //A window too short for the whole note scrolls the note, not the entries (#1052's way)
            ScrollsWhenTall(column, body);
            return ScreenRoot(Plate(column, prose: true));
        }

        public override void Enter()
        {
            base.Enter();
            _arrivedAt = Game.WallClock;
            System.Console.WriteLine("[welcome] The author's note is up");
        }

        public override void Leave()
        {
            base.Leave();
            Game.MarkWelcomeShown();
        }

        /// <summary>The Let's play entry: back to the main menu, the same pop Escape makes.</summary>
        private void Play()
        {
            if (!InGrace) GoBack();
        }
    }
}

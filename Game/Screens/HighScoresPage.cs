using BS3D.Online;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using System;
using System.Collections.Generic;
using System.Globalization;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>High Scores (#685): every level's board at a glance</b> — the main menu's door onto the online boards as a whole,
    /// where the level picker's button (#547) opens one level's at a time. A chapter to a page, as the picker pages its tiles,
    /// and each unlocked level one row: its board's #1 and the player's own place, for this month or for all time.
    /// <para>
    /// <b>What it ranks is the levels, decided in the owner's stead</b> (the issue's option (a)): a leaderboard to climb is a
    /// row saying who leads a level and where the player stands on it, and a level the player leads says so in gold. A
    /// ranking of players across the campaign (option (b)) needs a rule weighing levels with different ceilings against
    /// each other, and is left for later.
    /// </para>
    /// <para>
    /// <b>One request per period, not one per level.</b> The overview is <c>GET /v1/boards</c> (BS3D-API#7, additive to
    /// contract v1): every board's #1 and the asker's place in one answer, where the per-board GET would take a hundred and
    /// thirty against a Cloudflare rule of fifty in ten seconds. A service older than that answers 404, and the page says
    /// the server does not offer the overview yet rather than that it is away.
    /// </para>
    /// <para>
    /// <b>A locked level shows its name and nothing of its board</b>, as the picker shows a lock: #266 keeps a level ahead
    /// of the player from showing its shape, and a board's top score is that shape in numbers. A row of an unlocked level
    /// opens that level's full boards (<c>OpenLevelBoard</c>), so the overview is also the way into every board.
    /// </para>
    /// <para>
    /// <b>Opted out, the page still opens</b>: it says what turning online scores on gives and offers Settings, the result
    /// page's own hint (#548) made into a door. The menu entry is a reason to opt in, so hiding it would hide the reason.
    /// </para>
    /// </summary>
    internal sealed class HighScoresPage : MenuPage
    {
        //A row: the level, its #1, the player's place. Wide enough for a sixteen-letter name beside its score in the small
        //face, and for "You: 12th of 130 · 123 456"; the three together are about the picker's band of five tiles.
        private const int NAME_WIDTH = 620;
        private const int TOP_WIDTH = 860;
        private const int ME_WIDTH = 760;
        private const int ROW_HEIGHT = 84;
        private const int ROW_PADDING_X = 30;
        private const int ROW_SPACING = 8;
        private const int COLUMN_GAP = 40;

        //A page holds a chapter; an unchaptered set is dealt in pages of this many, the campaign's own chapter length
        private const int UNCHAPTERED_PAGE = 10;

        private const int PERIOD_BUTTON_WIDTH = 560;
        //The picker's own arrow (#273): FontStars' triangle plus MenuTile's padding needs about this, and a tile smaller than
        //the glyph inside it lets the glyph overflow rather than clipping it (#348's trap), so the slab and the arrow part
        private const int ARROW_SIZE = 160;
        private const int CHAPTER_NAME_WIDTH = 1300;


        //Which period is up, and the summary of each as it has come (null until then); the tickets out for them
        private bool _allTime;
        private int _monthTicket = -1, _allTimeTicket = -1;
        private SummaryReply _monthReply, _allTimeReply;

        //The pages: where each starts in the set, which is up, and the level each row slot shows (-1 for none)
        private int[] _pageStart = Array.Empty<int>();
        private int _page = -1;
        private int[] _slotLevel = Array.Empty<int>();

        //Each level's board key, read off its file once and kept (LevelIdentity.Of reads and hashes the file)
        private readonly Dictionary<int, LevelIdentity> _identities = new();

        //The tree's parts the writing pass fills
        private Button _monthButton, _allTimeButton;
        private Label _monthLabel, _allTimeLabel;
        private Label _chapterName;
        private Button _previousArrow, _nextArrow;
        private Label _previousGlyph, _nextGlyph;
        private readonly List<Button> _rows = new();
        private readonly List<Label> _rowNames = new(), _rowTops = new(), _rowMes = new();
        private Label _status;
        private Button _settingsButton;

        public HighScoresPage(BS3DGame game) : base(game) { }

        /// <summary>Opens on a stated chapter (1-based) and period - the <c>highscores=</c> argument's way in.</summary>
        internal void Pin(int chapter, bool allTime)
        {
            _page = Math.Max(0, chapter - 1);
            _allTime = allTime;
        }

        public override void Enter()
        {
            _monthReply = _allTimeReply = null;
            Ask();
        }

        /// <summary>
        /// Coming back on top asks again for what is missing (#685's review): Enter runs on a push and never on a pop, so the
        /// player who opened Settings from here, opted in and came back would otherwise have waited on "Loading" for good -
        /// the very path the page's Settings door offers. A period that failed is asked again the same way.
        /// </summary>
        public override void CoveredChanged()
        {
            base.CoveredChanged();
            if (IsActive) Ask();
        }

        /// <summary>
        /// Both periods at once, so turning between them is instant; each is cached a minute by the session. Only a period
        /// with nothing on its way and nothing usable come back is asked, so a re-entry or a return asks no more than it must.
        /// </summary>
        private void Ask()
        {
            if (_monthTicket < 0 && _monthReply?.Summary == null) _monthTicket = Game.Online.RequestBoardsSummary(allTime: false);
            if (_allTimeTicket < 0 && _allTimeReply?.Summary == null) _allTimeTicket = Game.Online.RequestBoardsSummary(allTime: true);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            bool changed = false;

            if (_monthTicket >= 0 && Game.Online.TryTakeBoardsSummary(_monthTicket, out SummaryReply month))
            {
                _monthReply = month;
                _monthTicket = -1;
                changed = true;
            }

            if (_allTimeTicket >= 0 && Game.Online.TryTakeBoardsSummary(_allTimeTicket, out SummaryReply allTime))
            {
                _allTimeReply = allTime;
                _allTimeTicket = -1;
                changed = true;
            }

            if (changed) Refresh();
        }

        protected override Widget BuildTree()
        {
            FindPages();

            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("HIGH SCORES"));

            //The period, two entries side by side and both in the pad's walk. The one up is told by its type's brightness and
            //not by its slab: the host's focus highlight repaints every entry's slab each pass (ApplyNavHighlight), which is
            //why the settings page's inverted tabs (#686) are kept out of the walk and these, which a pad must reach, are not
            HorizontalStackPanel periods = new() { Spacing = Scaled(20), HorizontalAlignment = HorizontalAlignment.Center };
            _monthButton = MenuButton("This month", () => SetPeriod(false), out _monthLabel);
            _allTimeButton = MenuButton("All time", () => SetPeriod(true), out _allTimeLabel);
            _monthButton.Width = _allTimeButton.Width = Scaled(PERIOD_BUTTON_WIDTH);
            periods.Widgets.Add(_monthButton);
            periods.Widgets.Add(_allTimeButton);
            column.Widgets.Add(periods);

            column.Widgets.Add(BuildPager());

            _rows.Clear();
            _rowNames.Clear();
            _rowTops.Clear();
            _rowMes.Clear();

            VerticalStackPanel rows = new() { Spacing = Scaled(ROW_SPACING), HorizontalAlignment = HorizontalAlignment.Center };
            for (int slot = 0; slot < LongestPage(); slot++) rows.Widgets.Add(BuildRow(slot));
            column.Widgets.Add(rows);

            _status = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                HorizontalAlignment = HorizontalAlignment.Center,
                Wrap = true,
                Width = Scaled(NAME_WIDTH + TOP_WIDTH + ME_WIDTH + 2 * COLUMN_GAP),
            };
            column.Widgets.Add(_status);

            //The door to opting in, shown only while there is nothing to show without it
            _settingsButton = MenuButton("Settings", Game.OpenSettings);
            column.Widgets.Add(_settingsButton);

            column.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(column));
        }

        /// <summary>
        /// The chapter's name between two arrows, which turn it one chapter at a time and stop at the ends (#727): the picker
        /// wraps, and it can afford to because its pips say where the player is, which this page has none of.
        /// </summary>
        private Widget BuildPager()
        {
            HorizontalStackPanel row = new() { Spacing = Scaled(20), HorizontalAlignment = HorizontalAlignment.Center };

            _previousArrow = Arrow('◀', -1, out _previousGlyph);
            row.Widgets.Add(_previousArrow);

            _chapterName = new Label
            {
                Font = FontSection,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            //The name centred in a block of fixed width, so the arrows stand still between "THE MEADOW" and "THE SPECTRUM" -
            //a label given a width of its own draws its text from the left, so the width is the block's
            Panel name = new() { Width = Scaled(CHAPTER_NAME_WIDTH), VerticalAlignment = VerticalAlignment.Center };
            name.Widgets.Add(_chapterName);
            row.Widgets.Add(name);

            _nextArrow = Arrow('▶', 1, out _nextGlyph);
            row.Widgets.Add(_nextArrow);
            return row;
        }

        private Button Arrow(char glyph, int direction, out Label caption)
        {
            caption = new Label
            {
                Text = glyph.ToString(),
                Font = FontStars,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Button arrow = Game.MenuTile(caption, () => Turn(direction), ARROW_SIZE, ARROW_SIZE);

            //An arrow with nowhere to go keeps its slab and dims its glyph (see Refresh): Myra's own disabled slab and type
            //read as a lock, which is what a disabled row of this page already means, and this one means "no further this way"
            arrow.DisabledBackground = BS3DGame.MENU_BUTTON_BRUSH;

            return arrow;
        }

        /// <summary>One row slot: three labels in a tile-shaped entry, which opens the level's full boards.</summary>
        private Button BuildRow(int slot)
        {
            HorizontalStackPanel content = new() { Spacing = Scaled(COLUMN_GAP), VerticalAlignment = VerticalAlignment.Center };

            Label name = RowLabel(NAME_WIDTH, BS3DGame.MENU_TEXT);
            Label top = RowLabel(TOP_WIDTH, BS3DGame.MENU_TEXT_BODY);
            Label me = RowLabel(ME_WIDTH, BS3DGame.BoardYouColor);
            content.Widgets.Add(name);
            content.Widgets.Add(top);
            content.Widgets.Add(me);

            Button row = Game.MenuTile(content, () => OpenSlot(slot),
                NAME_WIDTH + TOP_WIDTH + ME_WIDTH + 2 * COLUMN_GAP + 2 * ROW_PADDING_X, ROW_HEIGHT);
            row.Padding = ScaledThickness(ROW_PADDING_X, 0);

            //A row that cannot be opened - a lock, or every row while the boards cannot be asked - is a quiet row and not a
            //greyed-out control: Myra's own disabled slab and type turned the opted-out page into ten unreadable bars
            //(photographed). The writing pass dims what it means to dim.
            row.DisabledBackground = BS3DGame.MENU_BUTTON_BRUSH;

            _rows.Add(row);
            _rowNames.Add(name);
            _rowTops.Add(top);
            _rowMes.Add(me);
            return row;
        }

        private Label RowLabel(int width, Color color) => new()
        {
            Font = FontSmall,
            TextColor = color,
            DisabledTextColor = color,
            Width = Scaled(width),
            VerticalAlignment = VerticalAlignment.Center,
        };

        /// <summary>A row label's colour, the same whether its row can be opened or not (see <see cref="BuildRow"/>).</summary>
        private static void Paint(Label label, Color color)
        {
            label.TextColor = color;
            label.DisabledTextColor = color;
        }

        /// <summary>Where each page starts: the set's chapters, or pages of <see cref="UNCHAPTERED_PAGE"/> in a set without them.</summary>
        private void FindPages()
        {
            List<int> starts = new();

            for (int index = 0; index < Game.LevelCount;)
            {
                starts.Add(index);

                if (Game.CampaignHasBlocks)
                {
                    Game.LevelBlockRange(index, out _, out int last);
                    index = last + 1;
                }
                else index += UNCHAPTERED_PAGE;
            }

            _pageStart = starts.ToArray();

            //The first time, the chapter the player has reached - the last one holding an unlocked level, the picker's own
            //opening guess; after that, where they left it
            if (_page < 0 || _page >= _pageStart.Length) _page = FrontierPage();
        }

        private int PageEnd(int page) => page + 1 < _pageStart.Length ? _pageStart[page + 1] - 1 : Game.LevelCount - 1;

        private int LongestPage()
        {
            int longest = 0;
            for (int p = 0; p < _pageStart.Length; p++) longest = Math.Max(longest, PageEnd(p) - _pageStart[p] + 1);
            return longest;
        }

        private int FrontierPage()
        {
            for (int p = _pageStart.Length - 1; p > 0; p--)
                for (int level = _pageStart[p]; level <= PageEnd(p); level++)
                    if (Game.IsLevelUnlocked(level)) return p;

            return 0;
        }

        /// <summary>Shows a period - and asks again for one that failed, which is the page's own "try again".</summary>
        private void SetPeriod(bool allTime)
        {
            Ask();

            if (_allTime == allTime) return;

            _allTime = allTime;
            Refresh();
        }

        /// <summary>
        /// The previous or next chapter, <b>stopping at the ends</b> (#727): no wrap in either direction, so the way from the last
        /// chapter back to the first is the arrow, a chapter at a time, and the player always knows where they are. A player who
        /// wants the first chapter at once leaves the page and opens it again, which opens on the chapter they have reached. The
        /// entries are re-read, as a picker's chapter turn re-reads them (a chapter's rows differ in number and in which are
        /// locked), with the cursor kept where it stood - or on the other arrow, when the turn reached an end and disabled the
        /// one it stood on, which has left the pad's walk.
        /// </summary>
        private void Turn(int direction)
        {
            if (_pageStart.Length <= 1) return;

            int wanted = Math.Clamp(_page + direction, 0, _pageStart.Length - 1);
            if (wanted == _page) return;

            _page = wanted;
            Button focused = _focused;
            Refresh();

            if (focused == _nextArrow && !_nextArrow.Enabled) focused = _previousArrow;
            else if (focused == _previousArrow && !_previousArrow.Enabled) focused = _nextArrow;

            Game.RefreshNavEntries(focused);
        }

        //The entry the pad's cursor stands on, so a chapter turn leaves it there
        private Button _focused;

        internal override void NavFocusChanged(Button focused) => _focused = focused;

        /// <summary>
        /// Left or right, the D-pad, the stick or the shoulders turn the chapter. False at an end, where nothing moved, so the
        /// host plays no tick and the cursor does not go anywhere (#727).
        /// </summary>
        internal override bool PageSideways(int direction)
        {
            int before = _page;
            Turn(direction);
            return _page != before;
        }

        private void OpenSlot(int slot)
        {
            int level = slot < _slotLevel.Length ? _slotLevel[slot] : -1;
            if (level >= 0 && Game.IsLevelUnlocked(level)) Game.OpenLevelBoard(level);
        }

        internal override void Refresh()
        {
            if (_chapterName == null || _pageStart.Length == 0) return;

            //The period up in the bright type, the other in the aside grey - emphasis by brightness, the menu's rule
            _monthLabel.TextColor = !_allTime ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;
            _allTimeLabel.TextColor = _allTime ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;

            //The arrows: one that would leave the campaign is off, and dim rather than greyed out by Myra (see Arrow)
            _previousArrow.Enabled = _page > 0;
            _nextArrow.Enabled = _page < _pageStart.Length - 1;
            Paint(_previousGlyph, _previousArrow.Enabled ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM);
            Paint(_nextGlyph, _nextArrow.Enabled ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM);

            int first = _pageStart[_page], last = PageEnd(_page);
            _chapterName.Text = Game.CampaignHasBlocks
                ? (Game.LevelBlockName(first) ?? "LEVELS").ToUpperInvariant()
                : $"LEVELS {first + 1} TO {last + 1}";

            SummaryReply reply = _allTime ? _allTimeReply : _monthReply;
            Dictionary<string, BoardSummaryBody> boards = Index(reply?.Summary);
            int rules = ScoreKeeper.RulesVersion;

            _slotLevel = new int[_rows.Count];

            for (int slot = 0; slot < _rows.Count; slot++)
            {
                int level = first + slot;
                bool shown = level <= last;

                _slotLevel[slot] = shown ? level : -1;
                _rows[slot].Visible = shown;
                if (!shown) continue;

                bool unlocked = Game.IsLevelUnlocked(level);
                _rows[slot].Enabled = unlocked && Game.Online.Enabled;

                _rowNames[slot].Text = $"{level + 1}  {Game.LevelDisplayName(level)}";
                Paint(_rowNames[slot], unlocked ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM);

                if (!unlocked)
                {
                    _rowTops[slot].Text = "Locked";
                    Paint(_rowTops[slot], BS3DGame.MENU_TEXT_DIM);
                    _rowMes[slot].Text = string.Empty;
                    continue;
                }

                BoardSummaryBody board = boards != null && Identity(level) is LevelIdentity id
                    && boards.TryGetValue(Key(id.File, id.Hash, rules), out BoardSummaryBody found) ? found : null;

                Paint(_rowTops[slot], BS3DGame.MENU_TEXT_BODY);
                _rowTops[slot].Text = reply?.Summary == null ? string.Empty
                    : board == null ? "No clears yet"
                    : $"#1  {board.Top.Name}   {ScoreText.Of(board.Top.Score)}{Unfinished(board.Top.Stars)}";

                _rowMes[slot].Text = board?.Me is BoardMeBody me && me.Rank > 0
                    ? (me.Rank == 1 ? $"You lead   {ScoreText.Of(me.Score)}"
                        : $"You: {Ordinal(me.Rank)} of {board.Total}   {ScoreText.Of(me.Score)}") + Unfinished(me.Stars)
                    : string.Empty;
            }

            _status.Text = StatusText(reply, boards);
            _settingsButton.Visible = !Game.Online.Enabled;
        }

        /// <summary>What the line under the rows says: why there is nothing, or what the player leads.</summary>
        private string StatusText(SummaryReply reply, Dictionary<string, BoardSummaryBody> boards)
        {
            if (!Game.Online.IsOn)
                return "Online leaderboards are off. Turn on Online scores in Settings to see who leads every level, and where you stand.";

            if (!Game.Online.Enabled)
                return "No score server to ask: set a nickname in Settings, or this build has no server.";

            if (reply == null) return "Loading the boards...";
            if (reply.NotOffered) return "The score server does not offer this overview yet. Each level's boards are still in Select Level.";
            if (reply.Summary == null) return "The score server did not answer. Try again in a moment.";

            //Only the boards the rows can show - this build's levels as they are, the ones the player has open (#685's
            //review): the summary carries every version of every level any table names, and a count over those said "you
            //lead 12 of 40" over rows that all read "No clears yet" after a level was edited or the rules moved
            int on = 0, leads = 0, rules = ScoreKeeper.RulesVersion;
            for (int level = 0; level < Game.LevelCount; level++)
            {
                if (!Game.IsLevelUnlocked(level) || Identity(level) is not LevelIdentity id) continue;
                if (!boards.TryGetValue(Key(id.File, id.Hash, rules), out BoardSummaryBody board)) continue;
                if (board.Me == null || board.Me.Rank <= 0) continue;

                on++;
                if (board.Me.Rank == 1) leads++;
            }

            string period = _allTime ? "All time" : "This month";
            return on == 0 ? $"{period}: you are on no board yet. Play a level to the end to get on one."
                : leads == 0 ? $"{period}: you are on {on} board{(on == 1 ? "" : "s")}. Lead one to see your name at the top."
                : $"{period}: you lead {leads} of the {on} board{(on == 1 ? "" : "s")} you are on.";
        }

        /// <summary>The summary keyed by board, the period's own; null while there is none.</summary>
        private static Dictionary<string, BoardSummaryBody> Index(BoardsSummaryBody summary)
        {
            if (summary == null) return null;

            Dictionary<string, BoardSummaryBody> boards = new(summary.Boards.Count);
            foreach (BoardSummaryBody board in summary.Boards) boards[Key(board.File, board.Hash, board.Rules)] = board;
            return boards;
        }

        private static string Key(string file, string hash, int rules) => file + "#" + hash + "#" + rules.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// A level's board key, read once (it reads and hashes the level's file). The status line asks it of every open
        /// level, so the first summary to arrive reads that many files - once a run, since the set does not change.
        /// </summary>
        private LevelIdentity Identity(int level)
        {
            if (!_identities.TryGetValue(level, out LevelIdentity identity))
                _identities[level] = identity = Game.LevelIdentityOf(level);

            return identity;
        }

        /// <summary>
        /// The mark after a row that is an unfinished attempt (#716): zero stars, which no clear has. The service ranks
        /// every clear above every attempt, so a "#1" that is one says nobody has cleared the level yet.
        /// </summary>
        private static string Unfinished(int stars) => stars == 0 ? ", " + BoardView.UnfinishedMark : string.Empty;

        private static string Ordinal(int n)
        {
            int tens = n % 100;
            string suffix = tens is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return n.ToString(CultureInfo.InvariantCulture) + suffix;
        }
    }
}

using BS3D.Online;
using FontStashSharp.RichText;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
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
    /// and each level one row: its board's #1 and the player's own place, for this month or for all time.
    /// <para>
    /// <b>What it ranks is the levels, decided in the owner's stead</b> (the issue's option (a)): a leaderboard to climb is a
    /// row saying who leads a level and where the player stands on it, and a level the player leads says so in their colour. A
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
    /// <b>Every level shows its real board, the levels ahead of the player too</b> (#898, the owner's ruling of 2026-10-16,
    /// which reverses this page's "Locked" of #266 and only here - the level picker keeps its locks). The table of every
    /// level of every chapter is the point of a leaderboard to climb, and a leader's name and score were never hidden
    /// anywhere else a board is open. What a row says of the player's own progress is its <b>tint</b>: a level the player
    /// has cleared (<see cref="HighScoresRow.Cleared"/>: at least one star; a skip is not a clear) rests in the menu's usual
    /// slab, a level they have not in a darker, recessed one with its number and name a step dimmer, so a page of leaders still says
    /// which of the levels are the player's own. "You" is empty where the player has no place. Every row opens that level's
    /// full boards (<c>OpenLevelBoard</c>, which has no unlock check), so the overview is also the way into every board, and
    /// every row is in the pad's walk whenever the boards can be asked.
    /// </para>
    /// <para>
    /// <b>Opted out, the page still opens</b>: it says what turning online scores on gives and offers Settings, the result
    /// page's own hint (#548) made into a door. The menu entry is a reason to opt in, so hiding it would hide the reason.
    /// </para>
    /// <para>
    /// <b>The numbers stand in columns, in tabular figures</b> (#806): the level's number flush right before its name, and
    /// each score flush right at the end of its block, so the leaders' scores and the player's own read down the page as
    /// two columns. They were written into one line with the name before them ("#1  Kuba   41 200"), so each score stood
    /// wherever its name ended, with runs of spaces doing a column's work.
    /// </para>
    /// </summary>
    internal sealed class HighScoresPage : MenuPage
    {
        //A row: the level, its #1, the player's place. Wide enough for a sixteen-letter name beside its score in the small
        //face, and for "You: 112th of 130" beside 123 456; the three together are about the picker's band of five tiles.
        //The level's block holds the set's longest number and name ("140" and "Cannonball", 440 units in Inter at the small
        //size, read off the face's own advances); what it does not need went to the gutters since #806: with each score
        //flush right at its block's end, a gutter of 40 set the leader's score against the player's line beside it
        //("355 230 You lead"), and 90 parts the two columns.
        private const int NAME_WIDTH = 520;
        private const int TOP_WIDTH = 860;
        private const int ME_WIDTH = 760;
        private const int ROW_HEIGHT = 84;
        private const int ROW_PADDING_X = 30;
        private const int ROW_SPACING = 8;
        private const int COLUMN_GAP = 90;

        //Inside a block (#806): between the level's number and its name, about the word space the line had there, and
        //between a text and its score, at the least, where a long name meets a long score
        private const int NUMBER_GAP = 18;
        private const int SCORE_GAP = 24;

        //The ellipsis is one character (#806); FontStashSharp's default is three full stops
        private const string ELLIPSIS = "…";

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

        //Each level's board key, read off its file once and kept, on a pool thread (LevelIdentity.Of reads and hashes the file, and
        //the page needs all of them): made and started the first time the boards can be asked, since the set is fixed for a run,
        //and asked only for what is ready (#898's review). _identitiesShown is whether Update has seen it complete and written
        //the rows again
        private LevelIdentityCache _identities;
        private bool _identitiesShown;

        //The tree's parts the writing pass fills
        private Button _monthButton, _allTimeButton;
        private Label _monthLabel, _allTimeLabel;
        private Label _chapterName;
        private Button _previousArrow, _nextArrow;
        private Label _previousGlyph, _nextGlyph;
        private readonly List<Button> _rows = new();
        private readonly List<Label> _rowNumbers = new(), _rowNames = new(), _rowTops = new(), _rowMes = new();
        private readonly List<Label> _rowTopScores = new(), _rowMeScores = new();

        //The slab each row rests in (#898): the usual one for a level the player has cleared, the recessed one for a level not
        //finished. Both are the menu's static brushes; this list only says which, and EntryRestBrushFor hands it to the host
        private readonly List<IBrush> _rowRest = new();
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
        /// Starts reading the levels' board keys off the frame, where the first summary used to read and hash every level's file on
        /// it. Only when the boards can be asked at all (an opted-out page has no use for them), and once: a later call finds the
        /// worker running or done. The rows of a level not yet read wait as they do before the boards answer, and Update writes
        /// the page again when the last one is in.
        /// </summary>
        private void WarmIdentities()
        {
            if (!Game.Online.Enabled) return;

            _identities ??= new LevelIdentityCache(Game.LevelCount, Game.LevelIdentityOf);
            _identities.Start();
        }

        /// <summary>
        /// Both periods at once, so turning between them is instant; each is cached a minute by the session. Only a period
        /// with nothing on its way and nothing usable come back is asked, so a re-entry or a return asks no more than it must.
        /// </summary>
        private void Ask()
        {
            WarmIdentities();

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

            //The levels' keys have all been read (on the worker, once a run): write the rows again, which waited on them
            if (!_identitiesShown && _identities != null && _identities.IsComplete)
            {
                _identitiesShown = true;
                changed = true;
            }

            if (changed) Refresh();
        }

        protected override Widget BuildTree()
        {
            FindPages();

            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("HIGH SCORES"));

            //The period, two entries side by side and both in the pad's walk. The one up wears the inverted slab the Help and
            //Settings tabs do (#1003): told by its type's brightness alone, the player was not sure which was chosen. The host's
            //focus highlight repaints every entry's slab each pass (ApplyNavHighlight), so the slab is handed over through
            //EntryRestBrushFor rather than set once; focused or under the pointer the chosen one takes the shared hover slab
            //like any entry, its dark type still readable on it
            HorizontalStackPanel periods = new() { Spacing = Scaled(20), HorizontalAlignment = HorizontalAlignment.Center };
            _monthButton = MenuButton("This month", () => SetPeriod(false), out _monthLabel);
            _allTimeButton = MenuButton("All time", () => SetPeriod(true), out _allTimeLabel);
            _monthButton.Width = _allTimeButton.Width = Scaled(PERIOD_BUTTON_WIDTH);
            periods.Widgets.Add(_monthButton);
            periods.Widgets.Add(_allTimeButton);
            column.Widgets.Add(periods);

            column.Widgets.Add(BuildPager());

            _rows.Clear();
            _rowNumbers.Clear();
            _rowNames.Clear();
            _rowTops.Clear();
            _rowMes.Clear();
            _rowTopScores.Clear();
            _rowMeScores.Clear();
            _rowRest.Clear();

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

            column.Widgets.Add(BackButton());

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

            //What turns the chapter, under the arrows (#799, #848): the pad's shoulders, dimmed with an arrow that has nowhere
            //to go, and nothing for the keyboard (an arrow key beside an arrow only repeated it)
            row.Widgets.Add(ArrowWithShoulder(_previousArrow, PromptGlyph.PAD_LB));

            _chapterName = new Label
            {
                Font = FontSection,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            //The name centred in a block of fixed width, so the arrows stand still between "THE MEADOW" and "THE SPECTRUM" -
            //a label given a width of its own draws its text from the left, so the width is the block's
            //Level with the arrows' slabs, the shoulders hanging under them (ArrowWithShoulder stacks from the top)
            Panel name = new() { Width = Scaled(CHAPTER_NAME_WIDTH), Height = Scaled(ARROW_SIZE), VerticalAlignment = VerticalAlignment.Top };
            name.Widgets.Add(_chapterName);
            row.Widgets.Add(name);

            _nextArrow = Arrow('▶', 1, out _nextGlyph);
            row.Widgets.Add(ArrowWithShoulder(_nextArrow, PromptGlyph.PAD_RB));
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
            //read as a locked control, which is not what an arrow means: this one says "no further this way"
            arrow.DisabledBackground = BS3DGame.MENU_BUTTON_BRUSH;

            return arrow;
        }

        /// <summary>
        /// One row slot: the level (its number flush right in a column as wide as the set's longest number, then its name),
        /// the board's leader with their score flush right, and the player's place with theirs, in a tile-shaped entry that
        /// opens the level's full boards.
        /// </summary>
        private Button BuildRow(int slot)
        {
            HorizontalStackPanel content = new() { Spacing = Scaled(COLUMN_GAP), VerticalAlignment = VerticalAlignment.Center };

            //The number's column holds the set's longest number in tabular figures, so every name starts at one edge
            int numberWidth = FontFigures.Width(Game.LevelCount.ToString(CultureInfo.InvariantCulture), FontSmall);
            Label number = RowLabel(BS3DGame.MENU_TEXT, HorizontalAlignment.Right);
            Label name = RowLabel(BS3DGame.MENU_TEXT, HorizontalAlignment.Stretch);
            Label top = RowLabel(BS3DGame.MENU_TEXT_BODY, HorizontalAlignment.Stretch);
            Label topScore = RowLabel(BS3DGame.MENU_TEXT_BODY, HorizontalAlignment.Right);
            Label me = RowLabel(BS3DGame.BoardYouColor, HorizontalAlignment.Stretch);
            Label meScore = RowLabel(BS3DGame.BoardYouColor, HorizontalAlignment.Right);
            content.Widgets.Add(Block(NAME_WIDTH, number, name, numberWidth));
            content.Widgets.Add(Block(TOP_WIDTH, top, topScore));
            content.Widgets.Add(Block(ME_WIDTH, me, meScore));

            Button row = Game.MenuTile(content, () => OpenSlot(slot),
                NAME_WIDTH + TOP_WIDTH + ME_WIDTH + 2 * COLUMN_GAP + 2 * ROW_PADDING_X, ROW_HEIGHT);
            row.Padding = ScaledThickness(ROW_PADDING_X, 0);

            //A row that cannot be opened - every row while the boards cannot be asked - is a quiet row and not a greyed-out
            //control: Myra's own disabled slab and type turned the opted-out page into ten unreadable bars (photographed). The
            //writing pass gives it the slab of its level (DisabledBackground, as the host gives an entry its Background).
            row.DisabledBackground = BS3DGame.MENU_BUTTON_BRUSH;

            _rows.Add(row);
            _rowRest.Add(BS3DGame.MENU_BUTTON_BRUSH);
            _rowNumbers.Add(number);
            _rowNames.Add(name);
            _rowTops.Add(top);
            _rowMes.Add(me);
            _rowTopScores.Add(topScore);
            _rowMeScores.Add(meScore);
            return row;
        }

        /// <summary>
        /// A row's label. One that fills its cell (a name, a leader, a place) ends in an ellipsis where it would run into the
        /// score beside it (a long nickname with an unfinished mark can) rather than drawing over it.
        /// </summary>
        private Label RowLabel(Color color, HorizontalAlignment alignment) => new()
        {
            Font = FontSmall,
            TextColor = color,
            DisabledTextColor = color,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            AutoEllipsisMethod = alignment == HorizontalAlignment.Stretch ? AutoEllipsisMethod.Character : AutoEllipsisMethod.None,
            AutoEllipsisString = ELLIPSIS,
        };

        /// <summary>
        /// One of a row's three blocks, <paramref name="width"/> design units wide, in two cells: with a
        /// <paramref name="lead"/>, a first cell that many pixels wide (the level's number, flush right) and a second filling
        /// the rest (its name); without one, a first cell filling what the second - a score, flush right - leaves it.
        /// </summary>
        private Grid Block(int width, Label first, Label second, int lead = 0)
        {
            Grid block = new()
            {
                Width = Scaled(width),
                ColumnSpacing = Scaled(lead > 0 ? NUMBER_GAP : SCORE_GAP),
                VerticalAlignment = VerticalAlignment.Center,
            };
            block.ColumnsProportions.Add(lead > 0 ? new Proportion(ProportionType.Pixels, lead) : new Proportion(ProportionType.Fill));
            block.ColumnsProportions.Add(lead > 0 ? new Proportion(ProportionType.Fill) : new Proportion(ProportionType.Auto));
            block.RowsProportions.Add(new Proportion(ProportionType.Auto));

            Grid.SetColumn(first, 0);
            Grid.SetColumn(second, 1);
            block.Widgets.Add(first);
            block.Widgets.Add(second);
            return block;
        }

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

            //The chosen slab moved to the other entry (#1003): the host repaints the slabs, the cursor staying where it stood
            if (IsActive) Game.RefreshNavEntries(_focused);
        }

        /// <summary>
        /// The previous or next chapter, <b>stopping at the ends</b> (#727): no wrap in either direction, so the way from the last
        /// chapter back to the first is the arrow, a chapter at a time, and the player always knows where they are. A player who
        /// wants the first chapter at once leaves the page and opens it again, which opens on the chapter they have reached. The
        /// entries are re-read, as a picker's chapter turn re-reads them (a chapter's rows differ in number, and in which are
        /// tinted as not cleared), with the cursor kept where it stood - or on the other arrow, when the turn reached an end and disabled the
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

        /// <summary>
        /// A row opens its level's full boards, <b>whether or not the player may play the level yet</b> (#898): the boards of
        /// every level are open here, and <c>OpenLevelBoard</c> asks only whether they can be asked at all.
        /// </summary>
        private void OpenSlot(int slot)
        {
            int level = slot < _slotLevel.Length ? _slotLevel[slot] : -1;
            if (level >= 0) Game.OpenLevelBoard(level);
        }

        /// <summary>
        /// Each row rests in the slab of its level (#898): the host's focus highlight repaints every entry's background on
        /// each pass, and asks the page for the tone to rest an unfocused one to, so a tint set on the row alone would be
        /// gone with the first press of a key. Anything that is not a row (the periods, the arrows, Back) takes the page's own.
        /// </summary>
        internal override IBrush EntryRestBrushFor(Button entry)
        {
            if (entry != null && (entry == _monthButton || entry == _allTimeButton))
                return (entry == _allTimeButton) == _allTime ? SELECTED_BRUSH : BS3DGame.MENU_BUTTON_BRUSH;

            int slot = _rows.IndexOf(entry);
            return slot >= 0 && slot < _rowRest.Count ? _rowRest[slot] : base.EntryRestBrushFor(entry);
        }

        internal override void Refresh()
        {
            if (_chapterName == null || _pageStart.Length == 0) return;

            //The period up inverted, as a chosen tab is (#1003), the other a plain entry in the body grey. The type only: the
            //slab is the host's to paint (EntryRestBrushFor), so a reply arriving under a focused period cannot repaint its
            //focus away, and SetPeriod has the host repaint at once
            _monthLabel.TextColor = !_allTime ? SELECTED_TEXT : BS3DGame.MENU_TEXT_BODY;
            _allTimeLabel.TextColor = _allTime ? SELECTED_TEXT : BS3DGame.MENU_TEXT_BODY;

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

                //Every level's real board (#898), the player's progress told by the row's tint and not by what it says. Enabled
                //follows the boards alone: the level may be one the player cannot play yet, and its boards open all the same
                bool cleared = HighScoresRow.Cleared(Game.LevelStars(level));
                _rows[slot].Enabled = Game.Online.Enabled;
                _rowRest[slot] = _rows[slot].DisabledBackground = cleared ? BS3DGame.MENU_BUTTON_BRUSH : BS3DGame.MENU_ROW_UNFINISHED_BRUSH;

                //The number and name in the bright type for a level the player has cleared and in the body grey for one not
                //finished: the recessed slab's partner, so the tint is not carried by the slab alone
                Color nameColor = cleared ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_BODY;
                _rowNumbers[slot].Text = FontFigures.Tabular((level + 1).ToString(CultureInfo.InvariantCulture), FontSmall);
                _rowNames[slot].Text = Game.LevelDisplayName(level);
                Paint(_rowNumbers[slot], nameColor);
                Paint(_rowNames[slot], nameColor);

                //A level whose key is not read yet waits like a row before the boards answer (blank, the status line saying why)
                bool identified = TryIdentity(level, out LevelIdentity id);
                BoardSummaryBody board = boards != null && id != null
                    && boards.TryGetValue(Key(id.File, id.Hash, rules), out BoardSummaryBody found) ? found : null;

                HighScoresRow.Texts texts = HighScoresRow.Of(reply?.Summary != null && identified, board);
                Paint(_rowTops[slot], BS3DGame.MENU_TEXT_BODY);
                _rowTops[slot].Text = texts.Leader;
                _rowTopScores[slot].Text = texts.HasLeaderScore ? FontFigures.Tabular(texts.LeaderScore, FontSmall) : string.Empty;
                _rowMes[slot].Text = texts.Place;
                _rowMeScores[slot].Text = texts.HasPlaceScore ? FontFigures.Tabular(texts.PlaceScore, FontSmall) : string.Empty;
            }

            //As prose (#855): the line wraps, and a wrapping label is never handed raw text. Refresh runs on a change, not
            //per frame (Update's `changed`), so the lines are chosen as often as the reply moves
            SetProse(_status, StatusText(reply, boards));
            _settingsButton.Visible = !Game.Online.Enabled;
        }

        /// <summary>What the line under the rows says: why there is nothing, or what the player leads.</summary>
        private string StatusText(SummaryReply reply, Dictionary<string, BoardSummaryBody> boards)
        {
            if (!Game.Online.IsOn)
                return "Online leaderboards are off. Turn on Online scores in Settings to see who leads every level, and where you stand.";

            if (!Game.Online.Enabled)
                return "No score server to ask: set a nickname in Settings, or this build has no server.";

            //Waiting on the levels' keys is waiting on the boards: the count below is over all of them
            if (reply == null || !IdentitiesComplete) return "Loading the boards…";
            if (reply.NotOffered) return "The score server does not offer this overview yet. Each level’s boards are still in Select Level.";
            if (reply.Summary == null) return "The score server did not answer. Try again in a moment.";

            //Only the boards the rows can show - this build's levels as they are, every one of them since #898 (#685's
            //review): the summary carries every version of every level any table names, and a count over those said "you
            //lead 12 of 40" over rows that all read "No clears yet" after a level was edited or the rules moved
            int on = 0, leads = 0, rules = ScoreKeeper.RulesVersion;
            for (int level = 0; level < Game.LevelCount; level++)
            {
                if (!TryIdentity(level, out LevelIdentity id) || id == null) continue;
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
        /// A level's board key if the worker has read it (see <see cref="LevelIdentityCache"/>): true with the key, which is null
        /// when the level's file will not read; false while it is still to be read. A lookup in an array - no file is read here.
        /// </summary>
        private bool TryIdentity(int level, out LevelIdentity identity)
        {
            if (_identities != null) return _identities.TryGet(level, out identity);

            identity = null;
            return false;
        }

        private bool IdentitiesComplete => _identities != null && _identities.IsComplete;
    }
}

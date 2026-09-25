using BS3D.Online;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Prazsky.BS3D.Levels;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// One level's two online boards, opened from the level picker (#547): this month and all time side by side, a
    /// page of <see cref="ROWS"/> at a time with the player's own place under each, paged together.
    /// <para>
    /// It asks when it opens and when it pages, and draws whatever has come: a board that is slow is a board that is
    /// late, never a frame that is (the requests are the online client's worker's). A page asked for in the last minute
    /// comes from the game's short cache, so paging back does not ask the Pi again.
    /// </para>
    /// </summary>
    internal sealed class LevelBoardPage : MenuPage
    {
        /// <summary>Rows a page shows of each board: two boards of ten fit the design height with the buttons under them.</summary>
        private const int ROWS = 10;

        private const int BOARD_WIDTH = 820;
        private const int BOARD_GAP = 110;
        private const int PAGER_BUTTON_WIDTH = 490;
        private const int PAGER_BUTTON_GAP = 20;

        private int _level = -1;
        private LevelIdentity _identity;
        private int _offset;
        private int _monthTicket = -1, _allTimeTicket = -1;
        private BoardReply _monthReply, _allTimeReply;

        private Label _heading, _status;
        private BoardView _month, _allTime;
        private Button _previous, _next;

        public LevelBoardPage(BS3DGame game) : base(game) { }

        /// <summary>Points the page at a level before it is opened.</summary>
        internal void Show(int level, LevelIdentity identity, int page = 0)
        {
            _level = level;
            _identity = identity;
            _offset = System.Math.Max(page, 0) * ROWS;
        }

        public override void Enter() => Ask();

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();

            _heading = ScreenHeading(string.Empty);
            column.Widgets.Add(_heading);

            HorizontalStackPanel boards = new() { Spacing = Scaled(BOARD_GAP), HorizontalAlignment = HorizontalAlignment.Center };
            _month = new BoardView(FontBody, FontSmall, Scaled, ROWS, Scaled(BOARD_WIDTH));
            _allTime = new BoardView(FontBody, FontSmall, Scaled, ROWS, Scaled(BOARD_WIDTH));
            boards.Widgets.Add(_month.Root);
            boards.Widgets.Add(_allTime.Root);
            column.Widgets.Add(boards);

            _status = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            column.Widgets.Add(_status);

            //Side by side, the About page's own pair: two entries of a little under half a column each
            HorizontalStackPanel pager = new() { Spacing = Scaled(PAGER_BUTTON_GAP), HorizontalAlignment = HorizontalAlignment.Center };
            _previous = MenuButton("Previous", () => Page(-1));
            _next = MenuButton("Next", () => Page(+1));
            _previous.Width = _next.Width = Scaled(PAGER_BUTTON_WIDTH);
            pager.Widgets.Add(_previous);
            pager.Widgets.Add(_next);
            column.Widgets.Add(pager);

            column.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(column));
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            bool changed = false;

            if (Game.TryTakeLevelBoard(_monthTicket, out BoardReply month))
            {
                _monthReply = month;
                _monthTicket = -1;
                changed = true;
            }

            if (Game.TryTakeLevelBoard(_allTimeTicket, out BoardReply allTime))
            {
                _allTimeReply = allTime;
                _allTimeTicket = -1;
                changed = true;
            }

            if (changed) Refresh();
        }

        private void Ask()
        {
            _monthReply = _allTimeReply = null;
            _monthTicket = Game.RequestLevelBoard(_identity, allTime: false, _offset, ROWS);
            _allTimeTicket = Game.RequestLevelBoard(_identity, allTime: true, _offset, ROWS);
            Refresh();
        }

        /// <summary>Both boards turn a page together; nothing past the longer board's end, nothing before the top.</summary>
        private void Page(int direction)
        {
            int longest = System.Math.Max(_monthReply?.Page?.Total ?? 0, _allTimeReply?.Page?.Total ?? 0);
            int offset = _offset + direction * ROWS;

            if (offset < 0 || offset >= System.Math.Max(longest, 1)) return;

            _offset = offset;
            Ask();
        }

        internal override void Refresh()
        {
            if (_heading == null || _level < 0) return;

            _heading.Text = Game.LevelDisplayName(_level).ToUpperInvariant();

            BoardPageBody month = _monthReply?.Page, allTime = _allTimeReply?.Page;

            _month.Fill("THIS MONTH", BoardView.MonthName(month?.Month), month, month?.Me?.Rank ?? 0, month?.Total ?? 0);
            _allTime.Fill("ALL TIME", BoardView.AllTimePeriod, allTime, allTime?.Me?.Rank ?? 0, allTime?.Total ?? 0);

            bool waiting = _monthTicket >= 0 || _allTimeTicket >= 0;
            bool failed = _monthReply?.Page == null && _monthReply != null || _allTimeReply?.Page == null && _allTimeReply != null;
            int longest = System.Math.Max(month?.Total ?? 0, allTime?.Total ?? 0);

            _status.Text = waiting ? "Loading the boards..."
                : failed ? "The score server did not answer. Try again in a moment."
                : longest > ROWS ? $"Ranks {_offset + 1} to {System.Math.Min(_offset + ROWS, longest)} of {longest}"
                : " ";

            _previous.Enabled = _offset > 0;
            _next.Enabled = _offset + ROWS < longest;
        }
    }
}

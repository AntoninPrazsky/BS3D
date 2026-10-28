using BS3D.Online;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>Extras > Top Players</b> (#827): the players ranked across the whole campaign by their total score, every level's
    /// best clear added up (<c>GET /v1/ranking</c>, BS3D-API#11), with the levels cleared and the stars beside it for
    /// information. This month or all time, as every board.
    /// <para>
    /// <b>A winners' podium for the period's top three</b> (the owner's decision 3): <see cref="Effects.WinnersPodium"/> stands
    /// the gold, silver and bronze cups on its steps in the sharp foreground layer, placed against the page's own podium area
    /// as Help places its balls (#856), and the names are written over it in the menu's own type at the steps' fronts
    /// (<see cref="DrawOverForeground"/>), so they stay sharp at every resolution and no text is rendered into the 3D. The
    /// podium is the first page's three, kept while the player pages on, so a later page asks for nothing more.
    /// </para>
    /// <para>
    /// <b>The layout follows the screen's shape</b> (decision 5): the podium on the left and the table on the right on a screen
    /// wider than tall, the podium above the table on one taller than wide. A window dragged across that line is built again.
    /// </para>
    /// <para>
    /// The table is <see cref="LevelBoardPage"/>'s: <see cref="ROWS"/> a page, paged with the same buttons and shoulders, the
    /// player's own line under it. Loading, offline, opted out and a service older than the ranking each say so; the podium
    /// stands empty then, and with fewer than three ranked it carries only the cups it has.
    /// </para>
    /// </summary>
    internal sealed class TopPlayersPage : MenuPage
    {
        /// <summary>Rows a page shows, the level board page's own.</summary>
        internal const int ROWS = 10;

        //The podium's area and the table's width, in design units; the gap between them; the period's and the pager's entries
        private const int PODIUM_WIDTH = 1000, PODIUM_HEIGHT = 760;
        private const int TABLE_WIDTH = 1080;
        private const int STAGE_GAP = 60;
        private const int PERIOD_BUTTON_WIDTH = 560;
        private const int PAGER_BUTTON_WIDTH = 490, PAGER_BUTTON_GAP = 20;

        //The names on the steps' panels: the reading face (it draws any letter a nickname has, BS3D#548), as tall as this share
        //of the panel, and never under the small face's size in design units
        private const float NAME_SHARE = 1.05f;
        private const int NAME_FONT_FLOOR = 30;

        private bool _allTime;
        private int _offset;
        private int _ticket = -1, _podiumTicket = -1;
        private RankingReply _reply;

        //The podium's three, off the first page of the period they were asked for; how many there are; and whether the player
        //is among them, by place
        private readonly RankingEntryBody[] _podium = new RankingEntryBody[3];
        private int _podiumCount;
        private bool? _podiumAllTime;
        private int _podiumYou;

        private Button _monthButton, _allTimeButton, _previous, _next, _settingsButton;
        private Label _monthLabel, _allTimeLabel, _status;
        private RankingView _table;
        private Widget _podiumArea;
        private bool _builtTall;

        public TopPlayersPage(BS3DGame game) : base(game) { }

        /// <summary>Opens on a period: the <c>topplayers</c> argument's (#827).</summary>
        internal void Pin(bool allTime)
        {
            _allTime = allTime;
            _offset = 0;
        }

        public override void Enter()
        {
            Game.Winners?.Begin();
            Ask();
        }

        //Taller than wide: the podium above the table
        private bool Tall
        {
            get
            {
                var screen = Game.GraphicsDevice.PresentationParameters;
                return screen.BackBufferHeight > screen.BackBufferWidth;
            }
        }

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading(RankingCopy.TITLE.ToUpperInvariant()));

            //The period, two entries side by side, the one up inverted as a chosen tab is (#1003), as on High Scores
            HorizontalStackPanel periods = new() { Spacing = Scaled(20), HorizontalAlignment = HorizontalAlignment.Center };
            _monthButton = MenuButton("This month", () => SetPeriod(false), out _monthLabel);
            _allTimeButton = MenuButton("All time", () => SetPeriod(true), out _allTimeLabel);
            _monthButton.Width = _allTimeButton.Width = Scaled(PERIOD_BUTTON_WIDTH);
            periods.Widgets.Add(_monthButton);
            periods.Widgets.Add(_allTimeButton);
            column.Widgets.Add(periods);

            //The podium's place: nothing of its own to draw, only the area the 3D podium is fitted to
            _podiumArea = new Widget
            {
                Width = Scaled(PODIUM_WIDTH),
                Height = Scaled(PODIUM_HEIGHT),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            VerticalStackPanel board = new() { Spacing = Scaled(14), HorizontalAlignment = HorizontalAlignment.Center };
            _table = new RankingView(FontBody, FontSmall, Game.MenuFontPromptSmall, Scaled, ROWS, Scaled(TABLE_WIDTH));
            board.Widgets.Add(_table.Root);

            _status = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                HorizontalAlignment = HorizontalAlignment.Center,
                Wrap = true,
                Width = Scaled(TABLE_WIDTH),
            };
            board.Widgets.Add(_status);

            HorizontalStackPanel pager = new() { Spacing = Scaled(PAGER_BUTTON_GAP), HorizontalAlignment = HorizontalAlignment.Center };
            _previous = MenuButton("Previous", () => Page(-1));
            _next = MenuButton("Next", () => Page(+1));
            _previous.Width = _next.Width = Scaled(PAGER_BUTTON_WIDTH);
            pager.Widgets.Add(PageTurnUnder(_previous, PromptGlyph.PAD_LB, PromptGlyph.KEY_LEFT));
            pager.Widgets.Add(PageTurnUnder(_next, PromptGlyph.PAD_RB, PromptGlyph.KEY_RIGHT));
            board.Widgets.Add(pager);

            //The door to opting in, shown only while there is nothing to show without it
            _settingsButton = MenuButton("Settings", Game.OpenSettings);
            board.Widgets.Add(_settingsButton);

            //Side by side on a wide screen, stacked on a tall one (decision 5)
            _builtTall = Tall;
            if (_builtTall)
            {
                VerticalStackPanel stage = new() { Spacing = Scaled(STAGE_GAP / 2), HorizontalAlignment = HorizontalAlignment.Center };
                stage.Widgets.Add(_podiumArea);
                stage.Widgets.Add(board);
                column.Widgets.Add(stage);
            }
            else
            {
                HorizontalStackPanel stage = new() { Spacing = Scaled(STAGE_GAP), HorizontalAlignment = HorizontalAlignment.Center };
                stage.Widgets.Add(_podiumArea);
                stage.Widgets.Add(board);
                column.Widgets.Add(stage);
            }

            column.Widgets.Add(BackButton());
            return ScreenRoot(Plate(column));
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            //A window dragged across the square: the other layout (the menu's own rebuild follows the height alone)
            if (IsActive && _podiumArea != null && Tall != _builtTall)
            {
                InvalidateTree();
                Game.RebuildPage(this, () => _monthButton);
                return;
            }

            Game.Winners?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

            bool changed = false;
            if (_ticket >= 0 && Game.Online.TryTakeRanking(_ticket, out RankingReply reply))
            {
                _reply = reply;
                _ticket = -1;
                if (_offset == 0) TakePodium(reply);
                changed = true;
            }

            if (_podiumTicket >= 0 && Game.Online.TryTakeRanking(_podiumTicket, out RankingReply first))
            {
                _podiumTicket = -1;
                TakePodium(first);
                changed = true;
            }

            if (changed) Refresh();
        }

        //The period's first page: its top three stand on the podium, and the reveal starts again when they are new
        private void TakePodium(RankingReply first)
        {
            RankingPageBody page = first?.Page;
            int count = 0;
            for (int i = 0; i < _podium.Length; i++)
            {
                RankingEntryBody entry = page?.Entries != null && i < page.Entries.Count ? page.Entries[i] : null;
                if (entry != null && entry.Rank == i + 1)
                {
                    _podium[i] = entry;
                    count = i + 1;
                }
                else _podium[i] = null;
            }

            bool fresh = _podiumAllTime != _allTime || count != _podiumCount;
            _podiumCount = count;
            _podiumAllTime = _allTime;
            _podiumYou = page?.Me?.Rank is int rank and >= 1 and <= 3 ? rank : 0;
            if (fresh) Game.Winners?.Begin();
        }

        private void Ask()
        {
            _reply = null;
            _ticket = Game.Online.RequestRanking(_allTime, _offset, ROWS);

            //The podium is the first page's: asked for when the page shown is a later one and the period's three are not in
            //(a cached first page comes back on the next frame without asking the service)
            if (_offset > 0 && _podiumAllTime != _allTime) _podiumTicket = Game.Online.RequestRanking(_allTime, 0, ROWS);
            if (_podiumAllTime != _allTime && _offset == 0) _podiumCount = 0;

            Refresh();
        }

        private void SetPeriod(bool allTime)
        {
            if (_allTime != allTime)
            {
                _allTime = allTime;
                _offset = 0;
            }

            //Asked again whether it changed or not: the page's own "try again" for a period that failed
            Ask();
            if (IsActive) Game.RefreshNavEntries(_monthButton);
        }

        /// <summary>A page on or back; nothing past the last player, nothing before the first.</summary>
        private void Page(int direction)
        {
            int total = _reply?.Page?.Total ?? 0;
            int offset = _offset + direction * ROWS;
            if (offset < 0 || offset >= System.Math.Max(total, 1)) return;

            _offset = offset;
            Ask();
        }

        internal override bool PageSideways(int direction)
        {
            Button turn = direction > 0 ? _next : _previous;
            if (turn == null || !turn.Enabled) return false;

            Page(direction);
            return true;
        }

        internal override IBrush EntryRestBrushFor(Button entry)
        {
            if (entry != null && (entry == _monthButton || entry == _allTimeButton))
                return (entry == _allTimeButton) == _allTime ? SELECTED_BRUSH : BS3DGame.MENU_BUTTON_BRUSH;

            return base.EntryRestBrushFor(entry);
        }

        internal override void Refresh()
        {
            if (_table == null) return;

            _monthLabel.TextColor = !_allTime ? SELECTED_TEXT : BS3DGame.MENU_TEXT_BODY;
            _allTimeLabel.TextColor = _allTime ? SELECTED_TEXT : BS3DGame.MENU_TEXT_BODY;

            RankingPageBody page = _reply?.Page;
            int total = page?.Total ?? 0;
            RankingMeBody me = page?.Me;

            string you = !Game.Online.Enabled || page == null ? " "
                : RankingCopy.YouLine(me?.Rank ?? 0, total, me?.Levels ?? 0, me?.Stars ?? 0, me?.Score ?? 0);
            _table.Fill(page, you);

            SetProse(_status, StatusText(total));
            _previous.Enabled = _offset > 0;
            _next.Enabled = _offset + ROWS < total;
            _settingsButton.Visible = !Game.Online.Enabled;
        }

        //Why there is nothing, or which ranks the table shows
        private string StatusText(int total)
        {
            if (!Game.Online.IsOn) return RankingCopy.OFF;
            if (!Game.Online.Enabled) return RankingCopy.NO_SERVER;
            if (_ticket >= 0 || _reply == null) return RankingCopy.LOADING;
            if (_reply.NotOffered) return RankingCopy.NOT_OFFERED;
            if (_reply.Page == null) return RankingCopy.NO_ANSWER;
            return RankingCopy.PageLine(_offset, ROWS, total);
        }

        //THE PODIUM, in the frame's sharp foreground layer (#856's device): fitted to the area the tree laid out, its cups
        //standing on the places the period has
        internal override bool PresentsForeground => IsActive && _podiumArea != null && Game.Winners != null;

        internal override void DrawForeground()
        {
            var buffer = Game.GraphicsDevice.PresentationParameters;
            if (buffer.BackBufferWidth <= 0 || buffer.BackBufferHeight <= 0) return;

            Game.Winners.Draw(Game.Camera, OnDesktop(_podiumArea), buffer.BackBufferWidth, buffer.BackBufferHeight,
                Game.Online.Enabled ? _podiumCount : 0);
        }

        //The names on the steps' fronts, after the layer has been composited over the page: in front of the podium they label
        internal override void DrawOverForeground()
        {
            if (!PresentsForeground || !Game.Online.Enabled || _podiumCount == 0) return;

            SpriteBatch batch = Game.OverlayBatch;
            batch.Begin();
            for (int place = 1; place <= _podiumCount; place++)
            {
                RankingEntryBody entry = _podium[place - 1];
                if (entry == null || !Game.Winners.TryNameAnchor(place, out Vector2 anchor, out float room, out float panel)) continue;

                //Sized to the panel and rounded to a whole pixel, so the face's cache holds one size while the window does
                var font = Game.ReadingFontAt(System.MathF.Round(System.MathF.Max(panel * NAME_SHARE, Scaled(NAME_FONT_FLOOR))));

                string name = BoardLayout.Ellipsize(entry.Name, (int)room, text => (int)System.MathF.Ceiling(font.MeasureString(text).X)) ?? string.Empty;
                Vector2 size = font.MeasureString(name);
                Vector2 at = new(System.MathF.Round(anchor.X - size.X * 0.5f), System.MathF.Round(anchor.Y - size.Y * 0.5f));
                Color colour = place == _podiumYou ? BS3DGame.BoardYouColor : BS3DGame.MENU_TEXT;

                //A shadow under the type, so a name reads on the step's face whatever the metal's highlight behind it does
                batch.DrawString(font, name, at + new Vector2(0f, Scaled(3)), Color.Black * 0.7f);
                batch.DrawString(font, name, at, colour);
            }
            batch.End();
        }

        private static Rectangle OnDesktop(Widget widget)
        {
            Rectangle local = widget.ActualBounds;
            Point corner = widget.ToGlobal(new Point(local.X, local.Y));
            return new Rectangle(corner.X, corner.Y, local.Width, local.Height);
        }
    }
}

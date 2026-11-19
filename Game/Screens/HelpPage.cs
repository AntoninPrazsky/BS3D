using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using Prazsky.Core.Tools;
using System;
using Color = Microsoft.Xna.Framework.Color;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// How to play, what the words on the result page mean, what the odd balls do, and which key does what
    /// (#427) — the reference a player reads once, beside About rather than inside it.
    /// <para>
    /// <b>Six topics behind one menu entry, as a row of tabs</b> (#857), on the Settings page's pattern (#686,
    /// <see cref="MenuPage.TabRow"/>): every topic visible at once and one click away, the chosen one inverted, turned
    /// wrapping by the arrow keys, the D-pad, the left stick and the pad's shoulders (<see cref="PageSideways"/>) and by
    /// the wheel over the row (<see cref="OnScrollWheel"/>). <b>Controls is first and the tab Help opens on</b>: "which
    /// key does what" is what a player opens Help for, and until #857 it was the last of six pages walked with Previous
    /// and Next (#427, #738), five presses away, with nothing saying what the other pages were. The tabs are not in the
    /// pad's walk, as Settings' are not; Back is the one entry, so the cursor arrives on it, and a pad player turns the
    /// tabs on the shoulders and scrolls on the right stick.
    /// </para>
    /// <para>
    /// <b>Each topic's body scrolls</b> (#606): the owner's 3840×1600 cut the Scoring page off top and bottom with
    /// Back off the screen, and any shorter window is worse. Every body sits in a <c>MenuScroll</c> of its own, all six
    /// in one page area and one shown, with the heading, the tab row and Back outside them so the way out never scrolls
    /// away. The body is text with no entry in it for the focus cursor to walk to, so the wheel scrolls it (Myra's own),
    /// and so do Page Up/Page Down and the pad's right stick (<see cref="OnScrollAxis"/>).
    /// </para>
    /// <para>
    /// <b>⚠ Every number on the scoring tab is COMPUTED, not written</b>: the ten a matched ball is worth, the twenty a
    /// detached one is, the streak's step and cap all come out of <see cref="ScoreKeeper"/>'s own constants at build time,
    /// so a tuning of the scoring cannot leave a stale figure here. The tab is the price list and nothing of the
    /// arithmetic (#998): the worked example it carried until then read as a page of a maths textbook, and a bubble
    /// shooter's player concluded the game needed arithmetic.
    /// </para>
    /// <para>
    /// <b>The controls page draws real keycaps</b> out of PromptFont, the face the tutorial's cards already
    /// use, rather than letters in drawn boxes. #463's ruling is that the controls paragraph leaves About; this
    /// is where it lands.
    /// </para>
    /// <para>
    /// <b>The odd balls are shown, not only described</b> (#856): each entry has the kind's own ball turning beside it,
    /// drawn live by the game's ball path into the frame's sharp foreground layer (<see cref="DrawForeground"/>), the
    /// layer the host composites over the UI at the end of its Draw (#242) so the page's plate does not dim it. A
    /// picture cannot go stale as a bitmap would (#771's worry): when a kind's look changes, this changes with it. The
    /// table is <see cref="OddBallCopy"/> and a test holds it to every kind (#895): the ten the cluster holds, and the two
    /// the cannon holds under a line of their own — the rainbow ball and the Cutter, drawn as they sit loaded in the gun.
    /// The rainbow ball cycles its colours here on a cycle of the page's own (<see cref="_cycle"/>), since only a session
    /// steps the game's. After them, outside the table, the dead weight (#1023): a ball the cluster let go of that stuck on its
    /// way down, drawn with its pixels dithered out as the map draws it.
    /// </para>
    /// </summary>
    internal sealed class HelpPage : MenuPage
    {
        //The text's width: the controls page's lines and every paragraph from BASICS on are set across it (ProseWidth), 83
        //characters of the small face. Wider than the prose measure #806 set them at (67): the owner's ruling on #865
        //(2026-10-15), the whole width for less scrolling. The frame is at least this wide, and the tab row's width where
        //that is wider (BuildTree)
        private const int TEXT_WIDTH = 1860;

        //The space between a keycap and its words on the controls page
        private const int KEY_GAP = 24;

        //THE ODD BALLS' LIVE PICTURES (#856): each kind's entry has a square at its left, design units, and the ball itself is
        //drawn in it by the game's own ball path - the kind card's own device (#735, GameplayScreen.CollectTutorialBall):
        //one more ball in a frame, hung BALL_DEPTH in front of the lens on the ray through the square and sized to fill
        //BALL_FILL of it, so it is lit and glows and breathes as that kind does on the map, and cannot go stale as a
        //picture would. Into the sharp foreground layer (DrawForeground), which the host composites over the UI, so the
        //plate does not dim it. The square is two and a half lines of the prose beside it (the pitch is 70), between the
        //card's 200 and a keycap; BALL_GAP is the space to the words.
        private const int BALL_SQUARE = 176;
        private const int BALL_GAP = 36;
        private const float BALL_FILL = 0.92f;

        //World units in front of the lens (nearer than anything the menu's frame holds at that spot, far past the near
        //plane), the turntable's rate in radians a second (slow: a look is read, not watched), the tilt of its axis
        //towards the viewer so the top is seen too, and the phase between neighbours so the column does not turn in step
        private const float BALL_DEPTH = 3f;
        private const float BALL_SPIN = 0.5f;
        private const float BALL_TILT = 0.35f;
        private const float BALL_PHASE = 0.7f;

        //How far up the Cutter's bore is drawn pointing (#767's verdict), as a slope away from the viewer: the overview lens
        //sees a loaded round from about 14 degrees above its bore at the opening aim (BallRenderSet.CUTTER_FACE_LIFT's note)
        private const float CUTTER_BORE_RISE = 0.25f;

        //Under each of the controls page's lines: a quarter of a keycap, so the nine lines and the two under them fit the
        //tab's frame (#997). 52 until then, the 26 of its own and the 26 of the column it stood in before #806 let every
        //block carry its own space, when the lines were the display face's two and three lines tall
        private const int KEY_LINE_GAP = 24;

        //Between a heading and its paragraph (#996), in design units: none of its own, because the display face's line
        //already stands a descender's depth under its capitals, which is less than a paragraph's gap above the heading
        private const int HEADING_GAP = 0;

        //Back's width in the row under the text (#738): a third of the frame's row, where every other entry on a page is one
        //column wide, so it stands under the text's first letter and leaves the row's right end to the scroll prompt (#857;
        //Previous and Next had the other two thirds until the tabs took their place)
        private const int WALK_BUTTON_WIDTH = 490;

        //PromptFont's own glyphs, the tutorial's constants exactly (Tutorial.cs) — one face, one set of
        //codepoints, so a key drawn here and the same key drawn on a card cannot come out different
        private const string KEY_W = "Ｗ", KEY_A = "Ａ", KEY_S = "Ｓ", KEY_D = "Ｄ", KEY_E = "Ｅ", KEY_Q = "Ｑ", KEY_R = "Ｒ";
        private const string MOUSE = "⟼", MOUSE_LEFT = "⟵", MOUSE_RIGHT = "⟶";

        //The tabs in the order a player needs them (#857): which key does what first - what a player opens Help for - then
        //the game's rule, the score, the odd balls, the campaign, and the glass that steps down. Short names, because six
        //tabs cut to the widest name's width must fit a row: measured with the section face at 1080p, this row is 1036 px
        //against the text frame's 930, and "HOW TO PLAY" in place of BASICS took it to 1210
        private const int TAB_CONTROLS = 0, TAB_BALLS = 3;
        private static readonly string[] TAB_NAMES = { "CONTROLS", "BASICS", "SCORING", "ODD BALLS", "CAMPAIGN", "THE GLASS" };

        //The tab that is up, the one asked for since the last frame (-1 for none, see SelectTab), and the one help=<n>
        //asked for before the page was entered (null for none, see ShowPage)
        private int _tab = TAB_CONTROLS;
        private int _pendingTab = -1;
        private int? _openOn;

        //What sits around the scroller, in 2160p design units: the heading, the tab row, the one row of Back and the
        //plate's padding (#606). 680 while the page counter's line stood where the tab row does (#738, #857): the row is
        //the section face's line (145) with its padding (2 × 14) and the gap under it (20), 193 against the counter's
        //line of 70 and its 30 of margin, so 93 more.
        private const int BODY_SURROUNDINGS = 773;

        //Design pixels a second the right stick scrolls the body at full tilt, and one Page Up/Down step's share
        //of the scroller's own height
        private const int STICK_SCROLL_SPEED = 2400;
        private const float PAGE_KEY_SHARE = 0.8f;

        //Built with the tree: the tab row, every tab's scroller, the shown one (what the pad and the keys scroll) and the
        //prompt that says it scrolls
        private TabRow _tabRow;
        private readonly ScrollViewer[] _scrollers = new ScrollViewer[TAB_NAMES.Length];
        private ScrollViewer _body;
        private Widget _scrollPrompt;
        private float _scrollCarry;

        //The layout the tree was built for (#1151): the phone's rail and two columns, or a monitor's row and one; the width
        //a column of text is set across (ProseWidth); and on the phone the space between the two columns and the height the
        //frame shows unscrolled, which a tab's columns are balanced against (TwoColumns). Pixels at the layout in force
        private bool _phone;
        private int _proseWidth, _columnGutter, _frameHeight;

        public HelpPage(BS3DGame game) : base(game)
        {
            //Bound once: the ball set's aside takes a method group, and a lambda here would be a closure a frame
            _collectBalls = CollectBalls;
        }

        /// <summary>
        /// Controls every time the page opens (#857), unless <c>help=&lt;n&gt;</c> asked for a tab first (<see cref="ShowPage"/>):
        /// "which key does what" is what a player opens Help for. The tree is built again on every entry, so the tab
        /// chosen here is the one it comes up on.
        /// </summary>
        public override void Enter()
        {
            _tab = _openOn ?? TAB_CONTROLS;
            _openOn = null;
            _pendingTab = -1;
            _cycleClock = -1f;
            _ballChosen = 0;
            InvalidateTree();
        }

        protected override Widget BuildTree()
        {
            _phone = PhoneLayout;
            _proseWidth = Scaled(TEXT_WIDTH);

            //The phone's odd-ball cards are built with its ODD BALLS tab, and nothing of them stands on a monitor's tree
            _ballCards = null;
            _ballCardNames = null;
            _ballCardStrips = null;
            _ballLead = _ballName = _ballText = null;
            _focused = null;

            if (_phone) return BuildRailTree();

            VerticalStackPanel column = MenuColumn();

            column.Widgets.Add(ScreenHeading("HELP"));

            //The tabs (#857), the Settings page's row: every topic visible at once and one click away, the chosen one
            //inverted. Not in the pad's walk; the shoulders, the arrow keys and the wheel over the row turn them
            //(PageSideways, OnScrollWheel), and a click asks Update to put the tab up (SelectTab)
            _tabRow = new TabRow(this, TAB_NAMES, SelectTab);
            column.Widgets.Add(_tabRow.Root);

            //Every tab's body in a scroller of its own, all in one place and one shown (ShowTab), every one given the
            //biggest's size (#729): the frame is as big as the biggest body's, so the tab row, Back and the plate's edges
            //stay where they are as the tabs turn. Minimums and not sizes, because MenuScroll's MaxHeight still bounds it
            //- a window too short for the tallest body scrolls it, as before. Width as well as height: a body that
            //overflows grows a scroll bar (17 px at 1920×1080) that the bodies that fit do not have. And never narrower
            //than the tab row (#770's rule): the row is wider than the text's frame at every size (measured where the
            //names are chosen), and a frame narrower than its own tabs read as a page cut short.
            Panel pageArea = new() { HorizontalAlignment = HorizontalAlignment.Center };
            Point frame = new(_tabRow.Width, 0);

            for (int tab = 0; tab < TAB_NAMES.Length; tab++)
            {
                //Every tab with the clear band at its clip's ends (#1002), the ones that fit as well: they stand in one frame,
                //and a tab without it would start its text a band higher than one that scrolls
                ScrollViewer scroller = MenuScroll(BuildBody(tab), BODY_SURROUNDINGS, clearEdges: true);
                Point size = scroller.Measure(new Point(int.MaxValue / 2, int.MaxValue / 2));
                frame = new Point(Math.Max(frame.X, size.X), Math.Max(frame.Y, size.Y));

                _scrollers[tab] = scroller;
                pageArea.Widgets.Add(scroller);
            }

            foreach (ScrollViewer scroller in _scrollers)
            {
                scroller.MinWidth = frame.X;
                scroller.MinHeight = frame.Y;
            }

            column.Widgets.Add(pageArea);

            //One row under the text (#738): Back at its left, under the text's first letter, and at its right what scrolls
            //the text (#799) - the right stick for a pad player, the wheel for one with the mouse - shown only while the
            //tab's body is longer than its frame (Update). The row is as wide as the frame, so Back stands where the text
            //starts and the prompt ends where the scroll bar does. Previous and Next stood in it until #857: the tabs
            //are every page at once, so the row is Back's.
            Panel walk = new() { Width = frame.X, HorizontalAlignment = HorizontalAlignment.Center };

            Button back = BackButton();
            back.Width = Scaled(WALK_BUTTON_WIDTH);
            back.HorizontalAlignment = HorizontalAlignment.Left;
            walk.Widgets.Add(back);
            walk.Widgets.Add(_scrollPrompt = ScrollPrompt());

            column.Widgets.Add(walk);

            ShowTab(_tab);

            return ScreenRoot(Plate(column, prose: true));
        }

        /// <summary>
        /// The phone layout (#1151): the tabs down a rail at the plate's left with Back at its foot, and beside it the heading
        /// and the tab's text set in two columns across the width, so a tab fits the screen or nearly where one column at a
        /// monitor's measure scrolled it (ODD BALLS was six screens). The owner: "It is widescreen, so we can use the space at
        /// the sides too - better to use it than to have frequent scrolling." Every tab still stands in the biggest one's
        /// frame and scrolls in it when it must (<see cref="TwoColumns"/>); the scroll prompt stands at the heading's right,
        /// where the rail's column has no room for a row of its own. ODD BALLS is a grid of cards beside the chosen one's words
        /// (<see cref="BuildBallCards"/>, #1183), and on the X300 every tab fits its frame (the run log's <c>[help]</c> line).
        /// </summary>
        private Widget BuildRailTree()
        {
            Button back = BackButton();
            _tabRow = new TabRow(this, TAB_NAMES, SelectTab, railFoot: back);

            //Two columns across what the rail leaves, each at most the prose measure (#806) - a wider screen stands its
            //plate narrower rather than its lines longer - with a gutter of a paragraph's gap and a half between them
            int gutter = ParagraphGap * 3 / 2;
            int width = Math.Min(RailContentWidth(_tabRow), 2 * ProseMeasure + gutter);
            _proseWidth = (width - gutter) / 2;
            _columnGutter = gutter;

            //The scroller's bound: the content's height less the heading's line. What it shows unscrolled (what a tab's
            //columns are balanced against, TwoColumns) is that less its clear band (#1002) at the top. Two bands of the
            //monitor's frame are not kept here (#1183), because the height is what decides whether CONTROLS and BASICS fit
            //(each ran a block past the fold, and CONTROLS ended mid-sentence there): the heading's own margin, since the
            //band under it already parts it from the text, and the band at the frame's foot, since what stands under the
            //text here is the plate's inset and not Back, which the band was there to keep a cut line off
            Label heading = ScreenHeading("HELP");
            heading.Margin = new Thickness(0);
            int band = Scaled(BS3DGame.MENU_SCROLL_EDGE_GAP);
            int bound = RailContentHeight - heading.Measure(new Point(int.MaxValue / 2, int.MaxValue / 2)).Y;
            _frameHeight = bound - band;

            Panel pageArea = new() { HorizontalAlignment = HorizontalAlignment.Left };
            Point frame = Point.Zero;

            //Each tab's text against the frame, said once a build (#1183): whether a tab fits the phone or scrolls is the page's
            //whole question there, and the run log answers it without a picture
            System.Text.StringBuilder fits = new($"[help] the phone layout's frame shows {_frameHeight} px, columns {_proseWidth} px:");

            for (int tab = 0; tab < TAB_NAMES.Length; tab++)
            {
                Widget body = BuildBody(tab);
                int tall = body.Measure(new Point(int.MaxValue / 2, int.MaxValue / 2)).Y;
                fits.Append($" {TAB_NAMES[tab]} {tall}{(tall <= _frameHeight ? "" : " (scrolls)")}");
                if (tab == TAB_BALLS && _ballCards != null) fits.Append($" (cards {_ballCardSize.X}x{_ballCardSize.Y} px)");
                if (tab < TAB_NAMES.Length - 1) fits.Append(',');

                //Bounded by what the rail page leaves rather than by a reserve of design units: the plate's insets here are
                //the rail page's own. The clear band at the top only (above)
                ScrollViewer scroller = MenuScroll(body, 0, clearEdges: true);
                scroller.Margin = new Thickness(0, band, 0, 0);
                scroller.MaxHeight = bound;
                scroller.HorizontalAlignment = HorizontalAlignment.Left;
                Point size = scroller.Measure(new Point(int.MaxValue / 2, int.MaxValue / 2));
                frame = new Point(Math.Max(frame.X, size.X), Math.Max(frame.Y, size.Y));

                _scrollers[tab] = scroller;
                pageArea.Widgets.Add(scroller);
            }

            foreach (ScrollViewer scroller in _scrollers)
            {
                scroller.MinWidth = frame.X;
                scroller.MinHeight = frame.Y;
            }

            Console.WriteLine(fits.ToString());

            //The heading over the text, and the scroll prompt at the line's right end, where Back's row had it
            Panel top = new() { Width = frame.X, HorizontalAlignment = HorizontalAlignment.Left };
            top.Widgets.Add(heading);
            top.Widgets.Add(_scrollPrompt = ScrollPrompt());

            VerticalStackPanel content = new() { Spacing = 0 };
            content.Widgets.Add(top);
            content.Widgets.Add(pageArea);

            ShowTab(_tab);

            return RailPage(_tabRow, content);
        }

        private Widget BuildBody(int page)
        {
            //The odd balls as cards on the phone (#1183): thirteen entries in two columns were three screens. A screen too narrow
            //for the cards and their panel gets none (null) and keeps the list
            if (_phone && page == TAB_BALLS && BuildBallCards() is Widget cards) return cards;

            VerticalStackPanel body = MenuColumn();

            //Against the scroller's left edge and its top rather than centred in it (#729, #865): every tab stands in the
            //biggest one's frame, so a tab that fits has room to spare, and it is left at the right and at the bottom. The
            //text then starts under the tabs where it does on a tab that scrolls, and does not drop as the tabs turn.
            body.HorizontalAlignment = HorizontalAlignment.Left;
            body.VerticalAlignment = VerticalAlignment.Top;

            //Each block carries the space under it (#806): a paragraph its ParagraphGap, a control's line KEY_LINE_GAP, so
            //the stack adds none of its own on top
            body.Spacing = 0;

            switch (page)
            {
                case TAB_CONTROLS: BuildControls(body); break;
                case 1: BuildPlaying(body); break;
                case 2: BuildScoring(body); break;
                case TAB_BALLS: BuildBalls(body); break;
                case 4: BuildCampaign(body); break;
                default: BuildCeiling(body); break;
            }

            return _phone ? TwoColumns(body) : body;
        }

        /// <summary>
        /// A tab's blocks in two columns across the phone layout's width (#1151), each block set at the column's width as it
        /// was built (<see cref="ProseWidth"/>). <b>Read down, then across</b> when the first column fits the frame unscrolled
        /// and the second nearly does (<see cref="NEARLY"/>): the blocks split where the taller column is shortest, so the
        /// columns end level, and only the end of the second, read last, may scroll into view. <b>Read across, then down</b>
        /// otherwise (ODD BALLS, thirteen entries): the blocks in pairs, row by row, so scrolling down never sends the eye
        /// back up to the top of the second column; a block marked to span (<see cref="SPANS"/>, the lines that head a group
        /// of entries) takes a row of its own.
        /// </summary>
        private Widget TwoColumns(VerticalStackPanel body)
        {
            //Taken out one by one rather than cleared, so each is let go of by the stack it was built in before it joins another
            Widget[] blocks = new Widget[body.Widgets.Count];
            for (int i = blocks.Length - 1; i >= 0; i--)
            {
                blocks[i] = body.Widgets[i];
                body.Widgets.RemoveAt(i);
            }

            Point free = new(_proseWidth, int.MaxValue / 2);
            int[] heights = new int[blocks.Length];
            int total = 0;
            for (int i = 0; i < blocks.Length; i++)
                total += heights[i] = blocks[i].Measure(free).Y;

            //The split that leaves the taller column shortest with the first within the frame: the first is read whole before
            //the second, so it must not need scrolling; the second may run a little past the bottom (NEARLY), read last
            int split = -1, tallest = int.MaxValue, above = 0;
            for (int i = 0; i <= blocks.Length && above <= _frameHeight; i++)
            {
                int taller = Math.Max(above, total - above);
                if (taller < tallest) (tallest, split) = (taller, i);
                if (i < blocks.Length) above += heights[i];
            }

            Grid grid = new()
            {
                ColumnSpacing = _columnGutter,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, _proseWidth));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, _proseWidth));

            if (split >= 0 && tallest <= _frameHeight * NEARLY)
            {
                grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
                for (int column = 0; column < 2; column++)
                {
                    VerticalStackPanel stack = new() { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                    for (int i = column == 0 ? 0 : split; i < (column == 0 ? split : blocks.Length); i++)
                        stack.Widgets.Add(blocks[i]);

                    Grid.SetColumn(stack, column);
                    grid.Widgets.Add(stack);
                }

                return grid;
            }

            int row = -1, side = 2;
            foreach (Widget block in blocks)
            {
                bool spans = block.Tag == SPANS;
                if (spans || side == 2)
                {
                    grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
                    row++;
                    side = 0;
                }

                block.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetRow(block, row);
                Grid.SetColumn(block, side);
                if (spans) Grid.SetColumnSpan(block, 2);
                grid.Widgets.Add(block);

                side = spans ? 2 : side + 1;
            }

            return grid;
        }

        /// <summary>The mark on a block that takes a row of its own in <see cref="TwoColumns"/>' pairs: a line that heads a group.</summary>
        private static readonly object SPANS = new();

        //How far past the frame's foot the second of two columns read down may run (#1151): CONTROLS on the phone, whose
        //touch scheme and first lines fill the first column and whose last lines scroll a little into view
        private const float NEARLY = 1.25f;

        /// <summary>
        /// Asks for a tab - a click, the sideways axis, the shoulders, the wheel. Put up by <see cref="Update"/> rather than
        /// here, because a click arrives from inside Myra's own processing of the tree whose scrollers it would be swapping.
        /// </summary>
        private void SelectTab(int tab) => _pendingTab = tab;

        /// <summary>
        /// Puts a tab up (#857): its scroller shown from the top and the rest hidden, the row painted, and the body the pad
        /// and the keys scroll (<see cref="OnScrollAxis"/>) and the prompt follow it. Nothing is rebuilt and no focus is
        /// moved: Back is the one entry on every tab but the phone's ODD BALLS, whose cards are entries too (#1183), so a turn
        /// to or from it on a page that stands asks the host to read the entries again (<paramref name="standing"/>), the
        /// cursor kept where it was if it is still an entry.
        /// </summary>
        private void ShowTab(int tab, bool standing = false)
        {
            _tab = Math.Clamp(tab, 0, TAB_NAMES.Length - 1);
            _scrollCarry = 0f;

            for (int t = 0; t < _scrollers.Length; t++)
                _scrollers[t].Visible = t == _tab;

            _body = _scrollers[_tab];
            _body.ScrollPosition = Point.Zero;
            _tabRow.Show(_tab);

            if (standing && _ballCards != null && IsActive && IsBuilt) Game.RefreshNavEntries(_focused);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_pendingTab >= 0)
            {
                int tab = _pendingTab;
                _pendingTab = -1;
                if (tab != _tab) ShowTab(tab, standing: true);
            }

            //The scroll prompt while there is something to scroll: the body is laid out by the frame just drawn, so a tab
            //that fits its frame, or a window tall enough for it, shows none
            bool scrolls = _body != null && _body.ScrollMaximum.Y > 0;
            if (_scrollPrompt != null && _scrollPrompt.Visible != scrolls) _scrollPrompt.Visible = scrolls;
        }

        /// <summary>
        /// Left or right on the arrows, the D-pad, the left stick or the pad's shoulders turns the tab, wrapping round
        /// (#857, as Settings' row does, #686). Until #857 the page turned the same way but clamped, a reference being read
        /// front to back; a row of tabs is read in any order, and the wrap is what lets Controls and The glass sit a press apart.
        /// </summary>
        internal override bool PageSideways(int direction)
        {
            SelectTab((_tab + direction + TAB_NAMES.Length) % TAB_NAMES.Length);
            return true;
        }

        /// <summary>
        /// The wheel over the tab row turns the tab (#686's rule): away from the player is the next one. Over the body it
        /// is the scroller's own (<see cref="MenuScrollViewer"/>, a fifth of the body a notch) and reaches nothing here.
        /// </summary>
        internal override void OnScrollWheel(int delta)
        {
            if (_tabRow == null || !_tabRow.IsMouseOver) return;

            if (PageSideways(delta > 0 ? 1 : -1)) Game.Audio?.PlayUiTick();
        }

        /// <summary>The odd balls' live pictures (#856), while that tab is up and the tree stands.</summary>
        internal override bool PresentsForeground => IsActive && IsBuilt && _tab == TAB_BALLS && _body != null;

        /// <summary>The tab's scroller: a ball beside an entry scrolled past its edge is cut where the words are.</summary>
        internal override Rectangle ForegroundClip => _body == null ? Rectangle.Empty : OnDesktop(_body);

        /// <summary>
        /// Where a widget stands on the desktop, in the back buffer's pixels. Myra keeps <c>ActualBounds</c> in the widget's
        /// own space, (0, 0) at its corner (the host's <c>ScrollNavEntryIntoView</c> learned it on the picker); <c>ToGlobal</c>
        /// is what puts it where it is drawn, the scroller's offset included.
        /// </summary>
        private static Rectangle OnDesktop(Widget widget)
        {
            Rectangle local = widget.ActualBounds;
            Point corner = widget.ToGlobal(new Point(local.X, local.Y));
            return new Rectangle(corner.X, corner.Y, local.Width, local.Height);
        }

        /// <summary>
        /// The odd balls, live, in their squares (#856): each the kind itself through the game's own ball path — one more
        /// ball in a frame of the ball set's, hung <see cref="BALL_DEPTH"/> in front of the lens on the ray through its
        /// square and sized to fill it, turning on a tilted axis — so it is lit, glows and breathes as that kind does on
        /// the map, in the style the backdrop's cluster hangs in. The kind card's own device (#735,
        /// <c>GameplayScreen.CollectTutorialBall</c>), once for each entry of <see cref="OddBallCopy.Entries"/>. The squares are read where the desktop laid them
        /// out last, in the back buffer's pixels, and turned into the frame's normalised coordinates; one scrolled wholly
        /// past the frame is left out, one scrolled partly past it is drawn and cut by the host (the desktop's layer) or
        /// left out (the Potato path, which draws straight over the frame and cannot cut).
        /// </summary>
        internal override void DrawForeground()
        {
            var buffer = Game.GraphicsDevice.PresentationParameters;
            if (buffer.BackBufferWidth <= 0 || buffer.BackBufferHeight <= 0) return;

            //What the collector reads, set here rather than captured: the collector is a method group bound once, so the
            //frame allocates nothing (BallDrawFrame is a ref struct and cannot be caught in a lambda anyway)
            _ballFrameRect = OnDesktop(_body);
            _ballBufferWidth = buffer.BackBufferWidth;
            _ballBufferHeight = buffer.BackBufferHeight;
            _ballProjection = Game.Camera.Projection;
            _ballToWorld = Matrix.Invert(Game.Camera.View);
            _ballSpin = Game.WallClock * BALL_SPIN;

            //The rainbow ball's colours (#895): the page's own cycle, stepped by the wall clock's elapsed time as the game's
            //is (Magazine.Step's reason: what a loaded ball shows is the gun saying so, not the simulation), and STATED to
            //the ball set before the aside opens its frame - the set holds one crossing for the whole program, and only a
            //session states it, so on a Help opened from the main menu an unstated set draws the ball as a plain one of the
            //first colour. Under a Help opened from the pause the session has drawn its own wildcards already, and states
            //its crossing afresh before every collection of its own (GameplayScreen), so this one cannot reach them.
            //A frame that did not draw the page leaves a gap the next one steps across whole (Step loops), and the first
            //frame after entering steps nothing
            float now = Game.WallClock;
            if (_cycleClock >= 0f) _cycle.Step(Math.Max(0f, now - _cycleClock));
            _cycleClock = now;
            Game.Balls.SetWildcardCrossing(_cycle.From, _cycle.To, _cycle.Progress, _cycle.Returning);

            //An aside of the ball set's, after the backdrop's or the session's own frame: its motion record is kept, so the
            //session's velocity pass still finds its balls under a Help opened from the pause
            Game.Balls.DrawAside(Game.Camera, Game.WallClock, _collectBalls);
        }

        //The collector DrawForeground hands the ball set, bound once (see there), and what it reads
        private readonly BallRenderSet.AsideCollector _collectBalls;
        private Rectangle _ballFrameRect;
        private float _ballBufferWidth, _ballBufferHeight, _ballSpin;
        private Matrix _ballProjection, _ballToWorld;

        //The rainbow ball's crossing between colours (#895), every colour in the cycle's own order - the page has no cluster
        //whose colours it should keep to - and the wall clock of the frame that last stepped it (negative: none yet since
        //the page was entered)
        private readonly WildcardCycle _cycle = new();
        private float _cycleClock = -1f;

        private void CollectBalls(BallDrawFrame balls)
        {
            for (int i = 0; i < _ballSquares.Length; i++)
            {
                OddBallCopy.Entry entry = OddBallCopy.Entries[i];
                if (!TryPlaceBall(_ballSquares[i], i, out Vector3 position, out Matrix world, out float worldRadius)) continue;

                //The Cutter's saw takes its frame from the round's own, which in the gun is the barrel's (#767's verdict,
                //BallRenderSet.CutterFrame), and turns itself: the page's turntable would swing its face away. So it is
                //handed a barrel's frame as the bore holds one, away from the viewer and up by the rise the overview lens
                //sees a loaded round's bore at
                if (entry.Kind == BallKind.Cutter)
                {
                    world = Matrix.CreateScale(worldRadius / Constants.HALF) * Matrix.CreateWorld(Vector3.Zero,
                        Vector3.Normalize(_ballToWorld.Forward + _ballToWorld.Up * CUTTER_BORE_RISE), _ballToWorld.Up);
                    world.M41 = position.X;
                    world.M42 = position.Y;
                    world.M43 = position.Z;
                }

                //The cannon's two as they sit loaded in it (#895): still, so the rainbow ball does not breathe and the Cutter's
                //blade idles at its loaded rate - the look the player will see in the bore, which is what this entry is for
                balls.Add(entry.Colour, position, world, BallRenderSet.UNOCCLUDED, still: entry.InGun, kind: entry.Kind);
            }

            //The dead weight (#1023) as the map draws it: an ordinary ball with its pixels dithered out, and still, since a
            //ball the cluster has let go of does not breathe (#473)
            if (TryPlaceBall(_deadSquare, _ballSquares.Length, out Vector3 deadAt, out Matrix deadWorld, out _))
                balls.Add(OddBallCopy.DeadColour, deadAt, deadWorld, BallRenderSet.UNOCCLUDED, dissolve: BallRenderSet.DEAD_DISSOLVE,
                    still: true);
        }

        /// <summary>
        /// Where the ball of the square <paramref name="square"/> hangs (#856), its <paramref name="phase"/>th turntable's,
        /// or false for a square scrolled wholly past the frame (or, on the Potato path, partly past it).
        /// </summary>
        private bool TryPlaceBall(Panel square, int phase, out Vector3 position, out Matrix world, out float worldRadius)
        {
            position = default;
            world = default;
            worldRadius = 0f;
            if (square == null) return false;

            Rectangle at = OnDesktop(square);
            if (!at.Intersects(_ballFrameRect) || (BS3DGame.PotatoPath && !_ballFrameRect.Contains(at))) return false;

            //The square's centre and half-height in the frame's normalised coordinates (x and y −1..1, y up)
            float cx = (at.X + at.Width * 0.5f) / _ballBufferWidth * 2f - 1f;
            float cy = 1f - (at.Y + at.Height * 0.5f) / _ballBufferHeight * 2f;
            float radius = at.Height / _ballBufferHeight * BALL_FILL;

            //The point on the ray through the square at the chosen depth, in view space and then in the world
            Vector3 inView = new(cx * BALL_DEPTH / _ballProjection.M11, cy * BALL_DEPTH / _ballProjection.M22, -BALL_DEPTH);
            position = Vector3.Transform(inView, _ballToWorld);
            worldRadius = radius * BALL_DEPTH / _ballProjection.M22;

            //Sized, turned about the camera's up, then the whole tilted about its right, so the axis leans towards
            //the viewer and the top of the ball is seen as well as its side
            world = Matrix.CreateScale(worldRadius / Constants.HALF)
                * Matrix.CreateFromAxisAngle(_ballToWorld.Up, _ballSpin + phase * BALL_PHASE)
                * Matrix.CreateFromAxisAngle(_ballToWorld.Right, BALL_TILT);
            world.M41 = position.X;
            world.M42 = position.Y;
            world.M43 = position.Z;

            return true;
        }

        /// <summary>
        /// Scrolls the body by the pad's right stick (<paramref name="stick"/>, up positive) and by Page Up/Page
        /// Down (<paramref name="pageSteps"/>, down positive), over <paramref name="elapsed"/> seconds (#606).
        /// </summary>
        internal override void OnScrollAxis(float stick, int pageSteps, float elapsed)
        {
            if (_body == null) return;

            float pixels = -stick * Scaled(STICK_SCROLL_SPEED) * elapsed + _scrollCarry
                           + pageSteps * _body.ActualBounds.Height * PAGE_KEY_SHARE;

            int whole = (int)pixels;
            _scrollCarry = pixels - whole;
            if (whole == 0) return;

            Point at = _body.ScrollPosition;
            _body.ScrollPosition = new Point(at.X, Math.Clamp(at.Y + whole, 0, Math.Max(0, _body.ScrollMaximum.Y)));
        }

        /// <summary>
        /// Opens on a stated tab, 1-based in the row's order (Controls 1, Basics 2, Scoring 3, Odd balls 4, Campaign 5, The
        /// glass 6) — the <c>help=</c> argument's way in (#427; the numbers are the tabs' since #857). Out of range is
        /// clamped rather than refused: a capture script asking for page nine should photograph the last tab, not crash
        /// the game it was pointed at. Before the page is entered it is what <see cref="Enter"/> opens on instead of
        /// Controls; on a page that is up it is put up at once.
        /// </summary>
        internal void ShowPage(int page)
        {
            int wanted = Math.Clamp(page - 1, 0, TAB_NAMES.Length - 1);

            if (IsActive && IsBuilt)
            {
                if (wanted != _tab) ShowTab(wanted, standing: true);
            }
            else _openOn = wanted;
        }

        private void BuildPlaying(VerticalStackPanel column)
        {
            //Every paragraph under a heading of its own (#996), so that the headings read down the page are the game in five
            //phrases for a player who wants it in five seconds
            column.Widgets.Add(Headed("The goal",
                "A cluster of coloured balls hangs from a sheet of glass above a stone island. You stand under it with a "
                + "cannon and fire balls up into it. Clear every ball that can be matched and the level is done."));
            column.Widgets.Add(Headed("Match & drop",
                "A shot sticks where it lands. Land it so that three or more of the same colour touch, and the whole "
                + "group falls — and everything that was hanging from it falls with it. The group you match is usually "
                + "the smaller half of what you bring down."));
            column.Widgets.Add(Headed("Find the anchor",
                "So the shot to look for is rarely the biggest clump of your colour. It is the one ball that is holding "
                + "a lot of other balls up."));
            column.Widgets.Add(Headed("Ammo & queue",
                "You have a fixed number of balls per level, shown at the bottom left. The queue at the bottom right "
                + "shows what you are about to fire and what comes after it, so you can plan two shots ahead."));

            //The colour cue (#737), where the balls carry it: a pattern that only ever means its colour, said once so
            //that nobody reads a striped or a spotted ball as a special one. Not on the Potato path, which draws none.
            if (Game.Balls?.DrawsColourCue == true)
                column.Widgets.Add(Headed("Patterns",
                    "Every colour also wears a pattern of its own: stripes, spots, checks or a printed symbol, "
                    + "depending on what the balls are made of. Every ball of a colour wears the same one, and so does "
                    + "its disc in the queue, so two colours that look alike still differ in pattern."));
        }

        private void BuildScoring(VerticalStackPanel column)
        {
            //Five values and a tip (#998, #1045): what scores, with the figure in the heading so the headings down the tab are the price
            //list, and nothing of the arithmetic behind it. Every figure is ScoreKeeper's own, written through string
            //interpolation rather than typed out, so a tuned constant cannot leave a stale number standing on this page.
            //What the game shows in play as it scores (the caption over the middle of the screen, the shattering multiplier,
            //the bonus flying to the score) is not narrated: the player sees it there. The streak's heading names its cap in
            //words: Anton's × read as "·5" when the heading was a run-in lead at the small face's size (photographed)
            column.Widgets.Add(Headed($"Matched balls · {ScoreKeeper.MatchedBallPoints} points each",
                "Every ball of the same-colour group your shot completes."));
            column.Widgets.Add(Headed($"Detached balls · {ScoreKeeper.OrphanedBallPoints} points each",
                "The balls that fall because the group was the last thing holding them up. Twice a matched one: bringing a "
                + "mass down is the better shot, and the score says so."));
            column.Widgets.Add(Headed($"Streak · multiplier up to {ScoreKeeper.MaxMultiplier}",
                $"Every shot that scores raises the multiplier by\u00A0{ScoreKeeper.MultiplierStep} for the shots "
                + "after it. A shot that matches nothing puts it back to ×1."));
            //The time bonus (#1045) is the price list's biggest line and the one four stars turn on, so it is said with its figure
            //like the others: ScoreKeeper's own worth a ball of the level, and its drain in words. The clock's pause under the
            //camera is said because a player watching the readout stand still during a drop would otherwise take it for a fault
            column.Widgets.Add(Headed($"Time bonus · up to {ScoreKeeper.TimeBonusWorthInFloors * ScoreKeeper.MatchedBallPoints} points a ball",
                "The faster you clear the level, the more it pays. It is shown under the score and drains every second: half of it "
                + "is gone at the level’s par time, all of it at twice par. The clock stops while the camera follows falling balls."));
            column.Widgets.Add(Headed("Unused balls · a small bonus",
                "Every ball still in the cannon when the field is clear pays what an average shot of the level brings down: the "
                + "cannon fires them off and counts them."));
            column.Widgets.Add(Headed("Tip",
                "Four stars need a fast, clean clear: beat the par time and keep the streak going."));
        }

        //The squares the kinds' live balls are drawn in (#856), one per entry of OddBallCopy.Entries and at the entry's index,
        //built with the tree; DrawForeground reads where each stands. And the dead weight's (#1023), after them
        private readonly Panel[] _ballSquares = new Panel[OddBallCopy.Entries.Length];
        private Panel _deadSquare;

        private void BuildBalls(VerticalStackPanel column)
        {
            //The lines that head a group span both of the phone layout's columns when its entries stand in pairs (#1151,
            //TwoColumns): a group's line over the first of its pair only read as that one entry's
            column.Widgets.Add(Spanning(Paragraph(BALLS_LEAD)));

            //The cluster's kinds, then a line of their own, then the two the cannon holds (#895). Which group a kind is in is
            //the entry's own (OddBallCopy.Entry.InGun) and the table keeps the cluster's first, so the squares stand in the
            //table's order whichever way this walks it
            AddOddBalls(column, inGun: false);
            column.Widgets.Add(Spanning(Paragraph(OddBallCopy.GunLead)));
            AddOddBalls(column, inGun: true);

            //And the dead weight (#1023), a ball of any colour on its way out rather than a kind, under a line of its own
            column.Widgets.Add(Spanning(Paragraph(OddBallCopy.DeadLead)));
            column.Widgets.Add(BallRow(out _deadSquare, OddBallCopy.DeadName, OddBallCopy.DeadText));

            column.Widgets.Add(Spanning(Aside(POWERUPS_ASIDE, ProseWidth)));
        }

        //The tab's own lines: what stands before the cluster's kinds, and the aside under them all
        private const string BALLS_LEAD = "Most balls are ordinary. These are not — the first few you will meet as the campaign goes on.";
        private const string POWERUPS_ASIDE = "And the power-ups — the Swap, the Brake and the Cut — are on the Controls tab.";

        //THE ODD BALLS AS CARDS on the phone layout (#1183): a card a kind, its live ball over its name, in rows of
        //WideMenuLayout.ODD_BALL_COLUMNS - the cluster's kinds first, then a row of their own for the two the cannon holds and
        //the dead weight - and beside them a panel with the chosen card's group line, name and words. Thirteen entries with
        //their words in two columns were about three of the X300's screens; the words of one at a time and every ball at once
        //fit one. The ball's square (WideMenuLayout.ODD_BALL_SQUARE) is larger than the list's, since a card has the height
        //for it and the ball is what a player who met a strange one looks for; it alone is a finger's target tall
        private const int BALL_CARD_COLUMNS = WideMenuLayout.ODD_BALL_COLUMNS;
        private const int BALL_CARD_SQUARE = WideMenuLayout.ODD_BALL_SQUARE;

        //Inside a card, in design units: the room either side of the widest name, between the ball and its name, and above
        //and below the name inside its strip
        private const int BALL_CARD_SIDE = 30;
        private const int BALL_CARD_LINE_GAP = 6;
        private const int BALL_CARD_STRIP_Y = 6;

        //The cards, one per OddBallCopy.Entries entry at its index and the dead weight's last, their names and the strips
        //they stand on, the one chosen (what the panel says), and the panel's three labels. Null on a monitor's layout
        private Button[] _ballCards;
        private Label[] _ballCardNames;
        private Panel[] _ballCardStrips;
        private Point _ballCardSize;
        private int _ballChosen;
        private Label _ballLead, _ballName, _ballText;

        //The entry the pad's cursor stands on (NavFocusChanged), null while the pointer has it
        private Button _focused;

        //The cards' entries, the table's and then the dead weight's
        private static int BallCount => OddBallCopy.Entries.Length + 1;
        private static bool IsDead(int card) => card == OddBallCopy.Entries.Length;
        private static string BallName(int card) => IsDead(card) ? OddBallCopy.DeadName : OddBallCopy.Entries[card].Name;
        private static string BallText(int card) => IsDead(card) ? OddBallCopy.DeadText : OddBallCopy.Entries[card].Text;

        //The line over a card's words in the panel: its group's, as the list's lines head the groups
        private static string BallLead(int card) =>
            IsDead(card) ? OddBallCopy.DeadLead : OddBallCopy.Entries[card].InGun ? OddBallCopy.GunLead : BALLS_LEAD;

        /// <summary>
        /// The odd balls on the phone layout (#1183): the cards, the power-ups' aside under them, and the chosen card's panel
        /// beside them. A card is an entry (a press chooses it, and the pad's cursor on it chooses it, <see cref="NavFocusChanged"/>),
        /// so the pad walks the grid (<see cref="PadWalksGrid"/>). The chosen card's name stands on the inverted slab a choice
        /// wears on every page (<see cref="PaintBallCards"/>). The panel's lines are as tall as the tallest each can say, so a
        /// choice moves nothing.
        /// </summary>
        private Widget BuildBallCards()
        {
            FontStashSharp.SpriteFontBase nameFont = Game.MenuFontLead;
            int square = Scaled(BALL_CARD_SQUARE);
            int nameWidth = 0;
            for (int card = 0; card < BallCount; card++)
                nameWidth = Math.Max(nameWidth, (int)MathF.Ceiling(nameFont.MeasureString(BallName(card)).X));

            //MenuTile's own padding (18 x 12 units) inside the card, round the ball and the widest name on its strip
            int cardWidth = Math.Max(square, nameWidth) + 2 * Scaled(BALL_CARD_SIDE);
            int cardHeight = square + Scaled(BALL_CARD_LINE_GAP) + nameFont.LineHeight + 2 * Scaled(BALL_CARD_STRIP_Y) + 2 * Scaled(12);

            //The panel takes what the cards leave of the tab's two columns, at most the prose measure. A screen too narrow for
            //it at half the measure (a squarer phone drawn larger) keeps the list in two columns, which scrolls
            int gap = Scaled(WideMenuLayout.TILE_GAP);
            int gridWidth = BALL_CARD_COLUMNS * cardWidth + (BALL_CARD_COLUMNS - 1) * gap;
            int panelWidth = Math.Min(ProseMeasure, 2 * _proseWidth - gridWidth);
            if (panelWidth < ProseMeasure / 2) return null;

            _ballCards = new Button[BallCount];
            _ballCardNames = new Label[BallCount];
            _ballCardStrips = new Panel[BallCount];
            _ballChosen = Math.Clamp(_ballChosen, 0, BallCount - 1);
            _ballCardSize = new Point(cardWidth, cardHeight);

            Grid cards = new()
            {
                ColumnSpacing = gap,
                RowSpacing = gap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            for (int c = 0; c < BALL_CARD_COLUMNS; c++) cards.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

            //Across and then down, a new row for the cannon's two and the dead weight after the cluster's kinds
            int row = 0, column = 0;
            bool second = false;
            for (int card = 0; card < BallCount; card++)
            {
                if (!second && (IsDead(card) || OddBallCopy.Entries[card].InGun))
                {
                    second = true;
                    if (column > 0) (row, column) = (row + 1, 0);
                }

                Button tile = BallCard(card, cardWidth, cardHeight, square, nameFont);
                Grid.SetRow(tile, row);
                Grid.SetColumn(tile, column);
                cards.Widgets.Add(tile);

                if (++column == BALL_CARD_COLUMNS) (row, column) = (row + 1, 0);
            }

            VerticalStackPanel left = new() { Spacing = ParagraphGap, VerticalAlignment = VerticalAlignment.Top };
            left.Widgets.Add(cards);
            left.Widgets.Add(Aside(POWERUPS_ASIDE, gridWidth));

            VerticalStackPanel panel = new() { Spacing = 0, VerticalAlignment = VerticalAlignment.Top };
            _ballLead = Aside(BallLead(_ballChosen), panelWidth);
            _ballLead.Height = ProseHeight(_ballLead, BALLS_LEAD, OddBallCopy.GunLead, OddBallCopy.DeadLead);
            panel.Widgets.Add(_ballLead);

            _ballName = new Label { Text = BallName(_ballChosen), Font = FontBody, TextColor = BS3DGame.MENU_TEXT };
            panel.Widgets.Add(_ballName);

            _ballText = Paragraph(BallText(_ballChosen), panelWidth);
            string[] texts = new string[BallCount];
            for (int card = 0; card < BallCount; card++) texts[card] = BallText(card);
            _ballText.Height = ProseHeight(_ballText, texts);
            panel.Widgets.Add(_ballText);

            Grid page = new()
            {
                ColumnSpacing = _columnGutter,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            page.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            page.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            page.Widgets.Add(left);
            Grid.SetColumn(panel, 1);
            page.Widgets.Add(panel);

            PaintBallCards();
            return page;
        }

        /// <summary>
        /// One card (#1183): the kind's square over its name, both centred, the square the one <see cref="DrawForeground"/>
        /// hangs the live ball in (the table's entry's, or the dead weight's).
        /// </summary>
        private Button BallCard(int card, int width, int height, int square, FontStashSharp.SpriteFontBase nameFont)
        {
            Panel ball = new() { Width = square, Height = square, HorizontalAlignment = HorizontalAlignment.Center };
            if (IsDead(card)) _deadSquare = ball;
            else _ballSquares[card] = ball;

            Label name = new()
            {
                Text = BallName(card),
                Font = nameFont,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            //The name on a strip of the card's width inside its padding: the strip is what a choice inverts (PaintBallCards)
            Button tile = Game.MenuTile(new Panel(), () => ChooseBall(card), 0, 0);
            Panel strip = new()
            {
                Width = width - tile.Padding.Left - tile.Padding.Right,
                Padding = new Thickness(0, Scaled(BALL_CARD_STRIP_Y)),
            };
            strip.Widgets.Add(name);

            VerticalStackPanel content = new()
            {
                Spacing = Scaled(BALL_CARD_LINE_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            content.Widgets.Add(ball);
            content.Widgets.Add(strip);

            tile.Content = content;
            tile.Width = width;
            tile.Height = height;

            _ballCards[card] = tile;
            _ballCardNames[card] = name;
            _ballCardStrips[card] = strip;
            return tile;
        }

        /// <summary>A card pressed (#1183): its words in the panel. The cursor stays where it is.</summary>
        private void ChooseBall(int card)
        {
            if (_ballCards == null || card == _ballChosen) return;

            _ballChosen = card;
            SetProse(_ballLead, BallLead(card));
            _ballName.Text = BallName(card);
            SetProse(_ballText, BallText(card));
            PaintBallCards();
        }

        /// <summary>
        /// The chosen card's name on the inverted slab a choice wears on every page, in the dark, and every other name on the
        /// card's own slab (#1183). The name's strip and not the card: a card is mostly its ball, and with the whole card
        /// inverted the glass ball vanished into the white (photographed). The card's slab is the host's to paint, the pad's
        /// cursor's included, so the cursor on the chosen card reads as both.
        /// </summary>
        private void PaintBallCards()
        {
            if (_ballCards == null) return;

            for (int card = 0; card < _ballCards.Length; card++)
            {
                bool chosen = card == _ballChosen;
                _ballCardStrips[card].Background = chosen ? SELECTED_BRUSH : null;
                _ballCardNames[card].TextColor = chosen ? SELECTED_TEXT : BS3DGame.MENU_TEXT;
            }
        }

        /// <summary>
        /// The pad's cursor on a card chooses it (#1183): the panel says what the cursor stands on, as the level picker's
        /// panel does, so a pad player reads the kinds by walking the grid.
        /// </summary>
        internal override void NavFocusChanged(Button focused)
        {
            _focused = focused;
            if (_ballCards == null) return;

            int card = focused == null ? -1 : Array.IndexOf(_ballCards, focused);
            if (card >= 0) ChooseBall(card);
            PaintBallCards();
        }

        /// <summary>The phone's odd-ball cards are walked by the pad in the direction pressed (#1183), as the Scene grid is.</summary>
        internal override bool PadWalksGrid => _ballCards != null && _tab == TAB_BALLS;

        private static Widget Spanning(Widget block)
        {
            block.Tag = SPANS;
            return block;
        }

        /// <summary>
        /// The entries of one group, each kind beside its words (#856): a square at the paragraph's left, top-aligned with its
        /// first line, the words set across the text's width less the square and the gap. The square is a widget so that it
        /// takes its room in the body's flow and the scroller carries it; what is drawn in it is the frame's own ball
        /// (<see cref="DrawForeground"/>), found again by the entry's index in <see cref="OddBallCopy.Entries"/>.
        /// </summary>
        private void AddOddBalls(VerticalStackPanel column, bool inGun)
        {
            for (int i = 0; i < OddBallCopy.Entries.Length; i++)
            {
                if (OddBallCopy.Entries[i].InGun != inGun) continue;

                column.Widgets.Add(BallRow(out _ballSquares[i], OddBallCopy.Entries[i].Name, OddBallCopy.Entries[i].Text));
            }
        }

        /// <summary>One entry with its ball (#856): the square at the left, the heading and the words beside it.</summary>
        private Widget BallRow(out Panel ballSquare, string name, string text)
        {
            int square = Scaled(BALL_SQUARE), gap = Scaled(BALL_GAP);

            HorizontalStackPanel row = new() { Spacing = gap, HorizontalAlignment = HorizontalAlignment.Left };

            //With the paragraph's own gap under it, so an entry of one line is not a row the next square touches
            ballSquare = new Panel
            {
                Width = square,
                Height = square,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, ParagraphGap),
            };
            row.Widgets.Add(ballSquare);
            row.Widgets.Add(Headed(name, text, ProseWidth - square - gap));

            return row;
        }

        private void BuildCampaign(VerticalStackPanel column)
        {
            //In game terms and in the order a player asks (#1000): what a chapter is, what a star is for, that the cup is the
            //stars handed over, and that stars open the way on. The arithmetic behind a threshold and the last level's price
            //are the level picker's, where the player is looking at the lock. The level and chapter counts are the set's own
            //(#606: the page once said 120 levels in 12 chapters while the game shipped 130 in 13)
            column.Widgets.Add(Headed("Chapters",
                $"{Game.LevelCount}\u00A0levels in {Game.BlockCount}\u00A0chapters of ten, each against one backdrop and with a "
                + "puzzle of its own — towers, things hidden inside other things, mathematics."));
            //The stars as rules v2 rates them (#1045, #1185): every star past the first is score (StarRating's multiples of the
            //floor) and the time bonus is the biggest line of it, so four are said as SCORING's tip says them, a clean clear
            //well inside the par time (about three quarters of par in ScoreSim's casual shots), never by shots alone
            column.Widgets.Add(Headed("Stars",
                "Every level you clear earns one to four stars. One is for clearing it, the rest for your score, and the "
                + "faster the clear, the more it scores. Four need a clean clear well inside the par time."));
            column.Widgets.Add(Headed("Cups",
                "A cleared level hands you a cup for its stars: bronze for one, silver for two, gold for three and crystal "
                + "for four."));
            column.Widgets.Add(Headed("Stars open the way",
                "Later levels ask for a total of stars across everything you have played, so a level you scraped through is "
                + "worth playing again."));
        }

        private void BuildCeiling(VerticalStackPanel column)
        {
            //Each block under a heading, and the two things on the screen a player has to look for in bold (#1001): the panel on
            //the left and the red line in it. The warning is the floor's laser net (docs/game-feedback, "The floor's laser
            //net"), red because it is the ceiling flash's own red, and it comes a few steps early, LASER_WARN_STEPS of them
            column.Widgets.Add(Headed("The glass steps down",
                "The cluster does not hang still for ever. Every few shots the glass it hangs from steps down, and the whole "
                + $"cluster comes with it. The {Strong("panel on the left")} of the screen shows the field, the glass at the top "
                + "of it and how far down it has come."));
            column.Widgets.Add(Headed("The red line",
                $"At the bottom of that panel is a {Strong("red line")}. If the cluster reaches it, the level is lost. A red "
                + "net of light over the island says it is a few steps away, and once the cluster is over the line you have "
                + "about a second to knock balls off its bottom."));
            column.Widgets.Add(Headed("Your shots are the clock",
                "Every shot brings the glass nearer, whether it matched anything or not, which is the other reason a miss is "
                + "expensive. Clearing from the bottom buys height; clearing from the top does not."));
            column.Widgets.Add(Headed("Throwing away is fair",
                "When the ball in the gun has nowhere to score, fire it into empty space, or onto a ball of another colour. "
                + "It costs a shot and your streak, not the level."));
            column.Widgets.Add(Headed("The brake",
                $"From chapter\u00A0{LevelSet.BRAKE_FROM_BLOCK}, one press a level lifts the glass a step back up, which is "
                + "time the ceiling took. It does nothing until the glass has stepped down."));
        }

        //The pad's half of every line (#799), the tutorial's bindings exactly (Tutorial.cs): the right stick aims, the right
        //trigger fires, the left holds the precise aim, the left stick walks and steps, X, Y and the right bumper are the
        //three power-ups, and the Menu button pauses. Each line is the action's name and what the key does, and nothing
        //more (#997): the mechanics behind a power-up are the Cutter's entry on ODD BALLS and THE GLASS's brake, and the
        //chapter it arrives in is LevelSet's own constant, so a moved power-up cannot leave a stale chapter here
        //Precise aim's line as Settings has it (#1054): the table's words are the hold's, and a toggle's replace them
        private const string PRECISE_AIM = "Precise aim";

        //Fire's words, which name a key: not on the phone layout, which has no keys' column (#1183, KeyLine)
        private const string FIRE_KEY = "Space fires too.";
        private const string PRECISE_AIM_TOGGLE = "Press it to look down the barrel, and again to come back.";

        private static readonly (string Keys, string Pad, string Name, string What)[] CONTROLS =
        {
            (MOUSE, PromptGlyph.PAD_RIGHT_STICK, "Aim", ""),
            (MOUSE_LEFT, PromptGlyph.PAD_RT, "Fire", FIRE_KEY),
            (MOUSE_RIGHT, PromptGlyph.PAD_LT, PRECISE_AIM, "Hold it to look down the barrel."),
            (KEY_A + KEY_D, PromptGlyph.PAD_LEFT_STICK_SIDEWAYS, "Walk", "Move the gun round the field."),
            (KEY_W + KEY_S, PromptGlyph.PAD_LEFT_STICK_UP_DOWN, "Step", "In and out: closer means a steeper shot."),
            (KEY_E, PromptGlyph.PAD_X, "Swap", "The next two balls change places."),
            (KEY_Q, PromptGlyph.PAD_Y, "Brake", "Lifts the glass one step back up."),
            (KEY_R, PromptGlyph.PAD_RB, "Cut", "Your next ball destroys the whole storey it hits."),
            (PromptGlyph.KEY_ESCAPE, PromptGlyph.PAD_MENU, "Pause", $"{BS3DGame.NOTE_ENTRY} is there too."),
        };

        private void BuildControls(VerticalStackPanel column)
        {
            //The touch screen's scheme first where a finger is what plays (#1151): on a phone, or where the menus' last input
            //was a touch (the "touch" argument pins it). On a phone it was the last paragraph of the tab, two screens under
            //nine lines of mouse, keys and pad that a phone player has none of; the table follows it for a pad plugged into
            //the phone, with the pad's column alone on the phone layout (KeyLine, #1183)
            Widget touch = TouchScheme();
            bool touchFirst = touch != null && (BS3DGame.OnPhone || Game.PromptsForTouch);
            if (touchFirst) column.Widgets.Add(touch);

            //Three columns of their own width - the keycaps, the pad's buttons and the action's name, each as wide as the
            //widest of it - and what the key does beside them (#728, #799, #997). Each line used to be centred as a unit, so
            //where a keycap landed depended on how long its own words were and the icons zig-zagged down the page. Both
            //hands at once rather than the one on the menus: this is the page a player reads to learn the other one.
            int keyWidth = 0, padWidth = 0, nameWidth = 0;

            foreach ((string keys, string pad, string name, _) in CONTROLS)
            {
                keyWidth = Math.Max(keyWidth, (int)MathF.Ceiling(Game.MenuFontPrompt.MeasureString(keys).X));
                padWidth = Math.Max(padWidth, (int)MathF.Ceiling(Game.MenuFontPrompt.MeasureString(pad).X));
                nameWidth = Math.Max(nameWidth, (int)MathF.Ceiling(FontBody.MeasureString(name).X));
            }

            foreach ((string keys, string pad, string name, string what) in CONTROLS)
                column.Widgets.Add(KeyLine(keys, pad, name,
                    name == PRECISE_AIM && Game.PreciseAimToggles ? PRECISE_AIM_TOGGLE : _phone && what == FIRE_KEY ? "" : what,
                    keyWidth, padWidth, nameWidth));

            //Under the table, one line each: when the three power-ups arrive and their price, said once for the three, and the
            //keys that are not play. The tab fits its frame with both (#997), as it did not with the price on every line
            column.Widgets.Add(Paragraph("Power-ups", $"Swap from chapter\u00A0{LevelSet.SWAP_FROM_BLOCK}, Brake from {LevelSet.BRAKE_FROM_BLOCK}, Cut from {LevelSet.CUT_FROM_BLOCK}. Each once a level."));
            //Not on a phone (#1052), which has no such keys and no window, nor in the phone layout the desktop emulates one in
            //(#1151, inches=), so the emulation shows the phone's tab
            if (!BS3DGame.OnPhone && !_phone)
                column.Widgets.Add(Paragraph("More keys", "F11 fullscreen, F12 screenshot, F10 the frame-rate counter."));

            if (touch != null && !touchFirst) column.Widgets.Add(touch);
        }

        /// <summary>
        /// The touch screen's scheme (#1050, #1051), where there is one: in words rather than a third column of glyphs,
        /// because its precise aim is a toggle and its fire and walk share a side, which the table's lines cannot say. The
        /// sides as the player has them (Settings swaps them). Where a touch screen answers, or the "touch" argument stands in
        /// for one (<see cref="BS3DGame.TouchScreenPresent"/>); null elsewhere.
        /// </summary>
        private Widget TouchScheme()
        {
            if (!Game.TouchScreenPresent) return null;

            string aim = Game.TouchAimOnRight ? "right" : "left", fire = Game.TouchAimOnRight ? "left" : "right";
            string text = $"Drag on the {aim} to aim. On the {fire}, tap to fire and drag to walk the gun. The magnifier turns "
                + "precise aim on and off, and tapping a power-up's chip uses it. Settings can swap the sides.";

            //On the phone layout under a heading of its own (#1151), the size of the control names beside it: it opens the tab
            //there, and a run-in lead at the text's size read as small print over the table
            return _phone ? Headed("Touch", text) : Paragraph("Touch", text);
        }

        /// <summary>
        /// One control: the key and the pad's button as PromptFont draws them, the action's name in the display face and what
        /// it does in the reading face beside them (#997). Each glyph is its own label in its own face, because PromptFont
        /// fills its em with a rounded keycap while a line of body text stands well short of one — set as a single string
        /// they would not sit on the same line.
        /// <para>
        /// The glyphs' and the name's labels are <paramref name="keyWidth"/>, <paramref name="padWidth"/> and
        /// <paramref name="nameWidth"/> wide and the words' label fills the rest of the text width and wraps (#728), so every
        /// line is the same width and the glyphs and the names stand at the same x whichever words follow them. The name is
        /// the condensed face because it is what the eye looks for down the column; the sentence after it is the small face,
        /// because a paragraph in the condensed face closes up into a block the reader leaves halfway through its first line.
        /// </para>
        /// </summary>
        private Widget KeyLine(string keys, string pad, string name, string what, int keyWidth, int padWidth, int nameWidth)
        {
            int gap = Scaled(KEY_GAP);

            HorizontalStackPanel line = new()
            {
                Spacing = gap,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, Scaled(KEY_LINE_GAP)),
            };

            //The keys' column (the mouse's and the keyboard's) is not on the phone layout (#1183): a phone has neither, as it has
            //no F keys for More keys' line, and the pad's column is what the table there is for. The words take the width, which
            //is what lets the tab fit its two columns
            if (!_phone) line.Widgets.Add(GlyphLabel(keys, keyWidth));
            line.Widgets.Add(GlyphLabel(pad, padWidth));

            //On the phone layout (#1151) the name heads its words, as every other tab's headings head theirs: in one of two
            //columns a third column for the names left the words a dozen characters a line, and Precise aim's took five
            if (_phone)
            {
                //The glyphs level with the name over the words rather than with the block's middle
                foreach (Widget glyph in line.Widgets) glyph.VerticalAlignment = VerticalAlignment.Top;

                //The name as the tabs' headings are set (Headed), the words under it with the line's own gap and no
                //paragraph's: nine paragraph gaps were what kept the tab from fitting its two columns
                VerticalStackPanel block = new() { Spacing = Scaled(HEADING_GAP), VerticalAlignment = VerticalAlignment.Top };
                block.Widgets.Add(new Label { Text = name, Font = FontBody, TextColor = BS3DGame.MENU_TEXT });
                if (what.Length > 0)
                    block.Widgets.Add(Prose(what, ProseWidth - padWidth - gap, BS3DGame.MENU_TEXT_BODY, ProseLeading,
                        new Thickness(0)));

                line.Widgets.Add(block);
                return line;
            }

            //A gap more after the name than between the glyphs: the widest name ("Precise aim") otherwise ran into its words
            //(photographed), where a keycap's own round edge keeps it clear of the next
            line.Widgets.Add(new Label
            {
                Text = name,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                Width = nameWidth + gap,
                VerticalAlignment = VerticalAlignment.Center,
            });

            //What it does, in the paragraphs' face and grey with its lines chosen for their shape (#806); a line with nothing
            //to add (Aim, Pause) has no label at all
            if (what.Length > 0)
            {
                Label words = Prose(what, ProseWidth - keyWidth - padWidth - nameWidth - 4 * gap,
                    BS3DGame.MENU_TEXT_BODY, ProseLeading, new Thickness(0));
                words.VerticalAlignment = VerticalAlignment.Center;
                line.Widgets.Add(words);
            }

            return line;
        }

        /// <summary>
        /// "Scroll" with the right stick pushed up and down before it for a pad player, the mouse's wheel for one with the
        /// mouse (#799), at the right end of Back's row (#857), level with the slab. Hidden until <see cref="Update"/> finds
        /// the tab's body longer than its frame.
        /// </summary>
        private Widget ScrollPrompt()
        {
            HorizontalStackPanel prompt = new()
            {
                Spacing = Scaled(KEY_GAP),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = false,
            };

            //At the keycap size every other prompt is drawn at, in a widget only as tall as the small line: at the small
            //face's own size the stick's R read as a dot (photographed), and the glyph may stand past the line's box
            prompt.Widgets.Add(new PromptGlyph(Game, Game.MenuFontPrompt, PromptGlyph.PAD_RIGHT_STICK_UP_DOWN,
                PromptGlyph.MOUSE_WHEEL, FontSmall.LineHeight));
            prompt.Widgets.Add(new Label
            {
                Text = "Scroll",
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                VerticalAlignment = VerticalAlignment.Center,
            });

            return prompt;
        }

        private Label GlyphLabel(string glyph, int width) => new()
        {
            Text = glyph,
            Font = Game.MenuFontPrompt,
            TextColor = BS3DGame.MENU_TEXT,
            Width = width,
            VerticalAlignment = VerticalAlignment.Center,
        };

        /// <summary>
        /// How wide the pages' prose is set: the whole text width (<see cref="TEXT_WIDTH"/>), past the prose measure the
        /// other pages keep (#806) — the owner's ruling on #865, a longer line for less scrolling. Against the frame's left
        /// edge, so Back stands under the text's first letter (#738). On the phone layout, one of its two columns (#1151).
        /// </summary>
        private int ProseWidth => _proseWidth;

        private Label Paragraph(string text) => Paragraph(text, ProseWidth);

        private Label Paragraph(string lead, string text) => Paragraph(lead, text, ProseWidth);

        private Widget Headed(string heading, string text) => Headed(heading, text, ProseWidth);

        /// <summary>
        /// A block under a heading of its own (#996): <paramref name="heading"/> on a line of its own in the display face at
        /// the size CONTROLS names its actions at, in the loud white, and the paragraph under it. The owner's verdict of
        /// 2026-10-23 on BASICS, SCORING, ODD BALLS, CAMPAIGN and THE GLASS: the lead-ins run into the first line at the
        /// small face's size read as headings too small for the text under them. CONTROLS keeps its two run-in leads
        /// (<see cref="Paragraph(string, string)"/>), which he passed, under its own line of the same size.
        /// </summary>
        private Widget Headed(string heading, string text, int width)
        {
            VerticalStackPanel block = new() { Spacing = Scaled(HEADING_GAP), HorizontalAlignment = HorizontalAlignment.Left };

            block.Widgets.Add(new Label { Text = heading, Font = FontBody, TextColor = BS3DGame.MENU_TEXT });
            block.Widgets.Add(Paragraph(text, width));

            return block;
        }
    }
}

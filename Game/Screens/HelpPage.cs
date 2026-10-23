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
    /// steps the game's.
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
            InvalidateTree();
        }

        protected override Widget BuildTree()
        {
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

            return ScreenRoot(Plate(column));
        }

        private VerticalStackPanel BuildBody(int page)
        {
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

            return body;
        }

        /// <summary>
        /// Asks for a tab - a click, the sideways axis, the shoulders, the wheel. Put up by <see cref="Update"/> rather than
        /// here, because a click arrives from inside Myra's own processing of the tree whose scrollers it would be swapping.
        /// </summary>
        private void SelectTab(int tab) => _pendingTab = tab;

        /// <summary>
        /// Puts a tab up (#857): its scroller shown from the top and the rest hidden, the row painted, and the body the pad
        /// and the keys scroll (<see cref="OnScrollAxis"/>) and the prompt follow it. Nothing is rebuilt and no focus is
        /// moved: Back is the one entry, so the pad's cursor has nowhere else to be.
        /// </summary>
        private void ShowTab(int tab)
        {
            _tab = Math.Clamp(tab, 0, TAB_NAMES.Length - 1);
            _scrollCarry = 0f;

            for (int t = 0; t < _scrollers.Length; t++)
                _scrollers[t].Visible = t == _tab;

            _body = _scrollers[_tab];
            _body.ScrollPosition = Point.Zero;
            _tabRow.Show(_tab);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_pendingTab >= 0)
            {
                int tab = _pendingTab;
                _pendingTab = -1;
                if (tab != _tab) ShowTab(tab);
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
                Rectangle square = OnDesktop(_ballSquares[i]);
                if (!square.Intersects(_ballFrameRect) || (BS3DGame.PotatoPath && !_ballFrameRect.Contains(square))) continue;

                //The square's centre and half-height in the frame's normalised coordinates (x and y −1..1, y up)
                float cx = (square.X + square.Width * 0.5f) / _ballBufferWidth * 2f - 1f;
                float cy = 1f - (square.Y + square.Height * 0.5f) / _ballBufferHeight * 2f;
                float radius = square.Height / _ballBufferHeight * BALL_FILL;

                //The point on the ray through the square at the chosen depth, in view space and then in the world
                Vector3 inView = new(cx * BALL_DEPTH / _ballProjection.M11, cy * BALL_DEPTH / _ballProjection.M22, -BALL_DEPTH);
                Vector3 position = Vector3.Transform(inView, _ballToWorld);
                float worldRadius = radius * BALL_DEPTH / _ballProjection.M22;

                //Sized, turned about the camera's up, then the whole tilted about its right, so the axis leans towards
                //the viewer and the top of the ball is seen as well as its side
                Matrix world = Matrix.CreateScale(worldRadius / Constants.HALF)
                    * Matrix.CreateFromAxisAngle(_ballToWorld.Up, _ballSpin + i * BALL_PHASE)
                    * Matrix.CreateFromAxisAngle(_ballToWorld.Right, BALL_TILT);

                //The Cutter's saw takes its frame from the round's own, which in the gun is the barrel's (#767's verdict,
                //BallRenderSet.CutterFrame), and turns itself: the page's turntable would swing its face away. So it is
                //handed a barrel's frame as the bore holds one, away from the viewer and up by the rise the overview lens
                //sees a loaded round's bore at
                if (entry.Kind == BallKind.Cutter)
                    world = Matrix.CreateScale(worldRadius / Constants.HALF) * Matrix.CreateWorld(Vector3.Zero,
                        Vector3.Normalize(_ballToWorld.Forward + _ballToWorld.Up * CUTTER_BORE_RISE), _ballToWorld.Up);

                world.M41 = position.X;
                world.M42 = position.Y;
                world.M43 = position.Z;

                //The cannon's two as they sit loaded in it (#895): still, so the rainbow ball does not breathe and the Cutter's
                //blade idles at its loaded rate - the look the player will see in the bore, which is what this entry is for
                balls.Add(entry.Colour, position, world, BallRenderSet.UNOCCLUDED, still: entry.InGun, kind: entry.Kind);
            }
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
                if (wanted != _tab) ShowTab(wanted);
            }
            else _openOn = wanted;
        }

        private void BuildPlaying(VerticalStackPanel column)
        {
            //Every paragraph opens on its lead-in (#996), so that the leads read down the page are the game in five
            //phrases for a player who wants it in five seconds
            column.Widgets.Add(Paragraph("The goal",
                "A cluster of coloured balls hangs from a sheet of glass above a stone island. You stand under it with a "
                + "cannon and fire balls up into it. Clear every ball that can be matched and the level is done."));
            column.Widgets.Add(Paragraph("Match & drop",
                "A shot sticks where it lands. Land it so that three or more of the same colour touch, and the whole "
                + "group falls — and everything that was hanging from it falls with it. The group you match is usually "
                + "the smaller half of what you bring down."));
            column.Widgets.Add(Paragraph("Find the anchor",
                "So the shot to look for is rarely the biggest clump of your colour. It is the one ball that is holding "
                + "a lot of other balls up."));
            column.Widgets.Add(Paragraph("Ammo & queue",
                "You have a fixed number of balls per level, shown at the bottom left. The queue at the bottom right "
                + "shows what you are about to fire and what comes after it, so you can plan two shots ahead."));

            //The colour cue (#737), where the balls carry it: a pattern that only ever means its colour, said once so
            //that nobody reads a striped or a spotted ball as a special one. Not on the Potato path, which draws none.
            if (Game.Balls?.DrawsColourCue == true)
                column.Widgets.Add(Paragraph("Patterns",
                    "Every colour also wears a pattern of its own: stripes, spots, checks or a printed symbol, "
                    + "depending on what the balls are made of. Every ball of a colour wears the same one, and so does "
                    + "its disc in the queue, so two colours that look alike still differ in pattern."));
        }

        private void BuildScoring(VerticalStackPanel column)
        {
            //Four values and a tip (#998): what scores, with the figure in the lead so the leads down the tab are the price
            //list, and nothing of the arithmetic behind it. Every figure is ScoreKeeper's own, written through string
            //interpolation rather than typed out, so a tuned constant cannot leave a stale number standing on this page.
            //What the game shows in play as it scores (the caption over the middle of the screen, the shattering multiplier,
            //the bonus flying to the score) is not narrated: the player sees it there. The streak's lead names its cap in words:
            //Anton's × is a dot's size at the small face and read as "·5" (photographed)
            column.Widgets.Add(Paragraph($"Matched balls · {ScoreKeeper.MatchedBallPoints} points each",
                "Every ball of the same-colour group your shot completes."));
            column.Widgets.Add(Paragraph($"Detached balls · {ScoreKeeper.OrphanedBallPoints} points each",
                "The balls that fall because the group was the last thing holding them up. Twice a matched one: bringing a "
                + "mass down is the better shot, and the score says so."));
            column.Widgets.Add(Paragraph($"Streak · multiplier up to {ScoreKeeper.MaxMultiplier}",
                $"Every shot that scores raises the multiplier by\u00A0{ScoreKeeper.MultiplierStep} for the shots "
                + "after it. A shot that matches nothing puts it back to ×1."));
            column.Widgets.Add(Paragraph("Unused balls · a big bonus",
                "Every ball still in the cannon when the field is clear pays, and pays more on a big level."));
            column.Widgets.Add(Paragraph("Tip",
                "Finishing with balls to spare pays more than a streak you could farm by taking longer."));
        }

        //The squares the kinds' live balls are drawn in (#856), one per entry of OddBallCopy.Entries and at the entry's index,
        //built with the tree; DrawForeground reads where each stands
        private readonly Panel[] _ballSquares = new Panel[OddBallCopy.Entries.Length];

        private void BuildBalls(VerticalStackPanel column)
        {
            column.Widgets.Add(Paragraph(
                "Most balls are ordinary. These are not — the first few you will meet as the campaign goes on."));

            //The cluster's kinds, then a line of their own, then the two the cannon holds (#895). Which group a kind is in is
            //the entry's own (OddBallCopy.Entry.InGun) and the table keeps the cluster's first, so the squares stand in the
            //table's order whichever way this walks it
            AddOddBalls(column, inGun: false);
            column.Widgets.Add(Paragraph(OddBallCopy.GunLead));
            AddOddBalls(column, inGun: true);

            column.Widgets.Add(Aside("And the power-ups — the Swap, the Brake and the Cut — are on the Controls tab.", ProseWidth));
        }

        /// <summary>
        /// The entries of one group, each kind beside its words (#856): a square at the paragraph's left, top-aligned with its
        /// first line, the words set across the text's width less the square and the gap. The square is a widget so that it
        /// takes its room in the body's flow and the scroller carries it; what is drawn in it is the frame's own ball
        /// (<see cref="DrawForeground"/>), found again by the entry's index in <see cref="OddBallCopy.Entries"/>.
        /// </summary>
        private void AddOddBalls(VerticalStackPanel column, bool inGun)
        {
            int square = Scaled(BALL_SQUARE), gap = Scaled(BALL_GAP);

            for (int i = 0; i < OddBallCopy.Entries.Length; i++)
            {
                if (OddBallCopy.Entries[i].InGun != inGun) continue;

                HorizontalStackPanel row = new() { Spacing = gap, HorizontalAlignment = HorizontalAlignment.Left };

                //With the paragraph's own gap under it, so an entry of one line is not a row the next square touches
                _ballSquares[i] = new Panel
                {
                    Width = square,
                    Height = square,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 0, ParagraphGap),
                };
                row.Widgets.Add(_ballSquares[i]);
                row.Widgets.Add(Paragraph(OddBallCopy.Entries[i].Name, OddBallCopy.Entries[i].Text, ProseWidth - square - gap));

                column.Widgets.Add(row);
            }
        }

        private void BuildCampaign(VerticalStackPanel column)
        {
            //Read from the level set rather than typed: the page said 120 levels in 12 chapters and 236 stars for
            //the last level while the game shipped 130, 13 and 256 (#606)
            column.Widgets.Add(Paragraph(
                $"The campaign is {Game.LevelCount}\u00A0levels in {Game.BlockCount}\u00A0chapters. Each chapter is ten levels against one backdrop, "
                + "and each has its own shape of puzzle — a chapter of towers, a chapter of things hidden "
                + "inside other things, a chapter of mathematics."));
            column.Widgets.Add(Paragraph(
                "Every level you clear is rated from one to four stars. One star is clearing it at all; the "
                + "rest are score, and the thresholds are multiples of what the level is worth if you simply "
                + "finish it. Four stars means you cleared it well under the shots it allows."));
            column.Widgets.Add(Paragraph(
                "Stars are also a key. Later levels ask for a total across everything you have played, so a "
                + $"chapter you rushed can be gone back to. The last level of the campaign asks for {Game.LevelMinStars(Game.LevelCount - 1)}."));
            column.Widgets.Add(Paragraph(
                "A cleared level presents a cup: bronze, silver, gold, or — for four stars — crystal. It is the "
                + "same information as the stars, handed over as an object."));
        }

        private void BuildCeiling(VerticalStackPanel column)
        {
            column.Widgets.Add(Paragraph(
                "The cluster does not hang still for ever. Every few shots the glass it hangs from steps down, "
                + "and the whole cluster comes with it. The panel on the left of the screen shows the field, "
                + "the glass at the top of it, and how far down it has come."));
            column.Widgets.Add(Paragraph(
                "At the bottom of that panel is a red line. If the cluster reaches it, the level is lost. You "
                + "get a moment’s warning — the floor lights up — and under the line you have about a second "
                + "to drop something and lift the cluster back."));
            column.Widgets.Add(Paragraph(
                "So the clock is your own shots. Every shot brings the glass nearer, whether it matched "
                + "anything or not, which is the other reason a miss is expensive. Clearing from the bottom "
                + "buys height; clearing from the top does not."));
            column.Widgets.Add(Paragraph(
                "Even so, throwing a ball away is a fair move. When the ball in the gun has nowhere to score, fire it into "
                + "empty space, or onto a ball of another colour. It costs a shot and your streak, not the level."));
            column.Widgets.Add(Paragraph(
                "From the fourth chapter you also have a brake: one press a level lifts the glass a step back up, "
                + "which is time the ceiling took. It does nothing until the glass has stepped down."));
        }

        //The pad's half of every line (#799), the tutorial's bindings exactly (Tutorial.cs): the right stick aims, the right
        //trigger fires, the left holds the precise aim, the left stick walks and steps, X, Y and the right bumper are the
        //three power-ups, and the Menu button pauses. Each line is the action's name and what the key does, and nothing
        //more (#997): the mechanics behind a power-up are the Cutter's entry on ODD BALLS and THE GLASS's brake, and the
        //chapter it arrives in is LevelSet's own constant, so a moved power-up cannot leave a stale chapter here
        private static readonly (string Keys, string Pad, string Name, string What)[] CONTROLS =
        {
            (MOUSE, PromptGlyph.PAD_RIGHT_STICK, "Aim", ""),
            (MOUSE_LEFT, PromptGlyph.PAD_RT, "Fire", "Space fires too."),
            (MOUSE_RIGHT, PromptGlyph.PAD_LT, "Precise aim", "Hold it to look down the barrel."),
            (KEY_A + KEY_D, PromptGlyph.PAD_LEFT_STICK_SIDEWAYS, "Walk", "Move the gun round the field."),
            (KEY_W + KEY_S, PromptGlyph.PAD_LEFT_STICK_UP_DOWN, "Step", "In and out: closer means a steeper shot."),
            (KEY_E, PromptGlyph.PAD_X, "Swap", "The next two balls change places."),
            (KEY_Q, PromptGlyph.PAD_Y, "Brake", "Lifts the glass one step back up."),
            (KEY_R, PromptGlyph.PAD_RB, "Cut", "Your next ball destroys the whole storey it hits."),
            (PromptGlyph.KEY_ESCAPE, PromptGlyph.PAD_MENU, "Pause", $"{BS3DGame.NOTE_ENTRY} is there too."),
        };

        private void BuildControls(VerticalStackPanel column)
        {
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
                column.Widgets.Add(KeyLine(keys, pad, name, what, keyWidth, padWidth, nameWidth));

            //Under the table, one line each: when the three power-ups arrive and their price, said once for the three, and the
            //keys that are not play. The tab fits its frame with both (#997), as it did not with the price on every line
            column.Widgets.Add(Paragraph("Power-ups", $"Swap from chapter\u00A0{LevelSet.SWAP_FROM_BLOCK}, Brake from {LevelSet.BRAKE_FROM_BLOCK}, Cut from {LevelSet.CUT_FROM_BLOCK}. Each once a level."));
            column.Widgets.Add(Paragraph("More keys", "F11 fullscreen, F12 screenshot, F10 the frame-rate counter."));
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

            line.Widgets.Add(GlyphLabel(keys, keyWidth));
            line.Widgets.Add(GlyphLabel(pad, padWidth));
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
                Label words = Prose(what, Scaled(TEXT_WIDTH) - keyWidth - padWidth - nameWidth - 4 * gap,
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
        /// edge, so Back stands under the text's first letter (#738).
        /// </summary>
        private int ProseWidth => Scaled(TEXT_WIDTH);

        private Label Paragraph(string text) => Paragraph(text, ProseWidth);

        private Label Paragraph(string lead, string text) => Paragraph(lead, text, ProseWidth);
    }
}

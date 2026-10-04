using BS3D.Online;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// The settings. Every value is a button that cycles it rather than a slider or a drop-down: one widget
    /// kind, one click, and nothing that can be left half-dragged — and each change takes effect where it is
    /// made, so what the scene behind the panel looks like <i>is</i> the preview.
    /// <para>
    /// <b>Pages by category since #686, ONLINE first and open whenever the page is.</b> The rows stood in one
    /// column, then two (#138, when thirteen ran Back off the bottom: every size here is a 2160p design figure
    /// scaled by the viewport's <i>height</i>, so a page that overruns the design height overruns it at every
    /// resolution alike), then three (#548, when the right one had run off again). The third column is where the
    /// online rows went — top right, read last, third in the pad's walk, a dim heading like every other over rows
    /// that looked like every display toggle — and the owner, opting in for the first online test, did not find
    /// them. The answer was not a fourth column: the rows a player comes to change are a handful at a time, and
    /// twenty-six on one plate made every one of them a search. So a tab row now picks one category's page —
    /// ONLINE · DISPLAY · AUDIO · CONTROLS · GAME — and the page under it is one column again.
    /// </para>
    /// <para>
    /// <b>The tabs are not in the pad's walk.</b> The walk is one line down the page, and five tabs ahead of the
    /// first row would be five presses before anything could be changed. They are turned sideways instead — the
    /// arrows, the D-pad or the left stick (<see cref="PageSideways"/>), the pad's shoulder buttons (the host reads
    /// them as the same axis), the mouse wheel over the tab row — and clicked. That they are buttons for the mouse
    /// and not entries for the pad is the host's rule: a button with no action on its <c>Tag</c> is not walked.
    /// </para>
    /// <para>
    /// <b>Every page stands in the same frame</b>: the page area is as tall as the tallest page (DISPLAY's ten
    /// rows) and every caption column as wide as the widest caption on any page, so a tab changes what the rows
    /// say and not where they, the plate's edge or Back stand. Still no scroller (<see cref="MenuPage.MenuScroll"/>):
    /// the pad can reach a row inside one since #245, but a page that fits needs none, and every page here fits.
    /// </para>
    /// <para>
    /// <b>Within a page the order the rows were added is the order the pad steps through them</b>, since
    /// <c>CollectNavEntries</c> follows the order widgets were added rather than where they landed — then Back.
    /// The destructive rows are each the last of their page: "Remove scores" on ONLINE, the campaign pair on GAME.
    /// </para>
    /// <para>
    /// <b>The nickname is the one value that is typed rather than cycled</b> (#548), and it is still a button:
    /// activating it puts the page into typing, where the row shows what is being typed and the keyboard is the
    /// page's (<see cref="CapturesKeyboard"/>) — so a Space, an arrow or a letter types rather than walking the
    /// cursor. Enter or the pad's A keeps it, Escape or B drops it, and since #687 moving on — another row, or off
    /// the page — keeps it too when it is a name, because the owner typed one, did not know Enter was wanted and
    /// lost it. While typing, the value reads as a field (the draft from the left, a blinking caret) and the line
    /// under the rows leads with the keys in the bright face. A pad cannot type, and that line says so ("typed on
    /// the keyboard") rather than leaving a pad player to discover it.
    /// </para>
    /// </summary>
    internal sealed class SettingsPage : MenuPage
    {
        //The value buttons. One column again since #686, so back to the 560 the single column had before #138 split
        //it (420 was what a third column on a 4:3 viewport left) — no value is long ("Unlimited" is the widest), and
        //a nickname too long for it drops to the small face (see ShowNickname).
        private const int VALUE_WIDTH = 560;

        private const int COLUMN_SPACING = 58;

        //20 rather than the columns' 24, and PAGE_GAP under the 43 the columns stood above Back (#686): DISPLAY's ten
        //rows under the tab row are the tallest page there has been, and at these figures the plate stands about 30 px
        //clear of a 1600x900 client's top and bottom (photographed) where the columns' would have left about half of
        //that. Every size here is a design figure scaled by height, so that is the margin at every resolution.
        private const int ROW_SPACING = 20;

        //Above Back, under the page area
        private const int PAGE_GAP = 24;

        //Above a heading inside a page, so "CAMPAIGN" separates from the rows above it rather than reading as one
        //more of them.
        private const int GROUP_HEADING_GAP = 40;


        //The tab row (#686), in the section face the group headings were set in, since a tab is what those headings
        //became. Every tab is cut to the widest name's width so the row reads as one bar of equal parts.
        private const int TAB_PADDING_X = 40;
        private const int TAB_PADDING_Y = 14;
        private const int TAB_SPACING = 16;
        private const int TAB_GAP = 20;

        private const int TAB_ONLINE = 0, TAB_DISPLAY = 1, TAB_AUDIO = 2, TAB_CONTROLS = 3, TAB_GAME = 4;
        private static readonly string[] TAB_NAMES = { "ONLINE", "DISPLAY", "AUDIO", "CONTROLS", "GAME" };

        //The tab that is up reads as unmistakably chosen, inverted: the near-white of the type as the slab and a dark
        //grey as the type. Still the greyscale rule (BS3DGame's palette) — brightness, not hue — and the opposite of
        //a row's focus highlight, which lifts a grey slab a step and so can never be taken for it.
        private static readonly IBrush TAB_SELECTED_BRUSH = new FlatBrush(BS3DGame.MENU_TEXT);
        private static readonly Color TAB_SELECTED_TEXT = new(30, 30, 30);

        private Label _fullscreenValue, _qualityValue, _adaptiveQualityValue, _exposureValue, _skyValue, _fpsValue, _fpsLimitValue;
        private Label _volumeValue, _effectsValue, _musicValue, _ambienceValue, _rumbleValue, _sensitivityValue, _aimSensitivityValue, _tutorialValue;
        private Label _aberrationValue, _grainValue, _motionBlurValue, _dropCinematicValue, _introLogoValue;
        private Label _progressValue, _unlockAllValue;
        private Label _onlineValue, _nicknameValue, _removeValue, _onlineNote;

        //The width the note's text is laid out to, in pixels (SizeOnlineNote, #769)
        private int _noteTextWidth;

        //The tab row's width in pixels, which every page is cut to (#770); set by BuildTabRow
        private int _tabRowWidth;

        //The reset row asks twice. One click on a row that erases every star is an accident waiting beside
        //ten rows that are safe to click freely — so the first click only arms it and shows "Sure?", the
        //second wipes, and opening the page anew (Enter) stands it down again.
        private bool _resetArmed;

        //"Remove scores" asks twice for the reset row's reason, and it is worse than a reset: it cannot be undone
        //at all, because the server forgets the player and the id is never reused.
        private bool _removeArmed;

        //Typing a nickname (#548): whether the page has the keyboard, and whether the edit began from the Online row
        //(so keeping a name also turns the boards on). The draft, the caret that blinks after it (#687) and what was
        //wrong with the last attempt to keep it are the entry's, shared with the first-launch plate (#763).
        private bool _typing;
        private bool _turnOnAfterName;
        private readonly NicknameEntry _entry = new();

        //Every row's own button, in build order (#517) — cleared and refilled by AddRow each time BuildTree
        //runs, since a resize rebuilds the whole tree and a stale reference here would still answer
        //IsMouseInside for a button no longer on screen. Kept apart from Game's own _navEntries: that list
        //also carries the Back button, which is not a value to cycle and must not answer to the wheel.
        private readonly List<Button> _rows = new();

        //The tabs, their labels and the pages they pick (#686), rebuilt with the tree; which page is up, and a pick
        //waiting for Update to put it up (-1 for none — see SelectTab).
        private readonly Button[] _tabs = new Button[TAB_NAMES.Length];
        private readonly Label[] _tabLabels = new Label[TAB_NAMES.Length];
        private readonly Grid[] _pages = new Grid[TAB_NAMES.Length];
        private readonly List<Label> _captions = new();
        private int _tab = TAB_ONLINE;
        private int _pendingTab = -1;

        //Back, and the entry the pad's cursor stands on - so a tab turned with the cursor on Back leaves it there
        private Button _back;
        private Button _focused;

        public SettingsPage(BS3DGame game) : base(game) { }

        public override void Enter()
        {
            //ONLINE whenever the page opens (#686): the boards are the rows a player has a reason to come back to,
            //and the ones the owner could not find
            _tab = TAB_ONLINE;
            _pendingTab = -1;
            _resetArmed = false;
            _removeArmed = false;
            _typing = false;
            _turnOnAfterName = false;
            _entry.Begin(null);
            Game.Online.ForgetRemovalOutcome();
        }

        public override void Leave()
        {
            //A page that is not on top must not keep the keyboard - and a name typed and not yet confirmed is kept
            //when it is one (#687): leaving the page is not Escape
            KeepOrDropTyping();
            _typing = false;
            base.Leave();
        }

        /// <summary>
        /// A tab asked for since the last frame (#686, see <see cref="SelectTab"/>), and the caret's blink while typing
        /// (#687) - a label's text written twice a second, never per frame.
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_pendingTab >= 0)
            {
                int tab = _pendingTab;
                _pendingTab = -1;
                ShowTab(tab);
            }

            if (!_typing || _nicknameValue == null) return;

            if (_entry.Tick((float)gameTime.ElapsedGameTime.TotalSeconds)) ShowNickname();
        }

        internal override bool CapturesKeyboard => _typing;

        protected override Widget BuildTree()
        {
            //Rebuilt below by every AddRow call this tree's build makes — cleared first so a resize does not
            //leave a stale button from the tree just thrown away still answering the wheel.
            _rows.Clear();
            _captions.Clear();

            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("SETTINGS"));
            column.Widgets.Add(BuildTabRow());

            _pages[TAB_ONLINE] = BuildOnlinePage();
            _pages[TAB_DISPLAY] = BuildDisplayPage();
            _pages[TAB_AUDIO] = BuildAudioPage();
            _pages[TAB_CONTROLS] = BuildControlsPage();
            _pages[TAB_GAME] = BuildGamePage();

            //Every page as wide as the tab row (#770, the owner's call): the captions start under its left edge and the
            //value buttons end under its right one, the caption column taking whatever the buttons leave, so a value
            //button stands at the same x whichever tab is up and the rows read as one block with the tabs. They stood
            //as a narrow block centred under the row, about 180 px in from either end at 1920x1080. Never narrower than
            //the widest caption beside a button, though at these figures the row is the wider at every size, since
            //every one of them is a design figure scaled by height. The note under the online rows is as wide as a row.
            int captionWidth = 0;
            foreach (Label caption in _captions)
                captionWidth = Math.Max(captionWidth, (int)MathF.Ceiling(FontBody.MeasureString(caption.Text).X));

            int pageWidth = Math.Max(_tabRowWidth, captionWidth + Scaled(COLUMN_SPACING) + Scaled(VALUE_WIDTH));

            foreach (Grid page in _pages)
            {
                page.ColumnsProportions[0] = new Proportion(ProportionType.Fill);
                page.Width = pageWidth;
            }

            SizeOnlineNote(pageWidth);

            //Every page in one place, only the chosen one shown (Refresh) - and the place as tall as the tallest page,
            //measured with every page laid out, so Back stands still as the tabs turn. Myra lays out only what is
            //visible, so without the fixed height the plate would shrink round a short page and Back would jump.
            //⚠ No margin on it: Myra counts a widget's Margin INSIDE an explicit Height, so the gap under the page area
            //came out of the tallest page's own rows and cut DISPLAY's last one short by exactly the gap (photographed
            //at two gaps, short by each). The gap is Back's, above it.
            Panel pageArea = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            Point pageSize = Point.Zero;
            foreach (Grid page in _pages)
            {
                Point size = page.Measure(new Point(int.MaxValue / 2, int.MaxValue / 2));
                pageSize = new Point(Math.Max(pageSize.X, size.X), Math.Max(pageSize.Y, size.Y));
                pageArea.Widgets.Add(page);
            }

            pageArea.Width = pageSize.X;
            pageArea.Height = pageSize.Y;

            column.Widgets.Add(pageArea);

            _back = MenuButton("Back", GoBack);
            _back.Margin = ScaledThickness(0, PAGE_GAP, 0, 0);
            column.Widgets.Add(_back);

            return ScreenRoot(Plate(column));
        }

        /// <summary>
        /// The tab row (#686): one button per page, all cut to the widest name's width. Buttons, so the mouse gets the
        /// shared hover, press and click sound — but with nothing on their <c>Tag</c>, which is what keeps the host's
        /// walk off them (see the class remarks).
        /// </summary>
        private Widget BuildTabRow()
        {
            int nameWidth = 0;
            foreach (string name in TAB_NAMES)
                nameWidth = Math.Max(nameWidth, (int)MathF.Ceiling(FontSection.MeasureString(name).X));

            HorizontalStackPanel row = new()
            {
                Spacing = Scaled(TAB_SPACING),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, TAB_GAP),
            };

            for (int t = 0; t < TAB_NAMES.Length; t++)
            {
                //Captured per iteration, so five tabs are five destinations
                int tab = t;

                Button button = MenuButton(TAB_NAMES[t], () => SelectTab(tab), out Label label);
                button.Tag = null;
                button.Padding = ScaledThickness(TAB_PADDING_X, TAB_PADDING_Y);
                button.Width = nameWidth + 2 * Scaled(TAB_PADDING_X);
                label.Font = FontSection;

                _tabs[t] = button;
                _tabLabels[t] = label;
                row.Widgets.Add(button);
            }

            _tabRowWidth = TAB_NAMES.Length * (nameWidth + 2 * Scaled(TAB_PADDING_X)) + (TAB_NAMES.Length - 1) * row.Spacing;

            return row;
        }

        /// <summary>
        /// Asks for a tab - a click, the sideways axis, the wheel. Put up by <see cref="Update"/> rather than here,
        /// because a click arrives from inside Myra's own processing of the tree whose pages it would be swapping.
        /// </summary>
        private void SelectTab(int tab) => _pendingTab = tab;

        /// <summary>
        /// Puts a page up (#686). A name being typed is kept or dropped first, as moving to another row would, and
        /// the two armed rows stand down: a "Sure?" left behind on a page the player turned away from would still
        /// be waiting for them on the way back. The pad's cursor stays on Back if it was there, and otherwise goes
        /// to the new page's first row.
        /// </summary>
        private void ShowTab(int tab)
        {
            if (tab == _tab || tab < 0 || tab >= TAB_NAMES.Length) return;

            KeepOrDropTyping();
            _resetArmed = false;
            _removeArmed = false;
            _tab = tab;

            Refresh();

            if (IsActive && IsBuilt) Game.RefreshNavEntries(_focused == _back ? _back : null);
        }

        /// <summary>Left or right on the arrows, the D-pad, the left stick or the pad's shoulders turns the tab, wrapping round (#686).</summary>
        internal override bool PageSideways(int direction)
        {
            if (_typing) return false;

            SelectTab((_tab + direction + TAB_NAMES.Length) % TAB_NAMES.Length);
            return true;
        }

        /// <summary>Where the pad's cursor stands, so a tab turn can leave it on Back (see <see cref="ShowTab"/>).</summary>
        internal override void NavFocusChanged(Button focused) => _focused = focused;

        /// <summary>
        /// Everything about what the game looks like, in the order these rows have always been in. The drop camera
        /// stood last here until #686 and is on GAME now, with the other switches about what the game does round play.
        /// </summary>
        private Grid BuildDisplayPage()
        {
            Grid grid = NewGroupGrid();

            AddRow(grid, 0, "Fullscreen", Game.ToggleFullscreen, out _fullscreenValue);
            //One bundled tier rather than the antialiasing dial it replaces (#63). Supersampling was never a
            //performance setting — it is tied to a look decision — and it was the only thing here that reached
            //the rest of the frame at all; the tier reaches the city's per-pixel work and its skyline too.
            AddRow(grid, 1, "Quality", Game.CycleQuality, out _qualityValue);
            //Whether the game may lower that tier by itself (#390). Directly under it, because it is the other
            //half of the same answer — and because picking a tier above turns it off, which the player should
            //see happen rather than have to know.
            AddRow(grid, 2, "Auto quality", Game.ToggleAdaptiveQuality, out _adaptiveQualityValue);
            //The tonemap's exposure, said as the brightness it is (#711): a percent of the authored look, 100 % the default
            AddRow(grid, 3, "Brightness", Game.CycleExposure, out _exposureValue);
            AddRow(grid, 4, "Sky", Game.CycleSkyDome, out _skyValue);
            AddRow(grid, 5, "FPS counter", Game.ToggleFpsOverlay, out _fpsValue);
            //The presentation cap (#124): synced to the monitor's refresh (frames nobody can see cost only
            //heat) or unlimited — the "nocap" launch argument's toggle, in the menu so a benchmarking session
            //is not the only way to lift it.
            AddRow(grid, 6, "FPS limit", Game.ToggleFpsLimit, out _fpsLimitValue);
            //The lens's colour fringing at the frame edges — a taste toggle, and instant where it is made,
            //like every row here: the scene behind the panel is the preview.
            AddRow(grid, 7, "Aberration", Game.ToggleAberration, out _aberrationValue);
            AddRow(grid, 8, "Film grain", Game.ToggleGrain, out _grainValue);
            //What moves smeared along its motion (#402). With the lens's looks, being one; a tier that cannot
            //afford it says so on the row rather than leaving an "On" that does nothing.
            AddRow(grid, 9, "Motion blur", Game.ToggleMotionBlur, out _motionBlurValue);

            return grid;
        }

        /// <summary>
        /// The mix: the master and the three parts under it. The pad's rumble stood here until #686 and is on CONTROLS
        /// now, where a player looking for it looks; the Track row (#279's piece picker) stood under the volumes until
        /// the owner, who had never come across it, had it removed (#704) — the Jukebox is where the music is played by name.
        /// </summary>
        private Grid BuildAudioPage()
        {
            Grid grid = NewGroupGrid();

            //The three volume rows (#46) scale the authored mix rather than replacing it — 100 % is the game
            //as tuned, and effects and music each sit under the master. See "The sound" in
            //docs/game-feedback.md for the split.
            AddRow(grid, 0, "Volume", Game.CycleMasterVolume, out _volumeValue);
            AddRow(grid, 1, "Effects", Game.CycleSfxVolume, out _effectsValue);
            AddRow(grid, 2, "Music", Game.CycleMusicVolume, out _musicValue);
            AddRow(grid, 3, "Ambience", Game.CycleAmbienceVolume, out _ambienceValue);

            return grid;
        }

        /// <summary>
        /// The hands: the aim's two rates and how hard the pad answers. A page of its own since #686; the tutorial and
        /// the intro logo stood under this heading in the middle column only because that column had room (#621), and
        /// are on GAME now.
        /// </summary>
        private Grid BuildControlsPage()
        {
            Grid grid = NewGroupGrid();

            //The aim dial (#384) — neither a look nor a sound, which is why CONTROLS exists. The pad's own rate is a
            //separate quantity that may earn a row of its own (see MouseAim.PAD_RATE), and this is where it would go.
            AddRow(grid, 0, "Sensitivity", Game.CycleSensitivity, out _sensitivityValue);

            //The lean's own dial (#497): a second rung over the first, read as precise aim blends in. The same
            //ladder and the same percentages, so the two rows read as one family; 100 % is #384's feel.
            AddRow(grid, 1, "Aim sensitivity", Game.CycleAimSensitivity, out _aimSensitivityValue);

            //The pad's motors (#378), on the volume rows' quarter-step-with-off ladder. It stood among them in the
            //columns as "how hard the game hits back"; on pages it is with the pad's other row, which is where a
            //player with a pad in hand looks for vibration.
            AddRow(grid, 2, "Rumble", Game.CycleRumbleStrength, out _rumbleValue);

            return grid;
        }

        /// <summary>
        /// The boards (#548): the switch, the name they show, removal, and the note under them. The first tab since
        /// #686 and the one the page opens on. "Remove scores" is the page's last row, since it cannot be undone.
        /// </summary>
        private Grid BuildOnlinePage()
        {
            Grid grid = NewGroupGrid();

            //Opt-in (#548): off until the player turns it on, and turning it on the first time asks for the nickname
            //the boards will show. Off keeps the identity, so on again later is the same player.
            AddRow(grid, 0, "Online scores", OnOnline, out _onlineValue);

            //Typed, not cycled — see the class remarks
            AddRow(grid, 1, "Nickname", OnNickname, out _nicknameValue, typingRow: true);

            //Two-step, like the reset (see _removeArmed); the server is asked first and nothing here goes until it
            //has said yes — OnlineSession.RemoveScores
            AddRow(grid, 2, "Remove scores", OnRemove, out _removeValue);

            //What is sent and what is kept, in the About page's own words (one sentence, one source) — or, while it
            //is more use, what the player is doing: typing, or a removal's outcome. Its width is the rows' own and its
            //height the tallest thing it can say, both set in BuildTree once the caption column is known (SizeOnlineNote).
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _onlineNote = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                Wrap = true,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = ScaledThickness(0, GROUP_HEADING_GAP, 0, 0),
            };
            Grid.SetColumn(_onlineNote, 0);
            Grid.SetColumnSpan(_onlineNote, 2);
            Grid.SetRow(_onlineNote, 3);
            grid.Widgets.Add(_onlineNote);

            return grid;
        }

        /// <summary>
        /// What the game does round play — the switches about what it puts in front of the player — and under them
        /// the campaign, the destructive pair every layout of this page has kept last.
        /// </summary>
        private Grid BuildGamePage()
        {
            Grid grid = NewGroupGrid();

            //The tutorial's opt-out (#189): what the first chapter's cards teach is how the game is played.
            AddRow(grid, 0, "Tutorial", Game.ToggleTutorial, out _tutorialValue);

            //Whether the game opens on the 2D logo (#621), beside the tutorial because the two are the same kind of
            //switch — what the game puts in front of the player before letting them get on with it. It takes effect
            //at the next launch, since the splash is decided when the stack is built.
            AddRow(grid, 1, "Intro logo", Game.ToggleIntroLogo, out _introLogoValue);

            //Whether a big collapse takes the camera (#290). #290 kept it among the looks rather than under a
            //"GAMEPLAY" heading over a single row; with a page for what the game does round play it has one.
            AddRow(grid, 2, "Drop camera", Game.ToggleDropCinematic, out _dropCinematicValue);

            AddGroupHeading(grid, 3, "CAMPAIGN", first: false);

            //The campaign back to zero stars (#92) — for testing as much as for a fresh start. The resting
            //value shows the star total the click would erase; the click itself is two-step (see _resetArmed).
            AddRow(grid, 4, "Reset progress", OnResetProgress, out _progressValue);

            //The debug unlock (#349). Under the campaign heading because it is the same kind of thing the row above
            //is - the player's record - and it is a DEVELOPMENT convenience: it is off at every launch and writes
            //nothing, so it can never make a real save read further along than it is. Hiding it behind a build
            //flag is a shipping concern and not one yet.
            AddRow(grid, 5, "Unlock all", Game.ToggleUnlockAll, out _unlockAllValue);

            return grid;
        }

        /// <summary>
        /// One page's grid: a caption column (the room the value buttons leave in a page as wide as the tab row, set in
        /// <see cref="BuildTree"/>) and a value column, topped out rather than centred so a short page's first row stands where a tall one's does
        /// instead of floating half way down the page area.
        /// </summary>
        private Grid NewGroupGrid()
        {
            Grid grid = new()
            {
                ColumnSpacing = Scaled(COLUMN_SPACING),
                RowSpacing = Scaled(ROW_SPACING),
                VerticalAlignment = VerticalAlignment.Top,
            };

            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

            return grid;
        }

        /// <summary>
        /// A group's name over its rows, spanning both of the grid's columns. Still subordinate to those rows,
        /// but **by brightness alone** since #243 — the palette's aside grey, which is what this menu uses for
        /// emphasis everywhere and never hue.
        /// <para>
        /// It used to be quiet by SIZE and by FACE as well, and that half is reversed: it was the small face at
        /// 58 over rows in the display face at 80, so the heading was smaller than the rows it headed and set in
        /// the small-print family while they were not. A heading smaller than its own content is a heading
        /// upside down, which is what the owner was reading when they called it too small. It is
        /// <see cref="MenuPage.FontSection"/> now — the display face at 96, above the rows and well under the
        /// page heading's 124, which it shares a screen with and must not compete against.
        /// </para>
        /// </summary>
        /// <param name="first">Whether it heads its page. A later heading takes
        /// <see cref="GROUP_HEADING_GAP"/> of air above it to break from the rows before.</param>
        private void AddGroupHeading(Grid grid, int row, string text, bool first)
        {
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

            Label heading = new()
            {
                Text = text,
                Font = FontSection,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = ScaledThickness(0, first ? 0 : GROUP_HEADING_GAP, 0, 0),
            };

            Grid.SetColumn(heading, 0);
            Grid.SetColumnSpan(heading, 2);
            Grid.SetRow(heading, row);
            grid.Widgets.Add(heading);
        }

        /// <param name="typingRow">The nickname's own row, whose click keeps or starts typing. Every other row's
        /// click first keeps a name being typed when it is one, and drops it when it is not (#687) — the player has
        /// moved on, and only Escape says "not this name".</param>
        private void AddRow(Grid grid, int row, string caption, Action onClick, out Label value, bool typingRow = false)
        {
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

            Label captionLabel = new()
            {
                Text = caption,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(captionLabel, 0);
            Grid.SetRow(captionLabel, row);
            grid.Widgets.Add(captionLabel);
            _captions.Add(captionLabel);

            //"Off" until Refresh writes the real value: the page area is measured as soon as the tree is built
            //(BuildTree), and a button round an empty label measures a line shorter than it will stand - ten of those
            //cut DISPLAY's last row in half (photographed at 1600x900, #686)
            Button button = MenuButton("Off", typingRow ? onClick : () =>
            {
                KeepOrDropTyping();
                onClick();
            }, out value);
            button.Width = Scaled(VALUE_WIDTH);

            Grid.SetColumn(button, 1);
            Grid.SetRow(button, row);
            grid.Widgets.Add(button);

            _rows.Add(button);
        }

        /// <summary>
        /// The wheel cycles whichever row it is over (#352, #517), the same one step a click already does —
        /// there being only the one direction any row's own action performs, an up-notch and a down-notch do
        /// the same thing. <c>Tag</c> rather than the row's own click handler directly, so the wheel plays the
        /// same click sound a mouse press or a pad activation already does (see <c>MenuClickable</c>) instead
        /// of silently skipping it.
        /// <para>
        /// Over the tab row it turns the tab (#686), the way the level picker's wheel turns its chapter — away from the
        /// player is the next one. A row is asked only when it is on the page that is up: a page hidden under the
        /// pointer is not one the player is pointing at, whatever hover its buttons were left holding.
        /// </para>
        /// </summary>
        internal override void OnScrollWheel(int delta)
        {
            foreach (Button tab in _tabs)
            {
                if (tab == null || !tab.IsMouseInside) continue;

                if (!_typing && PageSideways(delta > 0 ? 1 : -1)) Game.Audio?.PlayUiTick();
                return;
            }

            foreach (Button row in _rows)
            {
                if (!row.IsMouseInside || !_pages[_tab].Widgets.Contains(row)) continue;

                (row.Tag as Action)?.Invoke();
                return;
            }
        }

        /// <summary>Writes the current value onto each setting's button. Cheap, and only run on a change.</summary>
        internal override void Refresh()
        {
            //A display hotkey works before this page has ever been opened, so there may be nothing to write onto
            if (_fullscreenValue == null) return;

            _fullscreenValue.Text = Game.IsFullscreen ? "On" : "Off";
            _qualityValue.Text = Game.Quality.ToString();
            _adaptiveQualityValue.Text = Game.IsAdaptiveQualityEnabled ? "On" : "Off";
            //Percent of the authored look, 100 % the default (#711), not the raw multiplier
            _exposureValue.Text = Game.BrightnessPercent.ToString(CultureInfo.InvariantCulture) + " %";
            _skyValue.Text = Game.SkyDomeNumber.ToString(CultureInfo.InvariantCulture);
            _fpsValue.Text = Game.IsFpsOverlayVisible ? "On" : "Off";
            //"Monitor", not a number: the cap is whatever the panel refreshes at, and naming the rate here
            //would go stale the moment the window lands on another monitor
            _fpsLimitValue.Text = Game.IsFpsUncapped ? "Unlimited" : "Monitor";
            _aberrationValue.Text = Game.IsAberrationEnabled ? "On" : "Off";
            _grainValue.Text = Game.IsGrainEnabled ? "On" : "Off";
            _motionBlurValue.Text = !Game.IsMotionBlurEnabled ? "Off" : Game.MotionBlurActive ? "On" : "Off (tier)";
            _dropCinematicValue.Text = Game.IsDropCinematicEnabled ? "On" : "Off";
            _introLogoValue.Text = Game.IsIntroLogoEnabled ? "On" : "Off";
            _tutorialValue.Text = Game.IsTutorialEnabled ? "On" : "Off";
            _unlockAllValue.Text = Game.IsUnlockAllEnabled ? "On" : "Off";
            _volumeValue.Text = FormatVolume(Game.MasterVolume);
            _effectsValue.Text = FormatVolume(Game.SfxVolume);
            _musicValue.Text = FormatVolume(Game.MusicVolume);
            _ambienceValue.Text = FormatVolume(Game.AmbienceVolume);
            _rumbleValue.Text = FormatVolume(Game.RumbleStrength);
            //As a percentage of the shipped feel, the volume rows' own idiom, and exact at every rung — the
            //ladder is written so that it is (0.75 is "75 %", where a multiplier would have to print "0.8×"
            //and lie, or "0.75×" and read as arithmetic).
            _sensitivityValue.Text = FormatSensitivity(Game.MouseSensitivity);
            _aimSensitivityValue.Text = FormatSensitivity(Game.AimSensitivity);
            //In words, not the ★ glyph the picker uses: the value column is set in the display face like
            //every row here, and Anton simply has no star glyph — FontStashSharp would drop it and leave a
            //bare number (which is exactly how this line first rendered).
            _progressValue.Text = _resetArmed ? "Sure?"
                : Game.TotalStars == 1 ? "1 star" : $"{Game.TotalStars} stars";

            //What is left for the player to do is red (#687, BS3DGame.MENU_TEXT_ALERT): the boards off, and on with
            //nowhere to send - a local build whose settings name no server, or one the client refused. A release
            //always has OnlineScores.DefaultServer, so a player meets the second only on a server they typed in
            bool online = Game.Online.IsOn;
            bool noServer = online && Game.Online.Nickname != null && !Game.Online.Enabled;
            _onlineValue.Text = !online ? "Off" : noServer ? "No server" : "On";
            _onlineValue.TextColor = !online || noServer ? BS3DGame.MENU_TEXT_ALERT : BS3DGame.MENU_TEXT;
            ShowNickname();
            _removeValue.Text = Game.Online.Removal == OnlineRemovalState.Removing ? "Removing..."
                : _removeArmed ? "Sure?"
                : Game.Online.Nickname == null ? "Nothing" : "Remove";
            _onlineNote.Text = SpaceWrap.Wrap(OnlineNote(), _noteTextWidth, MeasureNoteLine);
            //The instruction while typing is the thing to read on the page, so it is not set as an aside (#687)
            _onlineNote.TextColor = _typing ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;

            ShowTabState(onlineAlert: !online || noServer);
        }

        /// <summary>
        /// Which page is up, and how each tab reads (#686): the chosen one inverted, the rest a control's slab with
        /// type a shade under the rows'. ONLINE stays red from another page while the boards still want something
        /// done (#687's call to action), so turning to DISPLAY does not hide that there is something left on ONLINE.
        /// Written with the values, which is also every time the page is shown, before the host walks its entries.
        /// </summary>
        private void ShowTabState(bool onlineAlert)
        {
            for (int t = 0; t < TAB_NAMES.Length; t++)
            {
                bool chosen = t == _tab;

                _pages[t].Visible = chosen;
                _tabs[t].Background = chosen ? TAB_SELECTED_BRUSH : BS3DGame.MENU_BUTTON_BRUSH;
                _tabs[t].OverBackground = chosen ? TAB_SELECTED_BRUSH : BS3DGame.MENU_BUTTON_OVER_BRUSH;
                _tabLabels[t].TextColor = chosen ? TAB_SELECTED_TEXT
                    : t == TAB_ONLINE && onlineAlert ? BS3DGame.MENU_TEXT_ALERT
                    : BS3DGame.MENU_TEXT_BODY;
            }
        }

        /// <summary>
        /// The nickname in the display face like every other value — or in the small face when it would not fit
        /// the button, which sixteen wide letters in Anton do not. Measured rather than counted: letters differ.
        /// <para>
        /// While typing (#687) it reads as a field: the draft from the button's left edge with a blinking caret after
        /// it, so the letters stand still as the caret comes and goes (centred, each blink moved them half a caret).
        /// The face is chosen with the caret in, so a blink cannot flip it. Not set, it is red: a thing to type.
        /// </para>
        /// </summary>
        private void ShowNickname()
        {
            if (_typing)
            {
                _nicknameValue.Text = _entry.Field;
                _nicknameValue.Font = FontBody.MeasureString(_entry.FieldWithCaret).X <= Scaled(VALUE_WIDTH) * 0.9f ? FontBody : FontSmall;
                _nicknameValue.HorizontalAlignment = HorizontalAlignment.Left;
                _nicknameValue.TextColor = BS3DGame.MENU_TEXT;
                return;
            }

            string text = Game.Online.Nickname ?? "Not set";
            _nicknameValue.Text = text;
            _nicknameValue.Font = FontBody.MeasureString(text).X <= Scaled(VALUE_WIDTH) * 0.9f ? FontBody : FontSmall;
            _nicknameValue.HorizontalAlignment = HorizontalAlignment.Center;
            _nicknameValue.TextColor = Game.Online.Nickname == null ? BS3DGame.MENU_TEXT_ALERT : BS3DGame.MENU_TEXT;
        }

        /// <summary>
        /// What the line under the online rows says, most pressing first: what typing needs, a refusal of the name,
        /// how a removal went, that nothing can be sent — and otherwise the sentence saying what is sent and kept.
        /// </summary>
        private string OnlineNote()
        {
            //The keys first (#687): the owner typed a name, did not know Enter was wanted, and left it behind. Moving
            //on keeps it now as well, and the line says so
            if (_typing)
                return _entry.Problem ?? TYPING_NOTE;

            if (Game.Online.NameProblem != null)
                return $"The server refused the nickname ({Game.Online.NameProblem}). Choose another.";

            switch (Game.Online.Removal)
            {
                case OnlineRemovalState.Removing:
                    return "Asking the server to remove your scores...";
                case OnlineRemovalState.Removed:
                    return "Removed from the server and from this machine.";
                case OnlineRemovalState.RemovedHere:
                    return REMOVED_HERE_NOTE;
                case OnlineRemovalState.Failed:
                    string problem = Game.Online.RemovalProblem ?? string.Empty;
                    return "Nothing was removed: " + (problem.StartsWith("the server refused", StringComparison.Ordinal)
                        ? problem : "the server did not answer") + ". Try again when it is in reach.";
            }

            //On with a name and still not enabled: no server resolved - a local build whose settings name none (a
            //release has OnlineScores.DefaultServer), or a server the client refused. Said alone: with the privacy
            //sentence after it, it ran past the nine lines the note had then and was cut mid-sentence (#687), and
            //the sentence is for deciding to opt in, which this player has done
            if (Game.Online.IsOn && !Game.Online.Enabled && Game.Online.Nickname != null)
                return NO_SERVER_NOTE;

            return Game.Online.PrivacySentence;
        }

        //The note's longer texts, named so SizeOnlineNote can measure them beside the privacy sentence. The rest (a
        //refusal, a removal under way or failed) are a line or two and never the tallest.
        private static readonly string TYPING_NOTE = "Press Enter to keep the name, or Esc to cancel. Moving to another row keeps it too. "
            + $"{Nickname.MinLength} to {Nickname.MaxLength} letters, digits, spaces, _ or -, typed on the keyboard.";

        private const string REMOVED_HERE_NOTE =
            "Removed from this machine. No score server was in reach, so nothing had been sent from here.";

        private const string NO_SERVER_NOTE =
            "No score server: this build has none to send to, so nothing is sent. A release sends to the game's own server.";

        /// <summary>
        /// The note's width, and a height that holds the tallest thing it can say (#769), so what it says can change
        /// without the rows above it moving and none of it is ever cut. Measured, as the label will lay it out, rather
        /// than counted: the height was nine of the face's <c>LineHeight</c>s, but the drawn pitch is more than that and
        /// Myra counts the note's top margin inside an explicit height, so eight lines showed, and once #716 lengthened
        /// the privacy sentence its last clause ("Remove scores deletes it all.") was cut after "Remove" (photographed
        /// at 1920x1080). Every text goes through <see cref="SpaceWrap"/> first, exactly as <see cref="Refresh"/> sets it.
        /// </summary>
        private void SizeOnlineNote(int width)
        {
            _onlineNote.Width = width;
            _noteTextWidth = LaidOutWidth(_onlineNote);

            int tallest = 0;
            foreach (string text in new[] { Game.Online.PrivacySentence, TYPING_NOTE, REMOVED_HERE_NOTE, NO_SERVER_NOTE })
            {
                Label probe = new()
                {
                    Text = SpaceWrap.Wrap(text, _noteTextWidth, MeasureNoteLine),
                    Font = _onlineNote.Font,
                    Wrap = true,
                    Width = width,
                    Margin = _onlineNote.Margin,
                };

                tallest = Math.Max(tallest, probe.Measure(new Point(width, int.MaxValue / 2)).Y);
            }

            _onlineNote.Height = tallest;
        }

        /// <summary>A line of the note as its face draws it, for <see cref="SpaceWrap"/>.</summary>
        private float MeasureNoteLine(string line) => _onlineNote.Font.MeasureString(line).X;

        /// <summary>
        /// The Online row (#548): off from on at once; on from off at once when there is a nickname, and otherwise
        /// only once one has been typed — an empty nickname keeps it off.
        /// </summary>
        private void OnOnline()
        {
            _removeArmed = false;

            if (Game.Online.IsOn) Game.Online.SetOn(false);
            else if (Game.Online.Nickname != null) Game.Online.SetOn(true);
            else StartTyping(turnOnAfter: true);

            Refresh();
        }

        /// <summary>The nickname's row: keeps what is being typed, or starts typing.</summary>
        private void OnNickname()
        {
            if (_typing) KeepTyping();
            else StartTyping(turnOnAfter: false);
        }

        /// <summary>
        /// The two-step removal, like the reset: armed by the first click, done by the second. Nothing to remove
        /// without an identity, and nothing to do while one is already on its way.
        /// </summary>
        private void OnRemove()
        {
            if (Game.Online.Nickname == null || Game.Online.Removal == OnlineRemovalState.Removing)
            {
                _removeArmed = false;
            }
            else if (_removeArmed)
            {
                _removeArmed = false;
                Game.Online.RemoveScores();
            }
            else _removeArmed = true;

            Refresh();
        }

        private void StartTyping(bool turnOnAfter)
        {
            _typing = true;
            _turnOnAfterName = turnOnAfter;
            _entry.Begin(Game.Online.Nickname);
            _removeArmed = false;

            Refresh();
        }

        /// <summary>
        /// The player moved on from a name being typed - another row, or off the page (#687). A name that is one is
        /// kept, as Enter would keep it (and turns the boards on when the edit began from the Online row); anything
        /// else is dropped quietly, since there is no longer a field to say what is wrong with it under. Only Escape
        /// drops a good name.
        /// </summary>
        private void KeepOrDropTyping()
        {
            if (!_typing) return;

            if (_entry.IsKeepable) KeepTyping();
            else CancelTyping();
        }

        /// <summary>
        /// Keeps the name if it is one (<see cref="Nickname.TryNormalize"/>), and says what is wrong if it is not -
        /// the page stays in typing, so the player can fix it rather than start over.
        /// </summary>
        private void KeepTyping()
        {
            if (!_entry.TryKeep(out string name))
            {
                Refresh();
                return;
            }

            bool turnOn = _turnOnAfterName;

            _typing = false;
            _turnOnAfterName = false;

            Game.Online.SetNickname(name);
            if (turnOn) Game.Online.SetOn(true);

            Refresh();
        }

        private void CancelTyping()
        {
            if (!_typing) return;

            _typing = false;
            _turnOnAfterName = false;
            _entry.ClearProblem();

            Refresh();
        }

        /// <summary>
        /// A character the window typed (#548), while this page has the keyboard. Enter and Escape come through
        /// here as the characters Windows sends for them, not as key edges, so one press cannot act twice; a
        /// character no nickname may hold does nothing, and nothing past the longest name is taken
        /// (<see cref="NicknameEntry.Type"/>).
        /// </summary>
        internal override void OnTextInput(char character)
        {
            if (!_typing) return;

            switch (_entry.Type(character))
            {
                case NicknameKey.Enter:
                    KeepTyping();
                    break;

                case NicknameKey.Escape:
                    CancelTyping();
                    break;

                case NicknameKey.Edited:
                    Refresh();
                    break;
            }
        }

        /// <summary>The pad's half of typing: A keeps, B drops (#548). A pad cannot type the name itself.</summary>
        internal override void TypingButtons(bool keep, bool drop)
        {
            if (drop) CancelTyping();
            else if (keep) KeepTyping();
        }

        /// <summary>
        /// Testing only (<c>settings=&lt;row,...&gt;</c>, #548): activates the named rows in order, through the very
        /// handlers a click runs, so a run nobody is sitting at can reach the online rows' states. "remove" is
        /// refused outside a <c>userdata=</c> folder — it would remove the player's own scores from the server.
        /// Since #687 also <c>type:&lt;text&gt;</c>, <c>enter</c> and <c>esc</c>, typed through
        /// <see cref="OnTextInput"/>: <c>settings=online,type:Novak,intro</c> types a name and moves on.
        /// Since #686 a row puts up the page that holds it first, as a player would have to, and <c>tab:&lt;name&gt;</c>
        /// puts up a page by its tab's name (<c>settings=tab:audio</c>) for a picture of it.
        /// </summary>
        internal void ActivateForTesting(string rows)
        {
            foreach (string row in rows.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                //"type:<text>" feeds the characters through OnTextInput, the window's own path, and "enter"/"esc" the
                //two keys that end typing (#687) - so a run nobody is sitting at can type a name and then move on
                if (row.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (char c in row[5..]) OnTextInput(c);
                    System.Console.WriteLine($"[settings] Testing: typed '{row[5..]}'");
                    continue;
                }

                if (row.StartsWith("tab:", StringComparison.OrdinalIgnoreCase))
                {
                    int tab = Array.FindIndex(TAB_NAMES, name => name.Equals(row[4..], StringComparison.OrdinalIgnoreCase));
                    if (tab < 0)
                    {
                        System.Console.WriteLine($"[settings] Testing: no tab '{row[4..]}' to show");
                        continue;
                    }

                    ShowTab(tab);
                    System.Console.WriteLine($"[settings] Testing: showing tab {TAB_NAMES[tab]}");
                    continue;
                }

                //Every row but the nickname's goes through what its click does first (AddRow): a name being typed is
                //kept when it is one, dropped when not (#687)
                switch (row.ToLowerInvariant())
                {
                    case "enter": OnTextInput('\r'); break;
                    case "esc": OnTextInput('\x1b'); break;
                    case "online": ShowTab(TAB_ONLINE); KeepOrDropTyping(); OnOnline(); break;
                    case "intro": ShowTab(TAB_GAME); KeepOrDropTyping(); Game.ToggleIntroLogo(); break;
                    case "brightness": ShowTab(TAB_DISPLAY); KeepOrDropTyping(); Game.CycleExposure(); break;
                    case "nickname": ShowTab(TAB_ONLINE); OnNickname(); break;
                    case "remove" when UserData.IsTestingDirectory: ShowTab(TAB_ONLINE); KeepOrDropTyping(); OnRemove(); break;
                    case "remove":
                        System.Console.WriteLine("[settings] Testing: 'remove' refused outside a userdata= folder — it would remove the player's own scores");
                        continue;
                    default:
                        System.Console.WriteLine($"[settings] Testing: no row '{row}' to activate");
                        continue;
                }

                System.Console.WriteLine($"[settings] Testing: activated '{row}'");
            }
        }

        /// <summary>
        /// The two-step reset: armed by the first click, done by the second. The page-local state is the
        /// whole mechanism — the host's <see cref="BS3DGame.ResetProgress"/> is only ever called once the
        /// player has said it twice.
        /// </summary>
        private void OnResetProgress()
        {
            if (_resetArmed)
            {
                _resetArmed = false;
                Game.ResetProgress();
            }
            else _resetArmed = true;

            Refresh();
        }

        /// <summary>"Off" at zero rather than "0 %": silence is a state, not a quantity.</summary>
        private static string FormatVolume(float gain)
            => gain <= 0f ? "Off" : ((int)MathF.Round(gain * 100f)).ToString(CultureInfo.InvariantCulture) + " %";

        /// <summary>
        /// The aim dial as a percentage of the shipped feel (#384). No "Off" case, unlike the volume above:
        /// there is no rung at zero and there must not be one — a mouse that cannot turn the gun is a game that
        /// looks broken, and the row that did it is the one row the player then cannot find.
        /// </summary>
        private static string FormatSensitivity(float scale)
            => ((int)MathF.Round(scale * 100f)).ToString(CultureInfo.InvariantCulture) + " %";
    }
}

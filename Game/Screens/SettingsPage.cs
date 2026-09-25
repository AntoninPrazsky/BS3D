using BS3D.Online;
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
    /// <b>Three columns, because two stopped fitting</b> (#138, then #548). Every size on this page is a 2160p
    /// design figure scaled by the viewport's <i>height</i> (<c>BS3DGame.MENU_DESIGN_HEIGHT</c>), so a page that
    /// overruns the design height overruns it at <b>every</b> resolution and aspect alike — this was never a
    /// small-window bug, and the rows that run off the bottom take the Back button with them. Two columns put
    /// Back back on the screen at thirteen rows; by #548 the right one held three headings and eleven rows and
    /// had run off again — photographed at 1600×900 and at 3840×1600 alike, "Reset progress" cut through,
    /// "Unlock all" and Back gone below the frame — before the three online rows were even added. A third column
    /// (ONLINE over CAMPAIGN) brings the tallest column down to about the height the DISPLAY column always had.
    /// It costs width, which the height-derived scale leaves in hand at every landscape display: the plate is
    /// about 2800 design units across, which a 4:3 viewport (2880) still holds — the value buttons went from
    /// 460 to <see cref="VALUE_WIDTH"/> for that — and a 16:9 one (3840) holds with room to spare. A viewport
    /// narrower than about 1.3:1 is the first that would not.
    /// </para>
    /// <para>
    /// <b>The other candidate was a scroller</b> (<see cref="MenuPage.MenuScroll"/>, which the level picker
    /// uses), and it was not taken. The reason given at the time was that the pad and the arrow keys reached
    /// entries inside a scroller but could not scroll a focused one into view, leaving a pad player driving a
    /// cursor onto rows they cannot see — <b>that hole is closed since #245</b>
    /// (<c>BS3DGame.ScrollNavEntryIntoView</c>), so it no longer carries the decision. What does, and always
    /// did the better job of it: two columns need no scrolling <i>at all</i>, so every row is on screen at
    /// once and the page can be read rather than walked. A scroller would be a worse answer here even with
    /// the walk fixed.
    /// </para>
    /// <para>
    /// <b>The rows keep their old order, read down one column and then the other</b> — which is also the order
    /// the nav walk collects them in, since <c>CollectNavEntries</c> follows the order widgets were added
    /// rather than where they landed. So the split changed where a row sits and not the sequence a pad steps
    /// through: the display rows, then the audio and control rows, the online rows, the campaign rows, Back. The
    /// counts that used to stand here are left out on purpose — every group has grown since (#290, #349, #279,
    /// #548), and the ORDER is the part of this that carries anything.
    /// </para>
    /// <para>
    /// <b>The nickname is the one value that is typed rather than cycled</b> (#548), and it is still a button:
    /// activating it puts the page into typing, where the row shows what is being typed and the keyboard is the
    /// page's (<see cref="CapturesKeyboard"/>) — so a Space, an arrow or a letter types rather than walking the
    /// cursor. Enter or the pad's A keeps it, Escape or B drops it, and any other row cancels it. A pad cannot
    /// type, and the line under the rows says so rather than leaving a pad player to discover it.
    /// </para>
    /// </summary>
    internal sealed class SettingsPage : MenuPage
    {
        //Narrower than the 560 the single column could afford, because there are two of them now — and no
        //value here is long ("Unlimited" is the widest), so the button stays a comfortable target at this
        //width rather than a bar most of which is empty.
        //460 until #548, when a third column had to fit a 4:3 viewport (see the class remarks) — "Unlimited",
        //"Removing..." and a nickname in the display face all still sit well inside it, and a nickname too long
        //for it drops to the small face (see ShowNickname).
        private const int VALUE_WIDTH = 420;

        //Between the two columns. Wider than the grids' own ColumnSpacing (COLUMN_SPACING), or the gutter
        //between the columns would read as just another caption/value gap and the two groups would run
        //together into four ragged columns.
        private const int GROUP_GAP = 110;

        private const int COLUMN_SPACING = 58;
        private const int ROW_SPACING = 24;

        //Above a group heading that is not its column's first, so "CAMPAIGN" separates from the audio rows
        //above it rather than reading as one more of them.
        private const int GROUP_HEADING_GAP = 40;

        //The note under the online rows (#548): as wide as the rows it sits under — a caption, the grid's column
        //gap and a value button — and a fixed number of the small face's lines tall, so what it says can change
        //(the sentence, a typing hint, a removal's outcome) without the page moving. Nine lines hold the sentence
        //at this width with a line to spare; measured on the page, not reasoned.
        private const int NOTE_WIDTH = 780;
        private const int NOTE_LINES = 9;

        private Label _fullscreenValue, _qualityValue, _adaptiveQualityValue, _exposureValue, _skyValue, _fpsValue, _fpsLimitValue;
        private Label _volumeValue, _effectsValue, _musicValue, _ambienceValue, _rumbleValue, _trackValue, _sensitivityValue, _aimSensitivityValue, _tutorialValue;
        private Label _aberrationValue, _grainValue, _motionBlurValue, _dropCinematicValue;
        private Label _progressValue, _unlockAllValue;
        private Label _onlineValue, _nicknameValue, _removeValue, _onlineNote;

        //The reset row asks twice. One click on a row that erases every star is an accident waiting beside
        //ten rows that are safe to click freely — so the first click only arms it and shows "Sure?", the
        //second wipes, and opening the page anew (Enter) stands it down again.
        private bool _resetArmed;

        //"Remove scores" asks twice for the reset row's reason, and it is worse than a reset: it cannot be undone
        //at all, because the server forgets the player and the id is never reused.
        private bool _removeArmed;

        //Typing a nickname (#548): whether the page has the keyboard, what has been typed so far, whether the edit
        //began from the Online row (so keeping a name also turns the boards on), and what was wrong with the last
        //attempt to keep it.
        private bool _typing;
        private string _nameDraft = string.Empty;
        private bool _turnOnAfterName;
        private string _typingProblem;

        //Every row's own button, in build order (#517) — cleared and refilled by AddRow each time BuildTree
        //runs, since a resize rebuilds the whole tree and a stale reference here would still answer
        //IsMouseInside for a button no longer on screen. Kept apart from Game's own _navEntries: that list
        //also carries the Back button, which is not a value to cycle and must not answer to the wheel.
        private readonly List<Button> _rows = new();

        public SettingsPage(BS3DGame game) : base(game) { }

        public override void Enter()
        {
            _resetArmed = false;
            _removeArmed = false;
            _typing = false;
            _turnOnAfterName = false;
            _typingProblem = null;
            Game.ForgetOnlineRemovalOutcome();
        }

        public override void Leave()
        {
            //A page that is not on top must not keep the keyboard
            _typing = false;
            base.Leave();
        }

        internal override bool CapturesKeyboard => _typing;

        protected override Widget BuildTree()
        {
            //Rebuilt below by every AddRow call this tree's build makes — cleared first so a resize does not
            //leave a stale button from the tree just thrown away still answering the wheel.
            _rows.Clear();

            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("SETTINGS"));

            HorizontalStackPanel columns = new()
            {
                Spacing = Scaled(GROUP_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 43),
            };

            //Added in this order, and that IS the nav order — see the class remarks
            columns.Widgets.Add(BuildDisplayGroup());
            columns.Widgets.Add(BuildAudioGroup());
            columns.Widgets.Add(BuildRecordGroup());

            column.Widgets.Add(columns);
            column.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(column));
        }

        /// <summary>
        /// The left column: everything about what the game looks like and how it is presented, in the order
        /// these rows have always been in.
        /// </summary>
        private Grid BuildDisplayGroup()
        {
            Grid grid = NewGroupGrid();

            AddGroupHeading(grid, 0, "DISPLAY", first: true);

            AddRow(grid, 1, "Fullscreen", Game.ToggleFullscreen, out _fullscreenValue);
            //One bundled tier rather than the antialiasing dial it replaces (#63). Supersampling was never a
            //performance setting — it is tied to a look decision — and it was the only thing here that reached
            //the rest of the frame at all; the tier reaches the city's per-pixel work and its skyline too.
            AddRow(grid, 2, "Quality", Game.CycleQuality, out _qualityValue);
            //Whether the game may lower that tier by itself (#390). Directly under it, because it is the other
            //half of the same answer — and because picking a tier above turns it off, which the player should
            //see happen rather than have to know.
            AddRow(grid, 3, "Auto quality", Game.ToggleAdaptiveQuality, out _adaptiveQualityValue);
            AddRow(grid, 4, "Exposure", Game.CycleExposure, out _exposureValue);
            AddRow(grid, 5, "Sky", Game.CycleSkyDome, out _skyValue);
            AddRow(grid, 6, "FPS counter", Game.ToggleFpsOverlay, out _fpsValue);
            //The presentation cap (#124): synced to the monitor's refresh (frames nobody can see cost only
            //heat) or unlimited — the "nocap" launch argument's toggle, in the menu so a benchmarking session
            //is not the only way to lift it.
            AddRow(grid, 7, "FPS limit", Game.ToggleFpsLimit, out _fpsLimitValue);
            //The lens's colour fringing at the frame edges — a taste toggle, and instant where it is made,
            //like every row here: the scene behind the panel is the preview.
            AddRow(grid, 8, "Aberration", Game.ToggleAberration, out _aberrationValue);
            AddRow(grid, 9, "Film grain", Game.ToggleGrain, out _grainValue);
            //What moves smeared along its motion (#402). With the lens's looks, being one; a tier that cannot
            //afford it says so on the row rather than leaving an "On" that does nothing.
            AddRow(grid, 10, "Motion blur", Game.ToggleMotionBlur, out _motionBlurValue);

            //Whether a big collapse takes the camera (#290). It sits with the looks rather than under a
            //heading of its own because that is what it IS to the player - a flourish they can turn off - and
            //a "GAMEPLAY" heading over a single row would promise a group that does not exist.
            AddRow(grid, 11, "Drop camera", Game.ToggleDropCinematic, out _dropCinematicValue);

            return grid;
        }

        /// <summary>
        /// The middle column: the mix, and under it the player's own input rates. Two headings because those are
        /// two different kinds of thing. The campaign rows stood under them until #548 and moved to the third column
        /// with the online ones — both are the player's own record, and neither is a sound or a control.
        /// </summary>
        private Grid BuildAudioGroup()
        {
            Grid grid = NewGroupGrid();

            AddGroupHeading(grid, 0, "AUDIO", first: true);

            //The three volume rows (#46) scale the authored mix rather than replacing it — 100 % is the game
            //as tuned, and effects and music each sit under the master. See "The sound" in
            //docs/game-feedback.md for the split.
            AddRow(grid, 1, "Volume", Game.CycleMasterVolume, out _volumeValue);
            AddRow(grid, 2, "Effects", Game.CycleSfxVolume, out _effectsValue);
            AddRow(grid, 3, "Music", Game.CycleMusicVolume, out _musicValue);
            AddRow(grid, 4, "Ambience", Game.CycleAmbienceVolume, out _ambienceValue);

            //The pad's own row (#378) — not audio, but the same kind of thing to the player as the four rows
            //above it: how hard the game hits back, on the same quarter-step-with-off ladder. It sits with
            //them and not with CONTROLS below, which is about the player's OWN input rate rather than what
            //the game answers with.
            AddRow(grid, 5, "Rumble", Game.CycleRumbleStrength, out _rumbleValue);

            //Which piece plays, so a composition can be heard in the game against the real mix rather
            //than only in a .wav or by finding a level of the right chapter (#279). Under the volume rows
            //because it is the same kind of thing the player hears them through — and a listening tool
            //and not a setting: it writes nothing, and the game takes the choice back at the next level.
            AddRow(grid, 6, "Track", Game.CycleMusicTrack, out _trackValue);

            //The aim dial (#384). It gets a heading of its own where the drop camera deliberately did not,
            //and the difference is not how many rows each has: that one IS a look to the player, so it belongs
            //with the looks, while this is neither a look nor a sound and would be a lie under either heading.
            //A CONTROLS group is also one that genuinely exists rather than one promised by a lone row - the
            //pad's rate is a separate quantity that may earn its own row (see MouseAim.PAD_RATE), and this is
            //where it would go; the tutorial's switch (#189) is the second row it got.
            AddGroupHeading(grid, 7, "CONTROLS", first: false);

            //Above CAMPAIGN rather than below it, because the campaign rows are the destructive pair and the
            //page keeps them last - a row a player is meant to click freely does not belong under the one
            //that erases every star.
            AddRow(grid, 8, "Sensitivity", Game.CycleSensitivity, out _sensitivityValue);

            //The lean's own dial (#497): a second rung over the first, read as precise aim blends in. The same
            //ladder and the same percentages, so the two rows read as one family; 100 % is #384's feel.
            AddRow(grid, 9, "Aim sensitivity", Game.CycleAimSensitivity, out _aimSensitivityValue);

            //The tutorial's opt-out (#189). Under CONTROLS because what the first chapter's cards teach IS the
            //controls, so the switch that hides them belongs beside the dial that tunes them.
            AddRow(grid, 10, "Tutorial", Game.ToggleTutorial, out _tutorialValue);

            return grid;
        }

        /// <summary>
        /// The right column: the player's own record, online and here. ONLINE first (#548) and CAMPAIGN under it,
        /// because the campaign rows are the destructive pair the page has always kept last — and "Remove scores",
        /// which is destructive too, is the last of its own group.
        /// </summary>
        private Grid BuildRecordGroup()
        {
            Grid grid = NewGroupGrid();

            AddGroupHeading(grid, 0, "ONLINE", first: true);

            //Opt-in (#548): off until the player turns it on, and turning it on the first time asks for the nickname
            //the boards will show. Off keeps the identity, so on again later is the same player.
            AddRow(grid, 1, "Online scores", OnOnline, out _onlineValue);

            //Typed, not cycled — see the class remarks
            AddRow(grid, 2, "Nickname", OnNickname, out _nicknameValue, typingRow: true);

            //Two-step, like the reset (see _removeArmed); the server is asked first and nothing here goes until it
            //has said yes — BS3DGame.RemoveOnlineScores
            AddRow(grid, 3, "Remove scores", OnRemove, out _removeValue);

            //What is sent and what is kept, in the About page's own words (one sentence, one source) — or, while it
            //is more use, what the player is doing: typing, or a removal's outcome
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _onlineNote = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                Wrap = true,
                Width = Scaled(NOTE_WIDTH),
                Height = FontSmall.LineHeight * NOTE_LINES,
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetColumn(_onlineNote, 0);
            Grid.SetColumnSpan(_onlineNote, 2);
            Grid.SetRow(_onlineNote, 4);
            grid.Widgets.Add(_onlineNote);

            AddGroupHeading(grid, 5, "CAMPAIGN", first: false);

            //The campaign back to zero stars (#92) — for testing as much as for a fresh start. The resting
            //value shows the star total the click would erase; the click itself is two-step (see _resetArmed).
            AddRow(grid, 6, "Reset progress", OnResetProgress, out _progressValue);

            //The debug unlock (#349). Under the campaign heading rather than among the looks because it is the
            //same kind of thing the row above is - the player's record - and it is a DEVELOPMENT convenience:
            //it is off at every launch and writes nothing, so it can never make a real save read further along
            //than it is. Hiding it behind a build flag is a shipping concern and not one yet.
            AddRow(grid, 7, "Unlock all", Game.ToggleUnlockAll, out _unlockAllValue);

            return grid;
        }

        /// <summary>
        /// One column's grid: a caption column and a value column, topped out rather than centred so the
        /// shorter column's first row sits level with the taller one's instead of floating half way down it.
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
        /// <param name="first">Whether it heads its column. A later heading takes
        /// <see cref="GROUP_HEADING_GAP"/> of air above it to break from the group before.</param>
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
        /// click drops a name being typed first — the player has moved on.</param>
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

            Button button = MenuButton(string.Empty, typingRow ? onClick : () =>
            {
                CancelTyping();
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
        /// </summary>
        internal override void OnScrollWheel(int delta)
        {
            foreach (Button row in _rows)
            {
                if (!row.IsMouseInside) continue;

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
            _exposureValue.Text = Game.Exposure.ToString("0.0", CultureInfo.InvariantCulture);
            _skyValue.Text = Game.SkyDomeNumber.ToString(CultureInfo.InvariantCulture);
            _fpsValue.Text = Game.IsFpsOverlayVisible ? "On" : "Off";
            //"Monitor", not a number: the cap is whatever the panel refreshes at, and naming the rate here
            //would go stale the moment the window lands on another monitor
            _fpsLimitValue.Text = Game.IsFpsUncapped ? "Unlimited" : "Monitor";
            _aberrationValue.Text = Game.IsAberrationEnabled ? "On" : "Off";
            _grainValue.Text = Game.IsGrainEnabled ? "On" : "Off";
            _motionBlurValue.Text = !Game.IsMotionBlurEnabled ? "Off" : Game.MotionBlurActive ? "On" : "Off (tier)";
            _dropCinematicValue.Text = Game.IsDropCinematicEnabled ? "On" : "Off";
            _tutorialValue.Text = Game.IsTutorialEnabled ? "On" : "Off";
            _unlockAllValue.Text = Game.IsUnlockAllEnabled ? "On" : "Off";
            _volumeValue.Text = FormatVolume(Game.MasterVolume);
            _effectsValue.Text = FormatVolume(Game.SfxVolume);
            _musicValue.Text = FormatVolume(Game.MusicVolume);
            _ambienceValue.Text = FormatVolume(Game.AmbienceVolume);
            _rumbleValue.Text = FormatVolume(Game.RumbleStrength);
            //"Auto" is not one of the pieces: it is whatever the moment plays unasked — the front end's
            //loop in the menus, the level's own theme in a level. The rest name themselves off the music
            //folder's families (#486), so a new family appears in this row with no wiring here at all.
            _trackValue.Text = Game.MusicTrack is string family ? char.ToUpperInvariant(family[0]) + family.Substring(1) : "Auto";
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

            _onlineValue.Text = Game.IsOnlineOn ? "On" : "Off";
            ShowNickname(_typing ? _nameDraft + "|" : Game.OnlineNickname ?? "Not set");
            _removeValue.Text = Game.OnlineRemoval == OnlineRemovalState.Removing ? "Removing..."
                : _removeArmed ? "Sure?"
                : Game.OnlineNickname == null ? "Nothing" : "Remove";
            _onlineNote.Text = OnlineNote();
        }

        /// <summary>
        /// The nickname in the display face like every other value — or in the small face when it would not fit
        /// the button, which sixteen wide letters in Anton do not. Measured rather than counted: letters differ.
        /// </summary>
        private void ShowNickname(string text)
        {
            _nicknameValue.Text = text;
            _nicknameValue.Font = FontBody.MeasureString(text).X <= Scaled(VALUE_WIDTH) * 0.9f ? FontBody : FontSmall;
        }

        /// <summary>
        /// What the line under the online rows says, most pressing first: what typing needs, a refusal of the name,
        /// how a removal went, that nothing can be sent — and otherwise the sentence saying what is sent and kept.
        /// </summary>
        private string OnlineNote()
        {
            if (_typing)
                return _typingProblem ?? $"Type a nickname on the keyboard: {Nickname.MinLength} to {Nickname.MaxLength} letters, digits, "
                    + "spaces, _ or -. Enter keeps it, Esc drops it.";

            if (Game.OnlineNameProblem != null)
                return $"The server refused the nickname ({Game.OnlineNameProblem}). Choose another.";

            switch (Game.OnlineRemoval)
            {
                case OnlineRemovalState.Removing:
                    return "Asking the server to remove your scores...";
                case OnlineRemovalState.Removed:
                    return "Removed from the server and from this machine.";
                case OnlineRemovalState.RemovedHere:
                    return "Removed from this machine. No score server was in reach, so nothing had been sent from here.";
                case OnlineRemovalState.Failed:
                    string problem = Game.OnlineRemovalProblem ?? string.Empty;
                    return "Nothing was removed: " + (problem.StartsWith("the server refused", StringComparison.Ordinal)
                        ? problem : "the server did not answer") + ". Try again when it is in reach.";
            }

            if (Game.IsOnlineOn && !Game.OnlineEnabled && Game.OnlineNickname != null)
                return "There is no score server yet, so nothing is sent. " + Game.OnlinePrivacySentence;

            return Game.OnlinePrivacySentence;
        }

        /// <summary>
        /// The Online row (#548): off from on at once; on from off at once when there is a nickname, and otherwise
        /// only once one has been typed — an empty nickname keeps it off.
        /// </summary>
        private void OnOnline()
        {
            _removeArmed = false;

            if (Game.IsOnlineOn) Game.SetOnline(false);
            else if (Game.OnlineNickname != null) Game.SetOnline(true);
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
            if (Game.OnlineNickname == null || Game.OnlineRemoval == OnlineRemovalState.Removing)
            {
                _removeArmed = false;
            }
            else if (_removeArmed)
            {
                _removeArmed = false;
                Game.RemoveOnlineScores();
            }
            else _removeArmed = true;

            Refresh();
        }

        private void StartTyping(bool turnOnAfter)
        {
            _typing = true;
            _turnOnAfterName = turnOnAfter;
            _nameDraft = Game.OnlineNickname ?? string.Empty;
            _typingProblem = null;
            _removeArmed = false;

            Refresh();
        }

        /// <summary>
        /// Keeps the name if it is one (<see cref="Nickname.TryNormalize"/>), and says what is wrong if it is not —
        /// the page stays in typing, so the player can fix it rather than start over.
        /// </summary>
        private void KeepTyping()
        {
            if (!Nickname.TryNormalize(_nameDraft, out string name, out string problem))
            {
                _typingProblem = problem;
                Refresh();
                return;
            }

            bool turnOn = _turnOnAfterName;

            _typing = false;
            _turnOnAfterName = false;
            _typingProblem = null;

            Game.SetNickname(name);
            if (turnOn) Game.SetOnline(true);

            Refresh();
        }

        private void CancelTyping()
        {
            if (!_typing) return;

            _typing = false;
            _turnOnAfterName = false;
            _typingProblem = null;

            Refresh();
        }

        /// <summary>
        /// A character the window typed (#548), while this page has the keyboard. Enter and Escape come through
        /// here as the characters Windows sends for them, not as key edges, so one press cannot act twice; a
        /// character no nickname may hold does nothing, and nothing past the longest name is taken.
        /// </summary>
        internal override void OnTextInput(char character)
        {
            if (!_typing) return;

            switch (character)
            {
                case '\r':
                    KeepTyping();
                    return;

                case '\x1b':
                    CancelTyping();
                    return;

                case '\b':
                    if (_nameDraft.Length > 0) _nameDraft = _nameDraft[..^1];
                    break;

                default:
                    if (!Nickname.IsAllowed(character) || _nameDraft.Length >= Nickname.MaxLength) return;
                    _nameDraft += character;
                    break;
            }

            _typingProblem = null;
            Refresh();
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
        /// </summary>
        internal void ActivateForTesting(string rows)
        {
            foreach (string row in rows.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                switch (row.ToLowerInvariant())
                {
                    case "online": OnOnline(); break;
                    case "nickname": OnNickname(); break;
                    case "remove" when UserData.IsTestingDirectory: OnRemove(); break;
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

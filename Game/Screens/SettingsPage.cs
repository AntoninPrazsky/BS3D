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
    /// <b>Two columns, because one column of thirteen rows did not fit the screen</b> (#138). Every size on
    /// this page is a 2160p design figure scaled by the viewport's <i>height</i>
    /// (<c>BS3DGame.MENU_DESIGN_HEIGHT</c>), so a page that overruns the design height overruns it at
    /// <b>every</b> resolution and aspect alike — this was never a small-window bug, and the rows that ran off
    /// the bottom took the Back button with them. Splitting the rows across two columns roughly halves the
    /// stack's height and is what puts Back back on the screen; it costs width, which the height-derived scale
    /// leaves in hand: the plate comes out about 1960 design units across (measured — 817 px at a 1600×900
    /// client, re-measured with the Auto quality row in, #390), so the page needs a viewport only wider than
    /// about 0.9:1, which every display is.
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
    /// through: the display rows, then the audio rows, the campaign rows, Back. The counts that used to
    /// stand here are left out on purpose — all three groups have grown since (#290, #349, #279), and the
    /// ORDER is the part of this that carries anything.
    /// </para>
    /// </summary>
    internal sealed class SettingsPage : MenuPage
    {
        //Narrower than the 560 the single column could afford, because there are two of them now — and no
        //value here is long ("Unlimited" is the widest), so the button stays a comfortable target at this
        //width rather than a bar most of which is empty.
        private const int VALUE_WIDTH = 460;

        //Between the two columns. Wider than the grids' own ColumnSpacing (COLUMN_SPACING), or the gutter
        //between the columns would read as just another caption/value gap and the two groups would run
        //together into four ragged columns.
        private const int GROUP_GAP = 110;

        private const int COLUMN_SPACING = 58;
        private const int ROW_SPACING = 24;

        //Above a group heading that is not its column's first, so "CAMPAIGN" separates from the audio rows
        //above it rather than reading as one more of them.
        private const int GROUP_HEADING_GAP = 40;

        private Label _fullscreenValue, _qualityValue, _adaptiveQualityValue, _exposureValue, _skyValue, _fpsValue, _fpsLimitValue;
        private Label _volumeValue, _effectsValue, _musicValue, _ambienceValue, _rumbleValue, _trackValue, _sensitivityValue, _aimSensitivityValue, _tutorialValue;
        private Label _aberrationValue, _grainValue, _dropCinematicValue;
        private Label _progressValue, _unlockAllValue;

        //The reset row asks twice. One click on a row that erases every star is an accident waiting beside
        //ten rows that are safe to click freely — so the first click only arms it and shows "Sure?", the
        //second wipes, and opening the page anew (Enter) stands it down again.
        private bool _resetArmed;

        //Every row's own button, in build order (#517) — cleared and refilled by AddRow each time BuildTree
        //runs, since a resize rebuilds the whole tree and a stale reference here would still answer
        //IsMouseInside for a button no longer on screen. Kept apart from Game's own _navEntries: that list
        //also carries the Back button, which is not a value to cycle and must not answer to the wheel.
        private readonly List<Button> _rows = new();

        public SettingsPage(BS3DGame game) : base(game) { }

        public override void Enter() => _resetArmed = false;

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

            //Whether a big collapse takes the camera (#290). It sits with the looks rather than under a
            //heading of its own because that is what it IS to the player - a flourish they can turn off - and
            //a "GAMEPLAY" heading over a single row would promise a group that does not exist.
            AddRow(grid, 10, "Drop camera", Game.ToggleDropCinematic, out _dropCinematicValue);

            return grid;
        }

        /// <summary>
        /// The right column: the mix, and under it the one row that is neither a look nor a sound but the
        /// player's own record. It carries two headings because those are two different kinds of thing, and a
        /// campaign wipe sitting unlabelled under "Ambience" would read as part of the mix.
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

            AddGroupHeading(grid, 11, "CAMPAIGN", first: false);

            //The campaign back to zero stars (#92) — for testing as much as for a fresh start. The resting
            //value shows the star total the click would erase; the click itself is two-step (see _resetArmed).
            AddRow(grid, 12, "Reset progress", OnResetProgress, out _progressValue);

            //The debug unlock (#349). Under the campaign heading rather than among the looks because it is the
            //same kind of thing the row above is - the player's record - and it is a DEVELOPMENT convenience:
            //it is off at every launch and writes nothing, so it can never make a real save read further along
            //than it is. Hiding it behind a build flag is a shipping concern and not one yet.
            AddRow(grid, 13, "Unlock all", Game.ToggleUnlockAll, out _unlockAllValue);

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

        private void AddRow(Grid grid, int row, string caption, Action onClick, out Label value)
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

            Button button = MenuButton(string.Empty, onClick, out value);
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

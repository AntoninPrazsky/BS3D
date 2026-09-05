using Myra.Graphics2D.UI;
using System;
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
    /// leaves in hand: the plate comes out about 1930 design units across (measured — 805 px at a 1600×900
    /// client), so the page needs a viewport only wider than about 0.9:1, which every display is.
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
    /// through: the eight display rows, the four audio rows, the campaign row, Back.
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

        private Label _fullscreenValue, _qualityValue, _exposureValue, _skyValue, _fpsValue, _fpsLimitValue;
        private Label _volumeValue, _effectsValue, _musicValue, _ambienceValue, _aberrationValue, _grainValue, _dropCinematicValue;
        private Label _progressValue, _unlockAllValue;

        //The reset row asks twice. One click on a row that erases every star is an accident waiting beside
        //ten rows that are safe to click freely — so the first click only arms it and shows "Sure?", the
        //second wipes, and opening the page anew (Enter) stands it down again.
        private bool _resetArmed;

        public SettingsPage(BS3DGame game) : base(game) { }

        public override void Enter() => _resetArmed = false;

        protected override Widget BuildTree()
        {
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
            AddRow(grid, 3, "Exposure", Game.CycleExposure, out _exposureValue);
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

            //Whether a big collapse takes the camera (#290). It sits with the looks rather than under a
            //heading of its own because that is what it IS to the player - a flourish they can turn off - and
            //a "GAMEPLAY" heading over a single row would promise a group that does not exist.
            AddRow(grid, 9, "Drop camera", Game.ToggleDropCinematic, out _dropCinematicValue);

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
        }

        /// <summary>Writes the current value onto each setting's button. Cheap, and only run on a change.</summary>
        internal override void Refresh()
        {
            //A display hotkey works before this page has ever been opened, so there may be nothing to write onto
            if (_fullscreenValue == null) return;

            _fullscreenValue.Text = Game.IsFullscreen ? "On" : "Off";
            _qualityValue.Text = Game.Quality.ToString();
            _exposureValue.Text = Game.Exposure.ToString("0.0", CultureInfo.InvariantCulture);
            _skyValue.Text = Game.SkyDomeNumber.ToString(CultureInfo.InvariantCulture);
            _fpsValue.Text = Game.IsFpsOverlayVisible ? "On" : "Off";
            //"Monitor", not a number: the cap is whatever the panel refreshes at, and naming the rate here
            //would go stale the moment the window lands on another monitor
            _fpsLimitValue.Text = Game.IsFpsUncapped ? "Unlimited" : "Monitor";
            _aberrationValue.Text = Game.IsAberrationEnabled ? "On" : "Off";
            _grainValue.Text = Game.IsGrainEnabled ? "On" : "Off";
            _dropCinematicValue.Text = Game.IsDropCinematicEnabled ? "On" : "Off";
            _unlockAllValue.Text = Game.IsUnlockAllEnabled ? "On" : "Off";
            _volumeValue.Text = FormatVolume(Game.MasterVolume);
            _effectsValue.Text = FormatVolume(Game.SfxVolume);
            _musicValue.Text = FormatVolume(Game.MusicVolume);
            _ambienceValue.Text = FormatVolume(Game.AmbienceVolume);
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
    }
}

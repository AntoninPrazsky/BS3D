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
    /// </summary>
    internal sealed class SettingsPage : MenuPage
    {
        private const int VALUE_WIDTH = 560;

        private Label _fullscreenValue, _ssaaValue, _exposureValue, _skyValue, _fpsValue;

        public SettingsPage(BS3DGame game) : base(game) { }

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("SETTINGS"));

            Grid grid = new()
            {
                ColumnSpacing = Scaled(58),
                RowSpacing = Scaled(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 43),
            };
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

            AddRow(grid, 0, "Fullscreen", Game.ToggleFullscreen, out _fullscreenValue);
            AddRow(grid, 1, "Antialiasing", Game.CycleSupersampling, out _ssaaValue);
            AddRow(grid, 2, "Exposure", Game.CycleExposure, out _exposureValue);
            AddRow(grid, 3, "Sky", Game.CycleSkyDome, out _skyValue);
            AddRow(grid, 4, "FPS counter", Game.ToggleFpsOverlay, out _fpsValue);

            column.Widgets.Add(grid);
            column.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(column));
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
            _ssaaValue.Text = Game.SupersampleFactor == 1 ? "Off" : Game.SupersampleFactor + "×";
            _exposureValue.Text = Game.Exposure.ToString("0.0", CultureInfo.InvariantCulture);
            _skyValue.Text = Game.SkyDomeNumber.ToString(CultureInfo.InvariantCulture);
            _fpsValue.Text = Game.IsFpsOverlayVisible ? "On" : "Off";
        }
    }
}

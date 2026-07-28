using Myra.Graphics2D.UI;
using Prazsky.BS3D.Scoring;
using System.Globalization;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// The end-of-level screen: the one place both ways a level ends (cleared, #56; failed, #58) land, and the
    /// one place a player is told which happened and chooses what to do about it.
    /// <para>
    /// No back, for a different reason from the main menu's: the level has already ended, so there is nothing
    /// to resume into and "back one level" has no meaning. Retry, Next Level and Main Menu are the only ways
    /// off it.
    /// </para>
    /// </summary>
    internal sealed class ResultPage : MenuPage
    {
        private Label _heading, _reason, _bareScore;
        private Label _matchedDetail, _matchedValue;
        private Label _orphanedDetail, _orphanedValue;
        private Label _streakValue;
        private Label _unusedDetail, _unusedValue;
        private Label _totalValue, _neededValue;
        private Widget _breakdown;
        private Button _nextLevelButton;

        //Frozen at the end of the level. Held rather than read from the session on every showing - see
        //LevelResult for why that arithmetic has to be a snapshot.
        private LevelResult _result;

        public ResultPage(BS3DGame game) : base(game) { }

        internal override bool CanGoBack => false;
        internal override bool DimsFrame => true;

        /// <summary>Takes the figures the level ended on. Called once, as the page is pushed.</summary>
        internal void Take(LevelResult result)
        {
            _result = result;

            Refresh();
        }

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();

            //CLEARED / FAILED / CAMPAIGN COMPLETE — a title's size, like the main menu's name, because this is
            //the line the screen exists to state.
            _heading = new Label
            {
                Text = string.Empty,
                Font = FontTitle,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 30),
            };
            column.Widgets.Add(_heading);

            //Which limit ran out, said plainly — only on a fail. Held back (Visible = false) on a cleared level.
            _reason = new Label
            {
                Text = string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 12),
            };
            column.Widgets.Add(_reason);

            //The score reached, on a fail. The breakdown below is rightly held back — a failed level is awarded
            //no completion bonus and its partial rows would explain a total nobody is being offered — but the
            //total itself still has to be said, or the player is told they lost and nothing about how they did.
            _bareScore = new Label
            {
                Text = string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 30),
            };
            column.Widgets.Add(_bareScore);

            //The breakdown: caption · detail · value, the same three-column shape the settings screen uses, so a
            //number lines up under the number above it and reads at a glance. A plate behind it, because small
            //text over a frozen scene needs the backing the scrim does not give.
            _breakdown = Plate(BuildBreakdown());
            column.Widgets.Add(_breakdown);

            column.Widgets.Add(MenuButton("Retry", Game.RetryLevel));

            //Absent rather than disabled when there is no next level to go to or the score did not clear the
            //gate. Retry stays: it is the one thing that always makes sense at a level's end.
            column.Widgets.Add(_nextLevelButton = MenuButton("Next Level", Game.AdvanceLevel));

            column.Widgets.Add(MenuButton("Main Menu", Game.EndSessionAndReturnToMainMenu));

            return ScreenRoot(column);
        }

        /// <summary>
        /// The score breakdown grid: each row a caption, the detail that earned it, and the points it was
        /// worth. The labels are kept on fields so <see cref="Refresh"/> can write the numbers onto them.
        /// </summary>
        private Grid BuildBreakdown()
        {
            Grid grid = new()
            {
                ColumnSpacing = Scaled(48),
                RowSpacing = Scaled(12),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));   //caption
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));   //detail (count × worth)
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Part));   //value, right-aligned by the cell

            AddRow(grid, 0, "matched", out _matchedDetail, out _matchedValue);
            AddRow(grid, 1, "orphaned", out _orphanedDetail, out _orphanedValue);
            AddRow(grid, 2, "streak bonus", out _, out _streakValue);
            AddRow(grid, 3, "shots unused", out _unusedDetail, out _unusedValue);

            //The total sits on its own line under the rows — in the value column, so it lines up under the row
            //totals — in the heading weight, so it reads as the answer rather than as another line of the sum.
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _totalValue = new Label
            {
                Text = string.Empty,
                Font = Game.MenuFontHeading,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_totalValue, 2);
            Grid.SetRow(_totalValue, 4);
            grid.Widgets.Add(_totalValue);

            //The gate the level set, as an aside under the total — "needed 1500" — so a player can see at a
            //glance whether the score cleared it without having to remember the number.
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _neededValue = new Label
            {
                Text = string.Empty,
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_neededValue, 2);
            Grid.SetRow(_neededValue, 5);
            grid.Widgets.Add(_neededValue);

            return grid;
        }

        private void AddRow(Grid grid, int row, string caption, out Label detail, out Label value)
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

            detail = new Label
            {
                Text = string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(detail, 1);
            Grid.SetRow(detail, row);
            grid.Widgets.Add(detail);

            value = new Label
            {
                Text = string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(value, 2);
            Grid.SetRow(value, row);
            grid.Widgets.Add(value);
        }

        internal override void Refresh()
        {
            //The tree may not exist yet: the page is only built when it is first shown
            if (_heading == null) return;

            //Brightness, not colour: "FAILED" is the same grey as "CLEARED", and the reason below is what tells
            //them apart — see the palette comment for why nothing here carries a hue.
            _heading.Text = _result.CampaignComplete ? "CAMPAIGN COMPLETE" : (_result.Cleared ? "CLEARED" : "FAILED");

            //The reason and the score reached are only on a fail. Hidden rather than left blank on a clear, so
            //they take no space. The reason is worded where the result is built and not where the loss was
            //detected: a message built at the point of detection carries the figures that were convenient
            //there — which is how "a ball at -5,58 <= -5,50" once reached a player.
            _reason.Text = _result.FailureText;
            _reason.Visible = _result.Failed;

            _bareScore.Text = _result.ShowsBareScore
                ? $"Score {_result.Score.ToString("N0", CultureInfo.InvariantCulture)}"
                : string.Empty;
            _bareScore.Visible = _result.ShowsBareScore;

            _breakdown.Visible = _result.ShowsBreakdown;

            if (_result.ShowsBreakdown)
            {
                _matchedDetail.Text = $"{_result.MatchedBalls} × {ScoreKeeper.MatchedBallPoints}";
                _matchedValue.Text = (_result.MatchedBalls * ScoreKeeper.MatchedBallPoints).ToString("N0", CultureInfo.InvariantCulture);
                _orphanedDetail.Text = $"{_result.OrphanedBalls} × {ScoreKeeper.OrphanedBallPoints}";
                _orphanedValue.Text = (_result.OrphanedBalls * ScoreKeeper.OrphanedBallPoints).ToString("N0", CultureInfo.InvariantCulture);
                _streakValue.Text = _result.StreakBonus.ToString("N0", CultureInfo.InvariantCulture);

                _unusedDetail.Text = _result.HadBudget
                    ? $"{_result.UnusedShotsAwarded} × {ScoreKeeper.UnusedShotPoints}"
                    : "—";
                _unusedValue.Text = _result.CompletionBonusAwarded.ToString("N0", CultureInfo.InvariantCulture);
                _totalValue.Text = _result.Score.ToString("N0", CultureInfo.InvariantCulture);

                _neededValue.Text = _result.NeededScore > 0
                    ? $"needed {_result.NeededScore.ToString("N0", CultureInfo.InvariantCulture)}"
                    : string.Empty;
            }

            //Next Level is shown only when the level was passed AND there is another entry to go to. Absent,
            //not disabled, when neither holds — a greyed-out button over a frozen frame is a thing the player
            //cannot do, which reads as the game being broken rather than as the level being the last.
            _nextLevelButton.Visible = _result.Cleared && _result.HasNextLevel;
        }
    }
}

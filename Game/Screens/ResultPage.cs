using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Prazsky.Core.Camera;
using Prazsky.BS3D.Scoring;
using System;
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
        private Label _heading, _stars, _newBest, _reason, _bareScore;
        private Label _matchedDetail, _matchedValue;
        private Label _orphanedDetail, _orphanedValue;
        private Label _streakValue;
        private Label _unusedDetail, _unusedValue;
        private Label _totalValue, _unlockNote;
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

        #region The camera lets go of the gun

        //The level is over, so there is no longer any reason for the lens to sit behind a gun that cannot
        //fire: it eases back and out onto the front end's own slow orbit, and the arena turns behind the
        //numbers. That is the whole of the moment — the player has stopped playing and is being shown what
        //they did, and a frame frozen at eye level behind the barrel says nothing about it.
        //
        //Driven from HERE and not from the session, because the session is COVERED by this page and so is no
        //longer updated at all — it is frozen mid-frame, which is exactly what a pause wants and exactly what
        //this moment does not. This page is the only screen still running, so it is the only one that can
        //move anything. (A pause is left alone deliberately: it shows the game exactly as the player left it.)

        /// <summary>
        /// How long the release takes. Long enough to read as the camera being let go rather than as a cut,
        /// short enough that it is over well before anyone has finished reading the breakdown.
        /// </summary>
        private const float ORBIT_EASE_SECONDS = 2.5f;

        private float _orbitBlend;
        private Vector3 _fromPosition, _fromTarget;
        private float _fromFov, _fromRoll;

        /// <summary>
        /// Captures the pose the level ended on. The move is a plain Lerp away from it, so there is no first
        /// frame on which anything jumps — the same one-reversible-scalar shape precise aim and the drop
        /// cinematic use, and for the same reason.
        /// </summary>
        public override void Enter()
        {
            RecoilCamera camera = Game.Camera;

            _fromPosition = camera.BasePosition;
            _fromTarget = camera.BaseTarget;
            _fromFov = camera.FieldOfView;
            _fromRoll = camera.BaseRoll;
            _orbitBlend = 0f;

            //Started at the bearing the lens is already on, so the release is straight out from the arena
            Game.Backdrop.AlignOrbitTo(_fromPosition);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            Game.Backdrop.AdvanceOrbit(elapsed, out Vector3 position, out Vector3 target, out float fieldOfView);

            _orbitBlend = MathF.Min(1f, _orbitBlend + elapsed / ORBIT_EASE_SECONDS);

            //Smoothstep, whose derivative is zero at BOTH ends: the camera leaves at rest, because it was
            //standing still, and arrives at the orbit's own rate, because by then the blend has stopped
            //changing and the only thing still moving is the orbit itself. A linear blend would start with a
            //lurch and end with the camera visibly changing speed as it settled.
            float eased = MathHelper.SmoothStep(0f, 1f, _orbitBlend);

            RecoilCamera camera = Game.Camera;

            camera.BasePosition = Vector3.Lerp(_fromPosition, position, eased);
            camera.BaseTarget = Vector3.Lerp(_fromTarget, target, eased);
            camera.FieldOfView = MathHelper.Lerp(_fromFov, fieldOfView, eased);

            //Back to a level horizon: a level can end mid-tilt if a drop cinematic was running, and a result
            //screen read over a dutched frame reads as a fault
            camera.BaseRoll = MathHelper.Lerp(_fromRoll, 0f, eased);

            //Also what settles the recoil: the last shot's kick decays here rather than being frozen into the
            //pose the player is left looking at
            camera.Update(elapsed);
        }

        #endregion

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

            //The star rating, straight under the verdict — the headline a player reads at a glance where the
            //score below is the arithmetic (#111). Set in Inter (FontStars), not the display face: Anton has
            //no ★/☆ glyphs at all, and FontStashSharp would draw blanks. Opened up with spaces so four glyphs
            //read as a rating rather than as a word.
            _stars = new Label
            {
                Text = string.Empty,
                Font = FontStars,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 12),
            };
            column.Widgets.Add(_stars);

            //Under the stars, and only when a best actually moved: a line that is always there says nothing.
            _newBest = new Label
            {
                Text = "New best",
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 12),
            };
            column.Widgets.Add(_newBest);

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

            //The one gate left, as an aside under the total: the NEXT level's star requirement, shown only
            //when the total falls short of it — which is also exactly when the Next Level button is absent,
            //so the note is what explains the absence. Spanning the grid, because it is a sentence about the
            //campaign rather than another line of the sum.
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _unlockNote = new Label
            {
                Text = string.Empty,
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_unlockNote, 0);
            Grid.SetColumnSpan(_unlockNote, 3);
            Grid.SetRow(_unlockNote, 5);
            grid.Widgets.Add(_unlockNote);

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

            //Stars only on a clear. A failed level shows NO row rather than four hollow glyphs: a loss is not
            //a rating of zero, and an empty rating under "FAILED" reads as scorn.
            _stars.Text = _result.Cleared ? StarText(_result.Stars, " ") : string.Empty;
            _stars.Visible = _result.Cleared;

            _newBest.Visible = _result.Cleared && _result.NewBest;

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

                //Only when the road ahead is actually shut — which is also when the Next Level button below
                //is absent, so this line is the absence explained rather than a number always on display.
                bool nextLocked = _result.HasNextLevel && !_result.NextLevelUnlocked;
                _unlockNote.Text = nextLocked
                    ? $"Next level unlocks at {_result.NextLevelMinStars} ★ — you have {_result.TotalStars}"
                    : string.Empty;
                _unlockNote.Visible = nextLocked;
            }

            //Next Level is shown only when the level was cleared, there is another entry to go to AND the
            //star total opens it. Absent, not disabled, when any of that fails — a greyed-out button over a
            //frozen frame is a thing the player cannot do, which reads as the game being broken rather than
            //as the campaign asking for more stars (the note above says that in words).
            _nextLevelButton.Visible = _result.Cleared && _result.HasNextLevel && _result.NextLevelUnlocked;
        }
    }
}

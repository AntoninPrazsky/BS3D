using BS3D.Online;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;
using System.Globalization;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// One online board as the game draws it (#547): its heading, the period it covers, a column of ranks, nicknames
    /// and scores, and the player's own place under them in the gold the stars are struck in. One copy, for the
    /// result page's two top-five boards and the level picker's board page alike.
    /// <para>
    /// The nicknames are in the small face, which is Inter: the service takes any letter, and Inter carries Cyrillic
    /// and Greek where Anton — the display face every value button is set in — carries neither (BS3D#548's
    /// measurement), so a name typed on another build of the rule would otherwise draw as nothing.
    /// </para>
    /// </summary>
    internal sealed class BoardView
    {
        private readonly Label _heading, _period, _you;
        private readonly Label[] _rank, _name, _score;

        public Widget Root { get; }

        /// <summary>The player's own line, for a page that punches it in.</summary>
        public Label You => _you;

        public BoardView(SpriteFontBase body, SpriteFontBase small, Func<int, int> scaled, int rows, int width)
        {
            VerticalStackPanel stack = new() { Spacing = scaled(8) };

            _heading = new Label { Font = body, TextColor = BS3DGame.MENU_TEXT };
            _period = new Label { Font = small, TextColor = BS3DGame.MENU_TEXT_DIM };
            stack.Widgets.Add(_heading);
            stack.Widgets.Add(_period);

            Grid grid = new() { ColumnSpacing = scaled(30), RowSpacing = scaled(4), Width = width };
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

            _rank = new Label[rows];
            _name = new Label[rows];
            _score = new Label[rows];

            for (int i = 0; i < rows; i++)
            {
                grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
                _rank[i] = Cell(grid, small, i, 0, HorizontalAlignment.Right);
                _name[i] = Cell(grid, small, i, 1, HorizontalAlignment.Left);
                _score[i] = Cell(grid, small, i, 2, HorizontalAlignment.Right);
            }

            stack.Widgets.Add(grid);

            _you = new Label
            {
                Font = body,
                TextColor = BS3DGame.BoardYouColor,
                TransformOrigin = new Vector2(0.5f, 0.5f),
                Margin = new Thickness(0, scaled(6), 0, 0),
            };
            stack.Widgets.Add(_you);

            Root = stack;
        }

        private static Label Cell(Grid grid, SpriteFontBase font, int row, int column, HorizontalAlignment alignment)
        {
            Label cell = new() { Font = font, TextColor = BS3DGame.MENU_TEXT_BODY, HorizontalAlignment = alignment };
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            grid.Widgets.Add(cell);
            return cell;
        }

        /// <summary>
        /// Writes the board. <paramref name="board"/> may still be null — the rows stay empty and the player's own line
        /// still says where they stand, from the submission's answer. The row at the player's rank is theirs, in gold.
        /// </summary>
        public void Fill(string heading, string period, BoardPageBody board, int rank, int total)
        {
            _heading.Text = heading;
            _period.Text = period;

            for (int i = 0; i < _rank.Length; i++)
            {
                //The worker sanitizes every page (#572); the null check is the frame's own, because a crash here is the process
                BoardEntryBody entry = board?.Entries != null && i < board.Entries.Count ? board.Entries[i] : null;
                Color colour = entry != null && entry.Rank == rank ? BS3DGame.BoardYouColor : BS3DGame.MENU_TEXT_BODY;

                _rank[i].Text = entry != null ? entry.Rank.ToString(CultureInfo.InvariantCulture) : string.Empty;
                _name[i].Text = entry?.Name ?? string.Empty;
                _score[i].Text = entry != null ? ScoreText.Of(entry.Score) : string.Empty;
                _rank[i].TextColor = _name[i].TextColor = _score[i].TextColor = colour;
            }

            _you.Text = rank > 0 ? $"You  #{rank} of {total}"
                : total > 0 ? "You are not on this board yet"
                : "Nobody is on this board yet";
        }

        /// <summary>"September 2026" off a board's <c>2026-09</c>, or off the clock while the board is on its way.</summary>
        public static string MonthName(string month)
        {
            DateTime when = month != null && DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsed) ? parsed : DateTime.UtcNow;
            return when.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        }

        public const string AllTimePeriod = "Since the boards began";
    }
}

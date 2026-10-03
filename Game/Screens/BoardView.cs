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
    /// result page's two top-five boards and the level picker's board page alike. A row with zero stars is an
    /// unfinished attempt (#716) - the service ranks every clear above all of them - and says so in a column of its
    /// own, between the name and the score. The first three ranks of a board wear a cup in gold, silver and bronze
    /// (#725), in a column of its own at the left, <b>reserved on every row</b> so no row moves when a cup appears.
    /// <para>
    /// The nicknames are in the small face, which is Inter: the service takes any letter, and Inter carries Cyrillic
    /// and Greek where Anton — the display face every value button is set in — carries neither (BS3D#548's
    /// measurement), so a name typed on another build of the rule would otherwise draw as nothing.
    /// </para>
    /// </summary>
    internal sealed class BoardView
    {
        private readonly Label _heading, _period, _you;
        private readonly Label[] _cup, _rank, _name, _mark, _score;

        /// <summary>
        /// PromptFont's trophy, U+1F3C6 - outside the Basic Multilingual Plane, so a surrogate pair in a C# string, and the one
        /// glyph the game draws from a supplementary plane (#725). It is the face's own cup, a solid silhouette with two handles.
        /// </summary>
        public const string Cup = "\U0001F3C6";

        /// <summary>The ranks that wear a cup.</summary>
        public const int CUP_RANKS = 3;

        /// <summary>What an unfinished attempt's row and the player's own line say of it (#716).</summary>
        public const string UnfinishedMark = "not finished";

        public Widget Root { get; }

        /// <summary>The player's own line, for a page that punches it in.</summary>
        public Label You => _you;

        /// <param name="cup">PromptFont at the small face's size, the cup's face (<c>BS3DGame.MenuFontPromptSmall</c>).</param>
        public BoardView(SpriteFontBase body, SpriteFontBase small, SpriteFontBase cup, Func<int, int> scaled, int rows, int width)
        {
            VerticalStackPanel stack = new() { Spacing = scaled(8) };

            _heading = new Label { Font = body, TextColor = BS3DGame.MENU_TEXT };
            _period = new Label { Font = small, TextColor = BS3DGame.MENU_TEXT_DIM };
            stack.Widgets.Add(_heading);
            stack.Widgets.Add(_period);

            Grid grid = new() { ColumnSpacing = scaled(30), RowSpacing = scaled(4), Width = width };

            //The cup's column is as wide as the cup, on every row whether it is there or not
            int cupWidth = (int)MathF.Ceiling(cup.MeasureString(Cup).X);
            int cupHeight = (int)MathF.Ceiling(MathF.Max(small.LineHeight, cup.LineHeight));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, cupWidth));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

            _cup = new Label[rows];
            _rank = new Label[rows];
            _name = new Label[rows];
            _mark = new Label[rows];
            _score = new Label[rows];

            for (int i = 0; i < rows; i++)
            {
                grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
                _cup[i] = Cell(grid, cup, i, 0, HorizontalAlignment.Left);
                _cup[i].Height = cupHeight;
                _rank[i] = Cell(grid, small, i, 1, HorizontalAlignment.Right);
                _name[i] = Cell(grid, small, i, 2, HorizontalAlignment.Left);
                _mark[i] = Cell(grid, small, i, 3, HorizontalAlignment.Right);
                _score[i] = Cell(grid, small, i, 4, HorizontalAlignment.Right);
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
        /// still says where they stand, from the submission's answer. The row at the player's rank is theirs, in gold; the
        /// first three ranks of the board wear a cup whoever they belong to (#725), and a player's own row at one of them
        /// has both: its text in their gold and the cup in the place's metal.
        /// </summary>
        /// <param name="unfinished">Whether the player's own place is an unfinished attempt, for as long as the board's
        /// own row for them has not come to say (the result page's, from the ending it was for); the board's row wins
        /// once it is in.</param>
        public void Fill(string heading, string period, BoardPageBody board, int rank, int total, bool unfinished = false)
        {
            _heading.Text = heading;
            _period.Text = period;

            for (int i = 0; i < _rank.Length; i++)
            {
                //The worker sanitizes every page (#572); the null check is the frame's own, because a crash here is the process
                BoardEntryBody entry = board?.Entries != null && i < board.Entries.Count ? board.Entries[i] : null;
                Color colour = entry != null && entry.Rank == rank ? BS3DGame.BoardYouColor : BS3DGame.MENU_TEXT_BODY;

                bool placed = entry != null && entry.Rank >= 1 && entry.Rank <= CUP_RANKS;
                _cup[i].Text = placed ? Cup : string.Empty;
                if (placed) _cup[i].TextColor = BS3DGame.PlaceColor(entry.Rank);

                _rank[i].Text = entry != null ? entry.Rank.ToString(CultureInfo.InvariantCulture) : string.Empty;
                _name[i].Text = entry?.Name ?? string.Empty;
                _mark[i].Text = entry != null && entry.Stars == 0 ? UnfinishedMark : string.Empty;
                _score[i].Text = entry != null ? ScoreText.Of(entry.Score) : string.Empty;
                _rank[i].TextColor = _name[i].TextColor = _score[i].TextColor = colour;
                _mark[i].TextColor = colour == BS3DGame.BoardYouColor ? colour : BS3DGame.MENU_TEXT_DIM;
            }

            if (board?.Me is BoardMeBody me && me.Rank > 0) unfinished = me.Stars == 0;

            _you.Text = rank > 0 ? $"You  #{rank} of {total}" + (unfinished ? ", " + UnfinishedMark : string.Empty)
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

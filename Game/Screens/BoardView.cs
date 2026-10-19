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
    /// and scores, and the player's own place under them in their own colour (BS3DGame.BoardYouColor). One copy, for the
    /// result page's two top-five boards and the level picker's board page alike. A row with zero stars is an
    /// unfinished attempt (#716) - the service ranks every clear above all of them - and says so in a column of its
    /// own, between the name and the score. The first three ranks of a board wear a cup in gold, silver and bronze
    /// (#725), in a column of its own at the left, <b>reserved on every row</b> so no row moves when a cup appears.
    /// <para>
    /// The nicknames are in the small face, which is Inter: the service takes any letter, and Inter carries Cyrillic
    /// and Greek where Anton — the display face every value button is set in — carries neither (BS3D#548's
    /// measurement), so a name typed on another build of the rule would otherwise draw as nothing.
    /// </para>
    /// <para>
    /// <b>The ranks and the scores are set in tabular figures</b> (#806, <see cref="FontFigures"/>), flush right: Inter's
    /// figures are proportional, so "31 111" under "40 118" put its hundreds a narrow 1 to the right of the row above's.
    /// </para>
    /// <para>
    /// <b>Every column has a width of its own, and a name is cut to its column with an ellipsis</b> (#934, the arithmetic is
    /// <see cref="BoardLayout"/>'s): a Myra label does not clip to its cell, and the name's Fill column used to be whatever the
    /// rank, mark and score columns left it, so a long nickname was drawn on across "not finished" and the score. The rank and
    /// the score are as wide as the widest of the rows written, the mark's column is there only while a row wears the mark, the
    /// name has all that is left (a row that wears no mark may use the mark's column too) and <see cref="Fill"/> shortens a name
    /// to it. The longest nickname a board can hold shows whole
    /// at <see cref="FullWidth"/>, which is what the pages let a board grow to on a wide display.
    /// </para>
    /// </summary>
    internal sealed class BoardView
    {
        private readonly Label _heading, _period, _you;
        private readonly Label[] _cup, _rank, _name, _mark, _score;

        //The face the ranks and scores are set in, for their tabular figures, and the names are measured in
        private readonly SpriteFontBase _small;

        //The grid whose columns are re-sized every time the rows are written, the board's width, the gap between columns, the
        //cup's column and the mark's width while a row wears it, all in pixels at the layout in force
        private readonly Grid _grid;
        private readonly int _width, _gap, _cupWidth, _markWidth;
        private readonly Func<string, int> _measure;

        //The gap between two columns of a row, in design units
        private const int GAP = 30;

        //The ranks and scores a board is sized for in FullWidth: four digits of players, six of points
        private const string WidestRank = "8888";
        private const int WidestScore = 888_888;

        /// <summary>
        /// PromptFont's trophy, U+1F3C6 - outside the Basic Multilingual Plane, so a surrogate pair in a C# string, and the one
        /// glyph the game draws from a supplementary plane (#725). It is the face's own cup, a solid silhouette with two handles.
        /// </summary>
        public const string Cup = "\U0001F3C6";

        /// <summary>The ranks that wear a cup.</summary>
        public const int CUP_RANKS = 3;

        /// <summary>What an unfinished attempt's row and the player's own line say of it (#716).</summary>
        public const string UnfinishedMark = PickerCopy.UNFINISHED;

        public Widget Root { get; }

        /// <summary>The player's own line, for a page that punches it in.</summary>
        public Label You => _you;

        /// <param name="cup">PromptFont at the small face's size, the cup's face (<c>BS3DGame.MenuFontPromptSmall</c>).</param>
        public BoardView(SpriteFontBase body, SpriteFontBase small, SpriteFontBase cup, Func<int, int> scaled, int rows, int width)
        {
            _small = small;
            _measure = text => (int)MathF.Ceiling(small.MeasureString(text).X);
            VerticalStackPanel stack = new() { Spacing = scaled(8) };

            _heading = new Label { Font = body, TextColor = BS3DGame.MENU_TEXT };
            _period = new Label { Font = small, TextColor = BS3DGame.MENU_TEXT_DIM };
            stack.Widgets.Add(_heading);
            stack.Widgets.Add(_period);

            _width = width;
            _gap = scaled(GAP);
            _markWidth = _measure(UnfinishedMark);
            Grid grid = new() { ColumnSpacing = _gap, RowSpacing = scaled(4), Width = width };
            _grid = grid;

            //The cup's column is as wide as the cup, on every row whether it is there or not
            _cupWidth = (int)MathF.Ceiling(cup.MeasureString(Cup).X);
            int cupHeight = (int)MathF.Ceiling(MathF.Max(small.LineHeight, cup.LineHeight));

            //Every column a width of its own (#934); Fill writes them. Until the first Fill the rows are empty, and the name
            //has what the cup leaves
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, _cupWidth));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 0));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, BoardLayout.NameWidth(width, _cupWidth, 0, 0, 0, _gap)));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 0));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 0));

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
        /// still says where they stand, from the submission's answer. The row at the player's rank is theirs, in their colour; the
        /// first three ranks of the board wear a cup whoever they belong to (#725), and a player's own row at one of them
        /// has both: its text in their colour and the cup in the place's metal - two colours, which is why theirs is not gold (#739).
        /// </summary>
        /// <param name="unfinished">Whether the player's own place is an unfinished attempt, for as long as the board's
        /// own row for them has not come to say (the result page's, from the ending it was for); the board's row wins
        /// once it is in.</param>
        public void Fill(string heading, string period, BoardPageBody board, int rank, int total, bool unfinished = false)
        {
            _heading.Text = heading;
            _period.Text = period;

            //The columns' widths are worked out as the rows are written, since the name's is what the others leave it: the rank
            //and the score as wide as the widest row writes them, the mark only while a row wears it (#934)
            int rankWidth = 0, scoreWidth = 0;
            bool marked = false;

            for (int i = 0; i < _rank.Length; i++)
            {
                BoardEntryBody entry = EntryAt(board, i);
                Color colour = entry != null && entry.Rank == rank ? BS3DGame.BoardYouColor : BS3DGame.MENU_TEXT_BODY;

                bool placed = entry != null && entry.Rank >= 1 && entry.Rank <= CUP_RANKS;
                _cup[i].Text = placed ? Cup : string.Empty;
                if (placed) _cup[i].TextColor = BS3DGame.PlaceColor(entry.Rank);

                string rankText = entry != null ? entry.Rank.ToString(CultureInfo.InvariantCulture) : string.Empty;
                string scoreText = entry != null ? ScoreText.Of(entry.Score) : string.Empty;
                rankWidth = Math.Max(rankWidth, FontFigures.Width(rankText, _small));
                scoreWidth = Math.Max(scoreWidth, FontFigures.Width(scoreText, _small));
                marked |= entry != null && entry.Stars == 0;

                _rank[i].Text = FontFigures.Tabular(rankText, _small);
                _mark[i].Text = entry != null && entry.Stars == 0 ? UnfinishedMark : string.Empty;
                _score[i].Text = FontFigures.Tabular(scoreText, _small);
                _rank[i].TextColor = _name[i].TextColor = _score[i].TextColor = colour;
                _mark[i].TextColor = colour == BS3DGame.BoardYouColor ? colour : BS3DGame.MENU_TEXT_DIM;
            }

            int markWidth = marked ? _markWidth : 0;
            int nameWidth = BoardLayout.NameWidth(_width, _cupWidth, rankWidth, markWidth, scoreWidth, _gap);
            SetColumn(1, rankWidth);
            SetColumn(2, nameWidth);
            SetColumn(3, markWidth);
            SetColumn(4, scoreWidth);

            //A name is cut to its column with an ellipsis where it would run into the mark or the score (#934). A row that wears
            //no mark lets its name span the mark's column too, which is empty on it
            for (int i = 0; i < _name.Length; i++)
            {
                BoardEntryBody entry = EntryAt(board, i);
                bool marks = entry != null && entry.Stars == 0;

                Grid.SetColumnSpan(_name[i], marks ? 1 : 2);
                _name[i].Text = BoardLayout.Ellipsize(entry?.Name, BoardLayout.RowMeasure(nameWidth, markWidth, _gap, marks), _measure)
                    ?? string.Empty;
            }

            if (board?.Me is BoardMeBody me && me.Rank > 0) unfinished = me.Stars == 0;

            _you.Text = PickerCopy.YouLine(rank, total, unfinished);
        }

        //The worker sanitizes every page (#572); the null checks are the frame's own, because a crash here is the process
        private static BoardEntryBody EntryAt(BoardPageBody board, int index) =>
            board?.Entries != null && index < board.Entries.Count ? board.Entries[index] : null;

        private void SetColumn(int column, int pixels)
        {
            Proportion proportion = _grid.ColumnsProportions[column];
            if (proportion.Value != pixels) proportion.Value = pixels;
        }

        /// <summary>
        /// The width at which the longest nickname there can be shows whole, beside the widest mark, rank and score a row holds
        /// (#934): what the pages let a board grow to on a wide display, since a plate wider than this would only be emptier.
        /// A name is <see cref="Nickname.MaxLength"/> of the face's widest capital.
        /// </summary>
        public static int FullWidth(SpriteFontBase small, SpriteFontBase cup, Func<int, int> scaled)
        {
            int Measure(string text) => (int)MathF.Ceiling(small.MeasureString(text).X);

            return BoardLayout.FullWidth((int)MathF.Ceiling(cup.MeasureString(Cup).X),
                FontFigures.Width(WidestRank, small),
                Measure(new string('W', Nickname.MaxLength)),
                Measure(UnfinishedMark),
                FontFigures.Width(ScoreText.Of(WidestScore), small),
                scaled(GAP));
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

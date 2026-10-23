using BS3D.Online;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;
using System.Globalization;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// One online board as the game draws it (#547): its heading, the period it covers, a column of ranks, nicknames
    /// and scores, and the player's own place under them in their own colour (BS3DGame.BoardYouColor). One copy, for the
    /// result page's two top-five boards and the level picker's board page alike. The first three ranks of a board wear
    /// a cup in gold, silver and bronze (#725), in a column of its own at the left, <b>reserved on every row</b> so no
    /// row moves when a cup appears. A row with zero stars is an unfinished attempt (#716) - the service ranks every
    /// clear above all of them - and says so (#977) <b>without a column of its own</b>: the cup's column shows a hollow
    /// star in its place and the row rests in the recessed slab and the dimmer type High Scores gives a level not cleared
    /// (#898), so a name has the same room on every row. A cup is for a clear, so an unfinished attempt never wears one.
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
    /// rank and score columns left it, so a long nickname was drawn on across "not finished" and the score. The rank and
    /// the score are as wide as the widest of the rows written, the cup's column holds a cup or the hollow star, the
    /// name has all that is left and <see cref="Fill"/> shortens a name to it. The longest nickname a board can hold shows whole
    /// at <see cref="FullWidth"/>, which is what the pages let a board grow to on a wide display.
    /// </para>
    /// </summary>
    internal sealed class BoardView
    {
        private readonly Label _heading, _period, _you;
        private readonly Label[] _cup, _rank, _name, _score;

        //The slab behind each row, which rests in the recess of an unfinished attempt and in nothing otherwise (#977)
        private readonly Widget[] _band;

        //The face the ranks and scores are set in, for their tabular figures, and the names are measured in; the cup's face; the
        //cup column's width
        private readonly SpriteFontBase _small, _cupFont;

        //The grid whose columns are re-sized every time the rows are written, the board's width, the gap between columns and the
        //cup's column, all in pixels at the layout in force
        private readonly Grid _grid;
        private readonly int _width, _gap, _cupWidth;
        private readonly Func<string, int> _measure;

        //The gap between two columns of a row, in design units, and how many columns a row has: the cup, the rank, the name, the score
        private const int GAP = 30;
        private const int COLUMNS = 4;

        //The ranks and scores a board is sized for in FullWidth: four digits of players, six of points
        private const string WidestRank = "8888";
        private const int WidestScore = 888_888;

        //How far a recessed row's slab reaches past the first and the last column, in design units: type flush against the edge
        //of a slab reads as clipped
        private const int BAND_BLEED = 14;

        //The hollow star an unfinished attempt wears in the cup's column: Inter's, the same glyph the picker's tiles draw an
        //unearned star with (the cup's own face, PromptFont, would draw nothing for it), and the one High Scores' cells say it with
        private const string UnfinishedSign = PickerCopy.UNFINISHED_MARK;

        /// <summary>
        /// PromptFont's trophy, U+1F3C6 - outside the Basic Multilingual Plane, so a surrogate pair in a C# string, and the one
        /// glyph the game draws from a supplementary plane (#725). It is the face's own cup, a solid silhouette with two handles.
        /// </summary>
        public const string Cup = "\U0001F3C6";

        public Widget Root { get; }

        /// <summary>The player's own line, for a page that punches it in.</summary>
        public Label You => _you;

        /// <param name="cup">PromptFont at the small face's size, the cup's face (<c>BS3DGame.MenuFontPromptSmall</c>).</param>
        public BoardView(SpriteFontBase body, SpriteFontBase small, SpriteFontBase cup, Func<int, int> scaled, int rows, int width)
        {
            _small = small;
            _cupFont = cup;
            _measure = text => (int)MathF.Ceiling(small.MeasureString(text).X);
            VerticalStackPanel stack = new() { Spacing = scaled(8) };

            _heading = new Label { Font = body, TextColor = BS3DGame.MENU_TEXT };
            _period = new Label { Font = small, TextColor = BS3DGame.MENU_TEXT_DIM };
            stack.Widgets.Add(_heading);
            stack.Widgets.Add(_period);

            _width = width;
            _gap = scaled(GAP);
            Grid grid = new() { ColumnSpacing = _gap, RowSpacing = scaled(4), Width = width };
            _grid = grid;

            //The cup's column is as wide as the cup or the hollow star, whichever is wider, on every row whether either is there or not
            _cupWidth = CupColumnWidth(small, cup);
            int cupHeight = (int)MathF.Ceiling(MathF.Max(small.LineHeight, cup.LineHeight));

            //Every column a width of its own (#934); Fill writes them. Until the first Fill the rows are empty, and the name
            //has what the cup leaves
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, _cupWidth));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 0));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, BoardLayout.NameWidth(width, _cupWidth, 0, 0, _gap)));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 0));

            _band = new Widget[rows];
            _cup = new Label[rows];
            _rank = new Label[rows];
            _name = new Label[rows];
            _score = new Label[rows];

            int bleed = scaled(BAND_BLEED);

            for (int i = 0; i < rows; i++)
            {
                grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

                //The slab goes in first, so it is drawn under the row's text; it spans every column and reaches a little past them
                _band[i] = new Widget
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Margin = new Thickness(-bleed, 0, -bleed, 0),
                };
                Grid.SetRow(_band[i], i);
                Grid.SetColumn(_band[i], 0);
                Grid.SetColumnSpan(_band[i], COLUMNS);
                grid.Widgets.Add(_band[i]);

                _cup[i] = Cell(grid, cup, i, 0, HorizontalAlignment.Left);
                _cup[i].Height = cupHeight;
                _rank[i] = Cell(grid, small, i, 1, HorizontalAlignment.Right);
                _name[i] = Cell(grid, small, i, 2, HorizontalAlignment.Left);
                _score[i] = Cell(grid, small, i, 3, HorizontalAlignment.Right);
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
        /// first three ranks of the board wear a cup whoever they belong to (#725) unless the attempt did not finish, and a player's
        /// own row at one of them has both: its text in their colour and the cup in the place's metal - two colours, which is why
        /// theirs is not gold (#739). A row that did not finish (zero stars, #716) wears the hollow star in the cup's column, rests
        /// in the recessed slab and sets its type a step dimmer, the player's own row keeping their colour (#977).
        /// </summary>
        /// <param name="unfinished">Whether the player's own place is an unfinished attempt, for as long as the board's
        /// own row for them has not come to say (the result page's, from the ending it was for); the board's row wins
        /// once it is in.</param>
        public void Fill(string heading, string period, BoardPageBody board, int rank, int total, bool unfinished = false)
        {
            _heading.Text = heading;
            _period.Text = period;

            //The columns' widths are worked out as the rows are written, since the name's is what the others leave it: the rank
            //and the score as wide as the widest row writes them (#934)
            int rankWidth = 0, scoreWidth = 0;

            for (int i = 0; i < _rank.Length; i++)
            {
                BoardEntryBody entry = EntryAt(board, i);
                bool you = entry != null && entry.Rank == rank;
                BoardLayout.Sign sign = entry == null ? BoardLayout.Sign.None : BoardLayout.SignOf(entry.Rank, entry.Stars);
                bool didNotFinish = sign == BoardLayout.Sign.Unfinished;

                //Greyscale and the player's magenta only: the recess and the dimmer type carry the tint, and gold stays a cup's
                Color colour = you ? BS3DGame.BoardYouColor : didNotFinish ? BS3DGame.MENU_TEXT_DIM : BS3DGame.MENU_TEXT_BODY;
                _band[i].Background = didNotFinish ? BS3DGame.MENU_ROW_UNFINISHED_BRUSH : null;

                switch (sign)
                {
                    case BoardLayout.Sign.Cup:
                        _cup[i].Font = _cupFont;
                        _cup[i].HorizontalAlignment = HorizontalAlignment.Left;
                        _cup[i].Text = Cup;
                        _cup[i].TextColor = BS3DGame.PlaceColor(entry.Rank);
                        break;
                    case BoardLayout.Sign.Unfinished:
                        _cup[i].Font = _small;
                        _cup[i].HorizontalAlignment = HorizontalAlignment.Center;
                        _cup[i].Text = UnfinishedSign;
                        _cup[i].TextColor = colour;
                        break;
                    default:
                        _cup[i].Text = string.Empty;
                        break;
                }

                string rankText = entry != null ? entry.Rank.ToString(CultureInfo.InvariantCulture) : string.Empty;
                string scoreText = entry != null ? ScoreText.Of(entry.Score) : string.Empty;
                rankWidth = Math.Max(rankWidth, FontFigures.Width(rankText, _small));
                scoreWidth = Math.Max(scoreWidth, FontFigures.Width(scoreText, _small));

                _rank[i].Text = FontFigures.Tabular(rankText, _small);
                _score[i].Text = FontFigures.Tabular(scoreText, _small);
                _rank[i].TextColor = _name[i].TextColor = _score[i].TextColor = colour;
            }

            int nameWidth = BoardLayout.NameWidth(_width, _cupWidth, rankWidth, scoreWidth, _gap);
            SetColumn(1, rankWidth);
            SetColumn(2, nameWidth);
            SetColumn(3, scoreWidth);

            //A name is cut to its column with an ellipsis where it would run into the score (#934)
            for (int i = 0; i < _name.Length; i++)
                _name[i].Text = BoardLayout.Ellipsize(EntryAt(board, i)?.Name, nameWidth, _measure) ?? string.Empty;

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
        /// The width at which the longest nickname there can be shows whole, beside the widest rank and score a row holds
        /// (#934): what the pages let a board grow to on a wide display, since a plate wider than this would only be emptier.
        /// A name is <see cref="Nickname.MaxLength"/> of the face's widest capital.
        /// </summary>
        public static int FullWidth(SpriteFontBase small, SpriteFontBase cup, Func<int, int> scaled)
        {
            int Measure(string text) => (int)MathF.Ceiling(small.MeasureString(text).X);

            return BoardLayout.FullWidth(CupColumnWidth(small, cup),
                FontFigures.Width(WidestRank, small),
                Measure(new string('W', Nickname.MaxLength)),
                FontFigures.Width(ScoreText.Of(WidestScore), small),
                scaled(GAP));
        }

        /// <summary>The cup's column: as wide as the cup or the hollow star an unfinished attempt wears in its place, the wider.</summary>
        private static int CupColumnWidth(SpriteFontBase small, SpriteFontBase cup) =>
            (int)MathF.Ceiling(MathF.Max(cup.MeasureString(Cup).X, small.MeasureString(UnfinishedSign).X));

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

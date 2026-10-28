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
    /// The players' ranking as the Top Players page draws it (#827): a head of column names, a page of rows (the place's cup
    /// for the first three, the rank, the nickname, the levels cleared, the stars and the total) and the player's own line
    /// under them in their colour. <see cref="BoardView"/>'s board with the two columns the ranking adds, on its terms: the
    /// cup's column reserved on every row (#725), the figures in tabular figures flush right (#806), every column as wide as
    /// its widest row and the name cut to what is left with an ellipsis (#934), the nicknames in the small face, which draws
    /// any letter the service takes (BS3D#548).
    /// </summary>
    internal sealed class RankingView
    {
        private readonly Label _you;
        private readonly Label[] _head = new Label[COLUMNS];
        private readonly Label[] _cup, _rank, _name, _levels, _stars, _score;
        private readonly SpriteFontBase _small;
        private readonly Grid _grid;
        private readonly int _width, _gap, _cupWidth;
        private readonly Func<string, int> _measure;

        //The gap between two columns, in design units, and the columns: the cup, the rank, the name, the levels, the stars, the score
        private const int GAP = 30;
        private const int COLUMNS = 6;

        public Widget Root { get; }

        /// <param name="cup">PromptFont at the small face's size, the cup's face (<c>BS3DGame.MenuFontPromptSmall</c>).</param>
        public RankingView(SpriteFontBase body, SpriteFontBase small, SpriteFontBase cup, Func<int, int> scaled, int rows, int width)
        {
            _small = small;
            _measure = text => (int)MathF.Ceiling(small.MeasureString(text).X);
            VerticalStackPanel stack = new() { Spacing = scaled(8) };

            _width = width;
            _gap = scaled(GAP);
            _grid = new Grid { ColumnSpacing = _gap, RowSpacing = scaled(4), Width = width };
            _cupWidth = (int)MathF.Ceiling(cup.MeasureString(BoardView.Cup).X);
            int cupHeight = (int)MathF.Ceiling(MathF.Max(small.LineHeight, cup.LineHeight));

            for (int column = 0; column < COLUMNS; column++) _grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 0));
            SetColumn(0, _cupWidth);

            //The head: what each figure is, dim, over its column and aligned as it is
            _grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            string[] heads = { string.Empty, "#", "Player", "Levels", PickerCopy.STAR, "Score" };
            for (int column = 0; column < COLUMNS; column++)
            {
                _head[column] = Cell(_grid, small, 0, column, column is 2 or 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right);
                _head[column].Text = heads[column];
                _head[column].TextColor = BS3DGame.MENU_TEXT_DIM;
            }

            _cup = new Label[rows];
            _rank = new Label[rows];
            _name = new Label[rows];
            _levels = new Label[rows];
            _stars = new Label[rows];
            _score = new Label[rows];

            for (int i = 0; i < rows; i++)
            {
                _grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
                _cup[i] = Cell(_grid, cup, i + 1, 0, HorizontalAlignment.Left);
                _cup[i].Height = cupHeight;
                _rank[i] = Cell(_grid, small, i + 1, 1, HorizontalAlignment.Right);
                _name[i] = Cell(_grid, small, i + 1, 2, HorizontalAlignment.Left);
                _levels[i] = Cell(_grid, small, i + 1, 3, HorizontalAlignment.Right);
                _stars[i] = Cell(_grid, small, i + 1, 4, HorizontalAlignment.Right);
                _score[i] = Cell(_grid, small, i + 1, 5, HorizontalAlignment.Right);
            }

            stack.Widgets.Add(_grid);

            _you = new Label
            {
                Font = body,
                TextColor = BS3DGame.BoardYouColor,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, scaled(10), 0, 0),
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
        /// Writes a page of the ranking. <paramref name="page"/> may be null (on its way, or none to be had): the rows stay
        /// empty and the player's line says <paramref name="youLine"/>. The row at the player's own rank is theirs, in their
        /// colour; the first three ranks wear their place's cup whoever holds them (#725).
        /// </summary>
        public void Fill(RankingPageBody page, string youLine)
        {
            int rank = page?.Me?.Rank ?? 0;
            int rankWidth = FontFigures.Width("#", _small), levelsWidth = _measure("Levels"), starsWidth = _measure(PickerCopy.STAR);
            int scoreWidth = _measure("Score");

            for (int i = 0; i < _rank.Length; i++)
            {
                RankingEntryBody entry = page?.Entries != null && i < page.Entries.Count ? page.Entries[i] : null;
                bool you = entry != null && rank > 0 && entry.Rank == rank;
                Color colour = you ? BS3DGame.BoardYouColor : BS3DGame.MENU_TEXT_BODY;

                bool cup = entry != null && entry.Rank is >= 1 and <= 3;
                _cup[i].Text = cup ? BoardView.Cup : string.Empty;
                if (cup) _cup[i].TextColor = BS3DGame.PlaceColor(entry.Rank);

                string rankText = entry != null ? entry.Rank.ToString(CultureInfo.InvariantCulture) : string.Empty;
                string levelsText = entry != null ? entry.Levels.ToString(CultureInfo.InvariantCulture) : string.Empty;
                string starsText = entry != null ? entry.Stars.ToString(CultureInfo.InvariantCulture) : string.Empty;
                string scoreText = entry != null ? ScoreText.Of(entry.Score) : string.Empty;

                rankWidth = Math.Max(rankWidth, FontFigures.Width(rankText, _small));
                levelsWidth = Math.Max(levelsWidth, FontFigures.Width(levelsText, _small));
                starsWidth = Math.Max(starsWidth, FontFigures.Width(starsText, _small));
                scoreWidth = Math.Max(scoreWidth, FontFigures.Width(scoreText, _small));

                _rank[i].Text = FontFigures.Tabular(rankText, _small);
                _levels[i].Text = FontFigures.Tabular(levelsText, _small);
                _stars[i].Text = FontFigures.Tabular(starsText, _small);
                _score[i].Text = FontFigures.Tabular(scoreText, _small);
                _rank[i].TextColor = _name[i].TextColor = _levels[i].TextColor = _stars[i].TextColor = _score[i].TextColor = colour;
            }

            //The name has what the other five columns and their gaps leave it
            int nameWidth = Math.Max(0, _width - _cupWidth - rankWidth - levelsWidth - starsWidth - scoreWidth - (COLUMNS - 1) * _gap);
            SetColumn(1, rankWidth);
            SetColumn(2, nameWidth);
            SetColumn(3, levelsWidth);
            SetColumn(4, starsWidth);
            SetColumn(5, scoreWidth);

            for (int i = 0; i < _name.Length; i++)
            {
                RankingEntryBody entry = page?.Entries != null && i < page.Entries.Count ? page.Entries[i] : null;
                _name[i].Text = BoardLayout.Ellipsize(entry?.Name, nameWidth, _measure) ?? string.Empty;
            }

            _you.Text = youLine;
        }

        private void SetColumn(int column, int pixels)
        {
            Proportion proportion = _grid.ColumnsProportions[column];
            if (proportion.Value != pixels) proportion.Value = pixels;
        }
    }
}

using BS3D.Screens;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// Tabular figures by hand (#806): every digit in a cell as wide as the face's widest, so a column of scores set flush
    /// right lines up digit under digit although the faces' own figures are proportional. <c>TabularFigures</c> is compiled
    /// in from the Game; the face is a stand-in with Inter's own proportion of a narrow 1 against a wide 0, and the label is
    /// a stand-in of FontStashSharp's rich-text layout (<c>RichText.LayoutBuilder</c>, read off the 1.5.6 assembly): text
    /// between commands is one chunk measured whole, kerning included, and <c>/s[n]</c> is a chunk of n pixels.
    /// </summary>
    public class TabularFiguresTests
    {
        //'0' to '9': a narrow 1 and a spread of others, as Inter's are (its 1 is 833 units to its 0's 1292)
        private static readonly int[] ADVANCES = { 26, 17, 24, 25, 26, 24, 25, 22, 25, 25 };
        private const int CELL = 26;

        //The rest of the stand-in face: a space, and every other glyph 20 wide
        private static int Advance(char c) => c >= '0' && c <= '9' ? ADVANCES[c - '0'] : c == ' ' ? 9 : 20;

        //A chunk measured whole, with a kerning pair inside it that pulls every two neighbours 2 px together - the
        //thing that must never act between two figures
        private static int MeasureRun(string run)
        {
            int width = 0;
            for (int i = 0; i < run.Length; i++) width += Advance(run[i]) - (i > 0 ? 2 : 0);
            return width;
        }

        private sealed record Chunk(string Text, int X, int Width);

        /// <summary>
        /// The layout's reading of a label's text: chunks of text between commands, each at its pen position, and the line's
        /// width - where the pen ends, a trailing space command included, as the layout adds every chunk's width to the line.
        /// </summary>
        private static (List<Chunk> Chunks, int Width) Lay(string set)
        {
            List<Chunk> chunks = new();
            StringBuilder text = new();
            int x = 0;

            void Flush()
            {
                if (text.Length == 0) return;
                string run = text.ToString();
                chunks.Add(new Chunk(run, x, MeasureRun(run)));
                x += MeasureRun(run);
                text.Clear();
            }

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == '/' && i + 1 < set.Length && set[i + 1] == '/')
                {
                    text.Append('/');
                    i++;
                }
                else if (set[i] == '/' && i + 2 < set.Length && set[i + 1] == 's' && set[i + 2] == '[')
                {
                    Flush();
                    int close = set.IndexOf(']', i);
                    x += int.Parse(set.Substring(i + 3, close - i - 3));
                    i = close;
                }
                else text.Append(set[i]);
            }

            Flush();
            return (chunks, x);
        }

        /// <summary>Where each digit's cell starts, counted from the right edge, in the order the digits stand.</summary>
        private static List<int> CellsFromRight(string number)
        {
            (List<Chunk> chunks, int right) = Lay(TabularFigures.Set(number, ADVANCES));
            List<int> cells = new();

            foreach (Chunk chunk in chunks)
            {
                if (chunk.Text.Length != 1 || chunk.Text[0] < '0' || chunk.Text[0] > '9') continue;

                //The glyph is centred in its cell, so the cell starts half its shortfall before the glyph
                int shortfall = CELL - ADVANCES[chunk.Text[0] - '0'];
                cells.Add(right - (chunk.X - shortfall / 2));
            }

            return cells;
        }

        [Fact]
        public void The_cell_is_the_widest_digit() => Assert.Equal(CELL, TabularFigures.Cell(ADVANCES));

        [Theory]
        [InlineData("31 111", "40 118")]
        [InlineData("11 111", "88 888")]
        [InlineData("1 111", "9 990")]
        [InlineData("96 × 10", "24 × 20")]
        [InlineData("#11", "#20")]
        public void Numbers_of_one_shape_line_up_digit_under_digit_when_set_flush_right(string a, string b)
        {
            //Proportional, the two lay out to different widths - the fault being fixed
            Assert.NotEqual(Lay(a).Width, Lay(b).Width);

            Assert.Equal(Lay(TabularFigures.Set(a, ADVANCES)).Width, Lay(TabularFigures.Set(b, ADVANCES)).Width);
            Assert.Equal(CellsFromRight(a), CellsFromRight(b));
        }

        /// <summary>The set text as the layout reads it, one token at a time: a space command's pixels, or one drawn glyph.</summary>
        private static List<(int Space, char Glyph)> Tokens(string set)
        {
            List<(int, char)> tokens = new();

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == '/' && set[i + 1] == '/')
                {
                    tokens.Add((-1, '/'));
                    i++;
                }
                else if (set[i] == '/' && set[i + 1] == 's')
                {
                    int close = set.IndexOf(']', i);
                    tokens.Add((int.Parse(set.Substring(i + 3, close - i - 3)), '\0'));
                    i = close;
                }
                else tokens.Add((-1, set[i]));
            }

            return tokens;
        }

        [Theory]
        [InlineData("118 410")]
        [InlineData("1")]
        [InlineData("You #6 of 21")]
        public void Every_digit_takes_exactly_one_cell_and_is_centred_in_it(string text)
        {
            List<(int Space, char Glyph)> tokens = Tokens(TabularFigures.Set(text, ADVANCES));
            int digits = 0;

            for (int i = 0; i < tokens.Count; i++)
            {
                char c = tokens[i].Glyph;
                if (tokens[i].Space >= 0 || c < '0' || c > '9') continue;

                digits++;
                Assert.True(i > 0 && tokens[i - 1].Space >= 0, $"the {c} has no space command before it");
                Assert.True(i + 1 < tokens.Count && tokens[i + 1].Space >= 0, $"the {c} has no space command after it");

                int before = tokens[i - 1].Space, after = tokens[i + 1].Space;
                Assert.Equal(CELL, before + ADVANCES[c - '0'] + after);
                Assert.Equal((CELL - ADVANCES[c - '0']) / 2, before);
            }

            int expected = 0;
            foreach (char c in text) if (c >= '0' && c <= '9') expected++;
            Assert.Equal(expected, digits);
        }

        [Theory]
        [InlineData("118 410")]
        [InlineData("88 888")]
        [InlineData("7 × 50")]
        public void No_two_figures_share_a_chunk_so_no_kerning_pair_moves_one(string text)
        {
            foreach (Chunk chunk in Lay(TabularFigures.Set(text, ADVANCES)).Chunks)
                if (chunk.Text.IndexOfAny("0123456789".ToCharArray()) >= 0)
                    Assert.True(chunk.Text.Length == 1, $"'{chunk.Text}' sets a figure beside another glyph, where kerning reaches it");
        }

        [Theory]
        [InlineData("118 410")]
        [InlineData("96 × 10")]
        [InlineData("—")]
        [InlineData("You #6 of 21, not finished")]
        [InlineData("")]
        public void Width_is_what_the_layout_lays_the_set_text_out_to(string text) =>
            Assert.Equal(Lay(TabularFigures.Set(text, ADVANCES)).Width, TabularFigures.Width(text, ADVANCES, MeasureRun));

        [Fact]
        public void Text_without_figures_is_handed_back_as_it_was()
        {
            const string text = "not finished";
            Assert.Same(text, TabularFigures.Set(text, ADVANCES));
            Assert.Null(TabularFigures.Set(null, ADVANCES));
        }

        [Fact]
        public void A_slash_is_drawn_rather_than_read_as_a_command()
        {
            List<Chunk> chunks = Lay(TabularFigures.Set("1/2", ADVANCES)).Chunks;
            Assert.Contains(chunks, c => c.Text == "/");
        }

        [Fact]
        public void A_face_is_ten_advances() =>
            Assert.Throws<ArgumentException>(() => TabularFigures.Cell(new[] { 1, 2, 3 }));
    }
}

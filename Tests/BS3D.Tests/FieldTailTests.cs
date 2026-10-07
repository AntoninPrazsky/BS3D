using BS3D.Screens;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The note field's tail (#813): the last lines of a note as a fixed-height field shows them, counted in measured
    /// lines rather than characters, so the line being typed is always in view. <c>FieldTail</c> is compiled in from the
    /// Game; the measure is a stand-in of one unit a character.
    /// </summary>
    public class FieldTailTests
    {
        private static float Monospace(string line) => line.Length;

        private const string CARET = "_";

        [Theory]
        [InlineData(12f, 3)]
        [InlineData(20f, 6)]
        [InlineData(37f, 6)]
        public void Every_line_fits_and_no_more_lines_than_the_field_holds(float width, int lines)
        {
            string note = "The ceiling dropped right after my shot and I did not see why. Then a long word: "
                + "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz, and\nthree\nshort\nlines" + CARET;

            string[] shown = FieldTail.Show(note, width, lines, Monospace).Split('\n');

            Assert.True(shown.Length <= lines, $"{shown.Length} lines in a field of {lines}");
            foreach (string line in shown) Assert.True(line.Length <= width, $"'{line}' is wider than {width}");
        }

        [Fact]
        public void The_caret_is_on_the_last_line_shown_however_the_note_breaks()
        {
            //Short lines are what the 220-character tail got wrong: eight of them are far under 220 characters
            string note = "one\ntwo\nthree\nfour\nfive\nsix\nseven\neight" + CARET;

            string[] shown = FieldTail.Show(note, 37f, 6, Monospace).Split('\n');

            Assert.Equal(6, shown.Length);
            Assert.Equal("eight" + CARET, shown[^1]);
            Assert.StartsWith(FieldTail.ELLIPSIS, shown[0]);
        }

        [Fact]
        public void A_note_that_fits_is_shown_as_it_is()
        {
            const string note = "Lovely level" + CARET;
            Assert.Equal(note, FieldTail.Show(note, 37f, 6, Monospace));
        }

        [Fact]
        public void A_space_just_typed_moves_the_caret_even_at_a_break()
        {
            Assert.Equal("Hello " + CARET, FieldTail.Show("Hello " + CARET, 37f, 6, Monospace));

            //"aaaa bbbb" fills a line of 9; the space typed after it is the break, and the caret opens the next line
            string[] shown = FieldTail.Show("aaaa bbbb " + CARET, 9f, 6, Monospace).Split('\n');
            Assert.Equal(new[] { "aaaa bbbb", CARET }, shown);
        }

        [Fact]
        public void A_word_wider_than_the_line_breaks_between_characters_and_loses_nothing()
        {
            string word = new string('x', 95) + CARET;

            string[] shown = FieldTail.Show(word, 10f, 20, Monospace).Split('\n');

            Assert.All(shown, line => Assert.True(line.Length <= 10));
            Assert.Equal(word, string.Concat(shown));
        }

        [Fact]
        public void The_ellipsis_line_still_fits()
        {
            string note = string.Join(" ", Enumerable.Repeat("abcdefgh", 30)) + CARET;

            string[] shown = FieldTail.Show(note, 9f, 4, Monospace).Split('\n');

            Assert.Equal(4, shown.Length);
            Assert.StartsWith(FieldTail.ELLIPSIS, shown[0]);
            Assert.True(shown[0].Length <= 9);
        }
    }
}

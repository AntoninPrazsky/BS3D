using BS3D.Screens;
using System;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The paragraph breaker (#769, #806): lines break at spaces only, so the score server's address and a version number
    /// stay whole, and every line fits; the breaks are chosen for the paragraph's shape, so no last line is one word where
    /// two fit, no line starts with a dash or an operator, no line ends on a one-letter word, and a tie (U+00A0) holds.
    /// <c>SpaceWrap</c> is compiled in from the Game; the measure is a stand-in of one unit a character, which is all the
    /// rules need (the page hands it the face the label draws in).
    /// <para>
    /// Five of them failed on the greedy breaker this replaced (seen, #806): it ended <see cref="PLAIN"/> on one word at
    /// 7 of the 45 widths swept and left the quick brown fox's "dog" alone, opened lines with a dash or an operator, ended
    /// them on "a", and drew a tie as U+00A0.
    /// </para>
    /// </summary>
    public class SpaceWrapTests
    {
        private static float Monospace(string line) => line.Length;

        private const string SENTENCE = "With online scores on, each cleared level sends your nickname, the level, its score "
            + "and the game's version to scores.winphonew.eu, which adds only when it arrived. Remove scores deletes it all.";

        //No one-letter word, dash or operator in it, so no tie: a greedy fill of its words is the line count to keep
        private const string PLAIN = "Every shot brings the glass nearer, whether it matched anything or not, which is the "
            + "other reason to miss nothing. Clearing from the bottom buys height; clearing from the top does not.";

        [Theory]
        [InlineData(20f)]
        [InlineData(31f)]
        [InlineData(44f)]
        [InlineData(57f)]
        public void Every_line_fits_and_no_word_is_split(float width)
        {
            string wrapped = SpaceWrap.Wrap(SENTENCE, width, Monospace);

            foreach (string line in wrapped.Split('\n'))
            {
                Assert.True(line.Length <= width, $"'{line}' is wider than {width}");
                Assert.False(line.StartsWith(' ') || line.EndsWith(' '), $"'{line}' carries a space at an end");
            }

            //Nothing lost, nothing added: the lines joined by spaces are the sentence
            Assert.Equal(SENTENCE, wrapped.Replace('\n', ' '));
            Assert.Contains("scores.winphonew.eu,", wrapped.Split('\n', ' '));
        }

        [Fact]
        public void A_break_falls_before_the_address_rather_than_inside_it()
        {
            //"to scores.winphonew." is 20 characters: the label's own wrap would end the line there, at the dot. "which" is
            //alone at the end because nothing else fits beside the address
            string wrapped = SpaceWrap.Wrap("its version to scores.winphonew.eu, which", 20f, Monospace);

            Assert.Equal("its version to\nscores.winphonew.eu,\nwhich", wrapped);
        }

        [Fact]
        public void A_word_wider_than_the_line_stands_on_a_line_of_its_own()
        {
            string wrapped = SpaceWrap.Wrap("to scores.winphonew.eu now", 10f, Monospace);

            Assert.Equal("to\nscores.winphonew.eu\nnow", wrapped);
        }

        [Fact]
        public void A_line_break_in_the_text_is_kept()
        {
            string wrapped = SpaceWrap.Wrap("one two\nthree four five", 9f, Monospace);

            Assert.Equal("one two\nthree\nfour five", wrapped);
        }

        [Fact]
        public void A_line_that_fits_is_left_alone()
        {
            Assert.Equal("MonoGame 3.8.5", SpaceWrap.Wrap("MonoGame 3.8.5", 40f, Monospace));
        }

        [Fact]
        public void The_last_word_is_not_left_alone_when_the_lines_can_share_it()
        {
            //Greedy fills "The quick brown fox" and "jumps over the lazy" to 19 of 20 and leaves "dog" alone. Three lines
            //either way; the even setting gives the last line two words and the first two lines a near-equal edge
            string wrapped = SpaceWrap.Wrap("The quick brown fox jumps over the lazy dog", 20f, Monospace);

            Assert.Equal("The quick brown\nfox jumps over the\nlazy dog", wrapped);
        }

        [Fact]
        public void No_paragraph_ends_on_one_word_and_none_takes_a_line_more_than_a_greedy_fill()
        {
            int longest = PLAIN.Split(' ').Max(word => word.Length);

            //From two of the longest words a line upwards, where a last line of two words always exists
            for (int width = 2 * longest + 1; width <= 2 * longest + 45; width++)
            {
                string[] lines = SpaceWrap.Wrap(PLAIN, width, Monospace).Split('\n');

                Assert.Equal(GreedyLineCount(PLAIN, width), lines.Length);
                Assert.All(lines, line => Assert.True(line.Length <= width, $"'{line}' is wider than {width}"));
                if (lines.Length > 1)
                    Assert.True(lines[^1].Contains(' '), $"at {width} the paragraph ends on one word: '{lines[^1]}'");
                Assert.Equal(PLAIN, string.Join(" ", lines));
            }
        }

        [Fact]
        public void A_tie_holds_a_name_to_its_number_and_is_drawn_as_a_space()
        {
            const string TIED = "Built on .NET\u00A010, MonoGame\u00A03.8.5 (DirectX\u00A011) and BepuPhysics\u00A02.5, "
                + "with Myra for the interface and FontStashSharp for the text.";

            for (int width = 16; width <= 70; width++)
            {
                string wrapped = SpaceWrap.Wrap(TIED, width, Monospace);

                Assert.DoesNotContain(SpaceWrap.TIE, wrapped);
                Assert.Equal(TIED.Replace(SpaceWrap.TIE, ' '), wrapped.Replace('\n', ' '));

                foreach (string tied in new[] { ".NET 10,", "MonoGame 3.8.5", "(DirectX 11)", "BepuPhysics 2.5," })
                    Assert.True(wrapped.Split('\n').Any(line => line.Contains(tied)), $"at {width} '{tied}' was parted:\n{wrapped}");
            }
        }

        [Fact]
        public void No_line_starts_with_a_dash_or_an_operator()
        {
            const string SUM = "Shot 3 cuts an anchor — 3 matched, 11 orphaned — at the cap: (3×10 + 11×20) × 4 = 1000. "
                + "Three balls matched — and the shot is worth more than the first two together.";

            for (int width = 14; width <= 60; width++)
            {
                foreach (string line in SpaceWrap.Wrap(SUM, width, Monospace).Split('\n'))
                {
                    Assert.True(line.Length <= width, $"'{line}' is wider than {width}");
                    Assert.False(line.Length > 0 && "—–+=×÷−".Contains(line[0]), $"at {width} a line starts with '{line[0]}': '{line}'");
                }
            }
        }

        [Fact]
        public void No_line_ends_on_a_one_letter_word()
        {
            const string LETTERS = "One press a level lifts the glass a step back up, and a second press does nothing until "
                + "it has stepped down again, so I keep it for a bad moment.";

            for (int width = 14; width <= 60; width++)
            {
                foreach (string line in SpaceWrap.Wrap(LETTERS, width, Monospace).Split('\n'))
                {
                    string last = line.Split(' ')[^1];
                    Assert.False(last is "a" or "A" or "I", $"at {width} a line ends on '{last}': '{line}'");
                }
            }
        }

        //How many lines a fill that takes every word that fits takes - the line count the breaker must not exceed
        private static int GreedyLineCount(string text, int width)
        {
            int lines = 1, length = 0;

            foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                int next = length == 0 ? word.Length : length + 1 + word.Length;
                if (length > 0 && next > width)
                {
                    lines++;
                    length = word.Length;
                }
                else length = next;
            }

            return lines;
        }
    }
}

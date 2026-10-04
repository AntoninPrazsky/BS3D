using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The privacy note's wrap (#769): lines break at spaces only, so the score server's address and a version number
    /// stay whole, and every line fits. <c>SpaceWrap</c> is compiled in from the Game; the measure is a stand-in of one
    /// unit a character, which is all the rule needs (the page hands it the face the label draws in).
    /// </summary>
    public class SpaceWrapTests
    {
        private static float Monospace(string line) => line.Length;

        private const string SENTENCE = "With online scores on, each cleared level sends your nickname, the level, its score "
            + "and the game's version to scores.winphonew.eu, which adds only when it arrived. Remove scores deletes it all.";

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
            //"to scores.winphonew." is 20 characters: the label's own wrap would end the line there, at the dot
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
    }
}

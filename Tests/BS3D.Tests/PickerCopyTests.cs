using BS3D.Screens;
using Prazsky.BS3D.Levels;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What the level picker and the online boards say (#806): every line <c>PickerCopy</c> writes, over every shipped
    /// level's name and the figures a campaign reaches, is held to the typesetting rules (<c>CopyRules</c>). Before #806 the
    /// chapter's readout, the boards' "You  #6 of 21" and High Scores' "#1  Kuba" each set two spaces where one goes, and the
    /// detail line quoted the next level's name in straight quotes ('Moon').
    /// </summary>
    public class PickerCopyTests
    {
        private static LevelSet Set() => LevelSet.Load(Path.Combine(Shipped.LevelsDirectory, LevelSet.DefaultFileName));

        private static IEnumerable<string> Lines(LevelSet set)
        {
            int count = set.Count;

            for (int i = 0; i < count; i++)
            {
                string name = set.DisplayName(i);
                string next = set.DisplayName((i + 1) % count);

                yield return PickerCopy.DetailOpen(name, set.DescribeRules(i));
                yield return PickerCopy.DetailSequence(name, i + 2, next);
                yield return PickerCopy.DetailPrice(name, 2 * i + 1, i);
                yield return PickerCopy.BoardsHeading(i + 1, name);
                yield return PickerCopy.BoardButton(name);
                yield return PickerCopy.TileLock(true, i + 1, 0);
                yield return PickerCopy.TileLock(false, 0, 2 * i);
            }

            foreach (bool anyOpen in new[] { true, false })
                foreach (bool beyondReach in new[] { true, false })
                    yield return PickerCopy.ChapterLine(3, 14, anyOpen, 7, 10, 22, 40, beyondReach, 20, 58);

            yield return PickerCopy.StarsCollected(306);
            yield return PickerCopy.TILE_SKIPPED;
            yield return PickerCopy.BoardButton(null);

            foreach (bool unfinished in new[] { true, false })
            {
                yield return PickerCopy.YouLine(6, 21, unfinished);
                yield return PickerCopy.Leader("Tereza", unfinished);
                foreach (int rank in new[] { 1, 2, 3, 4, 11, 12, 13, 21, 22, 101, 111 })
                    yield return PickerCopy.Place(rank, 130, unfinished);
            }

            yield return PickerCopy.YouLine(0, 21, false);
            yield return PickerCopy.YouLine(0, 0, false);
        }

        [Fact]
        public void Every_line_the_picker_and_the_boards_write_is_set_by_the_rules()
        {
            int checkedLines = 0;

            foreach (string line in Lines(Set()))
            {
                Assert.Null(CopyRules.Fault(line) is { } fault ? $"'{line}': {fault}" : null);
                checkedLines++;
            }

            //A set that came back empty would pass the loop above
            Assert.True(checkedLines > 500, $"only {checkedLines} lines were checked");
        }

        [Fact]
        public void The_readout_parts_are_parted_by_one_spaced_dot() =>
            Assert.Equal("Chapter 2 of 14 · 3 of 10 cleared · ★ 4 of 40",
                PickerCopy.ChapterLine(2, 14, true, 3, 10, 4, 40, false, 0, 0));

        [Fact]
        public void A_quoted_name_takes_curly_quotes() =>
            Assert.Equal("Meerkat — one level at a time; next is 15 ‘Moon’", PickerCopy.DetailSequence("Meerkat", 15, "Moon"));

        [Theory]
        [InlineData(1, "1st")]
        [InlineData(2, "2nd")]
        [InlineData(3, "3rd")]
        [InlineData(4, "4th")]
        [InlineData(11, "11th")]
        [InlineData(12, "12th")]
        [InlineData(13, "13th")]
        [InlineData(21, "21st")]
        [InlineData(112, "112th")]
        public void Ordinals_are_english(int n, string expected) => Assert.Equal(expected, PickerCopy.Ordinal(n));
    }
}

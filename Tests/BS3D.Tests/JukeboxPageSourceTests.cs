using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The Jukebox does not rebuild its page when a chapter is picked (#901). A rebuild (<c>InvalidateTree</c> with
    /// <c>BS3DGame.RebuildPage</c>) builds the chapters' scroller anew, and a new scroller starts at the top: the player who
    /// scrolled down for a chapter found the list thrown back to the top for the next one. The recordings' column is
    /// refilled in place instead, and nothing the page does makes its tree again after it is built.
    /// <para>
    /// Held by reading the source, as <see cref="ProseLabelsTests"/> holds the prose labels: the page is a Myra tree no test
    /// can construct, so what a pick does to a scroller is seen in a Game run (the issue's evidence); what is held here is
    /// the one call that makes the jump possible. Seen to fail on the page as it was before the fix, which called both.
    /// </para>
    /// </summary>
    public class JukeboxPageSourceTests
    {
        [Fact]
        public void A_chapter_pick_does_not_rebuild_the_page()
        {
            string code = WithoutComments(File.ReadAllText(JukeboxPagePath()));

            Assert.DoesNotContain("InvalidateTree", code);
            Assert.DoesNotContain("RebuildPage", code);

            //The refill is there: a regex that stopped matching would otherwise pass by finding nothing
            Assert.Contains("ShowChapter(", code);
            Assert.Contains("FillTracks()", code);
        }

        /// <summary>
        /// The now-playing line (#900) stands between the visualizer and the player's buttons, and is cut to the column's
        /// width with an ellipsis. Above the recordings' list, where it stood first, it read as that list's heading rather
        /// than as the player's own caption; and a label measured at its text's extent would widen the whole column for a
        /// long chapter name beside a long recording's title.
        /// <para>
        /// Held by reading the source, as above: the order the widgets are added in is the order they stand in, and the
        /// bound is two properties on the label. How it looks is seen in a Game run. Seen to fail on the page as it was
        /// before the change, which added the line first and bounded nothing.
        /// </para>
        /// </summary>
        [Fact]
        public void The_now_playing_line_stands_between_the_visualizer_and_the_buttons_and_is_cut_to_the_column()
        {
            string code = WithoutComments(File.ReadAllText(JukeboxPagePath()));

            int build = code.IndexOf("private Widget BuildPlayer()", StringComparison.Ordinal);
            int end = code.IndexOf("private VerticalStackPanel SubColumn()", StringComparison.Ordinal);
            Assert.True(build >= 0 && end > build, "BuildPlayer was not found; update this test with the page.");
            string player = code.Substring(build, end - build);

            int scroller = player.IndexOf("right.Widgets.Add(_trackScroll)", StringComparison.Ordinal);
            int visualizer = player.IndexOf("right.Widgets.Add(new MusicVisualizer(", StringComparison.Ordinal);
            int line = player.IndexOf("right.Widgets.Add(_nowPlaying)", StringComparison.Ordinal);
            int controls = player.IndexOf("right.Widgets.Add(controls)", StringComparison.Ordinal);

            //Every one is found, or an ordering of -1s would pass
            Assert.True(scroller >= 0 && visualizer >= 0 && line >= 0 && controls >= 0, "A widget add was not found; update this test with the page.");
            Assert.True(scroller < visualizer, "The recordings' list stands above the visualizer.");
            Assert.True(visualizer < line, "The now-playing line stands below the visualizer.");
            Assert.True(line < controls, "The now-playing line stands above the player's buttons.");

            int label = player.IndexOf("_nowPlaying = new Label", StringComparison.Ordinal);
            Assert.True(label >= 0, "The label's initializer was not found; update this test with the page.");
            string initializer = player.Substring(label, player.IndexOf("};", label, StringComparison.Ordinal) - label);

            Assert.Contains("Width = ColumnWidth", initializer);
            Assert.Contains("AutoEllipsisMethod.Character", initializer);
        }

        /// <summary>
        /// The chapter that holds the recording playing carries the play mark whichever chapter is shown (#901: "the
        /// currently playing one should stay visibly selected"). Brightness marks the chapter shown, so once the player
        /// picked the next chapter to look through, nothing said where the music was: the mark is written from the chapter
        /// the player's index falls in, never from the shown one.
        /// <para>
        /// Held by reading the source, as above; how it looks is seen in a Game run. Seen to fail on the page as it was
        /// before the change, which had no mark.
        /// </para>
        /// </summary>
        [Fact]
        public void The_chapter_playing_is_marked_whichever_chapter_is_shown()
        {
            string code = WithoutComments(File.ReadAllText(JukeboxPagePath()));

            int show = code.IndexOf("private void ShowPlayer()", StringComparison.Ordinal);
            Assert.True(show >= 0, "ShowPlayer was not found; update this test with the page.");
            string body = code.Substring(show);

            Assert.Contains("playing = g;", body);
            Assert.Matches(@"_groupMarks\[g\]\.Visible = g == playing", body);
            Assert.DoesNotMatch(@"_groupMarks\[g\]\.Visible = g == _group", body);

            //Built on every chapter's slab, at the edge the pad's focus prompt leaves free
            Assert.Contains("_groupMarks.Add(mark)", code);
            Assert.Contains("mark.HorizontalAlignment = HorizontalAlignment.Left", code);
        }

        private static string WithoutComments(string source) => Regex.Replace(source, @"//[^\r\n]*", string.Empty);

        private static string JukeboxPagePath()
        {
            string relative = Path.Combine("Game", "Screens", "JukeboxPage.cs");

            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}

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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// <b>Every page put into the desktop is fitted first</b> (#1052, #1139). On an enlarged interface (a phone) a page taller
    /// than the screen has its long part put in a scroller, or is laid out smaller, by <c>BS3DGame.FitPage</c>; and that work
    /// is done to the page's tree. A tree rebuilt where it stands (About's turn, through <c>BS3DGame.RebuildPage</c>) is a
    /// new one with the long part unscrolled, while the page's <c>LayoutFit</c> survives, so a rebuild that did not fit again
    /// put the next page up at the menus' size, taller than the screen, with Back under its bottom edge.
    /// <para>
    /// Held by reading the source, as <see cref="JukeboxPageSourceTests"/> holds the Jukebox: the pages are Myra trees on the
    /// game's device and no test can construct one. What a turn does to the page on a phone's screen is seen in a Game run
    /// (<c>inches=2.8 about menucursor=…:0:rb</c>, the issue's evidence); what is held here is that no way of putting a
    /// page's tree into the desktop skips the fit. A check is evidence only once its failing branch has been seen to fire
    /// (BestPractices section 10), so the rule is also run over the text #1139 replaced.
    /// </para>
    /// </summary>
    public class PageFitSourceTests
    {
        //A tree put into the shared desktop, and what is put there
        private static readonly Regex RootPut = new(@"_desktop\.Root\s*=\s*(?<value>[^;=][^;]*);", RegexOptions.Compiled);

        //A member's header on a line of its own, its body opening on the next: where the method round an assignment starts
        private static readonly Regex MemberHeader = new(
            @"^[ \t]*(?:(?:internal|private|public|protected|static|override|virtual|sealed)\s+)+[\w<>\[\],.? ]+\s+\w+\s*\([^)]*\)\s*\r?\n[ \t]*\{",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>The method as #1139 found it: the rebuilt tree put up with no fit.</summary>
        private const string REBUILD_UNFITTED = @"
        internal void RebuildPage(MenuPage page, Func<Widget> keepFocusOn)
        {
            page.Refresh();
            _desktop.Root = page.Root;
            RefreshNavEntries(keepFocusOn());
        }";

        [Fact]
        public void Every_page_put_into_the_desktop_is_fitted_first()
        {
            int puts = 0;
            List<string> faults = new();

            foreach (string path in GameSources())
            {
                string code = WithoutComments(File.ReadAllText(path));
                puts += RootPut.Matches(code).Count;
                faults.AddRange(Unfitted(code).Select(fault => $"{Path.GetFileName(path)}: {fault}"));
            }

            //ShowPage's and RebuildPage's: a sweep that found none would pass by reading nothing
            Assert.True(puts >= 2, $"only {puts} assignments of the desktop's root were found in the Game");
            Assert.True(faults.Count == 0, string.Join(Environment.NewLine, faults));
        }

        [Fact]
        public void The_rule_fires_on_the_text_it_replaced()
        {
            //The rebuild as it was, and a fit that comes too late or fits another page
            Assert.Single(Unfitted(REBUILD_UNFITTED));
            Assert.Single(Unfitted(REBUILD_UNFITTED.Replace("RefreshNavEntries(", "FitPage(page);\n            RefreshNavEntries(")));
            Assert.Single(Unfitted(REBUILD_UNFITTED.Replace("page.Refresh();", "FitPage(_mainMenuPage);\n            page.Refresh();")));

            //A fit in the method before this one is not this one's
            Assert.Single(Unfitted(@"
        internal void ShowPage(MenuPage page)
        {
            FitPage(page);
            _desktop.Root = page.Root;
        }
" + REBUILD_UNFITTED));

            //Anything but a page's own tree is refused too, so a widget put up some other way cannot pass unread
            Assert.Single(Unfitted(REBUILD_UNFITTED.Replace("page.Root;", "BuildSomething();")));

            //And what it must let through: the rebuild as it stands, a desktop emptied
            Assert.Empty(Unfitted(REBUILD_UNFITTED.Replace("page.Refresh();", "FitPage(page);\n            page.Refresh();")));
            Assert.Empty(Unfitted(REBUILD_UNFITTED.Replace("page.Root;", "null;")));
        }

        /// <summary>
        /// The desktop's root assignments in <paramref name="code"/> that put up anything but a page's tree fitted earlier in
        /// the same method, one line each.
        /// </summary>
        private static List<string> Unfitted(string code)
        {
            List<string> faults = new();

            foreach (Match put in RootPut.Matches(code))
            {
                string value = put.Groups["value"].Value.Trim();
                if (value == "null") continue;

                int line = code.AsSpan(0, put.Index).Count('\n') + 1;
                Match page = Regex.Match(value, @"^(?<page>\w+)\.Root$");
                if (!page.Success)
                {
                    faults.Add($"line {line} puts '{value}' into the desktop, not a page's Root fitted by FitPage");
                    continue;
                }

                Match header = MemberHeader.Matches(code[..put.Index]).LastOrDefault();
                if (header == null)
                {
                    faults.Add($"line {line}: the method round the assignment was not found; update this test with the code");
                    continue;
                }

                string before = code[(header.Index + header.Length)..put.Index];
                string name = Regex.Escape(page.Groups["page"].Value);
                if (!Regex.IsMatch(before, $@"\bFitPage\(\s*{name}\s*\)"))
                    faults.Add($"line {line} puts {value} into the desktop with no FitPage({page.Groups["page"].Value}) before it in its method");
            }

            return faults;
        }

        private static string WithoutComments(string source) => Regex.Replace(source, @"//[^\r\n]*", string.Empty);

        private static string[] GameSources()
        {
            string game = FindUnderRepository("Game");
            string[] sources = Directory.GetFiles(game, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .ToArray();

            //The file the two puts live in is among those read, or the sweep could pass by reading nothing
            Assert.Contains(sources, path => path.EndsWith("BS3DGame.Menu.cs", StringComparison.Ordinal));
            return sources;
        }

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}

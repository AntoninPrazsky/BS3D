using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// <b>Every page put into the desktop is fitted first, and the tree fitted is the tree put up</b> (#1052, #1139). On an
    /// enlarged interface (a phone) a page taller than the screen has its long part put in a scroller, or is laid out
    /// smaller, by <c>BS3DGame.FitPage</c>; and that work is done to the page's tree. A tree rebuilt where it stands (About's
    /// turn, through <c>BS3DGame.RebuildPage</c>) is a new one with the long part unscrolled, so a rebuild that did not fit
    /// again put the next page up at the menus' size, taller than the screen, with Back under its bottom edge. And a page's
    /// <c>Refresh</c> may throw a stale tree away (the picker's, built for another width or online switch), so a fit that
    /// read the tree before refreshing would measure the tree thrown away and leave the one put up unfitted.
    /// <para>
    /// Held by reading the source, as <see cref="JukeboxPageSourceTests"/> holds the Jukebox: the pages are Myra trees on the
    /// game's device and no test can construct one. What a turn does to the page on a phone's screen is seen in a Game run
    /// (<c>inches=2.8 about menucursor=…:0:rb</c>, the issue's evidence); what is held here is that every put of a page's
    /// tree into the desktop has a fit of that page before it in the body it stands in (a method's, a local function's, a
    /// lambda's, never one before it), and that the fit refreshes the page before it reads the tree. A check is evidence
    /// only once its failing branch has been seen to fire (BestPractices section 10), so each rule is also run over the
    /// text it replaced.
    /// </para>
    /// </summary>
    public class PageFitSourceTests
    {
        //A tree put into the shared desktop, and what is put there
        private static readonly Regex RootPut = new(@"_desktop\.Root\s*=\s*(?<value>[^;=][^;]*);", RegexOptions.Compiled);

        //What stands before a block's brace when the block is a body of its own: a method's, a constructor's or a local
        //function's (a name and its parameters, neither a statement's keyword nor an object built with an initializer), an
        //accessor's, a lambda's
        private static readonly Regex BodyHead = new(
            @"(?:=>|\b(?:get|set|init|add|remove)|(?<!\bnew\s+)\b(?!(?:if|for|foreach|while|using|lock|switch|catch|fixed|when|return|new)\b)\w+\s*(?:<[^()]*>)?\s*\((?:[^()]|\([^()]*\))*\)(?:\s*where\b[^;{}]*)?)\s*$",
            RegexOptions.Compiled);

        //What stands before a type's or a namespace's brace: a walk out of a put that reaches one found no body round it
        private static readonly Regex TypeHead = new(@"\b(?:class|struct|interface|record|enum|namespace)\s+[\w.]+", RegexOptions.Compiled);

        //What ends the text before a block's brace that belongs to the block
        private static readonly char[] HEAD_EDGES = { ';', '{', '}' };

        /// <summary>The method as #1139 found it: the rebuilt tree put up with no fit.</summary>
        private const string REBUILD_UNFITTED = @"
        internal void RebuildPage(MenuPage page, Func<Widget> keepFocusOn)
        {
            page.Refresh();
            _desktop.Root = page.Root;
            RefreshNavEntries(keepFocusOn());
        }";

        //ShowPage as it stands: the fit first, then the put
        private const string SHOW_PAGE = @"
        internal void ShowPage(MenuPage page)
        {
            FitPage(page);
            _desktop.Root = page.Root;
        }
";

        /// <summary>FitPage's measuring pass as it was before #1139's review: the tree read, then the page refreshed.</summary>
        private const string FIT_READ_FIRST = @"
                Widget root = page.Root;
                page.Refresh();

                Point need = root.Measure(new Point(viewport.Width, viewport.Height * 4));";

        [Fact]
        public void Every_page_put_into_the_desktop_is_fitted_first()
        {
            int puts = 0;
            List<string> faults = new();

            foreach (string path in GameSources())
            {
                string source = File.ReadAllText(path);
                puts += RootPut.Matches(Prepared(source)).Count;
                faults.AddRange(Unfitted(source).Select(fault => $"{Path.GetFileName(path)}: {fault}"));
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

            //A fit in the method before this one is not this one's, whatever this one's header looks like: one with no
            //modifier, or no block, is read from its own start too
            Assert.Single(Unfitted(SHOW_PAGE + REBUILD_UNFITTED));
            Assert.Single(Unfitted(InClass(SHOW_PAGE + @"
        internal void PutPage(MenuPage page) => _desktop.Root = page.Root;
")));
            Assert.Single(Unfitted(InClass(SHOW_PAGE + @"
        void PutPage(MenuPage page)
        {
            _desktop.Root = page.Root;
        }
")));

            //A lambda's body is its own too: the fit round it is made when the lambda is, not when it runs
            Assert.Single(Unfitted(InClass(@"
        internal void ShowLater(MenuPage page)
        {
            FitPage(page);
            _later = () =>
            {
                _desktop.Root = page.Root;
            };
        }
")));

            //Anything but a page's own tree is refused too, so a widget put up some other way cannot pass unread
            Assert.Single(Unfitted(REBUILD_UNFITTED.Replace("page.Root;", "BuildSomething();")));

            //And what it must let through: the rebuild as it stands, a desktop emptied
            Assert.Empty(Unfitted(REBUILD_UNFITTED.Replace("page.Refresh();", "FitPage(page);\n            page.Refresh();")));
            Assert.Empty(Unfitted(REBUILD_UNFITTED.Replace("page.Root;", "null;")));

            //And a put inside a statement's block, after an object built and strings with lone braces, in the fitted method
            Assert.Empty(Unfitted(InClass(@"
        internal void ShowPage(MenuPage page)
        {
            FitPage(page);
            Label label = new Label(""{"")
            {
                Text = $""{page.Name}}}"",
            };
            if (page != null)
            {
                _desktop.Root = page.Root;
            }
        }
")));
        }

        /// <summary>
        /// <c>FitPage</c> refreshes the page before it reads the tree it measures. A <c>Refresh</c> that finds its tree built
        /// for another width or online switch throws it away (the picker's, the benchmark page's): read first, the tree
        /// measured and let out is the one thrown away, and the one put up is built afresh after the fit, unfitted, the
        /// picker's chapter scroller bound at its reserve.
        /// </summary>
        [Fact]
        public void The_fit_refreshes_the_page_before_it_reads_the_tree()
        {
            string fit = BodyOf(Prepared(File.ReadAllText(Path.Combine(FindUnderRepository("Game"), "BS3DGame.Menu.cs"))), "FitPage");
            Assert.True(RefreshesBeforeReading(fit), "FitPage reads page.Root before it calls page.Refresh(), or does not do both");

            //Seen to fire on the order it replaced, and on a fit with no Refresh at all
            Assert.False(RefreshesBeforeReading(FIT_READ_FIRST));
            Assert.False(RefreshesBeforeReading(FIT_READ_FIRST.Replace("page.Refresh();", string.Empty)));
        }

        //Whether the first page.Refresh() comes before the first read of page.Root, both there
        private static bool RefreshesBeforeReading(string body)
        {
            int refresh = body.IndexOf("page.Refresh()", StringComparison.Ordinal);
            Match read = Regex.Match(body, @"\bpage\.Root\b");
            return refresh >= 0 && read.Success && refresh < read.Index;
        }

        //Members set in a class, as they stand in a source file
        private static string InClass(string members) =>
            "namespace BS3D\n{\n    public partial class BS3DGame : Game\n    {" + members + "    }\n}\n";

        /// <summary>
        /// The desktop's root assignments in <paramref name="source"/> that put up anything but a page's tree fitted earlier
        /// in the body the assignment stands in, one line each.
        /// </summary>
        private static List<string> Unfitted(string source)
        {
            string code = Prepared(source);
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

                int body = BodyStart(code, put.Index);
                if (body < 0)
                {
                    faults.Add($"line {line} puts {value} into the desktop outside a block body (an expression body?), where no "
                        + "FitPage can come before it");
                    continue;
                }

                string before = code[body..put.Index];
                string name = Regex.Escape(page.Groups["page"].Value);
                if (!Regex.IsMatch(before, $@"\bFitPage\(\s*{name}\s*\)"))
                    faults.Add($"line {line} puts {value} into the desktop with no FitPage({page.Groups["page"].Value}) before it in its body");
            }

            return faults;
        }

        /// <summary>
        /// Where the body <paramref name="at"/> stands in begins, just past its brace: walking out through the blocks round
        /// it, the first whose head is a body's (<see cref="BodyHead"/>), so a statement's block is walked through and a
        /// method before it is never reached. -1 when the walk meets a type's block or the text's start first: the put
        /// stands in an expression body or an initializer.
        /// </summary>
        private static int BodyStart(string code, int at)
        {
            int depth = 0;
            for (int i = at - 1; i >= 0; i--)
            {
                if (code[i] == '}') depth++;
                else if (code[i] == '{' && depth > 0) depth--;
                else if (code[i] == '{')
                {
                    int from = i == 0 ? 0 : code.LastIndexOfAny(HEAD_EDGES, i - 1) + 1;
                    string head = code[from..i];
                    if (TypeHead.IsMatch(head)) return -1;
                    if (BodyHead.IsMatch(head)) return i + 1;
                }
            }

            return -1;
        }

        /// <summary>The text inside the braces of the method <paramref name="method"/> declares.</summary>
        private static string BodyOf(string code, string method)
        {
            Match header = Regex.Match(code, $@"\b{method}\([^)]*\)\s*\{{");
            Assert.True(header.Success, $"{method} was not found; update this test with the code");

            int start = header.Index + header.Length, depth = 1, i = start;
            for (; i < code.Length && depth > 0; i++)
                depth += code[i] == '{' ? 1 : code[i] == '}' ? -1 : 0;
            return code[start..(i - 1)];
        }

        /// <summary>
        /// The source with what cannot be code emptied, its lines kept: character and string literals first (a brace in one
        /// is no block's, a "//" in an address is no comment), then the comments.
        /// </summary>
        private static string Prepared(string source)
        {
            string code = Regex.Replace(source, @"'(?:\\.|[^'\\\r\n])'", "' '");
            code = Regex.Replace(code, @"""(?:\\.|[^""\\\r\n])*""", "\"\"");
            return Regex.Replace(code, @"//[^\r\n]*", string.Empty);
        }

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

using BS3D.Screens;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What the About page says (#807): its copy set by the typesetting rules (#806), and every credit the page carried
    /// before it was split still on it — a shorter page was the point, and a library, a licence or a tool quietly dropped
    /// on the way would be the one thing a reader of the old page could not see was gone. Read off
    /// <c>AboutCopy.Pages</c>, which is what <c>AboutPage</c> draws block by block (and throws on a block it cannot set),
    /// so a credit list, the privacy line or the player taken off a page is taken out of what these tests read.
    /// </summary>
    public class AboutCopyTests
    {
        //The game's title as the page inserts it (BS3DGame.GAME_TITLE, which this project does not compile)
        private const string TITLE = "Bubble Shooter 3D";

        /// <summary>
        /// Every credit the two-column page of #463 named, as it named them — the libraries with their versions, the
        /// typefaces and their licence, the model that wrote the code and the tools that generated the rest.
        /// </summary>
        public static TheoryData<string> Credits => new()
        {
            ".NET 10", "MonoGame 3.8.5", "DirectX 11", "BepuPhysics 2.5", "Myra", "FontStashSharp", "NVorbis",
            "Anton", "Inter", "PromptFont", "SIL OFL 1.1",
            "Claude", "Anthropic", "Claude Code", "ACE-Step 1.5", "Z-Image-Turbo", "RealESRGAN", "Gemma", "nomic-embed",
        };

        [Fact]
        public void Every_string_is_set_by_the_rules()
        {
            int checkedStrings = 0;

            foreach (string text in AboutCopy.Copy(TITLE))
            {
                Assert.Null(CopyRules.Fault(text) is { } fault ? $"'{text}': {fault}" : null);
                checkedStrings++;
            }

            //Four names, four blocks of prose and two credit lists: an accessor that came back short would pass the loop
            Assert.True(checkedStrings >= 8 + 2 * (AboutCopy.LIBRARIES.Length + AboutCopy.GENERATED.Length),
                $"only {checkedStrings} strings came back from AboutCopy.Copy");
        }

        [Theory]
        [MemberData(nameof(Credits))]
        public void No_credit_is_dropped(string credit)
        {
            //A tie is drawn as a space, so the page says "MonoGame 3.8.5" whichever of the two holds it together
            string page = string.Join("\n", AboutCopy.Copy(TITLE)).Replace(SpaceWrap.TIE, ' ');

            Assert.Contains(credit, page);
        }

        [Fact]
        public void A_version_is_tied_to_its_name()
        {
            //"Typesetting" in docs/game-shell/menu-look.md: a name keeps its version on its line. A credit list's name never
            //breaks today, but a role does, and the licence sits in one
            string[] tied = { "MonoGame\u00A03.8.5", "DirectX\u00A011", "BepuPhysics\u00A02.5", "SIL\u00A0OFL\u00A01.1", "ACE-Step\u00A01.5", ".NET\u00A010" };
            string page = string.Join("\n", AboutCopy.Copy(TITLE));

            foreach (string name in tied) Assert.Contains(name, page);
        }

        [Fact]
        public void Every_page_has_a_name()
        {
            AboutCopy.Page[] pages = AboutCopy.Pages(TITLE);

            Assert.Equal(4, pages.Length);
            Assert.All(pages, page => Assert.False(string.IsNullOrWhiteSpace(page.Title)));
            Assert.Equal(pages.Length, pages.Select(page => page.Title).Distinct().Count());
        }

        [Fact]
        public void The_player_is_the_last_page()
        {
            //The owner, 2026-10-12: the player is a small easter egg, so it waits at the end for whoever turns that far, and
            //About opens on what the game is
            AboutCopy.Page[] pages = AboutCopy.Pages(TITLE);

            Assert.Contains(pages[^1].Blocks, block => block.Kind == AboutCopy.BlockKind.Player);
            Assert.Single(Blocks(AboutCopy.BlockKind.Player));
            Assert.Contains(pages[0].Blocks, block => block.Text == AboutCopy.Game(TITLE));
        }

        [Fact]
        public void The_privacy_line_stands_under_what_the_game_is()
        {
            //Not copy of the page's (OnlineScores.PrivacySentence, the Settings page's own), so the copy cannot hold it: the
            //page holds a block that draws it, once, after the paragraph that says what the game is
            AboutCopy.Page page = Assert.Single(AboutCopy.Pages(TITLE),
                candidate => candidate.Blocks.Any(block => block.Kind == AboutCopy.BlockKind.Privacy));

            int privacy = System.Array.FindIndex(page.Blocks, block => block.Kind == AboutCopy.BlockKind.Privacy);
            int game = System.Array.FindIndex(page.Blocks, block => block.Text == AboutCopy.Game(TITLE));

            Assert.True(game >= 0 && game < privacy, "the privacy line must stand under what the game is, on its page");
            Assert.Single(Blocks(AboutCopy.BlockKind.Privacy));
        }

        [Fact]
        public void The_repository_and_the_author_are_reached()
        {
            //Write to the Author has no main-menu entry (#704, #813): About is where a player looks for who made the game
            Assert.Single(Blocks(AboutCopy.BlockKind.Links));
        }

        [Fact]
        public void Every_block_carries_what_its_kind_sets()
        {
            foreach (AboutCopy.Block block in AboutCopy.Pages(TITLE).SelectMany(page => page.Blocks))
            {
                bool text = block.Kind is AboutCopy.BlockKind.Line or AboutCopy.BlockKind.Paragraph;
                bool credits = block.Kind == AboutCopy.BlockKind.Credits;

                Assert.Equal(text, !string.IsNullOrWhiteSpace(block.Text));
                Assert.Equal(credits, block.Credits is { Length: > 0 });
            }
        }

        private static AboutCopy.Block[] Blocks(AboutCopy.BlockKind kind) =>
            AboutCopy.Pages(TITLE).SelectMany(page => page.Blocks).Where(block => block.Kind == kind).ToArray();
    }
}

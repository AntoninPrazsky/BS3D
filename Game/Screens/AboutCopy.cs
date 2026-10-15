using System.Collections.Generic;

namespace BS3D.Screens
{
    /// <summary>
    /// What the About page says, and in what order (#807), kept apart from the page so a test can hold it: every string to
    /// the typesetting rules (<see cref="CopyRules"/>, "Typesetting" in <c>docs/game-shell/menu-look.md</c>) and the credits
    /// to the list the page has carried since #463, so a shorter page cannot quietly drop a library, a licence or a tool
    /// (<c>AboutCopyTests</c>). One idea a page, each short enough to read without deciding to; whatever does not fit
    /// belongs in the repository, not on the page.
    /// <para>
    /// <b>The page draws <see cref="Pages"/> and nothing else</b>: each page is its name and a list of blocks, and
    /// <c>AboutPage</c> sets every block it is handed, in order, and throws on a kind it has no way to set. So what the test
    /// reads is what the player sees: a credit list, the privacy line or the player cannot leave the page without leaving
    /// this list, where the test sees it go.
    /// </para>
    /// </summary>
    internal static class AboutCopy
    {
        /// <summary>One line of a credit list: what is credited, and what it does in the game.</summary>
        internal readonly record struct Credit(string Name, string Role);

        /// <summary>What one block of a page is, which decides how <c>AboutPage</c> sets it.</summary>
        internal enum BlockKind
        {
            /// <summary>One line standing alone, centred: <see cref="Block.Text"/>.</summary>
            Line,

            /// <summary>A paragraph of the page's own copy at the prose measure: <see cref="Block.Text"/>.</summary>
            Paragraph,

            /// <summary>
            /// The online scores' privacy sentence as an aside. Not this page's copy: the Settings page reads the same
            /// sentence from the one place both take it (<c>OnlineScores.PrivacySentence</c>, #548), so the two cannot say
            /// different things about the same data.
            /// </summary>
            Privacy,

            /// <summary>A credit list, what is credited beside what it does: <see cref="Block.Credits"/>.</summary>
            Credits,

            /// <summary>The original score's player: the piece's name, the spectrum, Play and Next.</summary>
            Player,

            /// <summary>The repository's entry and the way to the author (#813).</summary>
            Links,
        }

        /// <summary>One block of a page: its kind, and the text or the credits the kind sets (null where it sets neither).</summary>
        internal readonly record struct Block(BlockKind Kind, string Text = null, Credit[] Credits = null);

        /// <summary>One page: its name, set between the arrows, and its blocks from the top.</summary>
        internal readonly record struct Page(string Title, Block[] Blocks);

        /// <summary>
        /// The pages, in the order they turn: what the game is with the privacy line, what it is built on, what it was made
        /// with (the repository and the way to the author under it), and <b>the player last</b> — the owner's ruling
        /// (2026-10-12): it is a small easter egg, waiting at the end for whoever turns that far, so About opens on what the
        /// game is. <paramref name="title"/> is the game's name as the page says it (<c>BS3DGame.GAME_TITLE</c>).
        /// </summary>
        internal static Page[] Pages(string title) => new Page[]
        {
            new("THE GAME", new Block[] { new(BlockKind.Paragraph, Game(title)), new(BlockKind.Privacy) }),
            new("BUILT ON", new Block[] { new(BlockKind.Paragraph, BUILT_ON), new(BlockKind.Credits, Credits: LIBRARIES) }),
            new("MADE WITH", new Block[]
            {
                new(BlockKind.Paragraph, MADE_WITH),
                new(BlockKind.Credits, Credits: GENERATED),
                new(BlockKind.Links),
            }),
            new("THE ORIGINAL SCORE", new Block[] { new(BlockKind.Line, SCORE), new(BlockKind.Player) }),
        };

        /// <summary>The player's one line, over the piece's name: the page's name already says whose score it is.</summary>
        internal const string SCORE = "Written as code — synthesized the moment it plays.";

        /// <summary>
        /// What the game is, in four sentences. The online scores' privacy line stands under it, from the one place the
        /// Settings page reads it too (<c>OnlineScores.PrivacySentence</c>), so it is not copy of this page's.
        /// </summary>
        internal static string Game(string title) =>
            title.Replace(' ', SpaceWrap.TIE) + " is a 3D arcade puzzle. Aim, match, and collapse colour clusters hanging beneath "
            + "a glass ceiling. Matching three or more drops them — severing everything anchored below. The first chapter "
            + "teaches the controls as you play.";

        /// <summary>Over the libraries: the one thing about how it is built that a player can see.</summary>
        internal const string BUILT_ON =
            "Everything you see — from the spheres to the city — is procedural: no 3D models, only code.";

        /// <summary>What the game is built on, with the typefaces' licence, as a list rather than one 50-word sentence.</summary>
        internal static readonly Credit[] LIBRARIES =
        {
            new(".NET\u00A010", "the runtime"),
            new("MonoGame\u00A03.8.5 (DirectX\u00A011)", "graphics, sound and input"),
            new("BepuPhysics\u00A02.5", "the physics"),
            new("Myra", "the interface"),
            new("FontStashSharp", "the text"),
            new("NVorbis", "the music"),
            new("Anton, Inter, PromptFont", "the typefaces, all SIL\u00A0OFL\u00A01.1"),
        };

        /// <summary>
        /// Over the tools: that the original game is the author's own code, written without AI, that what it has gained
        /// since 2026 was made with Claude, and that everything else was generated on the author's own machine (#866: the
        /// sentence before it read as if Claude had written all of it from the start).
        /// </summary>
        internal const string MADE_WITH =
            "The original game is the author’s own code, written without AI. Since 2026 it has been extended with Claude "
            + "(Anthropic), through Claude Code — the shaders, the level designs, the tools and the documentation among "
            + "them. The rest was generated locally:";

        /// <summary>What was generated locally, and what for.</summary>
        internal static readonly Credit[] GENERATED =
        {
            new("ACE-Step\u00A01.5", "the music"),
            new("Z-Image-Turbo", "the intro logo and design references"),
            new("RealESRGAN", "the logo’s upscale"),
            new("Gemma, nomic-embed", "small models, as development tools"),
        };

        /// <summary>
        /// Every string the pages draw of their own, read off <see cref="Pages"/> — the names, the lines and paragraphs, and
        /// every credit list's names and roles — so it is what the test holds to the rules and searches for the credits.
        /// </summary>
        internal static IEnumerable<string> Copy(string title)
        {
            foreach (Page page in Pages(title))
            {
                yield return page.Title;

                foreach (Block block in page.Blocks)
                {
                    if (block.Text != null) yield return block.Text;
                    if (block.Credits == null) continue;

                    foreach (Credit credit in block.Credits)
                    {
                        yield return credit.Name;
                        yield return credit.Role;
                    }
                }
            }
        }
    }
}

using System.Collections.Generic;

namespace BS3D.Screens
{
    /// <summary>
    /// What the About page says (#807), kept apart from the page so a test can hold it: every string to the typesetting
    /// rules (<see cref="CopyRules"/>, "Typesetting" in <c>docs/game-shell/menu-look.md</c>) and the credits to the list the
    /// page has carried since #463, so a shorter page cannot quietly drop a library, a licence or a tool
    /// (<c>AboutCopyTests</c>). One idea a page, each short enough to read without deciding to; whatever does not fit
    /// belongs in the repository, not on the page.
    /// </summary>
    internal static class AboutCopy
    {
        /// <summary>One line of a credit list: what is credited, and what it does in the game.</summary>
        internal readonly record struct Credit(string Name, string Role);

        /// <summary>
        /// The pages' names, in the order they turn: the player first, the owner's "Jukebox first" — the first thing a
        /// player sees is the thing they can use — then what the game is, what it is built on and what it was made with.
        /// </summary>
        internal static readonly string[] TITLES =
        {
            "THE ORIGINAL SCORE",
            "THE GAME",
            "BUILT ON",
            "MADE WITH",
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

        /// <summary>Over the tools: what Claude made, and that everything else was generated on the author's own machine.</summary>
        internal const string MADE_WITH =
            "The code, the shaders, the level designs, the tools and the documentation were made with Claude (Anthropic), "
            + "through Claude Code. The rest was generated locally:";

        /// <summary>What was generated locally, and what for.</summary>
        internal static readonly Credit[] GENERATED =
        {
            new("ACE-Step\u00A01.5", "the music"),
            new("Z-Image-Turbo", "the intro logo and design references"),
            new("RealESRGAN", "the logo’s upscale"),
            new("Gemma, nomic-embed", "small models, as development tools"),
        };

        /// <summary>Every string the page draws of its own, the page names included — what the test holds to the rules.</summary>
        internal static IEnumerable<string> Copy(string title)
        {
            foreach (string name in TITLES) yield return name;

            yield return SCORE;
            yield return Game(title);
            yield return BUILT_ON;
            yield return MADE_WITH;

            foreach (Credit credit in LIBRARIES)
            {
                yield return credit.Name;
                yield return credit.Role;
            }

            foreach (Credit credit in GENERATED)
            {
                yield return credit.Name;
                yield return credit.Role;
            }
        }
    }
}

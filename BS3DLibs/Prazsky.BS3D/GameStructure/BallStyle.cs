using System;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// What the balls of a map are MADE of (#258). Not what they are — that is <see cref="BallType"/>, the
    /// thirteen colours the lattice, the match rule and the score are all about — but what light does when it
    /// arrives at one, which is the whole of the difference between the two entries below and the whole of what
    /// this decides.
    /// <para>
    /// It is a property of the <b>map</b> and not of the player, the machine or the scene: a level is authored
    /// to look a particular way and a hand-built map that wants glass gets glass wherever it is opened, exactly
    /// as it gets the backdrop and the dome it names (<c>Prazsky.BS3D.Levels.Level.Balls</c>). Nothing about the
    /// simulation, the lattice or the rules reads it — a bubble level plays move for move like a vinyl one.
    /// </para>
    /// </summary>
    public enum BallStyle : byte
    {
        /// <summary>
        /// The moulded vinyl beach ball the game has always drawn: five gores of the type colour against white,
        /// a white disc at each pole, welded seams and a fine cast relief that breaks up the highlight. Opaque,
        /// and the value a map that says nothing gets — every level authored before #258 is this one.
        /// </summary>
        Beach = 0,

        /// <summary>
        /// A hollow glass bubble: a soap film with the type colour dyed into it, transparent through the middle,
        /// bright and iridescent along the rim. The look the game's own 3D wordmark is swept in, which is why it
        /// exists at all.
        /// </summary>
        Bubble = 1
    }

    /// <summary>
    /// Reading a <see cref="BallStyle"/> off a level file or a command line. Lenient in the same way and for the
    /// same reason <c>SceneRenderer.TryParseScene</c> and <c>ProceduralMusic.ThemeFor</c> are: a look is not
    /// worth throwing over, so an unknown spelling comes back false and the caller keeps its default rather
    /// than a level failing to open because somebody typed "bubbles".
    /// </summary>
    public static class BallStyles
    {
        /// <summary>
        /// The spellings a style answers to. <c>glass</c> and <c>bubbles</c> are there because they are what a
        /// hand-editing author reaches for first; <c>vinyl</c> names the beach ball by its material, which is
        /// how the rendering notes have always talked about it.
        /// </summary>
        public static bool TryParse(string name, out BallStyle style)
        {
            style = BallStyle.Beach;

            if (string.IsNullOrWhiteSpace(name)) return false;

            switch (name.Trim().ToLowerInvariant())
            {
                case "beach":
                case "vinyl":
                    style = BallStyle.Beach;
                    return true;

                case "bubble":
                case "bubbles":
                case "glass":
                    style = BallStyle.Bubble;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// The spelling a style is written back out as — the enum's own name, lowercased, which is one of the
        /// keys <see cref="TryParse"/> takes. It is what keeps a level saved by the map editor loadable by the
        /// map editor without either side carrying a table of names.
        /// </summary>
        public static string ToName(BallStyle style) => style.ToString().ToLowerInvariant();

        /// <summary>The next style in the enum, wrapping — what a cycling key in an authoring tool wants.</summary>
        public static BallStyle Next(BallStyle style)
        {
            BallStyle[] all = Enum.GetValues<BallStyle>();

            return all[(Array.IndexOf(all, style) + 1) % all.Length];
        }
    }
}

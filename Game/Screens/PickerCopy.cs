using System.Globalization;

namespace BS3D.Screens
{
    /// <summary>
    /// What the level picker and the online boards say (#806): the chapter's readout, the detail line under the tiles, a
    /// locked tile's line, the boards' headings and the player's own place — written here rather than inside the pages'
    /// trees, so a test can hold every one of them to the typesetting rules (<see cref="CopyRules"/>,
    /// <c>PickerCopyTests</c>), the way About's copy is (#807). Strings only; the pages decide which one is shown.
    /// <para>
    /// <b>One space between words and around a separator</b>: the readout's " · " and the boards' "You #6" were written
    /// with two (a column made of spaces), and "Typesetting" in <c>docs/game-shell/menu-look.md</c> sets one. A quoted
    /// name takes ‘ ’. The lines here never wrap (each is one label's single line), so they carry no ties.
    /// </para>
    /// </summary>
    internal static class PickerCopy
    {
        /// <summary>The separator between the parts of one line of readout.</summary>
        internal const string SEPARATOR = " · ";

        /// <summary>The star the readouts count in: Inter's, since Anton has none.</summary>
        internal const string STAR = "★";

        /// <summary>
        /// The chapter's readout under its name: where it stands in the campaign and what is done of it, or, with nothing
        /// in it open, what opens it — the level to clear first when the sequence shuts it (<paramref name="clearFirst"/>,
        /// 1-based), or else its cheapest price.
        /// </summary>
        internal static string ChapterLine(int chapter, int chapters, bool anyOpen, int cleared, int length, int stars,
            int maxStars, bool beyondReach, int clearFirst, int opensAt)
        {
            string position = $"Chapter {N(chapter)} of {N(chapters)}";

            return anyOpen ? position + SEPARATOR + $"{N(cleared)} of {N(length)} cleared" + SEPARATOR + $"{STAR} {N(stars)} of {N(maxStars)}"
                : beyondReach ? position + SEPARATOR + "locked" + SEPARATOR + $"clear level {N(clearFirst)} first"
                : position + SEPARATOR + "locked" + SEPARATOR + $"opens at {N(opensAt)} {STAR}";
        }

        /// <summary>The campaign's star total over the tiles.</summary>
        internal static string StarsCollected(int stars) => $"{STAR} {N(stars)} collected";

        /// <summary>A locked tile's line: the level that must go first, or the price.</summary>
        internal static string TileLock(bool beyondReach, int firstNumber, int price) =>
            beyondReach ? $"#{N(firstNumber)} first" : $"{N(price)} {STAR}";

        /// <summary>A skipped level's tile (#347).</summary>
        internal const string TILE_SKIPPED = "Skipped";

        /// <summary>The detail line for an open level: its name and the rules it is played under.</summary>
        internal static string DetailOpen(string name, string rules) => $"{name} — {rules}";

        /// <summary>The detail line for a level the sequence shuts: the one standing in the way, by number and name.</summary>
        internal static string DetailSequence(string name, int nextNumber, string nextName) =>
            $"{name} — one level at a time; next is {N(nextNumber)} ‘{nextName}’";

        /// <summary>The detail line for a level its price shuts: the price against what the player holds.</summary>
        internal static string DetailPrice(string name, int price, int have) =>
            $"{name} — unlocks at {N(price)} {STAR}, you have {N(have)}";

        /// <summary>The picker's boards plate's heading: the level's number and name, in capitals.</summary>
        internal static string BoardsHeading(int number, string name) => $"{N(number)} {name}".ToUpperInvariant();

        /// <summary>The picker's button onto the boards, naming the level it opens.</summary>
        internal static string BoardButton(string name) => name != null ? $"Board: {name}" : "Leaderboard";

        /// <summary>What the player's own line under a board says of their unfinished attempt (#716): words, because that line is
        /// the display face, which has no star glyph, and it is never cut (it has no cell to fit).</summary>
        internal const string UNFINISHED = "not finished";

        /// <summary>
        /// The mark of an unfinished attempt (#716) where a cell must fit it: the hollow star, the same glyph the boards' rows
        /// wear in the cup's column for one (#977, <c>BoardView</c>) and an unearned star is drawn with on the picker's tiles -
        /// an attempt that did not finish earned none. High Scores' leader and place cells (#1004): ", not finished" was cut
        /// to "not finis…" against the score beside it at 1920x1080 and below. Inter's glyph, so only in the small face.
        /// </summary>
        internal const string UNFINISHED_MARK = "☆";

        /// <summary>The player's own place under a board: their rank of how many, or that they or anyone is not on it.</summary>
        internal static string YouLine(int rank, int total, bool unfinished) =>
            rank > 0 ? $"You #{N(rank)} of {N(total)}" + (unfinished ? ", " + UNFINISHED : string.Empty)
            : total > 0 ? "You are not on this board yet"
            : "Nobody is on this board yet";

        /// <summary>High Scores (#685): a board's leader, left of their score, with the mark of an unfinished attempt.</summary>
        internal static string Leader(string name, bool unfinished) => $"#1 {name}" + (unfinished ? " " + UNFINISHED_MARK : string.Empty);

        /// <summary>High Scores: the player's own place on a level's board, left of their score.</summary>
        internal static string Place(int rank, int total, bool unfinished) =>
            (rank == 1 ? "You lead" : $"You: {Ordinal(rank)} of {N(total)}") + (unfinished ? " " + UNFINISHED_MARK : string.Empty);

        /// <summary>"1st", "2nd", "3rd", "4th", … "11th", "12th", "13th", "21st".</summary>
        internal static string Ordinal(int n)
        {
            int tens = n % 100;
            string suffix = tens is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return N(n) + suffix;
        }

        private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
    }
}

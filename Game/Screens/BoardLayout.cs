using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The arithmetic of an online board's plate (#934): how wide the nickname's column is, how a nickname is cut to it, and
    /// how wide a board may be on a display wider than 16:9. Pure over measured widths handed in, so a test pins its promises
    /// without a window (<c>BoardLayoutTests</c>) the way <see cref="ChipColumnLayout"/> pins the chips'; <see cref="BoardView"/>
    /// hands it the widths of the real face.
    /// <para>
    /// <b>A name can never draw over its neighbours.</b> The row used to leave the nickname the Fill column and the mark
    /// ("not finished"), the rank and the score their Auto ones; a Myra label does not clip to its cell, so a name wider than
    /// what the others left ran on across the mark - the owner's report, a long nickname and "not finished" printed one over the
    /// other. Now every column is a width of its own: the rank, the mark and the score are as wide as what they hold, the name
    /// has <see cref="NameWidth"/> (all that is left), and the name is cut to it with an ellipsis (<see cref="Ellipsize"/>) when
    /// the row is written.
    /// </para>
    /// <para>
    /// <b>The board takes the room a wide display adds.</b> Up to 16:9 its width is the one the strip beside the column always
    /// gave it; past that the strip beside the column grows by half of every pixel the window is wider than 16:9 of its own
    /// height, and <see cref="Ceiling"/> lets the board grow with it, so the margins stay the ones 16:9 has and every pixel the
    /// display adds goes to the names. It stops at <see cref="FullWidth"/>, the width at which the longest nickname there can be
    /// shows whole beside its mark and score: a wider plate would only be emptier.
    /// </para>
    /// </summary>
    internal static class BoardLayout
    {
        /// <summary>The ellipsis a cut name ends in.</summary>
        internal const string ELLIPSIS = "…";

        /// <summary>The columns of a row that are not the name: the cup, the rank, the mark and the score, and so the gaps between five columns.</summary>
        internal const int GAPS = 4;

        /// <summary>
        /// The width the name column has: the board's width less the cup, the rank, the mark and the score and the gaps between
        /// the five columns. Never negative.
        /// </summary>
        internal static int NameWidth(int boardWidth, int cup, int rank, int mark, int score, int gap) =>
            Math.Max(0, boardWidth - cup - rank - mark - score - GAPS * gap);

        /// <summary>
        /// How wide a row's name may draw: the name column's width, and - on a row that wears no mark, whose mark cell is empty -
        /// the mark column and the gap before it too (the row's name label spans both), so "not finished" on the bottom rows of a
        /// board does not cost the clears above them their room.
        /// </summary>
        internal static int RowMeasure(int nameWidth, int mark, int gap, bool marked) => marked ? nameWidth : nameWidth + gap + mark;

        /// <summary>
        /// The board width at which <paramref name="widestName"/> shows whole beside the widest mark, rank and score a row holds.
        /// </summary>
        internal static int FullWidth(int cup, int rank, int widestName, int mark, int score, int gap) =>
            cup + rank + widestName + mark + score + GAPS * gap;

        /// <summary>
        /// The widest a board may be on a window <paramref name="screenWidth"/> by <paramref name="screenHeight"/> pixels:
        /// <paramref name="baseWidth"/> up to 16:9, and on a wider window that plus half of what it is wider, which is what the
        /// strip beside the column gains (the column stays the same width), so the margins hold. Never over
        /// <paramref name="fullWidth"/>, and never under <paramref name="baseWidth"/>.
        /// </summary>
        internal static int Ceiling(int screenWidth, int screenHeight, int baseWidth, int fullWidth)
        {
            int wider = Math.Max(0, screenWidth - screenHeight * 16 / 9);
            return Math.Max(baseWidth, Math.Min(fullWidth, baseWidth + wider / 2));
        }

        /// <summary>
        /// <paramref name="text"/> as it is, or - when <paramref name="width"/> says it is wider than <paramref name="measure"/> -
        /// its longest beginning that, with <see cref="ELLIPSIS"/> after it, is not (a space before the ellipsis goes: "Nonex …"
        /// reads as two words). A name is cut between characters, never through a surrogate pair. Where not even the ellipsis
        /// fits it is nothing at all: a name column that narrow draws nothing rather than draw over its neighbour. Called when a
        /// row is written, never per frame.
        /// </summary>
        /// <param name="width">How wide a string draws, in the same unit as <paramref name="measure"/>.</param>
        internal static string Ellipsize(string text, int measure, Func<string, int> width)
        {
            if (string.IsNullOrEmpty(text) || width(text) <= measure) return text;

            //The longest prefix that fits with the ellipsis, by bisection: a prefix is never narrower than a shorter one, and
            //the answer is checked as it is made, so a face whose kerning breaks that still never returns a string too wide
            int low = 0, high = text.Length - 1;
            string best = string.Empty;

            while (low <= high)
            {
                int middle = (low + high) / 2;
                string cut = Cut(text, middle);

                if (width(cut) <= measure)
                {
                    best = cut;
                    low = middle + 1;
                }
                else high = middle - 1;
            }

            return best;
        }

        /// <summary>The first <paramref name="length"/> characters with the ellipsis on, whole characters and no space before it.</summary>
        private static string Cut(string text, int length)
        {
            if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;

            return text.AsSpan(0, length).TrimEnd().ToString() + ELLIPSIS;
        }
    }
}

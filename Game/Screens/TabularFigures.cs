using System;
using System.Globalization;
using System.Text;

namespace BS3D.Screens
{
    /// <summary>
    /// Tabular figures by hand (#806): a number written so that every digit takes the same width, which is what lets a
    /// column of scores or ranks line up digit under digit. The faces cannot do it themselves: FontStashSharp 1.5.6 does no
    /// text shaping, so OpenType's <c>tnum</c> is out of reach, Inter's figures are proportional (its 1 is 833 units wide
    /// against its 0's 1292) and Anton's are equal but for the 1 (677 against 1012) — so "31 111" and "40 118" set flush right
    /// in the same column put their hundreds in different places ("Typesetting" in <c>docs/game-shell/menu-look.md</c>).
    /// <para>
    /// <b>How.</b> Every digit is given a cell as wide as the face's widest digit, and the digit is centred in it by
    /// rich-text space commands either side (<c>/s[n]</c>: n pixels of nothing, which FontStashSharp's
    /// <c>RichText.LayoutBuilder</c> lays out as a chunk of its own). Every digit is fenced by two commands even where its
    /// share is zero, so no two digits stand in one text chunk and no kerning pair between figures can move one: the
    /// layout measures each chunk on its own, so kerning acts only inside a chunk. What is not a digit (the group space,
    /// "×", "#") keeps its own width. A glyph's advance is a whole pixel in FontStashSharp 1.5.6, so the cells are exact
    /// rather than rounded: each digit's two shares sum to the cell to the pixel.
    /// </para>
    /// <para>
    /// The result is a <see cref="Myra.Graphics2D.UI.Label"/>'s text (a label parses commands, <c>SupportsCommands</c>),
    /// measured, aligned and drawn by Myra like any other, so a cell's alignment, a label's punch and a
    /// <see cref="ShadowedLabel"/>'s backing all hold. It is made when the number changes, never per frame. Pure, so the
    /// arithmetic is pinned in a test with a stand-in face (<c>TabularFiguresTests</c>); <see cref="FontFigures"/> hands
    /// it the advances of a real one.
    /// </para>
    /// </summary>
    internal static class TabularFigures
    {
        /// <summary>The digits a cell is measured over and given to, '0' to '9'.</summary>
        internal const int DIGITS = 10;

        /// <summary>The rich-text command prefix a label parses (and the character that must be doubled to be drawn).</summary>
        private const char COMMAND = '/';

        /// <summary>The cell every digit takes: the widest of the face's ten advances.</summary>
        internal static int Cell(ReadOnlySpan<int> digitAdvances)
        {
            if (digitAdvances.Length != DIGITS) throw new ArgumentException("ten advances, '0' to '9'", nameof(digitAdvances));

            int cell = 0;
            foreach (int advance in digitAdvances) cell = Math.Max(cell, advance);
            return cell;
        }

        /// <summary>
        /// <paramref name="text"/> with every digit centred in a cell of <see cref="Cell"/>'s width, as a label's command
        /// text: the space before a digit is the floor of half its shortfall, the space after it the rest. A '/' in the text
        /// is doubled, so it is drawn rather than read as a command. Text with neither a digit nor a '/' comes back as it
        /// was, the same string.
        /// </summary>
        internal static string Set(string text, ReadOnlySpan<int> digitAdvances)
        {
            int cell = Cell(digitAdvances);
            if (string.IsNullOrEmpty(text) || text.AsSpan().IndexOfAny("0123456789/") < 0) return text;

            StringBuilder set = new(text.Length * 12);
            foreach (char c in text)
            {
                if (c >= '0' && c <= '9')
                {
                    int shortfall = cell - digitAdvances[c - '0'];
                    int before = shortfall / 2;
                    Space(set, before);
                    set.Append(c);
                    Space(set, shortfall - before);
                }
                else if (c == COMMAND) set.Append(COMMAND).Append(COMMAND);
                else set.Append(c);
            }

            return set.ToString();
        }

        /// <summary>
        /// How wide <see cref="Set"/>'s text lays out, in pixels: a cell for every digit, and every run between digits as
        /// <paramref name="measureRun"/> measures it whole (the layout measures that run as one chunk, kerning inside it
        /// included). What a column is sized by, since the face's own measure of the command text would count the commands
        /// as glyphs.
        /// </summary>
        internal static int Width(string text, ReadOnlySpan<int> digitAdvances, Func<string, int> measureRun)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int cell = Cell(digitAdvances);
            int width = 0, run = -1;

            for (int i = 0; i <= text.Length; i++)
            {
                bool digit = i < text.Length && text[i] >= '0' && text[i] <= '9';
                bool end = i == text.Length;

                if ((digit || end) && run >= 0)
                {
                    width += measureRun(text.Substring(run, i - run));
                    run = -1;
                }

                if (digit) width += cell;
                else if (!end && run < 0) run = i;
            }

            return width;
        }

        private static void Space(StringBuilder set, int pixels) =>
            set.Append(COMMAND).Append("s[").Append(pixels.ToString(CultureInfo.InvariantCulture)).Append(']');
    }
}

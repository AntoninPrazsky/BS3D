using System;
using System.Collections.Generic;
using System.Text;

namespace BS3D.Screens
{
    /// <summary>
    /// The end of a typed note as a fixed-height field shows it (#813): the text wrapped into lines that each fit the
    /// field's width, and the last <c>lines</c> of them, an ellipsis opening the first when earlier ones were left out.
    /// <para>
    /// <b>Counted in lines, not characters, and that is the whole fix.</b> The field first showed the last 220
    /// characters, sized to fill six lines of average prose; a note of short lines (Shift+Enter) or of long words wrapped
    /// into more than six, and the line being typed went on below the field, where nothing showed it. The owner typed
    /// "outside the box". Here every line is measured, so the caret's line is always the last one shown.
    /// </para>
    /// <para>
    /// Lines break at spaces, and a word wider than the line breaks between characters (a field takes whatever is typed,
    /// a URL included). A space inside a line is kept, so the caret moves the moment the key is pressed; a space that a
    /// break falls on goes with the break, and the caret (laid out with the text) opens the next line. Each line is
    /// measured to fit, so the label's own wrap meets nothing it has to break. A pure function of the text and a
    /// measure, so the promise is pinned in a test.
    /// </para>
    /// </summary>
    internal static class FieldTail
    {
        internal const string ELLIPSIS = "…";

        /// <param name="text">The note as typed, the caret included if it is to be laid out with it.</param>
        /// <param name="width">The line's width, in the measure's units.</param>
        /// <param name="lines">How many lines the field shows.</param>
        /// <param name="measure">A line's width as the label will draw it.</param>
        public static string Show(string text, float width, int lines, Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(text) || lines < 1) return text ?? string.Empty;

            List<string> laid = Lay(text, width, measure);
            if (laid.Count <= lines) return string.Join('\n', laid);

            //The last lines, the first of them opened by the ellipsis and shortened from its start until it fits
            int first = laid.Count - lines;
            string opening = laid[first];
            while (opening.Length > 0 && measure(ELLIPSIS + opening) > width) opening = opening[1..];
            laid[first] = ELLIPSIS + opening;

            return string.Join('\n', laid.GetRange(first, lines));
        }

        private static List<string> Lay(string text, float width, Func<string, float> measure)
        {
            List<string> result = new();
            StringBuilder line = new();

            foreach (string paragraph in text.Split('\n'))
            {
                line.Clear();
                int i = 0;

                while (i < paragraph.Length)
                {
                    //One token: a run of spaces or a run of anything else
                    bool space = paragraph[i] == ' ';
                    int end = i;
                    while (end < paragraph.Length && (paragraph[end] == ' ') == space) end++;
                    string token = paragraph[i..end];
                    i = end;

                    if (measure(line.ToString() + token) <= width)
                    {
                        line.Append(token);
                        continue;
                    }

                    if (space)
                    {
                        //A break falls on it: the line ends here, and the space goes with the break
                        result.Add(line.ToString());
                        line.Clear();
                        continue;
                    }

                    //A word that does not fit: onto a line of its own, broken between characters if even that is too narrow
                    if (line.Length > 0)
                    {
                        result.Add(line.ToString());
                        line.Clear();
                    }

                    foreach (char c in token)
                    {
                        if (line.Length > 0 && measure(line.ToString() + c) > width)
                        {
                            result.Add(line.ToString());
                            line.Clear();
                        }
                        line.Append(c);
                    }
                }

                result.Add(line.ToString());
            }

            return result;
        }
    }
}

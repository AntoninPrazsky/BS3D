using System;
using System.Text;

namespace BS3D.Screens
{
    /// <summary>
    /// Lays a paragraph out in lines that break at spaces only (#769), for a wrapped Myra label whose text has a dot
    /// inside a word. FontStashSharp's own wrap (<c>RichText.LayoutBuilder</c>, read off the 1.5.6 assembly) takes a
    /// <c>'.'</c> as a break opportunity as well as white space, so the privacy note broke the score server's address
    /// after <c>scores.winphonew.</c> and set <c>eu,</c> on the next line (photographed at 1920x1080), and the About
    /// page's <c>MonoGame 3.8.5</c> could come apart the same way. The lines come back joined by <c>'\n'</c>, each
    /// measured to fit, so the label's own wrap meets nothing it has to break; a single word wider than the line is
    /// left on a line of its own for the label to deal with.
    /// <para>
    /// A pure function of the text and a measure, so the promise is pinned in a test with a stand-in for the font. The
    /// page calls it with the face the label draws in and the label's own width.
    /// </para>
    /// </summary>
    internal static class SpaceWrap
    {
        /// <param name="text">The paragraph. A <c>'\n'</c> in it is kept as a line break of its own.</param>
        /// <param name="width">The line's width, in the measure's units.</param>
        /// <param name="measure">A line's width as the label will draw it.</param>
        public static string Wrap(string text, float width, Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(text)) return text;

            StringBuilder result = new(text.Length + 8);
            StringBuilder line = new();

            string[] paragraphs = text.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) result.Append('\n');
                line.Clear();

                foreach (string word in paragraphs[p].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Length > 0)
                    {
                        int before = line.Length;
                        line.Append(' ').Append(word);
                        if (measure(line.ToString()) <= width) continue;

                        line.Length = before;
                        result.Append(line).Append('\n');
                        line.Clear();
                    }

                    line.Append(word);
                }

                result.Append(line);
            }

            return result.ToString();
        }
    }
}

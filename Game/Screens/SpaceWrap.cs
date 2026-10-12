using System;
using System.Collections.Generic;
using System.Text;

namespace BS3D.Screens
{
    /// <summary>
    /// Sets a paragraph in lines (#806), breaking at spaces only (#769) and choosing <b>where</b> to break by the
    /// shape of the whole paragraph rather than line by line — the rules in "Typesetting" in
    /// <c>docs/game-shell/menu-look.md</c>. The lines come back joined by <c>'\n'</c>, each measured to fit, so the
    /// label's own wrap meets nothing it has to break; a single word wider than the line is left on a line of its own
    /// for the label to deal with.
    /// <para>
    /// <b>Why not the label's own wrap.</b> FontStashSharp's (<c>RichText.LayoutBuilder</c>, read off the 1.5.6
    /// assembly) takes a <c>'.'</c> as a break opportunity as well as white space, so the privacy note broke the score
    /// server's address after <c>scores.winphonew.</c> (photographed at 1920x1080, #769); and both it and the greedy
    /// breaker this replaced fill each line as full as it goes, which leaves whatever is over on the last line — the
    /// About page's "It is still in / here:" was one word alone under three full lines.
    /// </para>
    /// <para>
    /// <b>The rules it keeps</b>, in order of weight: the fewest lines the ties allow, which is what a greedy fill takes
    /// (the pages' heights are budgets, and a balanced paragraph that grew a line would push a button off the frame); a
    /// last line of one word only where nothing else fits; then the evenest rag — the squared slack of every line but the
    /// last, plus a penalty on a last line shorter than <see cref="LAST_LINE_MIN_FILL"/> of the measure and a price on a
    /// line that ends on an operator and so parts a sum. A minimum-raggedness fill, in the family of Knuth and Plass's,
    /// but for a ragged right edge and without hyphenation (FontStashSharp has none).
    /// </para>
    /// <para>
    /// <b>Ties</b> — places a line must not break — come from the copy and from two rules of the breaker's: a
    /// <b>no-break space</b> (U+00A0, <see cref="TIE"/>) written into the copy where a name meets its number or a number
    /// its unit ("MonoGame 3.8.5", "385 balls"); a <b>lone dash or operator</b> (— – + = × ÷ −) stays at the end of the
    /// line before it, so no line starts with one (TeX's rule for a binary operator); and a <b>one-letter word</b> (a, A,
    /// I) goes over to the next line with the word after it rather than ending a line. A tie is drawn as a plain space:
    /// both faces carry U+00A0, but the line has already been laid out, and the space is the glyph the measure saw.
    /// </para>
    /// <para>
    /// A pure function of the text and a measure, so the promises are pinned in a test with a stand-in for the font
    /// (<c>SpaceWrapTests</c>). The page calls it with the face the label draws in and the label's laid-out width
    /// (<see cref="MenuPage"/>'s <c>LaidOutWidth</c>). It measures each candidate line once, so a paragraph of a hundred
    /// words is a few hundred <c>MeasureString</c> calls, made when a tree is built or a note changes and never per frame.
    /// </para>
    /// </summary>
    internal static class SpaceWrap
    {
        /// <summary>The no-break space: written into copy between two words a line must not part (#806).</summary>
        internal const char TIE = '\u00A0';

        //A last line under this share of the measure reads as a scrap rather than as the paragraph's end; the squared
        //shortfall is added to the cost, so the breaker gives a word or two from the line above when that rags the rest
        //less than the scrap costs
        private const double LAST_LINE_MIN_FILL = 0.3;

        //A last line of one word when the paragraph has more than one line. Heavier than any rag a real paragraph can
        //run up (each line's squared slack is at most 1), so it is avoided whenever the line count allows it
        private const double ONE_WORD_LAST_LINE = 1000.0;

        //A line that ends on an operator, parting a sum ("4×10 × / 3 = 120."). Allowed, as TeX allows a break after a
        //binary operator, but at a price above what the rag usually differs by, so a short sum goes over whole when a
        //break before it costs the rag less than this
        private const double OPERATOR_AT_LINE_END = 0.25;

        //What a line may not start with: a dash set as punctuation, or an operator of a worked sum
        private static bool TiesBack(string word) => IsDash(word) || IsOperator(word);

        private static bool IsDash(string word) => word is "—" or "–";

        private static bool IsOperator(string word) => word is "+" or "=" or "×" or "÷" or "−";

        //What may not end a line: a word of one letter
        private static bool TiesForward(string word) => word is "a" or "A" or "I";

        /// <param name="text">The paragraph. A <c>'\n'</c> in it is kept as a line break of its own; a run of spaces is one space.</param>
        /// <param name="width">The line's width, in the measure's units.</param>
        /// <param name="measure">A line's width as the label will draw it.</param>
        public static string Wrap(string text, float width, Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(text)) return text;

            StringBuilder result = new(text.Length + 8);
            string[] paragraphs = text.Split('\n');

            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) result.Append('\n');
                AppendParagraph(result, paragraphs[p], width, measure);
            }

            return result.ToString();
        }

        /// <summary>
        /// The paragraph's breakable units: its words with every tie made, each as it will be drawn (a tie as a space).
        /// </summary>
        private static List<string> Units(string paragraph)
        {
            List<string> units = new();
            bool joinNext = false;

            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (units.Count > 0 && (joinNext || TiesBack(word)))
                    units[^1] = units[^1] + " " + word;
                else
                    units.Add(word);

                joinNext = TiesForward(word);
            }

            for (int i = 0; i < units.Count; i++)
                units[i] = units[i].Replace(TIE, ' ');

            return units;
        }

        private static void AppendParagraph(StringBuilder result, string paragraph, float width, Func<string, float> measure)
        {
            List<string> units = Units(paragraph);
            int n = units.Count;
            if (n == 0) return;

            //A break after unit k leaves an operator at the line's end when the unit is a word with an operator tied on
            bool[] endsOnOperator = new bool[n];
            for (int k = 0; k < n; k++)
            {
                int space = units[k].LastIndexOf(' ');
                endsOnOperator[k] = space >= 0 && IsOperator(units[k][(space + 1)..]);
            }

            //Filled from the end: the best setting of units i..n-1 is its line count, then its cost, and where its first
            //line ends. Line count first, so the shape never costs a line (see the class remarks)
            int[] lines = new int[n + 1];
            double[] cost = new double[n + 1];
            int[] end = new int[n + 1];

            StringBuilder line = new();

            for (int i = n - 1; i >= 0; i--)
            {
                lines[i] = int.MaxValue;
                cost[i] = double.MaxValue;
                line.Clear();

                for (int j = i + 1; j <= n; j++)
                {
                    if (j > i + 1) line.Append(' ');
                    line.Append(units[j - 1]);

                    float w = measure(line.ToString());

                    //A unit wider than the line stands alone on one; anything longer than the line is not a candidate
                    bool tooWide = w > width;
                    if (tooWide && j > i + 1) break;

                    bool oneWord = j == i + 1 && units[i].IndexOf(' ') < 0;
                    double here = j == n ? LastLineCost(i, oneWord, w, width)
                        : Slack(w, width) + (endsOnOperator[j - 1] ? OPERATOR_AT_LINE_END : 0.0);
                    int count = 1 + lines[j];
                    double total = here + cost[j];

                    if (count < lines[i] || (count == lines[i] && total < cost[i]))
                    {
                        lines[i] = count;
                        cost[i] = total;
                        end[i] = j;
                    }

                    if (tooWide) break;
                }
            }

            for (int i = 0; i < n; i = end[i])
            {
                if (i > 0) result.Append('\n');

                for (int k = i; k < end[i]; k++)
                {
                    if (k > i) result.Append(' ');
                    result.Append(units[k]);
                }
            }
        }

        //A line that is not the last: its squared share of empty measure, so one very short line costs more than two
        //slightly short ones and the rag comes out even
        private static double Slack(float w, float width)
        {
            double slack = Math.Max(0.0, (width - w) / width);
            return slack * slack;
        }

        //The paragraph's last line, starting at unit `start`. The whole paragraph on one line costs nothing; otherwise a
        //lone word is the fault to avoid and a scrap the lesser one
        private static double LastLineCost(int start, bool oneWord, float w, float width)
        {
            if (start == 0) return 0.0;

            double result = oneWord ? ONE_WORD_LAST_LINE : 0.0;
            double shortfall = LAST_LINE_MIN_FILL - w / width;
            if (shortfall > 0.0) result += shortfall * shortfall;

            return result;
        }
    }
}

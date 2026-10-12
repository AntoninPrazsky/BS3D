namespace BS3D.Screens
{
    /// <summary>
    /// The characters of the game's English copy (#806), as one check: "Typesetting" in
    /// <c>docs/game-shell/menu-look.md</c> sets the rules, and this says which one a string breaks. Pure, so a test can
    /// hold every caption the tutorial draws to it (<c>CopyRulesTests</c>); the Myra pages' copy is held to the same rules
    /// by review, since their strings are built inside a tree no test can construct.
    /// </summary>
    internal static class CopyRules
    {
        /// <summary>
        /// The first fault in <paramref name="copy"/>, worded as what to write instead, or null for none. Literal
        /// characters a player types (the nickname's <c>_</c> and <c>-</c>) are not faults: only a hyphen standing alone
        /// between two spaces is a dash set wrong.
        /// </summary>
        internal static string Fault(string copy)
        {
            if (string.IsNullOrEmpty(copy)) return null;

            for (int i = 0; i < copy.Length; i++)
            {
                char c = copy[i];
                char before = i > 0 ? copy[i - 1] : ' ';
                char after = i + 1 < copy.Length ? copy[i + 1] : ' ';

                switch (c)
                {
                    case '\'':
                        return "a straight apostrophe or quote: write ’ (or ‘ ’ round a quotation)";
                    case '"':
                        return "a straight double quote: write “ ”";
                    case '-' when before == ' ' && after == ' ':
                        return "a hyphen set as a dash: write a spaced em dash, \" — \"";
                    case '.' when after == '.':
                        return "dots for an ellipsis: write …";
                    case '—' when !IsSpace(before) || !IsSpace(after):
                        return "an unspaced em dash: the house dash is spaced, \" — \"";
                    case ' ' when after == ' ':
                        return "two spaces: one space after a sentence, as between words";
                }
            }

            return null;
        }

        private static bool IsSpace(char c) => c == ' ' || c == SpaceWrap.TIE;
    }
}

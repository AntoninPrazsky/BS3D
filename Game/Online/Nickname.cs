using System.Text;

namespace BS3D.Online
{
    /// <summary>
    /// What a nickname may be (#548): the rule the settings page holds a typed name to, <b>mirroring the score
    /// service's</b> (#544) — 3 to 16 characters after trimming, letters and digits, single spaces, underscore and
    /// hyphen — so what the page accepts the service accepts.
    /// <para>
    /// <b>Stricter than the service in one respect, deliberately: the letters are the ones this game can draw.</b>
    /// The service takes any Unicode letter, but a nickname is set in Anton on the settings page and will be on the
    /// boards (#547), and Anton carries no Cyrillic, no Greek and no CJK — FontStashSharp drops a glyph a face lacks
    /// without a word, so such a name would be a blank button. Measured off the two faces' own character maps:
    /// Basic Latin, Latin-1 Supplement and Latin Extended-A are complete in Anton, and in Inter but for one
    /// letter, U+0149 (a deprecated Afrikaans ligature), which is left out. So the letters are those up to
    /// U+017F: every Czech, Slovak, Polish, German, French, Nordic and Hungarian name there is. Latin
    /// Extended-B and beyond have gaps in Anton (35 and 14 missing letters), and a rule with holes in it is a
    /// rule nobody can state.
    /// </para>
    /// <para>
    /// Normalized before it is judged: composed to NFC, so a letter typed as a base and a combining mark is the one
    /// precomposed letter the fonts carry; trimmed; and a run of spaces closed to one, which is friendlier than
    /// refusing it and is what "single spaces" means once the player has seen the result.
    /// </para>
    /// </summary>
    internal static class Nickname
    {
        internal const int MinLength = 3;
        internal const int MaxLength = 16;

        /// <summary>The highest code point a letter may have — the end of Latin Extended-A. See the class doc.</summary>
        private const char LastDrawableLetter = 'ſ';

        /// <summary>The one letter under it that Inter lacks.</summary>
        private const char MissingFromInter = 'ŉ';

        /// <summary>
        /// Whether <paramref name="c"/> may be typed into a nickname at all — what the page lets through as it is
        /// typed, so a key that could never be part of a name simply does nothing.
        /// </summary>
        internal static bool IsAllowed(char c) =>
            c == ' ' || c == '_' || c == '-'
            || (c <= LastDrawableLetter && c != MissingFromInter && char.IsLetterOrDigit(c));

        /// <summary>
        /// <paramref name="raw"/> as the service will hold it, or false with what is wrong, worded for the player.
        /// </summary>
        internal static bool TryNormalize(string raw, out string name, out string problem)
        {
            name = null;
            string composed = (raw ?? string.Empty).Normalize(NormalizationForm.FormC);

            StringBuilder built = new(composed.Length);
            bool space = false;

            foreach (char c in composed)
            {
                if (char.IsWhiteSpace(c))
                {
                    space = built.Length > 0;
                    continue;
                }

                if (!IsAllowed(c))
                {
                    problem = "Letters, digits, spaces, _ and - only.";
                    return false;
                }

                if (space) built.Append(' ');
                space = false;
                built.Append(c);
            }

            if (built.Length < MinLength)
            {
                problem = $"At least {MinLength} characters.";
                return false;
            }

            if (built.Length > MaxLength)
            {
                problem = $"At most {MaxLength} characters.";
                return false;
            }

            name = built.ToString();
            problem = null;
            return true;
        }
    }
}

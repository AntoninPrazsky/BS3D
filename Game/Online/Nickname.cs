using System;
using System.Globalization;
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

        //The words a nickname can be that put the party hat on the gun (#230, the code-word egg): a nod to the two libraries
        //the game stands on and to the author's own name. Never explained anywhere, and nothing about play reads it.
        private static readonly string[] SecretWords = { "bepu", "bepuphysics", "monogame", "prazsky" };

        /// <summary>
        /// Whether <paramref name="name"/> is one of the secret words (#230): compared without regard to case, and with
        /// spaces, underscores and hyphens left out, so "BEPU", "Bepu Physics" and "mono_game" all count. Null and
        /// anything else is false. <b>The one thing a secret word does is put the party hat on the gun</b> — a cosmetic,
        /// like every egg of #230's list that is not a change to the physics or the rules, which would silently
        /// invalidate what ScoreSim, ClearProbe and the sag gate measured.
        /// </summary>
        internal static bool IsSecretWord(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            foreach (string word in SecretWords)
                if (Matches(name, word)) return true;

            return false;
        }

        //Case-blind equality that skips the separators a nickname may hold, with no allocation
        private static bool Matches(string name, string word)
        {
            int w = 0;

            foreach (char c in name)
            {
                if (c == ' ' || c == '_' || c == '-') continue;
                if (w >= word.Length || char.ToLowerInvariant(c) != word[w]) return false;

                w++;
            }

            return w == word.Length;
        }

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

            string normal = built.ToString();

            //The service's denied names and words (#1020), here so the player is told while typing rather than refused later
            if (Array.Exists(DeniedNames, d => string.Equals(d, normal, StringComparison.OrdinalIgnoreCase)) || HasDeniedWord(normal))
            {
                name = null;
                problem = "That name is not allowed.";
                return false;
            }

            name = normal;
            problem = null;
            return true;
        }

        /// <summary>The names the score service refuses outright (its <c>Scores:DeniedNames</c>), compared without regard to case.</summary>
        internal static readonly string[] DeniedNames = { "admin", "administrator", "moderator", "system", "bs3d" };

        /// <summary>
        /// Vulgar and hateful words no part of a name may hold (#1020: "KuloMrd" on the boards, the owner: "Mrd must be a
        /// forbidden word"), matched in the name folded (<see cref="Fold"/>): diacritics off, lower case, separators out, and
        /// the digits that stand for letters read as those letters, so "KuloMrd", "k_u_r_v_a" and "kurv4" are all caught.
        /// <b>The service's copy is BS3D-API's <c>Nicknames.DeniedWords</c>, the authority</b>: change both in step.
        /// </summary>
        internal static readonly string[] DeniedWords =
        {
            "mrd", "kurv", "kokot", "curak", "prdel", "hovn", "srac", "jebat", "jeban", "jebn", "zasran", "buzna", "buzerant",
            "negr", "kreten", "hajzl", "sukat", "picus", "picovin", "debil",
            "fuck", "shit", "cunt", "nigg", "fagg", "bitch", "whore", "slut", "porn", "nazi", "hitler", "penis", "vagin",
            "asshole", "bastard", "retard", "wanker", "dildo", "pussy", "motherf", "cocksuck", "blowjob",
            "ficken", "fotze", "arschloch",
        };

        /// <summary>
        /// Words refused only as a whole part of a name, because they also sit inside ordinary ones ("pica" in Picard, "ass"
        /// in Bass, "kunda" in Kundera). A part is what spaces, <c>_</c>, <c>-</c>, digits and a change from a lower-case to a
        /// capital letter divide, folded with no digit read as a letter. The service's copy is <c>Nicknames.DeniedParts</c>.
        /// </summary>
        internal static readonly string[] DeniedParts =
        {
            "pica", "pice", "pici", "kunda", "suka", "jeb", "sex", "rape", "anal", "cock", "dick", "fag", "ass", "kys", "hure",
            "fick", "cum", "tit", "tits", "boob", "boobs",
        };

        /// <summary>Whether <paramref name="name"/> holds a denied word anywhere, or a denied part as one of its parts.</summary>
        internal static bool HasDeniedWord(string name)
        {
            string folded = Fold(name, digitsAsLetters: true);
            foreach (string word in DeniedWords)
                if (folded.Contains(word, StringComparison.Ordinal)) return true;

            int start = 0;
            for (int i = 1; i <= name.Length; i++)
            {
                bool end = i == name.Length || !char.IsLetter(name[i]) || (char.IsLower(name[i - 1]) && char.IsUpper(name[i]));
                if (!end) continue;
                string part = Fold(name[start..i], digitsAsLetters: false);
                if (part.Length > 0 && Array.IndexOf(DeniedParts, part) >= 0) return true;
                start = i;
                while (start < name.Length && !char.IsLetter(name[start])) start++;
                i = start;
            }
            return false;
        }

        /// <summary>
        /// A name as the word lists read it: decomposed and its combining marks dropped (so "Píča" reads "pica"), lower case,
        /// letters only; with <paramref name="digitsAsLetters"/> the digits that stand for letters read as them (0 o, 1 i, 3 e,
        /// 4 a, 5 s, 7 t, 8 b) and the rest dropped with the separators.
        /// </summary>
        internal static string Fold(string name, bool digitsAsLetters)
        {
            StringBuilder folded = new(name.Length);
            foreach (char c in name.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetter(c)) folded.Append(char.ToLowerInvariant(c));
                else if (digitsAsLetters && "0134578".IndexOf(c) is int at && at >= 0) folded.Append("oieastb"[at]);
            }
            return folded.ToString();
        }
    }
}

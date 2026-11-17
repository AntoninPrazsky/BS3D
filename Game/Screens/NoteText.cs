using System.Text;

namespace BS3D.Screens
{
    /// <summary>
    /// What a note's draft may hold and how a key edits it (#813), apart from <see cref="NotePage"/> so the phone's text
    /// dialog's answer (#1141) is held to the same rule as the keys without a device: the page owns what Enter and Escape
    /// mean and how the field is drawn, this owns the characters.
    /// </summary>
    internal static class NoteText
    {
        /// <summary>The highest code point typed: the end of Latin Extended-A, which both menu faces carry (see <c>Nickname</c>).</summary>
        private const char LAST_DRAWABLE = 'ſ';
        private const char MISSING_FROM_INTER = 'ŉ';

        /// <summary>A letter, digit, space or mark the menu's faces draw: Basic Latin, Latin-1 and Latin Extended-A.</summary>
        internal static bool IsTypable(char c) => c >= ' ' && c <= LAST_DRAWABLE && c != MISSING_FROM_INTER && !char.IsControl(c);

        /// <summary>
        /// One key into <paramref name="draft"/>: a carriage return or a line feed is a new line, Backspace takes the last
        /// character, and anything else goes in when the faces draw it. Nothing goes past <paramref name="maxLength"/>.
        /// False for a character refused, which changes nothing and is not an edit; Enter's and Escape's meanings are the page's.
        /// </summary>
        internal static bool Edit(StringBuilder draft, char character, int maxLength)
        {
            switch (character)
            {
                case '\r':
                case '\n':
                    if (draft.Length < maxLength) draft.Append('\n');
                    return true;

                case '\b':
                    if (draft.Length > 0) draft.Length--;
                    return true;

                default:
                    if (!IsTypable(character) || draft.Length >= maxLength) return false;
                    draft.Append(character);
                    return true;
            }
        }

        /// <summary>
        /// A phone's text dialog's answer in place of <paramref name="draft"/> (#1141): the whole text, each character by
        /// <see cref="Edit"/>'s rule, so the page lays the field out once rather than once a key. A carriage return and line
        /// feed together are one new line; Escape and Backspace inside it are not keys and do nothing.
        /// </summary>
        internal static void Replace(StringBuilder draft, string text, int maxLength)
        {
            draft.Clear();
            text ??= string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (character is '\b' or '\u001b' || (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n')) continue;
                Edit(draft, character, maxLength);
            }
        }
    }
}

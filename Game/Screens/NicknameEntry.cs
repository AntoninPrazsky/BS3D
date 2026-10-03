using BS3D.Online;
using Prazsky.Core.Tools;

namespace BS3D.Screens
{
    /// <summary>What one typed character did to a <see cref="NicknameEntry"/>.</summary>
    internal enum NicknameKey
    {
        /// <summary>A character no nickname may hold, or one past the longest name: nothing changed.</summary>
        Ignored,

        /// <summary>The draft changed (a letter, or Backspace), the caret is on again and any complaint is gone.</summary>
        Edited,

        /// <summary>Enter: the page decides what keeping the draft means.</summary>
        Enter,

        /// <summary>Escape: the page decides what dropping it means.</summary>
        Escape,
    }

    /// <summary>
    /// A nickname being typed (#548, #687, #763): the draft, the caret that blinks after it, and what is wrong with it
    /// - the one copy of the editing both pages that ask for a name share, the Settings page's Nickname row and the
    /// first-launch plate. The page owns what the keys <i>mean</i> (keeping a name turns the boards on from one and
    /// not from the other) and how the field is drawn; this owns the characters.
    /// <para>
    /// The keys arrive as the characters Windows sends (<see cref="BS3DGame"/>'s <c>Window.TextInput</c>), Enter,
    /// Escape and Backspace included, so one press cannot act twice. A name is validated only when kept
    /// (<see cref="TryKeep"/>), by <see cref="Nickname.TryNormalize"/>, which is the service's own rule.
    /// </para>
    /// <para>
    /// The caret is an underscore, because Anton's "|" reads as a lowercase L ("Karel|" was "Karell"), and it blinks,
    /// because a still mark is not a place to type. Its clock restarts on every edit, so it is always on while the
    /// player types.
    /// </para>
    /// </summary>
    internal sealed class NicknameEntry
    {
        internal const string CARET = "_";
        private const float CARET_PERIOD = 1.0f;

        private float _caretClock;

        /// <summary>What has been typed so far.</summary>
        internal string Draft { get; private set; } = string.Empty;

        /// <summary>What was wrong with the last attempt to keep the draft, or null.</summary>
        internal string Problem { get; private set; }

        /// <summary>Whether the caret is in its on half-period.</summary>
        internal bool CaretShown { get; private set; } = true;

        /// <summary>The draft with the caret after it when the caret is on - what a field shows.</summary>
        internal string Field => CaretShown ? Draft + CARET : Draft;

        /// <summary>The draft with the caret always in, which is what a face is chosen by so a blink cannot flip it.</summary>
        internal string FieldWithCaret => Draft + CARET;

        /// <summary>Starts typing from <paramref name="initial"/> (null: an empty draft), with the caret on and no complaint.</summary>
        internal void Begin(string initial)
        {
            Draft = initial ?? string.Empty;
            Problem = null;
            ShowCaret();
        }

        /// <summary>Drops a complaint without touching the draft.</summary>
        internal void ClearProblem() => Problem = null;

        /// <summary>Advances the caret's clock. Returns whether it changed side, so a label is rewritten twice a second and not every frame.</summary>
        internal bool Tick(float elapsedSeconds)
        {
            _caretClock += elapsedSeconds;

            bool shown = _caretClock % CARET_PERIOD < CARET_PERIOD * Constants.HALF;
            if (shown == CaretShown) return false;

            CaretShown = shown;
            return true;
        }

        /// <summary>Feeds one typed character in. See <see cref="NicknameKey"/>.</summary>
        internal NicknameKey Type(char character)
        {
            switch (character)
            {
                case '\r':
                    return NicknameKey.Enter;

                case '\x1b':
                    return NicknameKey.Escape;

                case '\b':
                    if (Draft.Length > 0) Draft = Draft[..^1];
                    break;

                default:
                    //A character no nickname may hold does nothing, and nothing past the longest name is taken
                    if (!Nickname.IsAllowed(character) || Draft.Length >= Nickname.MaxLength) return NicknameKey.Ignored;
                    Draft += character;
                    break;
            }

            Problem = null;
            ShowCaret();
            return NicknameKey.Edited;
        }

        /// <summary>
        /// Whether the draft is a name (<see cref="Nickname.TryNormalize"/>), and the name in its normal form when it
        /// is. When it is not, <see cref="Problem"/> says what is wrong, for the page to show under the field.
        /// </summary>
        internal bool TryKeep(out string name)
        {
            if (Nickname.TryNormalize(Draft, out name, out string problem))
            {
                Problem = null;
                return true;
            }

            Problem = problem;
            return false;
        }

        /// <summary>Whether the draft is a name, said without touching <see cref="Problem"/>.</summary>
        internal bool IsKeepable => Nickname.TryNormalize(Draft, out _, out _);

        private void ShowCaret()
        {
            _caretClock = 0f;
            CaretShown = true;
        }
    }
}

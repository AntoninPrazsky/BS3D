namespace BS3D.Screens
{
    /// <summary>
    /// The line under the note's field (#1149): what the hand in use does with the page, and the draft's length against
    /// its limit. Apart from <see cref="NotePage"/> so which hand gets which words is held without a device.
    /// <para>
    /// <b>A finger's line is a phone's</b>: the field opens the phone's typing dialog (<c>BS3DGame.StepPhoneTyping</c>),
    /// whose OK only puts the text in the field, so the page's Send is what sends it. <b>A keyboard's</b> are its keys.
    /// <b>A pad's</b> is the keyboard's with the sentence that it cannot type, since the keyboard is where a pad player
    /// has to go. Each is short (the owner, 2026-10-18), and the count is tied to its limit so a line never ends on the
    /// one and starts on the other.
    /// </para>
    /// </summary>
    internal static class NoteHint
    {
        internal enum Hand { Keyboard, Pad, Touch }

        /// <summary>The complaint that takes the line's place when Send finds nothing written.</summary>
        internal const string NOTHING_WRITTEN = "Write something first.";

        /// <summary>
        /// The hand the line speaks to: a finger's on a phone whatever else is held, since there nothing types but the
        /// phone's dialog; elsewhere the hand the menus last saw (<c>BS3DGame.PromptsForPad</c>, <c>PromptsForTouch</c>),
        /// the pad first, as the skip hint asks them.
        /// </summary>
        internal static Hand HandFor(bool promptsForPad, bool promptsForTouch, bool onPhone) =>
            onPhone ? Hand.Touch : promptsForPad ? Hand.Pad : promptsForTouch ? Hand.Touch : Hand.Keyboard;

        /// <summary>The line for <paramref name="hand"/> with <paramref name="length"/> of <paramref name="maxLength"/> characters written.</summary>
        internal static string Line(Hand hand, int length, int maxLength)
        {
            string count = $"{length}{SpaceWrap.TIE}/{SpaceWrap.TIE}{maxLength}.";
            return hand switch
            {
                Hand.Touch => $"Tap the field to type, then tap Send. {count}",
                Hand.Pad => $"Enter sends. Shift+Enter starts a new line. {count} A pad cannot type.",
                _ => $"Enter sends. Shift+Enter starts a new line. {count}",
            };
        }
    }
}

using System.Collections.Generic;

namespace BS3D.Screens
{
    /// <summary>
    /// The line under the note's field (#1149): what the hand in use does with the page, and the draft's length against
    /// its limit. Apart from <see cref="NotePage"/> so which hand gets which words is held without a device.
    /// <para>
    /// <b>A finger's line is a phone's</b>: there the field opens the phone's typing dialog (<c>BS3DGame.StepPhoneTyping</c>),
    /// whose OK only puts the text in the field, so the page's Send is what sends it. Nowhere else does a tap on the field
    /// open anything, so a finger on another touch screen gets the keyboard's line: a keyboard is what types there.
    /// <b>A keyboard's</b> are its keys. <b>A pad's</b> is the keyboard's with the sentence that it cannot type, since the
    /// keyboard is where a pad player has to go. Each is short (the owner, 2026-10-18), and the count is tied to its limit
    /// so a line never ends on the one and starts on the other.
    /// </para>
    /// </summary>
    internal static class NoteHint
    {
        internal enum Hand { Keyboard, Pad, Touch }

        /// <summary>The complaint that takes the line's place when Send finds nothing written.</summary>
        internal const string NOTHING_WRITTEN = "Write something first.";

        //What HandFor can give with the phone's hand and without it
        private static readonly Hand[] PHONE_HANDS = { Hand.Touch };
        private static readonly Hand[] OTHER_HANDS = { Hand.Keyboard, Hand.Pad };

        /// <summary>
        /// The hand the line speaks to. With <paramref name="phoneHand"/> (the phone, or the <c>touch</c> argument laying
        /// the phone's hand out on a monitor, <c>BS3DGame.TouchPinned</c>) a finger's whatever else is held, since on a
        /// phone nothing types but its dialog. Elsewhere the pad's if the menus last saw a pad (<c>BS3DGame.PromptsForPad</c>),
        /// and otherwise the keyboard's, a finger's included: there only a keyboard types.
        /// </summary>
        internal static Hand HandFor(bool promptsForPad, bool phoneHand) =>
            phoneHand ? Hand.Touch : promptsForPad ? Hand.Pad : Hand.Keyboard;

        /// <summary>
        /// Every hand <see cref="HandFor"/> can give for <paramref name="phoneHand"/>, and no other: what the line's height
        /// is measured for, so a phone keeps no row for a line it never shows.
        /// </summary>
        internal static IReadOnlyList<Hand> HandsFor(bool phoneHand) => phoneHand ? PHONE_HANDS : OTHER_HANDS;

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

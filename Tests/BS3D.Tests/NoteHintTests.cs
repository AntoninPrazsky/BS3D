using BS3D.Online;
using BS3D.Screens;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #1149: on the phone the note page's line read "Enter sends. Shift+Enter starts a new line. … A pad cannot type." -
    /// a keyboard's words on a touch screen, where the note is typed through the phone's dialog and sent with the page's
    /// button. Held here: which hand each set of flags gets (a phone always a finger's, elsewhere the pad if last seen and
    /// otherwise the keyboard), that the height is measured for exactly those, and that each hand's line names its own
    /// way of typing and sending and no other's.
    /// </summary>
    public class NoteHintTests
    {
        private const int MAX = OnlineNotes.MaxTextLength;

        [Theory]
        //A phone types only through its dialog, whatever the menus last saw
        [InlineData(false, true, "Touch")]
        [InlineData(true, true, "Touch")]
        //Elsewhere the pad if it was last seen, otherwise the keyboard's - a finger's too, since a tap on the field opens
        //nothing off the phone: HandFor is not even told about a finger there
        [InlineData(true, false, "Pad")]
        [InlineData(false, false, "Keyboard")]
        public void Each_hand_gets_its_line(bool promptsForPad, bool phoneHand, string expected) =>
            Assert.Equal(expected, NoteHint.HandFor(promptsForPad, phoneHand).ToString());

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void The_height_is_measured_for_every_line_the_platform_shows_and_no_other(bool phoneHand)
        {
            //The hint's height is the tallest of HandsFor's lines: one HandFor cannot give is a row a phone keeps empty,
            //one it can give and HandsFor misses is Send moving when that hand is picked up
            var given = new SortedSet<NoteHint.Hand>
            {
                NoteHint.HandFor(promptsForPad: false, phoneHand),
                NoteHint.HandFor(promptsForPad: true, phoneHand),
            };

            Assert.Equal(given, new SortedSet<NoteHint.Hand>(NoteHint.HandsFor(phoneHand)));
        }

        [Fact]
        public void A_finger_is_told_to_tap_the_field_and_Send_and_no_key()
        {
            string line = NoteHint.Line(NoteHint.Hand.Touch, 42, MAX);

            Assert.Contains("Tap the field", line, StringComparison.Ordinal);
            Assert.Contains("tap Send", line, StringComparison.Ordinal);
            Assert.DoesNotContain("Enter", line, StringComparison.Ordinal);
            Assert.DoesNotContain("pad", line, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Count(42), line, StringComparison.Ordinal);
        }

        [Fact]
        public void A_keyboard_is_told_its_keys_and_nothing_of_a_pad()
        {
            string line = NoteHint.Line(NoteHint.Hand.Keyboard, 42, MAX);

            Assert.StartsWith("Enter sends. Shift+Enter starts a new line.", line, StringComparison.Ordinal);
            Assert.DoesNotContain("pad", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Tap", line, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Count(42), line, StringComparison.Ordinal);
        }

        [Fact]
        public void A_pad_is_told_it_cannot_type_and_the_keys_that_can()
        {
            string line = NoteHint.Line(NoteHint.Hand.Pad, 42, MAX);

            Assert.Equal($"Enter sends. Shift+Enter starts a new line. {Count(42)} A pad cannot type.", line);
        }

        [Theory]
        [InlineData("Keyboard")]
        [InlineData("Pad")]
        [InlineData("Touch")]
        public void The_count_is_the_drafts_and_tied_to_its_limit(string name)
        {
            NoteHint.Hand hand = Enum.Parse<NoteHint.Hand>(name);

            //Tied so a line never ends on the length and starts on "/ 1000" (#813, seen at 1080p)
            Assert.Contains(Count(0), NoteHint.Line(hand, 0, MAX), StringComparison.Ordinal);
            Assert.Contains(Count(MAX), NoteHint.Line(hand, MAX, MAX), StringComparison.Ordinal);
            Assert.DoesNotContain(" / ", NoteHint.Line(hand, 7, MAX), StringComparison.Ordinal);
        }

        private static string Count(int length) => $"{length}{SpaceWrap.TIE}/{SpaceWrap.TIE}{MAX}.";
    }
}

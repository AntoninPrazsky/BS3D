using BS3D.Screens;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The author's note on the Raspberry Pi build's first launch (#1083): its copy set by the typesetting rules, everything
    /// the owner asked it to say still in it, and the gates that decide whether it goes up.
    /// </summary>
    public class WelcomeNoteTests
    {
        //The game's title as the page inserts it (BS3DGame.GAME_TITLE, which this project does not compile)
        private const string TITLE = "Bubble Shooter 3D";

        [Fact]
        public void Every_string_is_set_by_the_rules()
        {
            foreach (string text in WelcomeNote.Copy(TITLE))
                Assert.Null(CopyRules.Fault(text) is { } fault ? $"'{text}': {fault}" : null);
        }

        /// <summary>
        /// What the owner asked the note to say (2026-11-10): an early release still in development, one person's project,
        /// the server a Raspberry Pi, bugs possible, thanks for the download and the play, and reports through the note
        /// entry - whose words must be the entry's own (BS3DGame.NOTE_ENTRY), since the page sets them strong by name.
        /// </summary>
        [Theory]
        [InlineData("very first releases")]
        [InlineData("still being made")]
        [InlineData("one person’s own project")]
        [InlineData("server behind the online scores is a Raspberry Pi")]
        [InlineData("bugs")]
        [InlineData("Write to the Author")]
        [InlineData("Thank you for downloading the game and for playing it")]
        public void The_note_says_what_the_owner_asked(string words)
        {
            //A tie is drawn as a space
            string note = string.Join("\n", WelcomeNote.Paragraphs(TITLE)).Replace(SpaceWrap.TIE, ' ').Replace(' ', ' ');

            Assert.True(note.Contains(words, StringComparison.Ordinal), $"the note no longer says '{words}'");
        }

        [Theory]
        //The Pi's first launch, a player at it: the note
        [InlineData(true, false, false, false, true)]
        //Shown on an earlier launch, or a scripted run, or another build: no note
        [InlineData(true, true, false, false, false)]
        [InlineData(true, false, true, false, false)]
        [InlineData(false, false, false, false, false)]
        //The welcome argument: always, on any build, shown before or not, scripted or not
        [InlineData(false, true, true, true, true)]
        [InlineData(true, true, false, true, true)]
        public void It_shows_once_on_the_Pi_or_when_asked(bool piBuild, bool shownBefore, bool scripted, bool forced, bool shows)
        {
            Assert.Equal(shows, WelcomeNote.ShouldShow(piBuild, shownBefore, scripted, forced));
        }
    }
}

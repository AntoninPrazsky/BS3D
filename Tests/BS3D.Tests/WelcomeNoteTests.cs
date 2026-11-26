using BS3D.Screens;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The author's note on the game's first launch (#1083, on every build since #1255): its copy set by the typesetting
    /// rules on every build, everything the owner asked it to say still in it, and the gates that decide whether it goes up.
    /// </summary>
    public class WelcomeNoteTests
    {
        //The game's title as the page inserts it (BS3DGame.GAME_TITLE, which this project does not compile)
        private const string TITLE = "Bubble Shooter 3D";

        //The three builds by name: a public test method cannot take the internal enum
        public static TheoryData<string> Builds => new(Enum.GetNames<AboutCopy.Build>());

        [Theory]
        [MemberData(nameof(Builds))]
        public void Every_string_is_set_by_the_rules(string build)
        {
            foreach (string text in WelcomeNote.Copy(TITLE, Enum.Parse<AboutCopy.Build>(build)))
                Assert.Null(CopyRules.Fault(text) is { } fault ? $"'{text}': {fault}" : null);
        }

        /// <summary>
        /// What the owner asked the note to say (2026-11-10): an early release still in development, one person's project,
        /// bugs possible, thanks for the download and the play, and reports through the note entry - whose words must be
        /// the entry's own (BS3DGame.NOTE_ENTRY), since the page sets them strong by name. Every build says all of it
        /// (#1255); the Raspberry Pi is the next test's.
        /// </summary>
        [Theory]
        [InlineData("very first releases")]
        [InlineData("still being made")]
        [InlineData("one person’s own project")]
        [InlineData("bugs")]
        [InlineData("Write to the Author")]
        [InlineData("Thank you for downloading the game and for playing it")]
        public void The_note_says_what_the_owner_asked(string words)
        {
            foreach (AboutCopy.Build build in Enum.GetValues<AboutCopy.Build>())
                Assert.True(Note(build).Contains(words, StringComparison.Ordinal), $"the {build} note no longer says '{words}'");
        }

        /// <summary>
        /// The Raspberry Pi is the Pi build's alone (the owner, 2026-11-26, #1255): its player is told the score server is
        /// a machine like theirs, and no other build's note names a Raspberry Pi at all.
        /// </summary>
        [Theory]
        [InlineData("Windows", false)]
        [InlineData("Linux", true)]
        [InlineData("Android", false)]
        public void Only_the_Pi_names_the_Raspberry_Pi(string build, bool pi)
        {
            string note = Note(Enum.Parse<AboutCopy.Build>(build));

            Assert.Equal(pi, note.Contains("Raspberry", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(pi, note.Contains("server behind the online scores is a Raspberry Pi, much like the one you are "
                + "playing on", StringComparison.Ordinal));
        }

        [Theory]
        //A first launch, a player at it: the note, on every build (#1255)
        [InlineData(false, false, false, true)]
        //Shown on an earlier launch, or a scripted run: no note
        [InlineData(true, false, false, false)]
        [InlineData(false, true, false, false)]
        //The welcome argument: always, shown before or not, scripted or not
        [InlineData(true, true, true, true)]
        [InlineData(true, false, true, true)]
        public void It_shows_once_or_when_asked(bool shownBefore, bool scripted, bool forced, bool shows)
        {
            Assert.Equal(shows, WelcomeNote.ShouldShow(shownBefore, scripted, forced));
        }

        //The note as a player reads it: a tie is drawn as a space
        private static string Note(AboutCopy.Build build) =>
            string.Join("\n", WelcomeNote.Paragraphs(TITLE, build)).Replace(SpaceWrap.TIE, ' ');
    }
}

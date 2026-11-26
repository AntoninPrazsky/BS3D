namespace BS3D.Screens
{
    /// <summary>
    /// The author's note a player meets on the game's first launch (#1083, #1255), and when it shows. It is kept apart
    /// from <c>WelcomePage</c>, the way <see cref="AboutCopy"/> is from the About page, so a test can hold the copy to the
    /// typesetting rules (<see cref="CopyRules"/>) and the decision to its gates without a graphics device.
    /// <para>
    /// The owner's request (2026-11-10, for the Pi-Apps release, #858): a personal note after the first launch. It says
    /// that the game is an early release and still in development, that it is one person's project, that the score server
    /// is itself a Raspberry Pi and that the game may have bugs. It thanks the player for downloading and playing, and asks
    /// for testing and reports through Write to the Author. <b>Since #1255 (2026-11-26, v0.7.0, the first release on
    /// itch.io) every build shows it</b>: a stranger from a storefront is the player who most needs to know what the game
    /// is before a bug tells them. The Raspberry Pi is the Pi build's alone (the owner, the same day): everywhere else the
    /// note has no word of it.
    /// </para>
    /// </summary>
    internal static class WelcomeNote
    {
        /// <summary>The page's heading, in the display face's capitals.</summary>
        internal const string HEADING = "HELLO, AND THANK YOU";

        /// <summary>The note as <see cref="AboutCopy.ThisBuild"/> says it: <see cref="Paragraphs(string, AboutCopy.Build)"/>.</summary>
        internal static string[] Paragraphs(string title) => Paragraphs(title, AboutCopy.ThisBuild);

        /// <summary>
        /// The note, a paragraph each. "Write to the Author" in the paragraph that asks for reports is the entry under
        /// them (<c>BS3DGame.NOTE_ENTRY</c>), set strong by the page so the sentence points at it. <b>The Raspberry Pi
        /// paragraph is the Pi build's alone</b> (the owner, 2026-11-26, #1255): its player is playing on the same kind
        /// of machine the score server is, and on every other build the note says nothing of it.
        /// </summary>
        internal static string[] Paragraphs(string title, AboutCopy.Build build)
        {
            string project = title.Replace(' ', SpaceWrap.TIE) + " is one person’s own project, and it is still being made. "
                + "This is one of its very first releases, so you may run into bugs.";
            const string reports = "If something breaks, looks wrong or could be better, please Write to the Author — it "
                + "comes straight to me, and every note is read. Your testing and your reports help more than anything.";
            const string thanks = "Thank you for downloading the game and for playing it.";

            return build == AboutCopy.Build.Linux
                ? new[] { project, "Even the server behind the online scores is a Raspberry Pi, much like the one you are "
                    + "playing on.", reports, thanks }
                : new[] { project, reports, thanks };
        }

        /// <summary>The sign-off under the note.</summary>
        internal const string SIGNATURE = "— Antonín";

        /// <summary>The entry that closes the page, which is also what Escape and the pad's B do.</summary>
        internal const string PLAY = "Let’s play";

        /// <summary>
        /// Whether the note goes up on this run: not shown before, and not in a run a script drives, which has nobody to
        /// read it (the nickname question's gate, #763). Every build alike since #1255, so a player updating from a version
        /// that never showed it off the Pi meets it once. The <c>welcome</c> argument puts it up regardless, so it can be
        /// photographed in a scripted run.
        /// </summary>
        internal static bool ShouldShow(bool shownBefore, bool scripted, bool forced) =>
            forced || (!shownBefore && !scripted);

        /// <summary>
        /// Every string the page draws of its own on <paramref name="build"/>, for the test that holds them to
        /// <see cref="CopyRules"/>.
        /// </summary>
        internal static string[] Copy(string title, AboutCopy.Build build)
        {
            string[] paragraphs = Paragraphs(title, build);
            string[] copy = new string[paragraphs.Length + 3];

            copy[0] = HEADING;
            paragraphs.CopyTo(copy, 1);
            copy[^2] = SIGNATURE;
            copy[^1] = PLAY;
            return copy;
        }
    }
}

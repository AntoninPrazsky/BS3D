namespace BS3D.Screens
{
    /// <summary>
    /// <b>The level's play clock</b> (#546): the seconds the player had the gun, which the score service is sent as a level's
    /// duration and which time in the score will be measured by (#1045). Counted where the frame steps the world, so a pause,
    /// an unfocused window and the page over a finished level never reach it.
    /// <para>
    /// <b>It stands still while the camera is taken over</b> (#1045, the owner's ruling 2026-11-02: "the clock never runs
    /// while the camera follows the falling balls"): the drop cinematic, whether a shot or a cut set it off, and the chapter's
    /// tour, which holds the cluster's release besides (#690). Neither is the player's time: the gun does not answer while
    /// the lens is elsewhere, and a drop cinematic is up to <c>DropCinematic.MAX_SECONDS</c> a time, which a fast clear would
    /// otherwise pay for its own reward. The line's loss and the unused balls' volley are takeovers too, but they come after
    /// the level is decided and its seconds frozen (<c>LevelRun.EndSeconds</c>). What does not stop it: the overview's focus
    /// (#1006) and a cut's lens pinch, under which the player keeps the gun.
    /// </para>
    /// </summary>
    internal static class PlayClock
    {
        /// <summary>
        /// The seconds a frame of <paramref name="elapsed"/> adds to the play clock: all of them, or none while the cluster's
        /// release waits for the chapter's tour (<paramref name="releasePending"/>) or a camera takeover has the lens
        /// (<paramref name="cameraTakeover"/>).
        /// </summary>
        public static float Counts(float elapsed, bool releasePending, bool cameraTakeover) =>
            releasePending || cameraTakeover ? 0f : elapsed;
    }
}

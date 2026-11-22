using BS3D.Effects;

namespace BS3D.Screens
{
    /// <summary>
    /// Whether the cleared beat's countdown stands still this frame: the one rule <c>GameplayScreen</c> asks before it counts
    /// a clear down to its result page, kept out of the screen so <c>SpareVolleyTests</c> can hold it (#1198).
    /// <para>
    /// A camera takeover still playing out holds the beat: the drop cinematic, a chapter intro, the line loss, and the unused
    /// balls' volley while it counts (<see cref="SpareVolley.Running"/>). <b>Not the volley's lens once the count is done.</b>
    /// That lens is held beside the gun until a new level's <see cref="SpareVolley.Reset"/>, so <see cref="SpareVolley.Engaged"/>,
    /// and the screen's <c>CameraTakeoverEngaged</c> with it, stays true to the result page and past it: a countdown waiting
    /// on either would never end, and every clear with unused balls would be left without its page.
    /// </para>
    /// </summary>
    internal static class ClearedBeatCountdown
    {
        /// <summary>True while the beat must wait: any of the three takeovers engaged, or the volley still counting.</summary>
        internal static bool Waits(bool dropCinematicEngaged, bool chapterIntroEngaged, bool lineLossEngaged, SpareVolley volley) =>
            dropCinematicEngaged || chapterIntroEngaged || lineLossEngaged || volley.Running;
    }
}

using System;

namespace BS3D.Screens
{
    /// <summary>
    /// When the benchmark may run, and what its page says about the run and about what leaves the machine (#988, #1041).
    /// Pure, so a test holds the rule and the copy (<c>BenchmarkConsentTests</c>) without the page's Myra tree.
    /// <para>
    /// <b>Online scores on are the consent</b> (the owner, 2026-10-29): the benchmark's whole point is the result that
    /// reaches the author, and the Online switch is the one place a player says the game may talk to the server. So a run
    /// starts only with them on, from Extras &gt; Benchmark and from the <c>benchmark</c> argument alike, and with them off
    /// (or never answered) Start says so over a door to Settings. <c>benchmarkplay</c>, testing only, is never saved or
    /// sent, and needs no consent.
    /// </para>
    /// <para>
    /// <b>A phone is called a phone</b> (#1183): the page said "your computer" and "the skip key" on a phone, where there is
    /// neither, so every sentence that names the machine or the key that stops a run takes <c>phone</c>, which the page sets
    /// on a phone and in the desktop's emulation of one (the phone layout, <c>inches=</c>), where a tap is what stops a run.
    /// </para>
    /// </summary>
    internal static class BenchmarkConsent
    {
        /// <summary>
        /// Whether a run may start: online scores on (<paramref name="online"/> is the setting, <c>null</c> when never
        /// answered, which is off), or the play alone, which sends nothing.
        /// </summary>
        internal static bool MayRun(bool? online, bool playOnly) => playOnly || online == true;

        //The machine as the page names it
        private static string Machine(bool phone) => phone ? "phone" : "computer";

        /// <summary>
        /// What the page says a run does, before one: <paramref name="tours"/> scenes at the player's
        /// <paramref name="quality"/>, the play, the swarm and the physics, and what stops it - a tap on a phone, the skip
        /// key elsewhere.
        /// </summary>
        internal static string Intro(int tours, string quality, bool phone) => FormattableString.Invariant(
            $"Flies through {tours} of the most demanding scenes at your quality ({quality}), with the frame rate unlocked, then plays the biggest level: the city’s opening flight to the cannon, and the cannon firing by itself. Then a storm of balls grows until your {Machine(phone)} can no longer draw it at 60 FPS. It measures every frame, then times the physics. About two and a half minutes. {(phone ? "A tap stops it." : "The skip key stops it.")}");

        /// <summary>What the page says before a run about what leaves the machine.</summary>
        internal static string SentNote(bool phone) => phone
            ? "When it finishes, the result and your phone’s specification (graphics chip, processor, memory, system and screen) go "
              + "to the author, to see how the game runs on phones like yours. Nothing that names you goes with them."
            : "When it finishes, the result and your computer’s specification (graphics card, processor, memory, system and screen) "
              + "go to the author, to see how the game runs on computers like yours. Nothing that names you goes with them.";

        /// <summary>What the page says once the result has reached the author.</summary>
        internal static string Sent(bool phone) => $"Sent to the author with your {Machine(phone)}’s specification. Thank you!";

        /// <summary>What Start says with online scores off, over the door to Settings (which opens on ONLINE).</summary>
        internal const string OFFLINE_NOTE = "The benchmark sends its result to the author, so it runs only with online scores on. "
            + "Turn them on in Settings, then come back to start it.";
    }
}

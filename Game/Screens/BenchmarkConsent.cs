namespace BS3D.Screens
{
    /// <summary>
    /// When the benchmark may run, and what its page says about what leaves the machine (#988, #1041). Pure, so a test holds
    /// the rule and the copy (<c>BenchmarkConsentTests</c>) without the page's Myra tree.
    /// <para>
    /// <b>Online scores on are the consent</b> (the owner, 2026-10-29): the benchmark's whole point is the result that
    /// reaches the author, and the Online switch is the one place a player says the game may talk to the server. So a run
    /// starts only with them on, from Extras &gt; Benchmark and from the <c>benchmark</c> argument alike, and with them off
    /// (or never answered) Start says so over a door to Settings. <c>benchmarkplay</c>, testing only, is never saved or
    /// sent, and needs no consent.
    /// </para>
    /// </summary>
    internal static class BenchmarkConsent
    {
        /// <summary>
        /// Whether a run may start: online scores on (<paramref name="online"/> is the setting, <c>null</c> when never
        /// answered, which is off), or the play alone, which sends nothing.
        /// </summary>
        internal static bool MayRun(bool? online, bool playOnly) => playOnly || online == true;

        /// <summary>What the page says before a run about what leaves the machine.</summary>
        internal const string SENT_NOTE = "When it finishes, the result and your computer’s specification (graphics card, processor, memory, "
            + "system and screen) go to the author, to see how the game runs on computers like yours. Nothing that names you goes with them.";

        /// <summary>What Start says with online scores off, over the door to Settings (which opens on ONLINE).</summary>
        internal const string OFFLINE_NOTE = "The benchmark sends its result to the author, so it runs only with online scores on. "
            + "Turn them on in Settings, then come back to start it.";
    }
}

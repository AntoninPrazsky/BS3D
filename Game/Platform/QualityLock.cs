namespace BS3D.Platform
{
    /// <summary>
    /// The quality tier this build is held at, which the player cannot change (#788): none here, in the Windows
    /// build, where the Quality row, the probe and <c>quality=</c> decide as they always have. GamePi brings its own
    /// file of this name answering <see cref="QualityLevel.Potato"/>, the Raspberry Pi's only tier (#785) - a
    /// platform seam like <see cref="CrashDialog"/>'s rather than an <c>#if</c>, so neither build compiles the
    /// other's answer and the Windows one stays what it was.
    /// <para>
    /// <b>Unless the run was started with <c>potato</c> (#808).</b> The owner wanted to see on a desktop what the game
    /// looks like on the Pi, and Potato is not a row of the Quality cycle that a running game can step to: it is a
    /// different renderer, chosen when the process starts (no scene target, no backdrop, its own effects - see
    /// <c>BS3DGame.Potato.cs</c>). So the argument holds this run at Potato exactly as GamePi is held, the Settings rows
    /// read "locked" as they do there, and nothing a player does inside the game ever arrives here: the probe stops at
    /// Low, and a run without the argument is the Windows build it always was.
    /// </para>
    /// </summary>
    internal static class QualityLock
    {
        /// <summary>The tier this run is locked to, or null where the player picks.</summary>
        internal static QualityLevel? Tier { get; private set; }

        /// <summary>Why the tier is held, for the line the run prints when it is.</summary>
        internal static string Why => "held for this run by the potato argument";

        /// <summary>
        /// Holds this run at <see cref="QualityLevel.Potato"/>: the <c>potato</c> argument. Called once, by
        /// <c>Program.Main</c>, before the game object exists - everything that asks <c>BS3DGame.PotatoPath</c> asks
        /// after that. True: this build was something else until now, and the run takes its effects from the Potato
        /// set beside the desktop's (<c>PotatoContent</c>).
        /// </summary>
        internal static bool HoldAtPotato()
        {
            Tier = QualityLevel.Potato;

            return true;
        }
    }
}

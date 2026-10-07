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
    /// read "locked" as they do there, and the probe stops at Low.
    /// </para>
    /// <para>
    /// <b>Or unless the player chose Potato on the Quality row (#808, the owner's verdict of 2026-10-07).</b> The choice
    /// is stored and read at the next start (<see cref="ChooseAtPotato"/>): the run draws through the same renderer, but
    /// <see cref="ChosenInSettings"/> leaves the row the player's, and the tier picked there takes effect at the start after.
    /// </para>
    /// </summary>
    internal static class QualityLock
    {
        /// <summary>The tier this run's renderer is held at, or null where the player picks.</summary>
        internal static QualityLevel? Tier { get; private set; }

        /// <summary>
        /// Whether that tier is the player's own choice from the Quality row (#808, the owner's verdict of 2026-10-07),
        /// read from <c>Settings.json</c> at start: the run draws through the Potato renderer, but the row is not locked,
        /// and a tier picked on it takes effect at the next start. False for the <c>potato</c> argument, which holds the
        /// run as GamePi is held.
        /// </summary>
        internal static bool ChosenInSettings { get; private set; }

        /// <summary>Why the tier is held, for the line the run prints when it is.</summary>
        internal static string Why => ChosenInSettings
            ? "chosen in Settings, so this run draws through the Raspberry Pi's renderer"
            : "held for this run by the potato argument";

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

        /// <summary>
        /// The same renderer for a player who chose Potato on the Quality row (#808): called by <c>Program.Main</c> when
        /// <c>Settings.json</c> names it and nothing on the command line says otherwise. The row stays the player's.
        /// </summary>
        internal static bool ChooseAtPotato()
        {
            ChosenInSettings = true;

            return HoldAtPotato();
        }
    }
}

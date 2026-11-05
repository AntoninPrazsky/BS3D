namespace BS3D.Platform
{
    /// <summary>
    /// The Android build's namesake of <c>Game/Platform/QualityLock.cs</c> (#1048): held at Potato, as GamePi is, unless
    /// the run was started with <c>desktop</c> (#1059). Then it draws through the desktop's renderer with the shared
    /// effects compiled for OpenGL ES (#1057), and the tier is the player's to pick as on Windows. A switch while the
    /// path is young; the phone's tiers and Auto quality decide it once they exist (#1060).
    /// </summary>
    internal static class QualityLock
    {
        internal static QualityLevel? Tier { get; private set; } = QualityLevel.Potato;

        internal static string Why => "this build's only tier until the desktop path is ready (desktop, #1059)";

        internal static bool HoldAtPotato() => false;

        internal static bool ChosenInSettings => false;

        internal static bool ChooseAtPotato() => false;

        /// <summary>
        /// The <c>desktop</c> argument (#1059): this run draws through the desktop's renderer. Called once, by
        /// <c>Program.Start</c>, before the game object exists, as the Windows build's <c>potato</c> is.
        /// </summary>
        internal static bool LeavePotato()
        {
            Tier = null;

            return true;
        }
    }
}

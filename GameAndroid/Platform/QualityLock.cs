namespace BS3D.Platform
{
    /// <summary>
    /// The Android build's namesake of <c>Game/Platform/QualityLock.cs</c> (#1048): held at Potato, as GamePi is. The
    /// desktop's effects are in the package compiled for OpenGL ES (#1057), but nothing draws through them on a phone
    /// yet: the desktop path at the phone's tiers is #1059 and #1060.
    /// </summary>
    internal static class QualityLock
    {
        internal static QualityLevel? Tier => QualityLevel.Potato;

        internal static string Why => "this build's only tier";

        internal static bool HoldAtPotato() => false;

        internal static bool ChosenInSettings => false;

        internal static bool ChooseAtPotato() => false;
    }
}

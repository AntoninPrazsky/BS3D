namespace BS3D.Platform
{
    /// <summary>
    /// The Android build's namesake of <c>Game/Platform/QualityLock.cs</c> (#1048): held at Potato, as GamePi is. The
    /// phone's MonoGame backend is OpenGL ES through MojoShader, which stops at Shader Model 3.0, so the desktop's
    /// effects do not exist here and Potato is the one renderer there is.
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

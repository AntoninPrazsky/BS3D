namespace BS3D.Platform
{
    /// <summary>
    /// Whether this device's GPU can carry the desktop's renderer (#1064). Always here, on Windows and the Pi: the Pi is
    /// held at Potato for its own reasons (<c>QualityLock</c>), and a Windows GPU has float render targets. GameAndroid
    /// brings its own file of this name, which asks the phone's OpenGL ES before MonoGame makes its context.
    /// </summary>
    internal static class GpuCaps
    {
        /// <summary>True when the desktop's renderer can run; otherwise false and <paramref name="why"/> says what is
        /// missing. <paramref name="hidden"/> is <c>gpumissing=</c>, which only the Android build reads.</summary>
        internal static bool DesktopPath(string hidden, out string why)
        {
            why = null;
            return true;
        }
    }
}

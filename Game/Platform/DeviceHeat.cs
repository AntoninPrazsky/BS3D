namespace BS3D.Platform
{
    /// <summary>
    /// How hot the device says it is (#1060), on Android's scale: 0 none, 1 light, 2 moderate, 3 severe (the system
    /// throttles), 4 critical and up. Auto quality reads it on a phone, where the frame rate falls only after the heat
    /// has built (#1061: Medium at 1080 rows went from 53 to 38 FPS in minute 7). Nothing reports it here, on Windows and
    /// the Pi: always 0. GameAndroid brings its own file of this name, which asks the PowerManager.
    /// </summary>
    internal static class DeviceHeat
    {
        internal const int MODERATE = 2;
        internal const int SEVERE = 3;

        /// <summary>The device's thermal status now.</summary>
        internal static int Status => 0;
    }
}

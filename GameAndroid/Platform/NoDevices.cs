using System;
using BS3D.Lighting;
using Microsoft.Xna.Framework;

namespace BS3D.Platform
{
    /// <summary>
    /// The Android build's namesakes of the Windows files whose devices a phone does not have (#1048): an Xbox pad's
    /// trigger motors through <c>Windows.Gaming.Input</c> (the rumble mixer falls back to MonoGame's own
    /// <c>SetVibration</c>, as on the Pi), a backlit keyboard's lamps, and a window icon (the launcher's icon is the
    /// APK's).
    /// </summary>
    internal static class PadMotors
    {
        internal static bool HasPad => false;

        internal static void Start() { }

        internal static bool TrySet(float leftMotor, float rightMotor, float leftTrigger, float rightTrigger) => false;
    }

    /// <inheritdoc cref="PadMotors"/>
    internal static class KeyboardLights
    {
        internal static KeyLightLayout Layout => null;

        internal static bool NoneFound => true;

        internal static void Start() { }

        internal static void SetWanted(bool wanted) { }

        internal static void Submit(Vector3[] lamps, bool resend) { }
    }

    /// <inheritdoc cref="PadMotors"/>
    internal static class WindowIcon
    {
        internal static void Apply(IntPtr windowHandle) { }
    }

    /// <summary>
    /// The crash notice (#570): the run log already holds the report, and an activity that is ending has nothing to show
    /// a dialog on, so it goes to the system log (logcat) as well.
    /// </summary>
    internal static class CrashDialog
    {
        internal static void Show(string message) => Android.Util.Log.Error("BS3D", message);
    }

    /// <summary>
    /// MonoGame's Android backend plays through OpenAL Soft, as DesktopGL does on the Pi, so a voice is placed only
    /// after it plays (#790, GamePi's namesake).
    /// </summary>
    internal static class AudioBackend
    {
        internal static bool PlacesAfterPlay => true;
    }
}

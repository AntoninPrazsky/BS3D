namespace BS3D.Platform
{
    /// <summary>
    /// GamePi's <c>Game/Platform/PadMotors.cs</c>: Windows.Gaming.Input, the route to an Xbox pad's trigger motors (#188),
    /// is Windows-only, so here no pad ever answers and <see cref="GamepadRumble"/> drives the two body motors through
    /// MonoGame's own <c>SetVibration</c>, which DesktopGL hands to SDL. Same names and members, so the mixer calls it
    /// unchanged.
    /// </summary>
    internal static class PadMotors
    {
        /// <summary>Never: there is no trigger route on Linux.</summary>
        internal static bool HasPad => false;

        /// <summary>Nothing to start.</summary>
        internal static void Start() { }

        /// <summary>Always false: the caller takes the body motors' route.</summary>
        internal static bool TrySet(float leftMotor, float rightMotor, float leftTrigger, float rightTrigger) => false;
    }
}

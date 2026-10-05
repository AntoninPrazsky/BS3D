using System;
using Windows.Gaming.Input;

namespace BS3D.Platform
{
    /// <summary>
    /// All four of an Xbox One/Series pad's motors through Windows.Gaming.Input (#188): the two in the body and the two
    /// impulse motors in the triggers. XInput, which MonoGame reads and vibrates the pad through, has no trigger motors at
    /// all — its <c>XINPUT_VIBRATION</c> is the two body speeds, and MonoGame's four-argument <c>SetVibration</c> drops the
    /// other two on WindowsDX — so <see cref="GamepadRumble"/> sends all four through here when a pad answers here, and
    /// falls back to XInput's two when none does (a pad WGI does not list as a <see cref="Gamepad"/>, or WGI itself
    /// missing).
    /// <para>
    /// <b>Tried on the owner's pad before it was built</b> (2026-10-05, an Xbox One S on USB, from an unpackaged exe as
    /// this one is): every motor felt, the triggers included. WGI is foreground-only by design, which costs nothing here:
    /// the mixer is already silent whenever the window is not active. Not tried: Bluetooth, where impulse triggers have
    /// historically not worked on a PC — the body motors still go through this path there.
    /// </para>
    /// <para>
    /// The pad is kept from WGI's own added/removed events rather than read off <see cref="Gamepad.Gamepads"/> per frame,
    /// which builds a fresh vector view on every call. The events arrive on a worker thread, so the one field they write
    /// is volatile, and a Win32 process sees the list empty at first and the pad arrive by event tens of milliseconds
    /// later (measured), which is why <see cref="Start"/> runs at startup and not on the first shot.
    /// </para>
    /// <para>
    /// Windows-only by construction, so GamePi compiles its own namesake (<c>GamePi/Platform/PadMotors.cs</c>), which
    /// knows no pad and leaves every frame to the XInput route MonoGame's DesktopGL maps onto SDL.
    /// </para>
    /// </summary>
    internal static class PadMotors
    {
        private static volatile Gamepad _pad;
        private static bool _started;

        /// <summary>Whether a pad answers here, so the mixer can say once which route it takes.</summary>
        internal static bool HasPad => _pad != null;

        /// <summary>
        /// Starts listening for the pad. Once, at startup; any failure leaves the game on XInput's two motors, which is
        /// what it had before.
        /// </summary>
        internal static void Start()
        {
            if (_started) return;
            _started = true;

            try
            {
                //Each event re-reads the list rather than trusting its own argument: two pads, one pulled, must leave
                //the other one rather than nothing, and a list read on a rare event costs nothing
                Gamepad.GamepadAdded += (_, _) => Refresh();
                Gamepad.GamepadRemoved += (_, _) => Refresh();
                Refresh();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[pad] Windows.Gaming.Input unavailable, the body motors only: {exception.Message}");
            }
        }

        //The first pad WGI lists — the one player's. WGI's order and XInput's PlayerIndex are two enumerations with no
        //stated correspondence; with one pad, as this game is played, they agree by having nothing else to choose.
        private static void Refresh()
        {
            try
            {
                var pads = Gamepad.Gamepads;
                _pad = pads.Count > 0 ? pads[0] : null;
            }
            catch (Exception)
            {
                _pad = null;
            }
        }

        /// <summary>
        /// Sends the four motors, each 0 to 1, to the pad WGI knows. False when it knows none — the caller's cue to
        /// drive XInput's two motors instead.
        /// </summary>
        internal static bool TrySet(float leftMotor, float rightMotor, float leftTrigger, float rightTrigger)
        {
            Gamepad pad = _pad;
            if (pad == null) return false;

            try
            {
                pad.Vibration = new GamepadVibration
                {
                    LeftMotor = leftMotor,
                    RightMotor = rightMotor,
                    LeftTrigger = leftTrigger,
                    RightTrigger = rightTrigger,
                };

                return true;
            }
            catch (Exception)
            {
                //A pad pulled between its removal and the event saying so: XInput's route this frame, and the event
                //settles which pad is next
                return false;
            }
        }
    }
}

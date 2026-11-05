using System;
using System.Runtime.Versioning;
using Android.Hardware.Input;
using Android.OS;
using Android.Runtime;
using Android.Views;

namespace BS3D.Platform
{
    /// <summary>
    /// The Android build's namesake of <c>Game/Platform/PadMotors.cs</c> (#1048): a pad's two body motors through the pad's
    /// own <see cref="VibratorManager"/>, each at the level the rumble mixer asks for. It is the <b>only</b> route on
    /// Android (<see cref="ReplacesSetVibration"/>), so <see cref="TrySet"/> takes every write whether or not a pad is there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>MonoGame's own <c>GamePad.SetVibration</c> must never be called on Android.</b> MonoGame.Framework.Android
    /// 3.8.5 ignores the four levels it is given: every call, a zero included, starts a 500 ms one-shot at the vibrator's
    /// default amplitude (read in its decompiled <c>GamePad.PlatformSetVibration</c>). The mixer writes once a frame while
    /// anything rings, so a menu step, a shot or a landing came back as half a second or more at that strength, the zero
    /// meant to stop it included (the owner, on an Xbox pad over Bluetooth: "it vibrates so much it almost falls out of
    /// my hands").
    /// </para>
    /// <para>
    /// <b>What Android offers.</b> From Android 12 (API 31) an input device has a <see cref="VibratorManager"/>; a pad with
    /// evdev's rumble lists two vibrators, 0 for the rumble's strong magnitude (the heavy left motor) and 1 for the weak
    /// one (the light right motor), and a one-shot's amplitude reaches the pad as that motor's magnitude. The trigger
    /// motors have no route at all, so the trigger pair is dropped, as XInput drops it. Before Android 12 a pad's motors
    /// ran at one fixed strength whatever the amplitude, so there this stays still rather than shake at that strength.
    /// </para>
    /// <para>
    /// <b>How it writes.</b> A level is held as a one-shot of <see cref="PULSE_MS"/>, re-sent every
    /// <see cref="REFRESH_MS"/> while it stays and at once when it changes, so a game that stops writing (a hitch, a crash)
    /// leaves the motors still within a quarter of a second instead of buzzing on. Levels are quantised to
    /// <see cref="LEVELS"/> steps, a step the hand does not tell apart, which bounds how often a decaying pulse is re-sent
    /// and lets each pair's vibration be built once and kept, so steady play allocates nothing. Both motors at zero is a
    /// cancel. Android takes a combined vibration only when every motor in it has the same timing, which is why both are
    /// one-shots of one length, and a motor at zero is left out of it (a one-shot cannot have amplitude 0, and a motor not
    /// named is driven at zero).
    /// </para>
    /// </remarks>
    internal static class PadMotors
    {
        //How long one write runs on the pad unless the next replaces it, and how long a level that does not change is left
        //before it is sent again: the refresh well inside the pulse, so a held level never lapses between two writes
        private const long PULSE_MS = 250;
        private const long REFRESH_MS = 150;

        //The steps a motor's level is quantised to (0 is still, LEVELS is full)
        private const int LEVELS = 32;

        private static volatile Pad _pad;
        private static bool _started;
        private static DeviceListener _listener;

        //The scan and the field it writes as one step: the start's own scan and the listener's could otherwise interleave
        private static readonly object _refreshLock = new();

        /// <summary>The route as the rumble mixer's log names it.</summary>
        internal static string Route => "Android's input vibrators: the body motors at the mixer's levels";

        /// <summary>
        /// True: on Android this is the one route to a pad's motors and MonoGame's own <c>SetVibration</c> is never called,
        /// since it shakes the pad at full whatever it is asked (the remarks).
        /// </summary>
        internal static bool ReplacesSetVibration => true;

        /// <summary>Whether a pad whose motors this reaches is connected.</summary>
        internal static bool HasPad => _pad != null;

        /// <summary>
        /// Starts listening for pads (the system's input device listener, on the main thread's looper) and finds the one
        /// already connected. Once, at startup; any failure leaves the pad still, never on MonoGame's route.
        /// </summary>
        internal static void Start()
        {
            if (_started) return;
            _started = true;

            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                Console.WriteLine("[pad] Android before 12 drives a pad's motors at one strength: the pad stays still");
                return;
            }

            try
            {
                //Listening first and scanning after, so a pad connected in between is not missed
                InputManager inputManager = Android.App.Application.Context
                    .GetSystemService(Android.Content.Context.InputService).JavaCast<InputManager>();
                _listener = new DeviceListener();
                inputManager.RegisterInputDeviceListener(_listener, new Handler(Looper.MainLooper));
                Refresh();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[pad] the pad's motors cannot be reached, the pad stays still: {exception.Message}");
            }
        }

        /// <summary>
        /// Sends the body motors, each 0 to 1, to the pad; the trigger pair has no route on Android and is dropped. Always
        /// true: there is no other route to fall back to (<see cref="ReplacesSetVibration"/>), and with no pad connected
        /// the write simply goes nowhere.
        /// </summary>
        internal static bool TrySet(float leftMotor, float rightMotor, float leftTrigger, float rightTrigger)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31)) _pad?.Write(leftMotor, rightMotor);
            return true;
        }

        //The first connected pad with motors - the one player's. MonoGame's PlayerIndex.One and the system's device order
        //are two enumerations with no stated correspondence; with one pad, as the game is played, they agree
        [SupportedOSPlatform("android31.0")]
        private static void Refresh()
        {
            lock (_refreshLock)
            {
                Pad found = null;
                try
                {
                    foreach (int id in InputDevice.GetDeviceIds() ?? Array.Empty<int>())
                    {
                        InputDevice device = InputDevice.GetDevice(id);
                        if (device == null) continue;

                        bool pad = (device.Sources & InputSourceType.Gamepad) == InputSourceType.Gamepad
                            || (device.Sources & InputSourceType.Joystick) == InputSourceType.Joystick;
                        if (!pad) continue;

                        //The same pad as before keeps its own state and its built vibrations
                        Pad current = _pad;
                        if (current != null && current.DeviceId == id)
                        {
                            found = current;
                            break;
                        }

                        VibratorManager motors = device.VibratorManager;
                        int[] ids = motors.GetVibratorIds();
                        if (ids == null || ids.Length == 0) continue;

                        found = new Pad(id, device.Name, motors, ids);
                        break;
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[pad] the input devices could not be read: {exception.Message}");
                    found = null;
                }

                if (found != _pad)
                {
                    _pad?.Stop();
                    _pad = found;
                    Console.WriteLine(found != null ? $"[pad] {found.Describe()}" : "[pad] no pad with motors connected");
                }
            }
        }

        //The system's word that a device came, went or changed, on the main thread's looper
        private sealed class DeviceListener : Java.Lang.Object, InputManager.IInputDeviceListener
        {
            public void OnInputDeviceAdded(int deviceId) => RefreshFromListener();

            public void OnInputDeviceChanged(int deviceId) => RefreshFromListener();

            public void OnInputDeviceRemoved(int deviceId) => RefreshFromListener();

            private static void RefreshFromListener()
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(31)) Refresh();
            }
        }

        /// <summary>One pad's motors, what was last sent to them and the vibrations built for them so far.</summary>
        [SupportedOSPlatform("android31.0")]
        private sealed class Pad
        {
            //One one-shot per level, shared by every pad: an effect names no motor
            private static readonly VibrationEffect[] _effects = new VibrationEffect[LEVELS + 1];

            private readonly VibratorManager _motors;
            private readonly int _leftId, _rightId;
            private readonly bool _twoMotors;
            private readonly string _name;
            private readonly int[] _ids;

            //Each (left, right) pair's vibration, built the first time it is sent
            private readonly CombinedVibration[] _built = new CombinedVibration[(LEVELS + 1) * (LEVELS + 1)];

            //Written from the game's thread only
            private int _sentLeft, _sentRight;
            private long _sentAt;
            private bool _failed;

            public readonly int DeviceId;

            public Pad(int deviceId, string name, VibratorManager motors, int[] ids)
            {
                DeviceId = deviceId;
                _name = name;
                _motors = motors;
                _ids = (int[])ids.Clone();
                Array.Sort(_ids);
                //The lowest id is the rumble's strong magnitude, the heavy left motor; the next the weak, the light right
                _leftId = _ids[0];
                _twoMotors = _ids.Length > 1;
                _rightId = _twoMotors ? _ids[1] : _ids[0];
            }

            public string Describe()
            {
                bool amplitude = true;
                foreach (int id in _ids) amplitude &= _motors.GetVibrator(id).HasAmplitudeControl;
                return $"{_name}: vibrators {string.Join(", ", _ids)}, amplitude control {(amplitude ? "yes" : "no")}";
            }

            public void Write(float leftMotor, float rightMotor)
            {
                int left = Level(leftMotor), right = Level(rightMotor);
                //A pad with one motor feels the louder of the two
                if (!_twoMotors)
                {
                    left = Math.Max(left, right);
                    right = 0;
                }

                long now = System.Environment.TickCount64;
                bool same = left == _sentLeft && right == _sentRight;
                if (same && (left == 0 && right == 0 || now - _sentAt < REFRESH_MS)) return;

                try
                {
                    if (left == 0 && right == 0) _motors.Cancel();
                    else _motors.Vibrate(Built(left, right));

                    _sentLeft = left;
                    _sentRight = right;
                    _sentAt = now;
                }
                catch (Exception exception)
                {
                    //A pad pulled between its removal and the listener saying so: the listener settles which pad is next.
                    //Counted as sent, so a refusing pad is asked again on a change or a refresh, not on every frame
                    if (!_failed) Console.WriteLine($"[pad] the motors refused a write: {exception.Message}");
                    _failed = true;
                    _sentLeft = left;
                    _sentRight = right;
                    _sentAt = now;
                }
            }

            public void Stop()
            {
                try
                {
                    _motors.Cancel();
                }
                catch (Exception)
                {
                    //Gone already
                }
            }

            private static int Level(float level) => (int)MathF.Round(Math.Clamp(level, 0f, 1f) * LEVELS);

            private CombinedVibration Built(int left, int right)
            {
                int index = left * (LEVELS + 1) + right;
                CombinedVibration built = _built[index];
                if (built != null) return built;

                CombinedVibration.ParallelCombination parallel = CombinedVibration.StartParallel();
                if (left > 0) parallel.AddVibrator(_leftId, Effect(left));
                if (right > 0) parallel.AddVibrator(_rightId, Effect(right));
                return _built[index] = parallel.Combine();
            }

            private static VibrationEffect Effect(int level) =>
                _effects[level] ??= VibrationEffect.CreateOneShot(PULSE_MS, Math.Max(1, (int)MathF.Round(level * 255f / LEVELS)));
        }
    }
}

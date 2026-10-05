using BS3D.Platform;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace BS3D
{
    /// <summary>
    /// The two body motors' own answer to a shot (#378) — a decaying pulse per channel, accumulated and
    /// clamped the way <see cref="Prazsky.Core.Camera.CameraShake"/>'s kick and rumble are, and pushed as
    /// <b>one</b> <see cref="GamePad.SetVibration(PlayerIndex, float, float)"/> call a frame rather than one
    /// per event — so two triggers landing on the same frame blend into that call instead of the second
    /// silently overwriting the first's.
    /// <para>
    /// The two channels are physically different motors (#188) and are free to be fed differently: the left
    /// is the heavy, low-frequency one, the right the lighter buzz. An event names both but may leave either
    /// at zero.
    /// </para>
    /// <para>
    /// <b>And the two impulse motors in the triggers (#188)</b>, fed by <see cref="KickTriggers"/> the same way: the right
    /// answers the shot, the left the aim. XInput has no trigger motors, so all four go out through
    /// <see cref="PadMotors"/> (Windows.Gaming.Input) whenever a pad answers there, and only the body's two through
    /// <see cref="GamePad.SetVibration(PlayerIndex, float, float)"/> when none does — never both on one frame, since two
    /// APIs writing the same body motors would each overwrite the other.
    /// </para>
    /// </summary>
    internal sealed class GamepadRumble
    {
        //A kick with nothing to decay it over would divide by zero; nothing this is fed is ever this short on
        //purpose, but a caller's typo should decay fast rather than throw.
        private const float MIN_SECONDS = 0.02f;

        private float _left, _right;
        private float _leftDecayRate, _rightDecayRate;

        //The triggers' two (#188), decayed the same way
        private float _leftTrigger, _rightTrigger;
        private float _leftTriggerDecayRate, _rightTriggerDecayRate;

        //Which route the last frame took, so the log says it once when it changes rather than every frame
        private bool _throughPadMotors;

        //Whether the last frame was allowed to feel anything — see the re-entry in Update
        private bool _wasAllowed;

        //Whether the last call already sent (0, 0) — so an idle pad is not told to stop every single frame
        //of a level nobody is shooting in.
        private bool _silent = true;

        /// <summary>
        /// The player's own row in Settings (#378), 0 for off. Applied where <see cref="Update"/> writes
        /// rather than where a <see cref="Kick"/> is taken, so turning it down silences what is already
        /// ringing on the very next frame instead of only the next event.
        /// </summary>
        public float Strength { get; set; } = 1f;

        /// <summary>Starts listening for a pad whose trigger motors can be reached (<see cref="PadMotors.Start"/>).</summary>
        public GamepadRumble() => PadMotors.Start();

        /// <summary>
        /// Adds one event's worth of pulse, <paramref name="left"/> and <paramref name="right"/> each 0 to 1.
        /// Accumulates and saturates at 1 per channel like <see cref="Prazsky.Core.Camera.CameraShake.Kick"/>,
        /// so two events landing in the same frame blend rather than one clobbering the other's timing.
        /// <paramref name="seconds"/> is how long the channel takes to fall back to zero from here — the
        /// heavier events (a ceiling step) ask for longer than the light ones (a ball landing), which a single
        /// fixed decay rate could not tell apart.
        /// </summary>
        public void Kick(float left, float right, float seconds)
        {
            float rate = 1f / MathF.Max(seconds, MIN_SECONDS);

            Add(ref _left, ref _leftDecayRate, left, rate);
            Add(ref _right, ref _rightDecayRate, right, rate);
        }

        /// <summary>
        /// <see cref="Kick"/> for the impulse motors in the triggers (#188), <paramref name="left"/> and
        /// <paramref name="right"/> each 0 to 1. Felt only on a pad that has them and only through
        /// <see cref="PadMotors"/>; anywhere else it is accepted and goes nowhere, as the body's own kicks do on a pad
        /// without motors.
        /// </summary>
        public void KickTriggers(float left, float right, float seconds)
        {
            float rate = 1f / MathF.Max(seconds, MIN_SECONDS);

            Add(ref _leftTrigger, ref _leftTriggerDecayRate, left, rate);
            Add(ref _rightTrigger, ref _rightTriggerDecayRate, right, rate);
        }

        //One channel's share of a kick: accumulated, clamped to 1, and given the fall that takes it to zero over the
        //kick's own length from where it now stands
        private static void Add(ref float channel, ref float decayRate, float amount, float rate)
        {
            if (amount <= 0f) return;

            channel = MathHelper.Clamp(channel + amount, 0f, 1f);
            decayRate = channel * rate;
        }

        /// <summary>
        /// Decays both channels and pushes the one call a frame this type exists for.
        /// <paramref name="allowed"/> is the caller's own answer to whether the pad should feel anything right
        /// now — unfocused, paused or off the gameplay screen reads false regardless of what is still ringing,
        /// since vibration is a device state that outlives the frame that asked for it and has to be told to
        /// stop rather than merely left alone.
        /// <para>
        /// <b>Gated by <see cref="GamePad.GetCapabilities(PlayerIndex)"/> since #516</b> — until then this fired blind: a
        /// connected pad with no vibration motors at all (a wheel, a generic pad through an XInput shim) got
        /// <c>SetVibration</c> on every shot regardless, and a pad with only one motor got told to drive the
        /// other anyway. Read once per call rather than cached, so a pad swapped mid-session is never fed
        /// stale capabilities — cheap next to <c>SetVibration</c> itself, and this method already fires no
        /// more often than the rumble actually changes (the <c>_silent</c> guard above it).
        /// </para>
        /// </summary>
        public void Update(float elapsedSeconds, bool allowed)
        {
            if (!allowed)
            {
                _left = 0f;
                _right = 0f;
                _leftTrigger = 0f;
                _rightTrigger = 0f;
            }
            else
            {
                _left = MathF.Max(0f, _left - _leftDecayRate * elapsedSeconds);
                _right = MathF.Max(0f, _right - _rightDecayRate * elapsedSeconds);
                _leftTrigger = MathF.Max(0f, _leftTrigger - _leftTriggerDecayRate * elapsedSeconds);
                _rightTrigger = MathF.Max(0f, _rightTrigger - _rightTriggerDecayRate * elapsedSeconds);
            }

            //Coming back (focus regained, a pause left) the pad is told the state once more, zero included: the stop sent
            //as the window lost focus went to Windows.Gaming.Input as the app stopped being the foreground one, which is
            //exactly when WGI stops listening, and a motor whose zero was dropped could resume on the pad's return to us
            if (allowed && !_wasAllowed) _silent = false;
            _wasAllowed = allowed;

            bool silentNow = _left <= 0f && _right <= 0f && _leftTrigger <= 0f && _rightTrigger <= 0f;
            if (silentNow && _silent) return;

            //All four through Windows.Gaming.Input when a pad answers there (#188) — the capabilities below are XInput's
            //and know nothing of triggers, and a pad without some motor simply does not turn it
            bool throughPadMotors = PadMotors.TrySet(_left * Strength, _right * Strength,
                _leftTrigger * Strength, _rightTrigger * Strength);

            if (throughPadMotors != _throughPadMotors)
            {
                _throughPadMotors = throughPadMotors;
                Console.WriteLine(throughPadMotors
                    ? "[pad] rumble through Windows.Gaming.Input: body and trigger motors"
                    : "[pad] rumble through XInput: the body motors only");
            }

            if (throughPadMotors)
            {
                _silent = silentNow;
                return;
            }

            GamePadCapabilities capabilities = GamePad.GetCapabilities(PlayerIndex.One);
            if (!capabilities.HasLeftVibrationMotor && !capabilities.HasRightVibrationMotor)
            {
                _silent = true;
                return;
            }

            float left = capabilities.HasLeftVibrationMotor ? _left * Strength : 0f;
            float right = capabilities.HasRightVibrationMotor ? _right * Strength : 0f;

            GamePad.SetVibration(PlayerIndex.One, left, right);
            _silent = silentNow;
        }
    }
}

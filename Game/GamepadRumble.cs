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
    /// Reaching an Xbox One/Series pad's impulse <i>triggers</i> needs a different API entirely and stays
    /// #188's unclaimed half — this is the two motors XInput already reaches.
    /// </para>
    /// </summary>
    internal sealed class GamepadRumble
    {
        //A kick with nothing to decay it over would divide by zero; nothing this is fed is ever this short on
        //purpose, but a caller's typo should decay fast rather than throw.
        private const float MIN_SECONDS = 0.02f;

        private float _left, _right;
        private float _leftDecayRate, _rightDecayRate;

        //Whether the last call already sent (0, 0) — so an idle pad is not told to stop every single frame
        //of a level nobody is shooting in.
        private bool _silent = true;

        /// <summary>
        /// The player's own row in Settings (#378), 0 for off. Applied where <see cref="Update"/> writes
        /// rather than where a <see cref="Kick"/> is taken, so turning it down silences what is already
        /// ringing on the very next frame instead of only the next event.
        /// </summary>
        public float Strength { get; set; } = 1f;

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

            if (left > 0f)
            {
                _left = MathHelper.Clamp(_left + left, 0f, 1f);
                _leftDecayRate = _left * rate;
            }

            if (right > 0f)
            {
                _right = MathHelper.Clamp(_right + right, 0f, 1f);
                _rightDecayRate = _right * rate;
            }
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
            }
            else
            {
                _left = MathF.Max(0f, _left - _leftDecayRate * elapsedSeconds);
                _right = MathF.Max(0f, _right - _rightDecayRate * elapsedSeconds);
            }

            bool silentNow = _left <= 0f && _right <= 0f;
            if (silentNow && _silent) return;

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

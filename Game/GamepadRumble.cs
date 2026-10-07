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

        /// <summary>
        /// Whether the player's hand is on the pad (#817): the play's own answer, the one the tutorial draws its glyphs
        /// for (<c>GameplayScreen.NoteDevice</c>). A game event (<see cref="Kick"/>, <see cref="KickTriggers"/>) is felt
        /// only while it is true. A pad lying beside a keyboard player used to answer every shot, landing and ceiling
        /// step, and the owner's rule is that it says nothing then but the startup signature. The signature, the connect
        /// pulse and the menu's words go out whatever this says: the first two are the pad announcing itself, and the
        /// menu speaks only when the pad itself moved it.
        /// </summary>
        public bool HandOnPad { get; set; }

        /// <summary>Starts listening for a pad whose trigger motors can be reached (<see cref="PadMotors.Start"/>).</summary>
        public GamepadRumble() => PadMotors.Start();

        /// <summary>
        /// A game event's pulse, <paramref name="left"/> and <paramref name="right"/> each 0 to 1, felt only while the
        /// hand is on the pad (<see cref="HandOnPad"/>). Accumulates and saturates at 1 per channel like
        /// <see cref="Prazsky.Core.Camera.CameraShake.Kick"/>, so two events landing in the same frame blend rather than
        /// one clobbering the other's timing. <paramref name="seconds"/> is how long the channel takes to fall back to
        /// zero from here — the heavier events (a ceiling step) ask for longer than the light ones (a ball landing),
        /// which a single fixed decay rate could not tell apart.
        /// </summary>
        public void Kick(float left, float right, float seconds)
        {
            if (HandOnPad) Feel(left, right, seconds);
        }

        /// <summary>
        /// <see cref="Kick"/> for the impulse motors in the triggers (#188), <paramref name="left"/> and
        /// <paramref name="right"/> each 0 to 1, and gated the same way. Felt only on a pad that has them and only
        /// through <see cref="PadMotors"/>; anywhere else it is accepted and goes nowhere, as the body's own kicks do on
        /// a pad without motors.
        /// </summary>
        public void KickTriggers(float left, float right, float seconds)
        {
            if (HandOnPad) FeelTriggers(left, right, seconds);
        }

        //The pulse itself, whoever asked for it: the game's kicks through the gate above, the patterns and the menu's
        //words straight
        private void Feel(float left, float right, float seconds)
        {
            float rate = 1f / MathF.Max(seconds, MIN_SECONDS);

            Add(ref _left, ref _leftDecayRate, left, rate);
            Add(ref _right, ref _rightDecayRate, right, rate);
        }

        private void FeelTriggers(float left, float right, float seconds)
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

        #region Patterns, the menus' vocabulary and the pad's arrival (#800)

        /// <summary>One pulse of a pattern: when it starts, its four motors and how long they take to fall.</summary>
        private readonly struct Pulse
        {
            public readonly float At, Left, Right, LeftTrigger, RightTrigger, Seconds;

            public Pulse(float at, float left, float right, float leftTrigger, float rightTrigger, float seconds)
            {
                At = at;
                Left = left;
                Right = right;
                LeftTrigger = leftTrigger;
                RightTrigger = rightTrigger;
                Seconds = seconds;
            }
        }

        //The game saying hello through the pad when it starts with one plugged in (#800, the owner's sketch: "left trigger,
        //right trigger, left, right, the main motors…"): the triggers called in turn, a little firmer the second time
        //round, and then the whole pad at once — a warm swell of the body under both triggers — so it lands as a flourish
        //and not as four separate buzzes. About a second. A pad without trigger motors feels only the last pulse.
        private static readonly Pulse[] SIGNATURE =
        {
            new(0.00f, 0f, 0f, 0.5f, 0f, 0.10f),
            new(0.16f, 0f, 0f, 0f, 0.5f, 0.10f),
            new(0.32f, 0f, 0f, 0.7f, 0f, 0.10f),
            new(0.48f, 0f, 0f, 0f, 0.7f, 0.10f),
            new(0.68f, 0.45f, 0.3f, 0.4f, 0.4f, 0.45f),
        };

        //A pad plugged in after the start: a short "ba-dum" of the whole pad, the confirmation, not the greeting
        private static readonly Pulse[] CONNECTED =
        {
            new(0.00f, 0.25f, 0.35f, 0.35f, 0.35f, 0.14f),
            new(0.20f, 0.25f, 0.35f, 0.35f, 0.35f, 0.14f),
        };

        //How long after the start a pad seen for the first time still gets the greeting rather than the confirmation:
        //the logo and the first menu, which is where a player who launched with the pad in hand is
        private const float SIGNATURE_WINDOW_SECONDS = 15f;

        //The menus' four words (#800), all on the body motors — a hand in a menu is on the sticks and the face buttons,
        //not the triggers. A step of the focus is the lightest thing the pad says; a page turn is felt on the side it
        //turns to (the left grip holds the heavy motor, the right the light one); a confirm is firmer and brighter; a
        //back is softer and lower.
        //The first cut's step (the light motor at 0.25 for 0.04 s) went unnoticed in the owner's hand (#800): a motor
        //that is spun up from rest needs some tens of milliseconds before it is felt at all, and forty was most of them.
        //Every word is longer now, and the step has a touch of the heavy motor under it.
        //The page turn was one figure for both sides (0.4 for 0.07 s), and the owner felt LB and RB differ, and each
        //press differ from the last (#800 c). Both were the motors: the heavy one at 0.4 is a much bigger thing in the
        //hand than the light one at 0.4, so each side now has its own figure, the light motor's about twice the heavy
        //one's (the accept's own ratio); and 0.07 s is barely past a motor's spin-up from rest,
        //so how much of a pulse was felt depended on whether the motor was still turning from the press before. It is
        //longer now, as the accept is.
        private const float UI_STEP_LEFT = 0.12f, UI_STEP_RIGHT = 0.35f, UI_STEP_SECONDS = 0.07f;
        private const float UI_PAGE_LEFT = 0.26f, UI_PAGE_RIGHT = 0.55f, UI_PAGE_SECONDS = 0.1f;
        private const float UI_ACCEPT_LEFT = 0.3f, UI_ACCEPT_RIGHT = 0.55f, UI_ACCEPT_SECONDS = 0.1f;
        private const float UI_BACK_LEFT = 0.45f, UI_BACK_RIGHT = 0.15f, UI_BACK_SECONDS = 0.09f;

        private Pulse[] _pattern;
        private int _patternNext;
        private float _patternClock;

        //Seconds this mixer has been updated for, and whether a pad was there on the last NotePad
        private float _clock;
        private bool _padSeen;

        /// <summary>The menu's focus moved one entry (#800).</summary>
        public void UiStep() => Feel(UI_STEP_LEFT, UI_STEP_RIGHT, UI_STEP_SECONDS);

        /// <summary>A page or a tab turned, felt on the side it turned to (#800).</summary>
        public void UiPage(int direction) =>
            Feel(direction < 0 ? UI_PAGE_LEFT : 0f, direction > 0 ? UI_PAGE_RIGHT : 0f, UI_PAGE_SECONDS);

        /// <summary>An entry pressed (#800).</summary>
        public void UiAccept() => Feel(UI_ACCEPT_LEFT, UI_ACCEPT_RIGHT, UI_ACCEPT_SECONDS);

        /// <summary>Backed out of a page (#800).</summary>
        public void UiBack() => Feel(UI_BACK_LEFT, UI_BACK_RIGHT, UI_BACK_SECONDS);

        /// <summary>
        /// Whether a pad is there this frame, from the host's own snapshots (no device poll of its own). The first time one
        /// is — at the start or later — the pad is greeted: the signature within <see cref="SIGNATURE_WINDOW_SECONDS"/> of
        /// the start, the short confirmation after it. A pad unplugged and plugged back is confirmed again.
        /// </summary>
        public void NotePad(bool connected)
        {
            if (connected && !_padSeen) Play(_clock < SIGNATURE_WINDOW_SECONDS ? SIGNATURE : CONNECTED);
            _padSeen = connected;
        }

        /// <summary>
        /// Everything stops: every channel and any pattern still playing. For a moment that takes what was ringing away
        /// from the player — a pause, leaving a level — where letting it decay would be play still answering.
        /// </summary>
        public void Silence()
        {
            _left = _right = _leftTrigger = _rightTrigger = 0f;
            _pattern = null;
        }

        private void Play(Pulse[] pattern)
        {
            _pattern = pattern;
            _patternNext = 0;
            _patternClock = 0f;
        }

        //The pattern's pulses that have come due, fired as ordinary kicks so they blend with anything else ringing
        private void StepPattern(float elapsedSeconds)
        {
            if (_pattern == null) return;

            _patternClock += elapsedSeconds;

            while (_patternNext < _pattern.Length && _pattern[_patternNext].At <= _patternClock)
            {
                Pulse pulse = _pattern[_patternNext++];
                Feel(pulse.Left, pulse.Right, pulse.Seconds);
                FeelTriggers(pulse.LeftTrigger, pulse.RightTrigger, pulse.Seconds);
            }

            if (_patternNext >= _pattern.Length) _pattern = null;
        }

        #endregion

        /// <summary>
        /// Steps a pattern, decays every channel and pushes the one write a frame this type exists for.
        /// <paramref name="allowed"/> is the caller's own answer to whether the pad should feel anything right
        /// now — since #800 that is the window being the active one, the menus answering the pad too; what play started
        /// is stopped by <see cref="Silence"/> where play stops — since vibration is a device state that outlives the
        /// frame that asked for it and has to be told to stop rather than merely left alone.
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
            _clock += elapsedSeconds;

            //A pattern's due pulses first, so a pulse that starts this frame is written this frame
            if (allowed) StepPattern(elapsedSeconds);

            if (!allowed)
            {
                _pattern = null;
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

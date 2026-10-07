using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BS3D
{
    /// <summary>
    /// <b>Testing only</b> (#402): the few hands a run nobody is sitting at needs to photograph what the player's own
    /// hands do to the frame — the barrel swung across the field, the lens leaned in with the right button, a shot
    /// fired. Written for the motion blur, which is invisible on a still gun and cannot be judged from the Testbed
    /// (its game mode takes the camera, and the owner's rule is that aim-adjacent looks are judged in the Game with
    /// the button held, test-in-game-while-aiming). All three are on the wall clock <c>shot=</c> and <c>detonate=</c>
    /// count, so a timeline is written against its own photographs.
    /// <list type="bullet">
    /// <item><c>sweep=&lt;from&gt;:&lt;to&gt;[:&lt;degrees&gt;[:&lt;period&gt;]]</c> — swings the barrel's traverse
    /// back and forth across the interval, a sine of that amplitude (30° by default) and period (1.6 s): a peak rate
    /// of about 118°/s, a quick correction rather than a flick. The elevation stays wherever it was when the sweep
    /// began. It SETS the pose each frame (<c>Cannon.AimTo</c>), so the mouse cannot fight it.</item>
    /// <item><c>aim=&lt;t&gt;:&lt;elevation&gt;:&lt;traverse&gt;</c> — puts the barrel at a stated pose in degrees from that
    /// second on, the Testbed's spelling (#379): elevation above horizontal, traverse off the heading to the field's
    /// centre. Several accumulate, each holding until the next one's second; it SETS the pose every frame, so the
    /// mouse cannot move it. Written for #230's impossible shot, which wants three shots off one pose that miss.</item>
    /// <item><c>rmb=&lt;from&gt;:&lt;to&gt;</c> — holds precise aim across the interval, as the right button would.</item>
    /// <item><c>fire=&lt;t1,t2,…&gt;</c> — fires at those seconds, the shot a left click would fire.</item>
    /// <item><c>padrt=&lt;from&gt;:&lt;to&gt;</c> — holds the pad's right trigger fully down across the interval, written into
    /// the frame's own pad state so every reader sees a real pull: the fire, the camera takeover's skip, the trigger
    /// motors' answer. Written when the owner's RT did not skip a takeover and reading the code could not say why.</item>
    /// <item><c>padpress=&lt;start&gt;:&lt;step&gt;:&lt;name,name,…&gt;</c> — presses pad buttons one after another, the first at
    /// <c>start</c> and each <c>step</c> seconds after the one before, each held for half a step (#818): <c>up</c>,
    /// <c>down</c>, <c>left</c>, <c>right</c> on the D-pad, <c>a</c>, <c>b</c>, <c>x</c>, <c>y</c>, <c>lb</c>, <c>rb</c>,
    /// <c>start</c>, <c>back</c>, and <c>lsup</c>, <c>lsdown</c>, <c>lsleft</c>, <c>lsright</c> for the left stick pushed
    /// all the way. Written into the frame's pad state like <c>padrt=</c>, in the menus and in play, so everything
    /// downstream of the XInput read sees a real press; while one is down it stands in for the pad's buttons and left
    /// stick. Several accumulate. Written when the owner's Konami code did nothing on a pad and no pad here can be
    /// pressed by a script. ⚠ Like <c>padrt=</c>, it needs the window's focus: the pad is read only inside the menus' and
    /// the play loop's focus gates, so a press in a run that never had focus is never seen (the runs that verified it had
    /// it; <c>walk=</c> and <c>turn=</c> sit outside the gate on purpose and do not).</item>
    /// <item><c>swap=&lt;t1,t2,…&gt;</c> — presses the swap key at those seconds (#213), through the very call E makes, so
    /// a second press on a level with one swap is the refusal a player would hear.</item>
    /// <item><c>brake=&lt;t1,t2,…&gt;</c> — presses the ceiling's brake key at those seconds (#213), through the very call Q
    /// makes, so a press with the glass still at rest is the refusal a player would hear.</item>
    /// <item><c>cut=&lt;t1,t2,…&gt;</c> — presses the anchor cut's key at those seconds (#213), through the very call R makes,
    /// so the round in the bore becomes a cutter and the next <c>fire=</c> is the cut.</item>
    /// <item><c>walk=&lt;from&gt;:&lt;to&gt;[:in|out]</c> and <c>turn=&lt;from&gt;:&lt;to&gt;[:left|right]</c> — hold W (or S)
    /// and A (or D) across the interval, through the very calls the keys make. Written for the resize fault: a
    /// window resized mid-level re-solves the fit, and whether the player's walk and turn survive that could only be
    /// shown with the gun actually walked and turned first. The defaults are <c>in</c> and <c>left</c>.</item>
    /// <item><c>mbflip=&lt;seconds&gt;</c> — turns the motion blur off and on every that many seconds, printing an
    /// <c>[mbflip]</c> line at each flip: its cost measured <b>inside one process</b>, the two states alternating
    /// against the same drift, which separate runs on a machine other sessions are also rendering on cannot give.
    /// Off in the first period.</item>
    /// </list>
    /// A static set by <c>Program</c> before the game exists, like <c>UserData.UseForTesting</c>: it is a property of
    /// the run, and threading three testing arguments through the host's constructor would be three more parameters
    /// on a list already forty long.
    /// </summary>
    internal sealed class ScriptedPlay
    {
        /// <summary>The run's script, or null for a run with a player at it — every run but a scripted one.</summary>
        internal static ScriptedPlay Current { get; private set; }

        private float _sweepFrom = float.NaN, _sweepTo, _sweepAmplitude = 30f, _sweepPeriod = 1.6f;
        private float _sweepElevation = float.NaN;
        private float _rmbFrom = float.NaN, _rmbTo;
        private float _padRtFrom = float.NaN, _padRtTo;
        private readonly List<(float From, float To, Buttons Button, Vector2 Stick)> _padPresses = new();
        private readonly System.Collections.Generic.List<(float At, float Elevation, float Traverse)> _aims = new();
        private float[] _fire;
        private int _nextFire;
        private float[] _swap;
        private int _nextSwap;
        private float[] _brake;
        private int _nextBrake;
        private float[] _cut;
        private int _nextCut;
        private float _walkFrom = float.NaN, _walkTo, _walkSign = 1f;
        private float _turnFrom = float.NaN, _turnTo, _turnSign = 1f;
        private float _flipPeriod;
        private bool _flipReportedOff = true;

        /// <summary>Takes one command-line argument if it is one of this class's, and says whether it was.</summary>
        internal static bool TryParse(string arg)
        {
            if (arg.StartsWith("sweep=", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = arg.Substring("sweep=".Length).Split(':');
                if (parts.Length < 2 || !TryFloat(parts[0], out float from) || !TryFloat(parts[1], out float to)) return false;

                ScriptedPlay script = Current ??= new ScriptedPlay();
                script._sweepFrom = from;
                script._sweepTo = to;
                if (parts.Length > 2 && TryFloat(parts[2], out float amplitude)) script._sweepAmplitude = amplitude;
                if (parts.Length > 3 && TryFloat(parts[3], out float period) && period > 0f) script._sweepPeriod = period;
                return true;
            }

            if (arg.StartsWith("aim=", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = arg.Substring("aim=".Length).Split(':');
                if (parts.Length != 3 || !TryFloat(parts[0], out float at) || !TryFloat(parts[1], out float elevation)
                    || !TryFloat(parts[2], out float traverse)) return false;

                ScriptedPlay script = Current ??= new ScriptedPlay();
                script._aims.Add((at, elevation, traverse));
                script._aims.Sort((a, b) => a.At.CompareTo(b.At));
                return true;
            }

            if (arg.StartsWith("rmb=", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = arg.Substring("rmb=".Length).Split(':');
                if (parts.Length != 2 || !TryFloat(parts[0], out float from) || !TryFloat(parts[1], out float to)) return false;

                ScriptedPlay script = Current ??= new ScriptedPlay();
                script._rmbFrom = from;
                script._rmbTo = to;
                return true;
            }

            if (arg.StartsWith("padrt=", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = arg.Substring("padrt=".Length).Split(':');
                if (parts.Length != 2 || !TryFloat(parts[0], out float from) || !TryFloat(parts[1], out float to)) return false;

                ScriptedPlay script = Current ??= new ScriptedPlay();
                script._padRtFrom = from;
                script._padRtTo = to;
                return true;
            }

            if (arg.StartsWith("padpress=", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = arg.Substring("padpress=".Length).Split(':');
                if (parts.Length != 3 || !TryFloat(parts[0], out float start) || !TryFloat(parts[1], out float step) || step <= 0f)
                    return false;

                string[] names = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var presses = new List<(float, float, Buttons, Vector2)>(names.Length);

                for (int i = 0; i < names.Length; i++)
                {
                    if (!TryPadName(names[i], out Buttons button, out Vector2 stick)) return false;

                    float from = start + i * step;
                    presses.Add((from, from + step * 0.5f, button, stick));
                }

                if (presses.Count == 0) return false;

                (Current ??= new ScriptedPlay())._padPresses.AddRange(presses);
                return true;
            }

            if (arg.StartsWith("walk=", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryHold(arg.Substring("walk=".Length), "in", "out", out float from, out float to, out float sign))
                    return false;

                ScriptedPlay script = Current ??= new ScriptedPlay();
                (script._walkFrom, script._walkTo, script._walkSign) = (from, to, sign);
                return true;
            }

            if (arg.StartsWith("turn=", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryHold(arg.Substring("turn=".Length), "left", "right", out float from, out float to, out float sign))
                    return false;

                ScriptedPlay script = Current ??= new ScriptedPlay();
                (script._turnFrom, script._turnTo, script._turnSign) = (from, to, sign);
                return true;
            }

            if (arg.StartsWith("mbflip=", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryFloat(arg.Substring("mbflip=".Length), out float period) || period <= 0f) return false;

                (Current ??= new ScriptedPlay())._flipPeriod = period;
                return true;
            }

            if (arg.StartsWith("fire=", StringComparison.OrdinalIgnoreCase))
            {
                float[] times = ScreenshotWriter.ParseSeconds(arg.Substring("fire=".Length));
                if (times == null || times.Length == 0) return false;

                (Current ??= new ScriptedPlay())._fire = times;
                return true;
            }

            if (arg.StartsWith("swap=", StringComparison.OrdinalIgnoreCase))
            {
                float[] times = ScreenshotWriter.ParseSeconds(arg.Substring("swap=".Length));
                if (times == null || times.Length == 0) return false;

                (Current ??= new ScriptedPlay())._swap = times;
                return true;
            }

            if (arg.StartsWith("cut=", StringComparison.OrdinalIgnoreCase))
            {
                float[] times = ScreenshotWriter.ParseSeconds(arg.Substring("cut=".Length));
                if (times == null || times.Length == 0) return false;

                (Current ??= new ScriptedPlay())._cut = times;
                return true;
            }

            if (arg.StartsWith("brake=", StringComparison.OrdinalIgnoreCase))
            {
                float[] times = ScreenshotWriter.ParseSeconds(arg.Substring("brake=".Length));
                if (times == null || times.Length == 0) return false;

                (Current ??= new ScriptedPlay())._brake = times;
                return true;
            }

            return false;
        }

        /// <summary>
        /// <c>&lt;from&gt;:&lt;to&gt;[:&lt;positive&gt;|&lt;negative&gt;]</c> — an interval and the direction of a held
        /// key, +1 for the first word (and by default), -1 for the second.
        /// </summary>
        private static bool TryHold(string text, string positive, string negative, out float from, out float to, out float sign)
        {
            to = 0f;
            sign = 1f;
            string[] parts = text.Split(':');
            if (parts.Length < 2 || parts.Length > 3 || !TryFloat(parts[0], out from) || !TryFloat(parts[1], out to)) { from = 0f; return false; }

            if (parts.Length == 3)
            {
                if (parts[2].Equals(negative, StringComparison.OrdinalIgnoreCase)) sign = -1f;
                else if (!parts[2].Equals(positive, StringComparison.OrdinalIgnoreCase)) return false;
            }

            return true;
        }

        /// <summary>One of <c>padpress=</c>'s names: a button (the D-pad's four among them), or the left stick pushed one way.</summary>
        private static bool TryPadName(string name, out Buttons button, out Vector2 stick)
        {
            stick = Vector2.Zero;
            button = name.ToLowerInvariant() switch
            {
                "up" => Buttons.DPadUp,
                "down" => Buttons.DPadDown,
                "left" => Buttons.DPadLeft,
                "right" => Buttons.DPadRight,
                "a" => Buttons.A,
                "b" => Buttons.B,
                "x" => Buttons.X,
                "y" => Buttons.Y,
                "lb" => Buttons.LeftShoulder,
                "rb" => Buttons.RightShoulder,
                "start" => Buttons.Start,
                "back" => Buttons.Back,
                _ => 0,
            };

            if (button != 0) return true;

            stick = name.ToLowerInvariant() switch
            {
                "lsup" => Vector2.UnitY,
                "lsdown" => -Vector2.UnitY,
                "lsleft" => -Vector2.UnitX,
                "lsright" => Vector2.UnitX,
                _ => Vector2.Zero,
            };

            return stick != Vector2.Zero;
        }

        private static bool TryFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>One line saying what the script will do, for the run's log.</summary>
        internal string Describe() =>
            "[script] "
            + (float.IsNaN(_sweepFrom) ? "" : $"sweep {_sweepFrom:0.##}-{_sweepTo:0.##} s ±{_sweepAmplitude:0.#}° / {_sweepPeriod:0.##} s; ")
            + (float.IsNaN(_rmbFrom) ? "" : $"rmb {_rmbFrom:0.##}-{_rmbTo:0.##} s; ")
            + (_aims.Count == 0 ? "" : $"aim {string.Join(", ", _aims.ConvertAll(a => $"{a.Elevation:0.#}°/{a.Traverse:0.#}° from {a.At:0.##} s"))}; ")
            + (float.IsNaN(_walkFrom) ? "" : $"walk {(_walkSign > 0f ? "in" : "out")} {_walkFrom:0.##}-{_walkTo:0.##} s; ")
            + (float.IsNaN(_turnFrom) ? "" : $"turn {(_turnSign > 0f ? "left" : "right")} {_turnFrom:0.##}-{_turnTo:0.##} s; ")
            + (_padPresses.Count == 0 ? "" : $"{_padPresses.Count} pad press(es) from {_padPresses[0].From:0.##} s; ")
            + (_fire == null ? "" : $"fire at {string.Join(", ", Array.ConvertAll(_fire, t => t.ToString("0.##", CultureInfo.InvariantCulture)))} s");

        /// <summary>
        /// The traverse, in radians, the sweep puts the barrel at this instant — and the elevation it holds, taken the
        /// first time the sweep is asked — or false outside the sweep's interval.
        /// </summary>
        internal bool TrySweep(float clock, float currentElevation, out float elevation, out float traverse)
        {
            elevation = traverse = 0f;
            if (float.IsNaN(_sweepFrom) || clock < _sweepFrom || clock > _sweepTo) return false;

            if (float.IsNaN(_sweepElevation)) _sweepElevation = currentElevation;

            elevation = _sweepElevation;
            traverse = MathF.PI / 180f * _sweepAmplitude * MathF.Sin(MathF.Tau * (clock - _sweepFrom) / _sweepPeriod);
            return true;
        }

        /// <summary>
        /// Whether <c>mbflip=</c> holds the motion blur off at this instant (every other period, the first one off),
        /// printing a line on each flip so a frame-rate log can be split by state.
        /// </summary>
        internal bool MotionBlurFlippedOff(float clock)
        {
            if (_flipPeriod <= 0f) return false;

            bool off = ((int)(clock / _flipPeriod) & 1) == 0;
            if (off != _flipReportedOff)
            {
                _flipReportedOff = off;
                Console.WriteLine($"[mbflip] {(off ? "off" : "on")} at {clock.ToString("0.00", CultureInfo.InvariantCulture)} s");
            }

            return off;
        }

        /// <summary>+1 while W is held by <c>walk=</c> at this instant, -1 for S, 0 outside its interval.</summary>
        internal float Walk(float clock) => !float.IsNaN(_walkFrom) && clock >= _walkFrom && clock <= _walkTo ? _walkSign : 0f;

        /// <summary>+1 while A is held by <c>turn=</c> at this instant, -1 for D, 0 outside its interval.</summary>
        internal float Turn(float clock) => !float.IsNaN(_turnFrom) && clock >= _turnFrom && clock <= _turnTo ? _turnSign : 0f;

        /// <summary>
        /// The pose <c>aim=</c> holds at this instant, in degrees - the latest one whose second has come - or false
        /// before the first. A short walk over a handful of entries, once a frame.
        /// </summary>
        internal bool TryAim(float clock, out float elevation, out float traverse)
        {
            elevation = traverse = 0f;
            bool any = false;

            for (int i = 0; i < _aims.Count && _aims[i].At <= clock; i++)
            {
                elevation = _aims[i].Elevation;
                traverse = _aims[i].Traverse;
                any = true;
            }

            return any;
        }

        /// <summary>Whether the pad's right trigger is held at this instant (<c>padrt=</c>).</summary>
        internal bool PadRightTrigger(float clock) => !float.IsNaN(_padRtFrom) && clock >= _padRtFrom && clock <= _padRtTo;

        /// <summary>
        /// The frame's pad with <c>padpress=</c>'s presses written in: unchanged while none is down, and while one is, its
        /// buttons and left stick in place of the pad's own (the triggers and the right stick stay the pad's).
        /// </summary>
        internal GamePadState WithPadPresses(float clock, GamePadState pad)
        {
            if (_padPresses.Count == 0) return pad;

            Buttons buttons = 0;
            Vector2 stick = Vector2.Zero;
            bool any = false;

            foreach ((float from, float to, Buttons button, Vector2 push) in _padPresses)
            {
                if (clock < from || clock >= to) continue;

                any = true;
                buttons |= button;
                stick += push;
            }

            if (!any) return pad;

            static ButtonState Down(Buttons all, Buttons one) => (all & one) != 0 ? ButtonState.Pressed : ButtonState.Released;

            GamePadDPad dpad = new(Down(buttons, Buttons.DPadUp), Down(buttons, Buttons.DPadDown),
                Down(buttons, Buttons.DPadLeft), Down(buttons, Buttons.DPadRight));

            return new GamePadState(new GamePadThumbSticks(stick, pad.ThumbSticks.Right), pad.Triggers, new GamePadButtons(buttons), dpad);
        }

        /// <summary>Whether precise aim is held at this instant.</summary>
        internal bool Rmb(float clock) => !float.IsNaN(_rmbFrom) && clock >= _rmbFrom && clock <= _rmbTo;

        /// <summary>Whether a scheduled shot has come due, consuming it — one a call, like <c>detonate=</c>.</summary>
        internal bool TryTakeFire(float clock)
        {
            if (_fire == null || _nextFire >= _fire.Length || clock < _fire[_nextFire]) return false;

            _nextFire++;
            return true;
        }

        /// <summary>Whether a scheduled swap press has come due, consuming it - one a call, like <see cref="TryTakeFire"/> (#213).</summary>
        internal bool TryTakeSwap(float clock)
        {
            if (_swap == null || _nextSwap >= _swap.Length || clock < _swap[_nextSwap]) return false;

            _nextSwap++;
            return true;
        }

        /// <summary>Whether a scheduled cut press has come due, consuming it - one a call, like <see cref="TryTakeBrake"/> (#213).</summary>
        internal bool TryTakeCut(float clock)
        {
            if (_cut == null || _nextCut >= _cut.Length || clock < _cut[_nextCut]) return false;

            _nextCut++;
            return true;
        }

        /// <summary>Whether a scheduled brake press has come due, consuming it - one a call, like <see cref="TryTakeSwap"/> (#213).</summary>
        internal bool TryTakeBrake(float clock)
        {
            if (_brake == null || _nextBrake >= _brake.Length || clock < _brake[_nextBrake]) return false;

            _nextBrake++;
            return true;
        }
    }
}

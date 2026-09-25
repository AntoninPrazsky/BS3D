using Prazsky.Core.Render;
using System;
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
    /// <item><c>rmb=&lt;from&gt;:&lt;to&gt;</c> — holds precise aim across the interval, as the right button would.</item>
    /// <item><c>fire=&lt;t1,t2,…&gt;</c> — fires at those seconds, the shot a left click would fire.</item>
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
        private float[] _fire;
        private int _nextFire;
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

            if (arg.StartsWith("rmb=", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = arg.Substring("rmb=".Length).Split(':');
                if (parts.Length != 2 || !TryFloat(parts[0], out float from) || !TryFloat(parts[1], out float to)) return false;

                ScriptedPlay script = Current ??= new ScriptedPlay();
                script._rmbFrom = from;
                script._rmbTo = to;
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

            return false;
        }

        private static bool TryFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>One line saying what the script will do, for the run's log.</summary>
        internal string Describe() =>
            "[script] "
            + (float.IsNaN(_sweepFrom) ? "" : $"sweep {_sweepFrom:0.##}-{_sweepTo:0.##} s ±{_sweepAmplitude:0.#}° / {_sweepPeriod:0.##} s; ")
            + (float.IsNaN(_rmbFrom) ? "" : $"rmb {_rmbFrom:0.##}-{_rmbTo:0.##} s; ")
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

        /// <summary>Whether precise aim is held at this instant.</summary>
        internal bool Rmb(float clock) => !float.IsNaN(_rmbFrom) && clock >= _rmbFrom && clock <= _rmbTo;

        /// <summary>Whether a scheduled shot has come due, consuming it — one a call, like <c>detonate=</c>.</summary>
        internal bool TryTakeFire(float clock)
        {
            if (_fire == null || _nextFire >= _fire.Length || clock < _fire[_nextFire]) return false;

            _nextFire++;
            return true;
        }
    }
}

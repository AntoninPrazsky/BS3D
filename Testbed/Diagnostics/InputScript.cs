using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Testbed.Diagnostics
{
    /// <summary>
    /// <b>The Testbed driving its own input</b> (#373): <c>at=&lt;t&gt;:&lt;key&gt;</c> taps an action at a
    /// wall-clock second, <c>hold=&lt;key&gt;:&lt;from&gt;:&lt;to&gt;</c> holds one of the movement keys down
    /// across an interval, and both run inside the process — no focus, no scan codes, no desktop.
    /// </summary>
    /// <remarks>
    /// Everything this program did beyond its command line used to have to be pressed into it from OUTSIDE,
    /// and that path fails in ways that look like findings rather than like failures.
    /// <c>.claude/skills/screenshot</c> and <c>.claude/skills/verify</c> carry the list: keys must go by SCAN
    /// code (SDL reads the scan code, so a wrong one silently presses a different key); a background
    /// <c>SetForegroundWindow</c> often fails, so the script has to click the window's title bar first; the
    /// numpad keys never arrive at all (they need NumLock), which is why the sky and the scene could only be
    /// pinned by relaunching; a key can be left physically down for the whole desktop if the script throws
    /// between the down and the up; and on a <b>locked</b> desktop nothing arrives whatever — so a held-key
    /// capture photographs a gun that simply did not move, <b>which reads as a broken feature</b>.
    /// <para>
    /// <b>The names are the key names, and they are not invented here.</b> A tap is looked up in the
    /// Testbed's own <see cref="ButtonAction"/> table, so <c>at=2:F10</c> runs exactly what pressing F10 runs
    /// and a key added to that table is scriptable the day it exists. The spellings are also the ones
    /// <c>screenshot.ps1</c>'s <c>-Keys</c> already used, so a script converting from it does not have to
    /// relearn them.
    /// </para>
    /// <para>
    /// <b>Two holds of the same interval really are simultaneous</b>, which the external route could only
    /// approximate by sending both downs before any sleep: <c>hold=W:2:4 hold=A:2:4</c> is read per frame from
    /// one clock, so the diagonal walk that makes an omnidirectional wheel decompose its motion is exact.
    /// </para>
    /// <para>
    /// It announces its whole plan on startup and logs every event it fires, because the alternative to a
    /// silent no-op is not an error — it is a line saying what the run is going to do, where a typo is
    /// obvious before the run is believed.
    /// </para>
    /// </remarks>
    public sealed class InputScript
    {
        /// <summary>One <c>at=</c> entry: press this key's action once, at this wall-clock second.</summary>
        public readonly record struct Tap(float Time, Keys Key);

        /// <summary>One <c>hold=</c> entry: this key counts as held from <paramref name="From"/> to <paramref name="To"/>.</summary>
        public readonly record struct Hold(Keys Key, float From, float To);

        /// <summary>
        /// The only keys anything reads as HELD: the cannon's orbit (A/D) and its advance walk (W/S), polled
        /// every game-mode frame in <c>Testbed.Input.cs</c>. A hold of anything else would be a silent no-op,
        /// so it is refused at the parse and named in the log instead.
        /// </summary>
        public static readonly Keys[] HoldableKeys = { Keys.W, Keys.A, Keys.S, Keys.D };

        private readonly Tap[] _taps;
        private readonly Hold[] _holds;
        private readonly Dictionary<Keys, Action> _actions;

        //How far down the tap list the run has got, and which holds are currently down - kept only so the
        //log can carry an edge rather than a line per frame.
        private int _nextTap;
        private readonly bool[] _holdDown;

        private float _wallClock;

        private InputScript(Tap[] taps, Hold[] holds, Dictionary<Keys, Action> actions)
        {
            _taps = taps;
            _holds = holds;
            _actions = actions;
            _holdDown = new bool[holds.Length];
        }

        /// <summary>
        /// Builds the script, or returns <c>null</c> when nothing was asked for — null rather than an empty
        /// object, so a run nobody scripted has nothing to tick and nothing to test per frame.
        /// </summary>
        /// <param name="taps">Parsed <c>at=</c> entries, in the order the command line gave them.</param>
        /// <param name="holds">Parsed <c>hold=</c> entries.</param>
        /// <param name="actions">
        /// The Testbed's own control table. A tap names a key in it; anything else is dropped and named in
        /// the plan, because a table entry is the only definition of what a key does.
        /// </param>
        public static InputScript Build(IReadOnlyList<Tap> taps, IReadOnlyList<Hold> holds, ButtonAction[] actions)
        {
            if ((taps == null || taps.Count == 0) && (holds == null || holds.Count == 0)) return null;

            Dictionary<Keys, Action> methods = new();
            foreach (ButtonAction action in actions) methods[action.Key] = action.Method;

            List<Tap> kept = new();
            List<Keys> unknown = new();

            foreach (Tap tap in taps ?? Array.Empty<Tap>())
            {
                if (methods.ContainsKey(tap.Key)) kept.Add(tap);
                else unknown.Add(tap.Key);
            }

            //Sorted by time, because the list is walked forward: the command line may name them in any order
            //(and a caller reading a timeline aloud often does), but a schedule that is not ordered would fire
            //everything after the first late entry at once.
            kept.Sort((left, right) => left.Time.CompareTo(right.Time));

            Hold[] held = new Hold[holds?.Count ?? 0];
            for (int i = 0; i < held.Length; i++) held[i] = holds[i];

            InputScript script = new(kept.ToArray(), held, methods);

            script.Announce(unknown);

            return script;
        }

        /// <summary>
        /// The whole plan on one line before the run does anything, and the keys that were dropped on
        /// another. This is what makes a typo visible: an entry that named nothing is not silently missing
        /// from a capture, it is named here.
        /// </summary>
        private void Announce(List<Keys> unknown)
        {
            StringBuilder plan = new();

            foreach (Tap tap in _taps)
            {
                if (plan.Length > 0) plan.Append(", ");
                plan.Append(CultureInfo.InvariantCulture, $"tap {tap.Key} at {tap.Time:0.00}");
            }

            foreach (Hold hold in _holds)
            {
                if (plan.Length > 0) plan.Append(", ");
                plan.Append(CultureInfo.InvariantCulture, $"hold {hold.Key} {hold.From:0.00}-{hold.To:0.00}");
            }

            Console.WriteLine($"[script] {_taps.Length + _holds.Length} entries: {plan}");

            if (unknown.Count > 0)
                Console.WriteLine($"[script] dropped, no such action: {string.Join(", ", unknown)}");
        }

        /// <summary>
        /// Ticks the timeline. <b>Called outside the focus gate and outside the simulation gate</b>, off the
        /// same wall clock the shot schedules and the ball pulse run on — so a scripted run behaves the same
        /// whether the window has focus, whether the desktop is locked and whether the simulation is paused,
        /// and <c>at=</c> and <c>shot=</c> can be read against each other.
        /// </summary>
        public void Update(float wallClock)
        {
            _wallClock = wallClock;

            //A while, not an equality test, for the reason the shot schedules use one: a long frame that
            //stepped over a whole entry still owes the run that press.
            while (_nextTap < _taps.Length && wallClock >= _taps[_nextTap].Time)
            {
                Tap tap = _taps[_nextTap++];

                //Invariant, like the plan line above and like every number the command line PARSES: a reader
                //copying a time out of this log back into an "at=" must get the same run, and a decimal comma
                //there parses as nothing.
                Console.WriteLine($"[script] {Seconds(wallClock)} tap {tap.Key}");

                //Straight to the method the table binds, not through a synthetic key: the edge path skips a
                //frame after focus returns (deliberately, see Testbed.Update), and a script must not be
                //subject to a rule that exists for a human's mouse click.
                _actions[tap.Key]();
            }

            for (int i = 0; i < _holds.Length; i++)
            {
                bool down = IsHeld(_holds[i].Key);

                if (down == _holdDown[i]) continue;

                _holdDown[i] = down;

                Console.WriteLine($"[script] {Seconds(wallClock)} {(down ? "down" : "up")} {_holds[i].Key}");
            }
        }

        /// <summary>
        /// The seconds on a log line, in the same invariant form the arguments are PARSED in — a time copied
        /// out of this log back into an <c>at=</c> has to give the same run, and a decimal comma parses as
        /// nothing.
        /// </summary>
        private static string Seconds(float wallClock) => wallClock.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>
        /// Whether the script is holding this key at the moment of the last <see cref="Update"/>. The Testbed's
        /// game-mode poll ORs this with the real keyboard, so a hand at the keyboard and a script can both
        /// drive the gun and neither disables the other.
        /// </summary>
        public bool IsHeld(Keys key)
        {
            foreach (Hold hold in _holds)
                if (hold.Key == key && _wallClock >= hold.From && _wallClock < hold.To) return true;

            return false;
        }
    }
}

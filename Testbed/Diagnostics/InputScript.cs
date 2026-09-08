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
        /// One <c>aim=</c> entry (#379): put the barrel at this pose at this wall-clock second, both angles in
        /// <b>degrees</b> — the units every angle this program prints is already in.
        /// </summary>
        public readonly record struct AimSet(float Time, float Elevation, float Traverse);

        /// <summary>One <c>rmb=</c> entry (#379): precise aim counts as held across this interval.</summary>
        public readonly record struct AdsHold(float From, float To);

        /// <summary>
        /// The only keys anything reads as HELD: the cannon's orbit (A/D) and its advance walk (W/S), polled
        /// every game-mode frame in <c>Testbed.Input.cs</c>. A hold of anything else would be a silent no-op,
        /// so it is refused at the parse and named in the log instead.
        /// </summary>
        public static readonly Keys[] HoldableKeys = { Keys.W, Keys.A, Keys.S, Keys.D };

        /// <summary>
        /// Keys whose action opens something <b>modal</b>, which a script may not tap: the run would stand in a
        /// Win32 dialog no game state can dismiss, and standing still is the one failure mode this whole
        /// facility exists to remove — a scripted run that stops silently is worse than one that never started.
        /// Refused at the parse and named in the plan, exactly as an unholdable key is.
        /// <para>
        /// Today that is F2 alone (the map loader's file dialog). It is a list here rather than a flag on
        /// <c>ButtonAction</c> for the reason <see cref="HoldableKeys"/> is: what the timeline can drive is a
        /// property of the timeline, and the shared control table is not the Testbed's to grow a field for.
        /// </para>
        /// </summary>
        public static readonly Keys[] ModalKeys = { Keys.F2 };

        private readonly Tap[] _taps;
        private readonly Hold[] _holds;
        private readonly AimSet[] _aims;
        private readonly AdsHold[] _adsHolds;
        private readonly Dictionary<Keys, Action> _actions;
        private readonly Action<float, float> _aimTo;

        //How far down the tap list the run has got, and which holds are currently down - kept only so the
        //log can carry an edge rather than a line per frame.
        private int _nextTap;
        private int _nextAim;
        private readonly bool[] _holdDown;
        private bool _adsDown;

        private float _wallClock;

        private InputScript(Tap[] taps, Hold[] holds, AimSet[] aims, AdsHold[] adsHolds,
            Dictionary<Keys, Action> actions, Action<float, float> aimTo)
        {
            _taps = taps;
            _holds = holds;
            _aims = aims;
            _adsHolds = adsHolds;
            _actions = actions;
            _aimTo = aimTo;
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
        /// <param name="aims">Parsed <c>aim=</c> entries (#379), angles in degrees.</param>
        /// <param name="adsHolds">Parsed <c>rmb=</c> intervals (#379).</param>
        /// <param name="aimTo">
        /// How to put the barrel at a stated pose, in <b>degrees</b> — the gun is the caller's, so the script
        /// is handed one delegate rather than learning what a cannon is, exactly as the taps are handed the
        /// control table's methods.
        /// </param>
        public static InputScript Build(IReadOnlyList<Tap> taps, IReadOnlyList<Hold> holds,
            IReadOnlyList<AimSet> aims, IReadOnlyList<AdsHold> adsHolds, ButtonAction[] actions,
            Action<float, float> aimTo)
        {
            if ((taps == null || taps.Count == 0) && (holds == null || holds.Count == 0)
                && (aims == null || aims.Count == 0) && (adsHolds == null || adsHolds.Count == 0)) return null;

            Dictionary<Keys, Action> methods = new();
            foreach (ButtonAction action in actions) methods[action.Key] = action.Method;

            List<Tap> kept = new();
            List<Keys> unknown = new();
            List<Keys> modal = new();

            foreach (Tap tap in taps ?? Array.Empty<Tap>())
            {
                //A modal key is refused BEFORE the table is consulted, so the reason printed is the useful one:
                //F2 is a perfectly good action and naming it "no such action" would send a reader looking for a
                //typo that is not there
                if (Array.IndexOf(ModalKeys, tap.Key) >= 0) modal.Add(tap.Key);
                else if (methods.ContainsKey(tap.Key)) kept.Add(tap);
                else unknown.Add(tap.Key);
            }

            //Sorted by time, because the list is walked forward: the command line may name them in any order
            //(and a caller reading a timeline aloud often does), but a schedule that is not ordered would fire
            //everything after the first late entry at once.
            kept.Sort((left, right) => left.Time.CompareTo(right.Time));

            Hold[] held = new Hold[holds?.Count ?? 0];
            for (int i = 0; i < held.Length; i++) held[i] = holds[i];

            //Sorted for the same reason the taps are: the list is walked forward, so an unordered schedule
            //would fire everything after the first late entry at once
            List<AimSet> poses = new(aims ?? Array.Empty<AimSet>());
            poses.Sort((left, right) => left.Time.CompareTo(right.Time));

            AdsHold[] leans = new AdsHold[adsHolds?.Count ?? 0];
            for (int i = 0; i < leans.Length; i++) leans[i] = adsHolds[i];

            InputScript script = new(kept.ToArray(), held, poses.ToArray(), leans, methods, aimTo);

            script.Announce(unknown, modal);

            return script;
        }

        /// <summary>
        /// The whole plan on one line before the run does anything, and the keys that were dropped on
        /// another. This is what makes a typo visible: an entry that named nothing is not silently missing
        /// from a capture, it is named here.
        /// </summary>
        private void Announce(List<Keys> unknown, List<Keys> modal)
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

            foreach (AimSet aim in _aims)
            {
                if (plan.Length > 0) plan.Append(", ");
                plan.Append(CultureInfo.InvariantCulture,
                    $"aim {aim.Elevation:0.0}/{aim.Traverse:0.0} deg at {aim.Time:0.00}");
            }

            foreach (AdsHold lean in _adsHolds)
            {
                if (plan.Length > 0) plan.Append(", ");
                plan.Append(CultureInfo.InvariantCulture, $"precise aim {lean.From:0.00}-{lean.To:0.00}");
            }

            Console.WriteLine($"[script] {_taps.Length + _holds.Length + _aims.Length + _adsHolds.Length}"
                + $" entries: {plan}");

            if (unknown.Count > 0)
                Console.WriteLine($"[script] dropped, no such action: {string.Join(", ", unknown)}");

            if (modal.Count > 0)
                Console.WriteLine($"[script] dropped, opens a modal dialog a script cannot dismiss: "
                    + $"{string.Join(", ", modal)}");
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

            //The aim is SET, not integrated, so it is a schedule like the taps rather than an interval like the
            //holds — and it is applied here, before UpdateCannon reads the pose this frame, so the barrel the
            //camera is framed against is the one that was asked for and not last frame's.
            while (_nextAim < _aims.Length && wallClock >= _aims[_nextAim].Time)
            {
                AimSet aim = _aims[_nextAim++];

                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"[script] {Seconds(wallClock)} aim {aim.Elevation:0.0}/{aim.Traverse:0.0} deg"));

                _aimTo?.Invoke(aim.Elevation, aim.Traverse);
            }

            for (int i = 0; i < _holds.Length; i++)
            {
                bool down = IsHeld(_holds[i].Key);

                if (down == _holdDown[i]) continue;

                _holdDown[i] = down;

                Console.WriteLine($"[script] {Seconds(wallClock)} {(down ? "down" : "up")} {_holds[i].Key}");
            }

            bool leaning = IsPreciseAimHeld();

            if (leaning != _adsDown)
            {
                _adsDown = leaning;

                Console.WriteLine($"[script] {Seconds(wallClock)} {(leaning ? "down" : "up")} precise aim");
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

        /// <summary>
        /// Whether the script is leaning the lens in at the moment of the last <see cref="Update"/> — the right
        /// mouse button, as far as everything downstream is concerned (#379).
        /// <para>
        /// <b>⚠ The caller ORs this OUTSIDE its <c>IsActive</c> gate, and that is the whole of why a scripted
        /// lean works on a minimised window.</b> The gate belongs on the real devices and is not decoration:
        /// XInput reports a held trigger to an unfocused window, so an alt-tabbed run must not stay leaned in.
        /// A script is not a stray device — it is the run driving itself, the same argument that puts the tick
        /// outside both gates — so it has to pass beside that test rather than through it. ORing it inside
        /// would compile, read correctly and produce exactly nothing on the unattended run this exists for.
        /// </para>
        /// </summary>
        public bool IsPreciseAimHeld()
        {
            foreach (AdsHold lean in _adsHolds)
                if (_wallClock >= lean.From && _wallClock < lean.To) return true;

            return false;
        }
    }
}

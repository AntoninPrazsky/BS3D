using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace BS3D.Platform
{
    /// <summary>
    /// Holds the frame rate to a target without vsync: the game presents immediately and this idles out the
    /// rest of each frame's period. It replaces <c>SynchronizeWithVerticalRetrace</c> as the game's default
    /// cap (#270), and the reason is measured rather than stylistic.
    /// <para>
    /// <b>Why the game does not vsync any more.</b> On the owner's desktop (RX 6900 XT, 3840×1600 at 75 Hz) a
    /// played level presented at <b>37.5 FPS — exactly half refresh — while the frame itself cost under 5 ms</b>.
    /// The same build, level and camera held <c>fpscap=200</c>, <c>150</c>, <c>100</c> and <c>76</c> exactly,
    /// and 76 paces a frame the same way a 75 Hz vsync does, so it was neither the pacing nor the frame's cost.
    /// Paired repeats: vsync <b>37.5 / 37.5</b> against a 75 Hz limiter <b>75.0 / 75.0</b>. It was also not
    /// monotonic in supersampling — ssaa 1 → 75, 2 → 37.5, 3 → 31.8, 4 → 75 — which no cost curve can be. The
    /// mechanism inside DXGI/MonoGame was <b>not</b> pinned down; what is established is that presenting
    /// immediately and idling here doubles the frame rate on the case that was reported.
    /// </para>
    /// <para>
    /// <b>This does not tear</b>, and that is what makes the trade cheap: the game is only ever windowed or
    /// <i>borderless</i> fullscreen (<c>HardwareModeSwitch = false</c>, #157), so DWM owns the flip in every
    /// mode the game has and composites at the panel's own rate whatever the app asks for. There is no display
    /// mode to lose and no scanout to race.
    /// </para>
    /// <para>
    /// <b>The target sits slightly ABOVE the refresh</b> (see <see cref="REFRESH_MARGIN"/>) rather than on it.
    /// A limiter never pays back the debt of a frame that overran — doing so would print a cheap frame as an
    /// expensive one — so a limiter aimed exactly at the refresh runs, on average, a shade *slower* than the
    /// compositor consumes and periodically leaves it nothing new to show, which reads as a repeated frame. A
    /// few percent of headroom means a fresh frame is always waiting instead.
    /// </para>
    /// </summary>
    internal sealed class FrameLimiter : IDisposable
    {
        /// <summary>
        /// How far over the display's refresh the default target sits, for the reason in the class doc. Small
        /// enough that it costs no meaningful extra work — at 75 Hz it is about two frames a second.
        /// </summary>
        private const float REFRESH_MARGIN = 1.03f;

        /// <summary>
        /// How much of the period is spun rather than slept. Even with the timer resolution raised to 1 ms a
        /// sleep may return late, and a limiter that overshoots its period is a limiter that misses the
        /// compositor — so the last stretch is spun, where the wait is exact. The <b>Testbed's</b> copy of this
        /// idle spins the whole period, which is right for a benchmark and wrong here: at 75 Hz with a 5 ms
        /// frame that is a core held at 100 % for the life of the session, and this project's other development
        /// machine is a laptop.
        /// </summary>
        private static readonly TimeSpan SpinThreshold = TimeSpan.FromMilliseconds(2.0);

        //timeBeginPeriod raises the process's timer resolution so Thread.Sleep(1) returns in about a
        //millisecond instead of at the next 15.6 ms tick. Without it the sleep below is useless: measured on
        //the Testbed's own idle, an unraised Sleep(1) cost about six milliseconds, which slept a 300 FPS cap
        //down to 143 and a 400 FPS cap to 209 — the instrument reading its own idle rather than the frame.
        //Per-process since Windows 10 2004, and paired with timeEndPeriod in Dispose.
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint milliseconds);

        private bool _timerResolutionRaised;
        private bool _disposed;

        //When the next frame may be presented, on the wall clock. Stopwatch and not GameTime, because what this
        //spends is REAL time between presents; MonoGame's own fixed time step was the other candidate and is
        //refused for the reason the Testbed refuses it — it feeds Update a synthetic elapsed and runs it more
        //than once per Draw to catch up, which changes what the physics and every animation are handed.
        private long _nextFrameDue;

        /// <summary>
        /// Frames a second to hold, or <b>0 for no limit at all</b> — which is what <c>nocap</c> and the
        /// Settings row ask for. Written whenever the monitor's refresh is re-read or the player changes the
        /// setting; a change takes effect on the very next frame and needs no device reset, which is the one
        /// thing this is simpler at than the vsync it replaced.
        /// </summary>
        public int TargetHz { get; set; }

        /// <summary>
        /// The default target for a display refreshing at <paramref name="refreshHz"/>: the refresh plus
        /// <see cref="REFRESH_MARGIN"/>. Zero (or nonsense) in gives zero out — an adapter that reports no
        /// refresh must not be turned into a 0 FPS cap, and unlimited is the honest fallback.
        /// </summary>
        public static int TargetForRefresh(int refreshHz) =>
            refreshHz > 0 ? (int)MathF.Ceiling(refreshHz * REFRESH_MARGIN) : 0;

        /// <summary>
        /// Idles out the rest of this frame's period. Call once per frame, at the very END of Draw — after
        /// anything that measures the frame, or the measurement reads this idle instead of the work.
        /// <para>
        /// A frame that already overran its period is never delayed and never made to pay the overrun back out
        /// of the next frame's idle: the debt would come out of a later frame and print a cheap one as an
        /// expensive one. So the plateau on the target reads as "cheaper than this", never as a cost.
        /// </para>
        /// </summary>
        public void EndFrame()
        {
            int target = TargetHz;

            if (target <= 0)
            {
                //Unlimited: drop the schedule so that turning the limit back on mid-session starts from now
                //rather than from a due time that went stale minutes ago and would burn one instant frame.
                _nextFrameDue = 0;
                return;
            }

            EnsureTimerResolution();

            long period = Stopwatch.Frequency / target;
            long now = Stopwatch.GetTimestamp();

            //Also catches the first frame after the limit was turned on, when _nextFrameDue is 0 and long past
            if (now >= _nextFrameDue)
            {
                _nextFrameDue = now + period;
                return;
            }

            //Sleep the bulk of it, spin the last SpinThreshold. Sleep(1) rather than Sleep(remaining): the
            //resolution is raised to a millisecond, not to the tick this actually wants, so asking for one
            //millisecond at a time is what keeps the overshoot bounded by one instead of by the whole wait.
            while (true)
            {
                long remainingTicks = _nextFrameDue - Stopwatch.GetTimestamp();
                if (remainingTicks <= 0) break;

                double remainingMs = remainingTicks * 1000.0 / Stopwatch.Frequency;
                if (remainingMs <= SpinThreshold.TotalMilliseconds) break;

                Thread.Sleep(1);
            }

            while (Stopwatch.GetTimestamp() < _nextFrameDue) Thread.SpinWait(64);

            _nextFrameDue += period;
        }

        //Raised lazily and only while something is actually being limited, so an unlimited session never asks
        //the system for a finer timer than it needs
        private void EnsureTimerResolution()
        {
            if (_timerResolutionRaised || _disposed) return;

            TimeBeginPeriod(1);
            _timerResolutionRaised = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_timerResolutionRaised)
            {
                TimeEndPeriod(1);
                _timerResolutionRaised = false;
            }
        }
    }
}

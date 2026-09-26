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

        //Blocks until the compositor has finished its current composition. It is the one primitive that lets a
        //windowed program pace itself BY the compositor rather than race it - see WaitForCompositor (#448).
        [DllImport("dwmapi.dll", EntryPoint = "DwmFlush")]
        private static extern int DwmFlush();

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
        /// Waits for the compositor's next composition, at the <b>end</b> of a frame — its work already flushed
        /// to the GPU, just before Present (#634; it stood at the top of the frame until then, see
        /// <c>BS3DGame.PaceFrame</c>) — and answers whether the wait actually happened (#448).
        /// <para>
        /// ⚠ <b>The schedule below cannot produce smooth motion against DWM, and the margin it is aimed at
        /// makes it worse rather than better.</b> The compositor shows at most one frame per refresh, so a
        /// program presenting at <see cref="TargetForRefresh"/>'s 78 into a 75 Hz panel has three of its
        /// frames a second thrown away - and a thrown-away frame is a skipped step of everything that moves,
        /// which is a small periodic hitch three times a second. That was #448, and it is arithmetic rather
        /// than a hypothesis: the shipped limiter measures a flat 78.0 FPS on a 75 Hz panel, dead steady.
        /// </para>
        /// <para>
        /// Aiming the schedule exactly at the refresh fixes most of it - measured, a flat 75.0 - but a
        /// free-running clock has no phase relationship with the compositor, so the two slide past each
        /// other on nothing but clock error and the hitch becomes rare and irregular rather than gone.
        /// <c>DwmFlush</c> removes the race instead of tuning it: the frame after it begins just past a
        /// composition, so exactly one frame is presented per refresh and nothing is discarded. Measured at
        /// a flat 75.0 as well, so the phase lock is free.
        /// </para>
        /// <para>
        /// <b>It is not vsync.</b> The wait is taken before Present rather than inside it, so a frame that fits
        /// in the interval cannot miss a vblank and land at half rate - which is the failure #270 measured on
        /// the vsync this limiter replaced (and re-measured on the Moon in #634: 55 FPS shown, 55 ms latency).
        /// </para>
        /// <para>
        /// A false answer is DWM refusing (composition off, or the call failing), and the caller then has
        /// <see cref="EndFrame"/>'s ordinary schedule behind it - which is why
        /// <see cref="REFRESH_MARGIN"/> keeps its shipped value: it now governs only that fallback.
        /// </para>
        /// </summary>
        public bool WaitForCompositor() => DwmFlush() == 0;

        //When the last compositor wait ended, and the refresh period measured off those ends (see PacedElapsed)
        private long _lastCompositionEnd;
        private double _compositionPeriod;

        /// <summary>
        /// Drops the last wait's end, on a frame that was not paced by the compositor, so the next paced frame does not
        /// measure its interval across the unpaced ones (a limit turned on and off, composition refused for a while).
        /// </summary>
        public void ForgetComposition() => _lastCompositionEnd = 0;

        /// <summary>
        /// How much time this frame stands for, when it was paced by <see cref="WaitForCompositor"/>: the interval
        /// between the ends of two successive waits, put on a whole number of refresh periods. Call it right after
        /// a wait that returned true; <paramref name="refreshHz"/> seeds the period until it has been measured.
        /// <para>
        /// <b>Why not MonoGame's <c>ElapsedGameTime</c>.</b> MonoGame reads its clock at the top of the tick, which
        /// is not where the wait is, so its elapsed carries the difference between the last two frames' costs. Measured on the Moon's front end at
        /// 3840×1600, 75 Hz: that elapsed ran <b>13.33 ± 1.0 ms, anywhere from 10.2 to 16.4</b>, while the ends of the
        /// waits, which are what the compositor shows, came <b>13.33 ± 0.16</b> apart. Every frame was shown one refresh
        /// after the last, but the world was stepped by up to a quarter more or less than that — invisible at the
        /// orbit's centre, where the island and the map hardly move on screen, and a plain judder on the far scenery
        /// the orbit sweeps across the frame (the owner saw it on the Earth over the Moon).
        /// </para>
        /// <para>
        /// The period is measured rather than taken from <paramref name="refreshHz"/>, which is a whole number
        /// (a 59.94 Hz panel reports 59 or 60); only intervals within a sixth of the running estimate refine it, so a
        /// missed composition does not drag it. An interval near a whole number of periods is snapped to it — one
        /// period for a frame on time, two for a frame that missed a composition, which is then shown two refreshes
        /// after the last and must move twice as far. One far from any (a stall, a wait DWM cut short) is returned as
        /// measured.
        /// </para>
        /// </summary>
        public float PacedElapsed(int refreshHz)
        {
            long now = Stopwatch.GetTimestamp();
            long last = _lastCompositionEnd;
            _lastCompositionEnd = now;

            if (_compositionPeriod <= 0.0) _compositionPeriod = refreshHz > 0 ? 1.0 / refreshHz : 0.0;
            if (last == 0 || _compositionPeriod <= 0.0) return -1f;

            double interval = (now - last) / (double)Stopwatch.Frequency;
            double periods = interval / _compositionPeriod;

            if (Math.Abs(periods - 1.0) < 1.0 / 6.0) _compositionPeriod += (interval - _compositionPeriod) * 0.02;

            double whole = Math.Round(periods);
            return whole >= 1.0 && Math.Abs(periods - whole) < 0.25 ? (float)(whole * _compositionPeriod) : (float)interval;
        }

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

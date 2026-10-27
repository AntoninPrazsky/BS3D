using BS3D.Capture;
using Microsoft.Xna.Framework;
using System;

namespace BS3D
{
    /// <summary>
    /// The host's half of a <b>capture run</b> (#946): the hooks the frame asks <see cref="CaptureRun"/> at, and what the
    /// session asks the host (<see cref="Capturing"/>, <see cref="TryCaptureLens"/>). What a capture run is lives in
    /// <see cref="CaptureRun"/>; <c>docs/website.md</c> says how to take one.
    /// </summary>
    public partial class BS3DGame
    {
        //Null in every run that is not a capture, which is every run but a scripted one: the three places the frame asks it
        //(the top of Update, the end of Draw and what the session draws) each test it once and do nothing else
        private CaptureRun _capture;

        /// <summary>
        /// Builds the capture run from the command line (the constructor's), or says that the arguments only a capture takes were
        /// given without one and are ignored: <c>campos=</c> in a plain run would otherwise read as accepted.
        /// </summary>
        private void BeginCapture(LaunchOptions launch)
        {
            if (launch.CaptureDirectory != null)
            {
                _capture = CaptureRun.Create(launch);

                //The first frame the run films, and the two things a script against it needs: the clock and the step
                string lens = _capture.Lens == null ? "the play camera" : $"a stated lens of {_capture.Lens.KeyCount} key(s)";
                string action = _capture.Demo ? ", the benchmark's action" : string.Empty;
                Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"[capture] {_capture.Plan.Fps} frames a second of game time (a step of {_capture.Plan.StepSeconds * 1000f:0.###} ms), {_capture.Plan.FrameCount} frame(s) from frame {_capture.Plan.FirstUpdate}, {lens}{action}"));
                return;
            }

            if (launch.CamPos.HasValue || launch.CamTarget.HasValue || launch.CamFov.HasValue || launch.CamPath != null
                || launch.CaptureShake || launch.CaptureAim || launch.CaptureDemo)
                Console.WriteLine("[capture] campos=, camtarget=, fov=, campath=, captureshake, captureaim and capturedemo only mean something with capture=<folder>: ignored");
        }

        /// <summary>
        /// Whether this run is a capture (#946). The session draws no HUD, crosshair, card or aim preview under it, reads its scripted
        /// hands (<c>fire=</c>, <c>aim=</c>...) whether or not the window has the focus, and takes no input from the keyboard, the
        /// mouse or the pad: nobody is at it, and one who is must not move a frame.
        /// </summary>
        internal bool Capturing => _capture != null;

        /// <summary>Whether the session leaves the aim preview (the beam and the ghost ball) out of the frame: a capture that did not ask for it with <c>captureaim</c>.</summary>
        internal bool HidesAimPreview => _capture is { ShowAim: false };

        /// <summary>Whether the benchmark's action plays in this capture (<c>capturedemo</c>): the gun fires by itself.</summary>
        internal bool CaptureDemo => _capture is { Demo: true };

        /// <summary>
        /// The capture's stated lens on this frame, or false for the play camera (a run that is not a capture, or one with no lens
        /// of its own). The field of view is NaN where the lens states none.
        /// </summary>
        internal bool TryCaptureLens(out Vector3 position, out Vector3 target, out float fieldOfView, out bool keepShake)
        {
            keepShake = _capture != null && _capture.KeepShake;
            if (_capture == null)
            {
                position = target = default;
                fieldOfView = float.NaN;
                return false;
            }

            return _capture.TryLens(out position, out target, out fieldOfView);
        }

        /// <summary>
        /// Says, once, on the first frame the capture writes, where the play camera stands and looks, and where the gun and the middle of
        /// the cluster are - the world coordinates <c>campos=</c>, <c>camtarget=</c> and a path's keys are written in, which nothing else
        /// prints for the Game (the Testbed's <c>C</c> key does the same). The play lens is the overview's before any takeover or lens
        /// of the capture's own is composed over it.
        /// </summary>
        internal void ReportCapturePose(Vector3 position, Vector3 target, float fieldOfView, Vector3 gun, Vector3 cluster)
        {
            if (_capture.Updates != _capture.Plan.FirstUpdate) return;

            static string Vec(Vector3 v) => FormattableString.Invariant($"{v.X:0.00},{v.Y:0.00},{v.Z:0.00}");

            Console.WriteLine(FormattableString.Invariant(
                $"[capture] play lens: campos={Vec(position)} camtarget={Vec(target)} fov={MathHelper.ToDegrees(fieldOfView):0.#}; gun at {Vec(gun)}; cluster centre {Vec(cluster)}"));
        }

        /// <summary>
        /// The end of <see cref="Draw"/>, after the frame is whole and the ordinary screenshot has been served: hands the back
        /// buffer to the writer and, after the last frame, ends the run. Said once when a page comes up over the level, since a page
        /// is a picture and the HUD's absence does not hide it (a result page, say).
        /// </summary>
        private void ServiceCapture()
        {
            if (_capture.Filming && _screens.Active is Screens.MenuPage page && !ReferenceEquals(page, _capturePageReported))
            {
                _capturePageReported = page;
                Console.WriteLine(FormattableString.Invariant(
                    $"[capture] a page is up over the frame at game time {_capture.GameSeconds:0.###} s: {page.GetType().Name} (it is in the picture)"));
            }

            if (!_capture.EndFrame(GraphicsDevice, _scene)) return;

            //The run is over: its frames are written and its manifest beside them. A failed one leaves a code a script can read
            if (_capture.Failed) Environment.ExitCode = 1;
            Exit();
        }

        private object _capturePageReported;
    }
}

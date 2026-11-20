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

                //Frames of their own size (#1177): the window only shows them, at width=/height= when those are given and otherwise as
                //large as the preview's box holds at the frames' aspect. The back buffer is made the frames' size in
                //Graphics_PreparingDeviceSettings, and HoldCaptureWindow keeps the window from growing to it
                if (_capture.Size is Point frames && (launch.WindowWidth <= 0 || launch.WindowHeight <= 0))
                    _windowedSize = CaptureSize.Preview(frames, CaptureSize.PREVIEW_BOX);

                //The game's own sound, recorded on the capture's clock (#1179): before LoadContent makes a single buffer. The run is not
                //muted then (LaunchOptions.ImplyCapture), so the recorded volumes are the real ones, and the hardware is silenced
                //instead: a capture is a run nobody is listening to
                if (launch.CaptureAudio)
                {
                    CaptureSounds.Start();
                    try
                    {
                        Microsoft.Xna.Framework.Audio.SoundEffect.MasterVolume = 0f;
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        Console.WriteLine($"[capture] the hardware's master volume could not be set to zero ({exception.GetType().Name}): the run may be heard");
                    }
                }

                //The first frame the run films, and the two things a script against it needs: the clock and the step
                string lens = _capture.Lens == null ? "the play camera" : $"a stated lens of {_capture.Lens.KeyCount} key(s)";
                _captureDemoNoCuts = launch.CaptureDemoNoCuts;
                string action = _capture.Demo ? (_captureDemoNoCuts ? ", the benchmark's action without its cuts" : ", the benchmark's action") : string.Empty;
                string hud = (_capture.ShowHud ? ", the HUD of play" : string.Empty) + (_capture.Splash ? ", the 2D logo first" : string.Empty)
                    + (launch.CaptureAudio ? ", the game's sound recorded" : string.Empty);
                string size = _capture.Size is Point film ? $", frames of {film.X}x{film.Y} shown in a {_windowedSize.X}x{_windowedSize.Y} window" : string.Empty;
                Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"[capture] {_capture.Plan.Fps} frames a second of game time (a step of {_capture.Plan.StepSeconds * 1000f:0.###} ms), {_capture.Plan.FrameCount} frame(s) from frame {_capture.Plan.FirstUpdate}, {lens}{action}{hud}{size}"));
                return;
            }

            if (launch.CamPos.HasValue || launch.CamTarget.HasValue || launch.CamFov.HasValue || launch.CamPath != null
                || launch.CaptureShake || launch.CaptureAim || launch.CaptureDemo || launch.CaptureHud || launch.CaptureSize != null
                || launch.CaptureSplash || launch.CaptureAudio)
                Console.WriteLine("[capture] campos=, camtarget=, fov=, campath=, captureshake, captureaim, capturedemo, capturehud, capturesize=, capturesplash and captureaudio only mean something with capture=<folder>: ignored");
        }

        /// <summary>
        /// The size the back buffer is made when a capture states its frames' own (#1177), or null for the window's: what
        /// <see cref="Graphics_PreparingDeviceSettings"/> writes over the presentation every time the device is made or reset.
        /// </summary>
        private Point? CaptureBackBufferSize => _capture?.Size;

        /// <summary>
        /// Keeps the window of a capture with frames of their own size at its preview (#1177), before the device is made. MonoGame
        /// sizes the window to the back buffer whenever the presentation changes (<c>WinFormsGameWindow.OnPresentationChanged</c>), and
        /// a 3840x2160 window on the desktop's 3840x1600 panel is the whole screen, clamped; held, the window stays the preview and the
        /// swap chain stays the frames' size, because MonoGame sets that size with its own resize handler switched off and the
        /// window's real size never reaches the back buffer. A resize by hand ends the same way: the back buffer is asked for the
        /// window's size, the presentation is made the frames' again, and the window is held again.
        /// </summary>
        private void HoldCaptureWindow()
        {
            if (_capture?.Size == null) return;

            Platform.DisplayRefresh.HoldClientSize(Window.Handle, _windowedSize.X, _windowedSize.Y);
        }

        /// <summary>
        /// Whether the session leaves the HUD of play out of the frame: a capture that did not ask for it with <c>capturehud</c>
        /// (#1177). The skip hint of a tour stays out of every capture whatever this says: nobody can skip a clip.
        /// </summary>
        internal bool CaptureHidesHud => _capture is { ShowHud: false };

        /// <summary>Whether this run is a capture that films the 2D logo first (<c>capturesplash</c>, #1179), whatever the Intro logo row says.</summary>
        internal bool CaptureFilmsSplash => _capture is { Splash: true };

        /// <summary>
        /// Whether this run is a capture (#946). The session draws no HUD or crosshair under it unless it asks for them
        /// (<see cref="CaptureHidesHud"/>, #1177), no tutorial card, skip hint or aim preview (unless <c>captureaim</c>), reads its scripted
        /// hands (<c>fire=</c>, <c>aim=</c>...) whether or not the window has the focus, and takes no input from the keyboard, the
        /// mouse or the pad: nobody is at it, and one who is must not move a frame.
        /// </summary>
        internal bool Capturing => _capture != null;

        /// <summary>Whether the session leaves the aim preview (the beam and the ghost ball) out of the frame: a capture that did not ask for it with <c>captureaim</c>.</summary>
        internal bool HidesAimPreview => _capture is { ShowAim: false };

        /// <summary>Whether the benchmark's action plays in this capture (<c>capturedemo</c>): the gun fires by itself.</summary>
        internal bool CaptureDemo => _capture is { Demo: true };

        //"capturedemonocuts", read once as the capture begins
        private bool _captureDemoNoCuts;

        /// <summary>
        /// Whether this capture's benchmark action leaves out its two cuts (<c>capturedemonocuts</c>, #1178): a run that looks for
        /// shots a scripted hand will replay, from a gun a script walks round the island. A cutter aimed from where the gun
        /// then stands into storeys the Cut may not strike is refused (#692), and the action waits on it and finds nothing more.
        /// Never in the benchmark itself, which is no capture.
        /// </summary>
        internal bool CaptureDemoSkipsCuts => CaptureDemo && _captureDemoNoCuts;

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
            //The sounding voices' volumes, pitches and stops, once a frame on the capture's clock (#1179)
            if (CaptureSounds.Recording) CaptureSounds.Poll();

            if (_capture.Filming && _screens.Active is Screens.MenuPage page && !ReferenceEquals(page, _capturePageReported))
            {
                _capturePageReported = page;
                Console.WriteLine(FormattableString.Invariant(
                    $"[capture] a page is up over the frame at game time {_capture.GameSeconds:0.###} s: {page.GetType().Name} (it is in the picture)"));
            }

            //Once, on the first frame written: what the frames are drawn from, which no other line of a capture says (#1177). An
            //8K-into-4K film is the Ultra tier's supersample of 2 under capturesize=3840x2160, and this is where that is seen to hold.
            //Not on the window's first UPDATE: a window from game time 0 begins with the second, the first is never drawn
            if (!_captureSceneReported && _capture.Updates >= _capture.Plan.FirstUpdate && _pipeline?.SceneTarget is { } scene)
            {
                _captureSceneReported = true;
                Console.WriteLine($"[capture] the scene is drawn at {scene.Width}x{scene.Height} (the {_quality} tier, supersample {_supersampleFactor}x, "
                    + $"{(scene.MultiSampleCount > 1 ? $"MSAA {scene.MultiSampleCount}x" : "no MSAA")}) and filtered down to the frames, film grain {(_effective.Grain ? "on" : "off")}");
            }

            if (!_capture.EndFrame(GraphicsDevice, _scene)) return;

            //The run is over: its frames are written and its manifest beside them. A failed one leaves a code a script can read
            if (_capture.Failed) Environment.ExitCode = 1;
            Exit();
        }

        private object _capturePageReported;
        private bool _captureSceneReported;
    }
}

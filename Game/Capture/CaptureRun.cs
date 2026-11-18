using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace BS3D.Capture
{
    /// <summary>
    /// A <b>capture run</b> (#946, <c>capture=&lt;folder&gt;</c>): the Game filming itself for the website's pictures and looping
    /// clips. The run is the ordinary game with four things changed, each by one object here and a hook where the host asks it:
    /// <list type="number">
    /// <item><b>The clock is a fixed step per frame</b> (<see cref="CapturePlan"/>, <see cref="FixStep"/>): the host's <c>Update</c>
    /// hands every screen the same <c>GameTime</c> step whatever the frame cost, so frame <i>n</i> is the world at <i>n</i> / fps
    /// seconds, and <c>fire=</c>, <c>sweep=</c>, <c>aim=</c>, <c>cut=</c> and the rest of <see cref="ScriptedPlay"/> (read off that clock)
    /// act on it.</item>
    /// <item><b>The frames are written off the render thread</b> (<see cref="CaptureWriter"/>), numbered from 1, for the window the plan
    /// names; the run ends itself once the last is written.</item>
    /// <item><b>The lens can be stated</b> (<see cref="CaptureCamera"/>): held (<c>campos=</c>, <c>camtarget=</c>, <c>fov=</c>) or flown
    /// (<c>campath=</c>), replacing the play camera's pose in <c>GameplayScreen.UpdateCamera</c>, and with it held still (no kick of the
    /// gun's shake) unless <c>captureshake</c> says otherwise.</item>
    /// <item><b>No HUD</b> unless <c>capturehud</c> asks for it (#1177): the session draws no readout, crosshair, chip, card or aim
    /// preview (<see cref="BS3DGame.CaptureHidesHud"/>), and the FPS line is off.</item>
    /// </list>
    /// And the frames may be a size of their own (<c>capturesize=</c>, #1177, <see cref="Size"/>): the back buffer is made that size
    /// and the window only shows it, so the film's size is not the display's. The game's 2D logo may open the film
    /// (<c>capturesplash</c>, #1179, <see cref="Splash"/>), and the game's own sound may be recorded beside it on the same clock
    /// (<c>captureaudio</c>, #1179, <see cref="CaptureSounds"/>), mixed into <see cref="AUDIO_FILE"/> when the run finishes.
    /// A run that is not a capture has none of this: the host holds a null here and tests it once a frame.
    /// </summary>
    internal sealed class CaptureRun
    {
        private readonly string _directory;
        private CaptureWriter _writer;
        private SceneKind _scene;
        private int _update, _lastDrawn, _captured, _undrawn;
        private float _firstFrameSeconds;
        private bool _failed, _finished;

        private CaptureRun(string directory, CapturePlan plan, CaptureCamera lens, bool keepShake, bool showAim, bool demo, bool showHud, Point? size,
            bool splash)
        {
            Splash = splash;
            _directory = directory;
            Plan = plan;
            Lens = lens;
            KeepShake = keepShake;
            ShowAim = showAim;
            Demo = demo;
            ShowHud = showHud;
            Size = size;
        }

        internal CapturePlan Plan { get; }

        /// <summary>The game's sound beside the frames, when the run records it (<c>captureaudio</c>, #1179).</summary>
        internal const string AUDIO_FILE = "audio.wav";

        /// <summary>The stated lens, or null to film through the play camera.</summary>
        internal CaptureCamera Lens { get; }

        /// <summary>Whether the gun's kick still shakes a stated lens (<c>captureshake</c>); off, a stated lens is held still.</summary>
        internal bool KeepShake { get; }

        /// <summary>Whether the aim preview (the beam and the ghost ball) is drawn (<c>captureaim</c>); off, as nobody holds the aim of a clip.</summary>
        internal bool ShowAim { get; }

        /// <summary>Whether the benchmark's action plays (<c>capturedemo</c>): the gun fires by itself and its lens circles it.</summary>
        internal bool Demo { get; }

        /// <summary>Whether the HUD of play is drawn (<c>capturehud</c>, #1177): the score, the chips, the strip, the crosshair. Off, the clip is the picture alone.</summary>
        internal bool ShowHud { get; }

        /// <summary>
        /// The frames' size when it is not the window's (<c>capturesize=</c>, #1177): the host makes the back buffer this size and holds
        /// the window at a preview of it (<see cref="CaptureSize"/>). Null: the frames are the window's back buffer, as before.
        /// </summary>
        internal Point? Size { get; }

        /// <summary>Whether the run films the game's 2D logo first (<c>capturesplash</c>, #1179), whatever the Intro logo row says.</summary>
        internal bool Splash { get; }

        /// <summary>The updates counted so far, from 1: the frame the run is on.</summary>
        internal int Updates => _update;

        /// <summary>Whether the window has begun and the run is still taking frames: what is on screen now is going into the clip.</summary>
        internal bool Filming => _captured > 0 && !_finished;

        /// <summary>True once the last frame is written or the run could not go on.</summary>
        internal bool Finished => _finished;

        /// <summary>Whether the run could not go on (a folder that cannot be written, a window that changed size).</summary>
        internal bool Failed => _failed;

        /// <summary>The folder the frames go to.</summary>
        internal string Folder => _directory;

        /// <summary>
        /// Builds the run from the command line, or throws a <see cref="FormatException"/> that says what in the arguments cannot
        /// be taken; <c>Program</c> reports it and ends before a window opens, because a capture that would film the wrong lens
        /// for its whole length is a wasted hour.
        /// </summary>
        internal static CaptureRun Create(LaunchOptions launch)
        {
            string directory = Path.GetFullPath(launch.CaptureDirectory);

            //Frames of an earlier run would sit among this one's and be encoded with them: refuse rather than delete (a folder
            //named by hand may hold anything)
            if (System.IO.Directory.Exists(directory) && System.IO.Directory.GetFiles(directory, "frame-*.png").Length > 0)
                throw new FormatException($"capture folder '{directory}' already holds frames: give it an empty folder");

            CapturePlan plan = new(launch.CaptureFps, launch.CaptureFrom, launch.CaptureSeconds);

            CaptureCamera lens = null;
            bool stated = launch.CamPos.HasValue || launch.CamTarget.HasValue || launch.CamFov.HasValue;
            if (launch.CamPath != null)
            {
                if (stated) throw new FormatException("give campos=, camtarget= and fov= or campath=, not both");

                lens = CaptureCamera.Load(launch.CamPath);
            }
            else if (stated)
            {
                if (!launch.CamPos.HasValue || !launch.CamTarget.HasValue) throw new FormatException("campos= and camtarget= go together (fov= is optional beside them)");

                lens = CaptureCamera.Held(launch.CamPos.Value, launch.CamTarget.Value, launch.CamFov ?? float.NaN);
            }

            Point? frames = null;
            if (launch.CaptureSize != null)
            {
                if (!CaptureSize.TryParse(launch.CaptureSize, out Point size))
                    throw new FormatException(string.Create(CultureInfo.InvariantCulture,
                        $"capturesize={launch.CaptureSize} is not <width>x<height> with both sides even, from {CaptureSize.MIN_SIDE} to {CaptureSize.MAX_SIDE} (3840x2160)"));
                frames = size;

                //The window is held at a preview of the frames through WinForms (DisplayRefresh.HoldClientSize); off Windows nothing
                //holds it, and a window the frames' size is the display's problem again
                if (!OperatingSystem.IsWindows()) throw new FormatException("capturesize= is the Windows build's (its window is held at a preview through WinForms)");

                //The scene is drawn at the frames' size times the tier's supersample factor (ssaa= over it), and Direct3D 11 makes no
                //texture over 16384 a side: an 8K film at Ultra is 15360 wide, at ssaa=3 it would fail on its first target
                int factor = Math.Clamp(launch.SupersampleFactor ?? QualityPreset.Presets[(int)(launch.Quality ?? QualityLevel.High)].SupersampleFactor,
                    1, PhoneTiers.MaxSupersample);
                if (!CaptureSize.SceneFits(size, factor))
                    throw new FormatException(string.Create(CultureInfo.InvariantCulture,
                        $"capturesize={size.X}x{size.Y} at a supersample factor of {factor} is a scene of {size.X * factor}x{size.Y * factor}, over Direct3D's {CaptureSize.MAX_SIDE} a side: a lower ssaa= or a smaller size"));
            }

            return new CaptureRun(directory, plan, lens, launch.CaptureShake, launch.CaptureAim, launch.CaptureDemo, launch.CaptureHud, frames,
                launch.CaptureSplash);
        }

        /// <summary>
        /// The top of the host's <c>Update</c>: counts the frame and sets the step on the <paramref name="gameTime"/> every screen
        /// is about to read, in place and without an allocation (<c>GameTime</c>'s two spans are settable). The same object is
        /// handed to <c>Draw</c>, so what draws reads the step too.
        /// </summary>
        internal void FixStep(GameTime gameTime)
        {
            //An update inside the window whose frame was never drawn (the framework skips a draw now and then, the first of all) is a
            //step of the world with no picture of it: the clip jumps there. Counted from the first frame written, and said at the end
            if (_captured > 0 && !_finished && _lastDrawn != _update) _undrawn++;

            _update++;
            gameTime.ElapsedGameTime = Plan.Step;
            gameTime.TotalGameTime = TimeSpan.FromTicks(Plan.StepTicks * _update);

            //The game's sound is stamped with the same clock as the frames (#1179)
            if (CaptureSounds.Recording) CaptureSounds.Now = GameSeconds;
        }

        /// <summary>
        /// Where the stated lens is on this frame, or false for a run with none. The seconds are those into the window (0 at the
        /// first written frame), so a path's times do not depend on how long the level took to start; before the window the lens
        /// stands at the path's first key. <paramref name="fieldOfView"/> is NaN where nothing states it.
        /// </summary>
        internal bool TryLens(out Vector3 position, out Vector3 target, out float fieldOfView)
        {
            position = target = default;
            fieldOfView = float.NaN;
            if (Lens == null) return false;

            Lens.Pose(Plan.WindowSeconds(_update), out position, out target, out float degrees);
            fieldOfView = float.IsNaN(degrees) ? float.NaN : MathHelper.ToRadians(degrees);
            return true;
        }

        /// <summary>The game's clock on this frame, as the scripted arguments read it (<c>fire=10</c> is the frame at 10 s).</summary>
        internal float GameSeconds => Plan.GameSeconds(_update);

        /// <summary>
        /// The end of the host's <c>Draw</c>, with the frame as it was presented: reads it back and hands it to the writer when the
        /// frame is in the window, and finishes the run after the last. Returns whether the run is over and the host should exit.
        /// </summary>
        internal bool EndFrame(GraphicsDevice device, SceneKind scene)
        {
            _scene = scene;
            if (_finished) return true;
            if (_update < Plan.FirstUpdate) return false;

            int width = device.PresentationParameters.BackBufferWidth;
            int height = device.PresentationParameters.BackBufferHeight;

            if (_writer == null)
            {
                //A minimized window reads as no back buffer at all, and nothing can be filmed through it
                if (width <= 0 || height <= 0) return Abort("the back buffer has no size (the window is minimized)");

                //Two threads do for 1080p at 60 frames a second on a few cores and four for more than that; the render thread and
                //the simulation's workers have the rest
                //A size of the run's own that the back buffer does not have is a film of the wrong size for its whole length: the
                //device was made at another (PreparingDeviceSettings did not run, or something reset it)
                if (Size is Point stated && (width != stated.X || height != stated.Y))
                    return Abort($"the back buffer is {width}x{height}, not the {stated.X}x{stated.Y} capturesize= asked for");

                _writer = new CaptureWriter(_directory, width, height, Math.Clamp(Environment.ProcessorCount / 4, 2, 4));
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"[capture] writing {Plan.FrameCount} frame(s) of {width}x{height} to '{_directory}', {Plan.Fps} a second of game time, the first drawn after update {_update} ({GameSeconds:0.###} s)"));
            }

            if (width != _writer.Width || height != _writer.Height)
                return Abort($"the back buffer changed size to {width}x{height} (the frames are {_writer.Width}x{_writer.Height}): do not resize the window during a capture");

            Color[] pixels = _writer.Rent();
            BackBufferPixels.Read(device, pixels);

            //Numbered by the frames written and not by the update, so a draw the framework skipped leaves no gap in the files
            _lastDrawn = _update;
            if (_captured == 0) _firstFrameSeconds = GameSeconds;
            int frame = ++_captured;
            _writer.Submit(pixels, frame);

            //One line a second of game time: the clock the scripted arguments read, for writing a script against a run
            if (frame % Plan.Fps == 0 || frame == Plan.FrameCount)
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"[capture] frame {frame}/{Plan.FrameCount} at game time {GameSeconds:0.###} s"));

            return frame >= Plan.FrameCount && Finish(scene);
        }

        /// <summary>Ends the run: waits for the writers, says what was written and leaves a <c>capture.json</c> beside the frames.</summary>
        internal bool Finish(SceneKind scene)
        {
            if (_finished) return true;
            _finished = true;

            if (_writer == null) return true;

            (int written, int failed, string firstFailure) = _writer.Complete();
            if (failed > 0)
            {
                _failed = true;
                Console.WriteLine($"[capture] {failed} frame(s) could not be written, the first: {firstFailure}");
            }

            if (_undrawn > 0)
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"[capture] {_undrawn} update(s) inside the window drew no frame: the clip jumps {_undrawn / (float)Plan.Fps:0.###} s ahead there"));

            //The game's sound for exactly the frames written (#1179): from the first frame's clock, as many samples as the frames last
            string audio = null;
            if (CaptureSounds.Recording && written > 0)
            {
                int samples = (int)Math.Round(written * (double)SoundMix.OUTPUT_RATE / Plan.Fps);
                try
                {
                    Console.WriteLine(CaptureSounds.Write(Path.Combine(_directory, AUDIO_FILE), _firstFrameSeconds, samples));
                    audio = AUDIO_FILE;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _failed = true;
                    Console.WriteLine($"[capture] {AUDIO_FILE} could not be written: {exception.Message}");
                }
            }

            try
            {
                string manifest = JsonSerializer.Serialize(new
                {
                    format = "bs3d-capture",
                    version = 1,
                    fps = Plan.Fps,
                    frames = written,
                    width = _writer.Width,
                    height = _writer.Height,
                    firstFrameGameSeconds = _firstFrameSeconds,
                    undrawnUpdates = _undrawn,
                    scene = scene.ToString(),
                    build = BuildVersion.DisplayName,
                    audio,
                    audioRate = audio != null ? SoundMix.OUTPUT_RATE : 0,
                }, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(_directory, "capture.json"), manifest + "\n");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[capture] capture.json could not be written: {exception.Message}");
            }

            Console.WriteLine($"[capture] done: {written} of {Plan.FrameCount} frame(s) in '{_directory}'");
            return true;
        }

        private bool Abort(string why)
        {
            Console.WriteLine($"[capture] stopped: {why}");
            _failed = true;
            Finish(_scene);
            return true;
        }
    }
}

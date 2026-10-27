using System;

namespace BS3D.Capture
{
    /// <summary>
    /// What a capture run takes and on what clock (#946): <b>a fixed step of game time per rendered frame</b>, and the window
    /// of those frames that is written out. Pure arithmetic over the arguments, so the clock's rules are held by tests
    /// without a device.
    /// <para>
    /// Frame <i>n</i> (the <i>n</i>th <c>Update</c> and the <c>Draw</c> after it, counted from 1) is the world at <i>n</i> /
    /// <see cref="Fps"/> seconds however long the frame took to render and write, which is what makes a clip repeatable and
    /// lets a frame take as long as the picture needs. The window starts at the first frame whose game time has reached
    /// <c>capturefrom=</c> and holds <c>captureseconds=</c> of it (<c>captureseconds=0</c> is one frame: a still).
    /// </para>
    /// <para>
    /// <b>The step is rounded UP to the clock's resolution, never down.</b> <see cref="TimeSpan"/> holds 100 ns, so
    /// 1/60 s is not representable, and the session spends its frame in whole physics steps of 1/120 s. A step a hair under
    /// two of them would leave a deficit that is carried and paid out as a frame of one step and a frame of three; rounded
    /// up, every frame holds at least its whole steps, and the excess (33 ns a frame at 60) takes over an hour of capture
    /// to add up to one more.
    /// </para>
    /// </summary>
    internal sealed class CapturePlan
    {
        /// <summary>
        /// The slowest frame rate a capture steps at. A capture raises the session's cap on physics steps a frame
        /// (<c>GameplayScreen.CAPTURE_MAX_STEPS_PER_FRAME</c>) so that a frame holds all of its time, and 120 / 15 is the 8
        /// steps a frame at this rate takes, well inside it.
        /// </summary>
        internal const int MIN_FPS = 15;

        /// <summary>The fastest, twice the physics' own 120 steps a second.</summary>
        internal const int MAX_FPS = 240;

        internal const int DEFAULT_FPS = 60;

        /// <summary>How much of the run is taken when <c>captureseconds=</c> is not given.</summary>
        internal const float DEFAULT_SECONDS = 5f;

        /// <summary>
        /// The latest game time a capture may start at or run for, an hour: an Infinity or a 1e10 would saturate the frame
        /// count's int cast into a run that never starts filming or never ends, and nothing a site wants is that long.
        /// </summary>
        internal const float MAX_SECONDS = 3600f;

        internal CapturePlan(int fps, float fromSeconds, float seconds)
        {
            if (fps < MIN_FPS || fps > MAX_FPS) throw new ArgumentOutOfRangeException(nameof(fps), fps, $"capturefps must be {MIN_FPS} to {MAX_FPS}");
            //Written as "not inside" so a NaN, which fails every comparison, is refused with the rest
            if (!(fromSeconds >= 0f && fromSeconds <= MAX_SECONDS)) throw new ArgumentOutOfRangeException(nameof(fromSeconds), fromSeconds, $"capturefrom must be 0 to {MAX_SECONDS} seconds");
            if (!(seconds >= 0f && seconds <= MAX_SECONDS)) throw new ArgumentOutOfRangeException(nameof(seconds), seconds, $"captureseconds must be 0 to {MAX_SECONDS} seconds");

            Fps = fps;
            StepTicks = (TimeSpan.TicksPerSecond + fps - 1) / fps;

            //The first frame whose game time has reached the start: frame n is at n / fps seconds. A hair under the product
            //is forgiven so that capturefrom=10 at 60 fps is frame 600 and not 601 for the float's sake
            FirstUpdate = Math.Max(1, (int)Math.Ceiling((double)fromSeconds * fps - 1e-4));
            FrameCount = seconds <= 0f ? 1 : Math.Max(1, (int)Math.Round((double)seconds * fps));
        }

        /// <summary>Frames of game time a second.</summary>
        internal int Fps { get; }

        /// <summary>The step as the clock holds it, in 100 ns ticks: <see cref="TimeSpan.TicksPerSecond"/> / <see cref="Fps"/>, rounded up.</summary>
        internal long StepTicks { get; }

        internal TimeSpan Step => TimeSpan.FromTicks(StepTicks);

        /// <summary>The step in seconds, as the game reads it off its <c>GameTime</c>.</summary>
        internal float StepSeconds => (float)Step.TotalSeconds;

        /// <summary>
        /// The first <c>Update</c> (counted from 1) whose <c>Draw</c> is written. The window is then the next <see cref="FrameCount"/>
        /// frames <i>drawn</i>: a frame the framework skips (the very first of a run is one) moves it on by one rather than leaving a hole.
        /// </summary>
        internal int FirstUpdate { get; }

        /// <summary>How many frames are written.</summary>
        internal int FrameCount { get; }

        /// <summary>
        /// Seconds into the window at update <paramref name="update"/>, 0 at the first written frame: what a camera path's times are
        /// measured in. Exact (frames over the nominal rate), not the game's clock with its tick rounding.
        /// </summary>
        internal float WindowSeconds(int update) => (float)((update - FirstUpdate) / (double)Fps);

        /// <summary>The game's clock at update <paramref name="update"/>, as <c>fire=</c> and <c>shot=</c> read it: the frames so far over the rate.</summary>
        internal float GameSeconds(int update) => (float)(update / (double)Fps);

        /// <summary>The file a written frame goes to: six digits, counted from 1, so <c>frame-%06d.png</c> reads a folder of them.</summary>
        internal static string FrameFileName(int frameNumber) => $"frame-{frameNumber:000000}.png";
    }
}

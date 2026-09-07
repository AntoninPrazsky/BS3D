using Prazsky.Core.Render;

namespace BS3D
{
    /// <summary>
    /// The host's half of <b>saving a frame</b> (#191): the game writes its own PNG, out of its own back
    /// buffer, rather than being photographed from outside.
    /// </summary>
    /// <remarks>
    /// <b>The mechanism itself is <see cref="ScreenshotWriter"/>'s since #371</b>, in one copy with the
    /// Testbed's — the reasons it exists at all (every external route to this window can hand back the wrong
    /// picture silently), the two schedules and the readback are all documented there. What is left here is
    /// what only this executable can answer: which clock the schedule runs on, what a shot is labelled with,
    /// and which key asks for one.
    /// <para>
    /// <b>F12</b> is the key, in both input paths — the menu chrome and the play loop — for the reason F11 is
    /// in both: one of them runs while a page is up and the other while a level is. F12 was the FPS overlay's
    /// toggle and the overlay moved to <b>F10</b> to give it up, on the author's call: F12 is where a hand
    /// reaches for a screenshot. It could not take F11 instead — that is fullscreen here, in the Testbed and
    /// in the map editor, and it is the one binding in this project a player already knows from everything
    /// else they run. So the Game's display keys are a contiguous F10 / F11 / F12 — overlay, fullscreen,
    /// shot — and the <b>Testbed keeps F12 for its own text overlay</b> and asks for a shot with F8, which is
    /// a small parity the two executables have lost and the price of the key.
    /// </para>
    /// </remarks>
    public partial class BS3DGame
    {
        //The schedule as the command line gave it, kept from the constructor until there is a device to build
        //the writer with: LoadContent is the first moment GraphicsDevice exists, and the writer holds it.
        private readonly float[] _shotSchedule;

        private ScreenshotWriter _shots;

        /// <summary>Built in <c>LoadContent</c>, once the device the readback needs is there.</summary>
        private void CreateScreenshotWriter() => _shots = new ScreenshotWriter(GraphicsDevice, "bs3d", _shotSchedule);

        /// <summary>Asks for a shot of the frame about to be drawn. Wired to F12 in both input paths.</summary>
        internal void RequestScreenshot() => _shots?.Request();

        /// <summary>
        /// Writes the finished frame if one was due. Called at the very end of <see cref="Draw"/> — after the
        /// screens, the FPS component, the Myra desktop and the sharp foreground layer — so what lands in the
        /// file is the frame as presented, the overlay and whatever page is up included. That is deliberate: a
        /// shot of the HUD or of the result screen is usually the point, and F10 hides the overlay first when
        /// a clean plate is wanted.
        /// <para>
        /// The label is the scene, because the backdrop is the first thing anybody asks about a shot of this
        /// game — and the level a run is playing overrides the scene it was launched with, so the name on the
        /// file is one of the two places (the <c>[fps]</c> line is the other) that say what was really drawn.
        /// </para>
        /// </summary>
        private void ServiceScreenshots() => _shots?.Service(_wallClock, _scene.ToString());
    }
}

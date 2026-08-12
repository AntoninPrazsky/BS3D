using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Globalization;
using System.IO;

namespace BS3D
{
    /// <summary>
    /// The host's half of <b>saving a frame</b> (#191): the game writes its own PNG, out of its own back
    /// buffer, rather than being photographed from outside.
    /// </summary>
    /// <remarks>
    /// It exists because every external route to this game's picture can hand back the wrong picture
    /// <i>silently</i>, all three measured on 2026-08-12 and recorded in
    /// <c>.claude/skills/screenshot/SKILL.md</c>: <c>CopyFromScreen</c> copies a rectangle of the SCREEN, so a
    /// locked desktop returns the lock screen and an unlocked one returns whatever window is in front of the
    /// game (a capture came back with a terminal in it, at the game window's exact rect); and the
    /// <c>PrintWindow</c> fallback returned a blank client area from this executable eleven times running while
    /// returning the real picture from the Testbed the same day, so it cannot be trusted either way. A
    /// contaminated capture is not blank — it is a sharp picture of the wrong thing, which is why this route
    /// exists rather than a better external one.
    /// <para>
    /// <b>Two triggers, and the second is the point.</b> The key serves a person at the machine; the
    /// <c>shot=</c> schedule serves the case the facility was asked for, a <b>locked desktop</b> — where
    /// neither a synthetic keystroke nor a focus click reaches the application at all, so a key-only facility
    /// would work exactly where the old external capture already worked and fail exactly where it did not.
    /// The schedule is also what makes a shot repeatable: "the result screen at 3.5 s and at 8 s" is a thing a
    /// script can ask for twice and compare.
    /// </para>
    /// <para>
    /// Nothing here touches the screen, the window or the compositor: it is the swap chain's own back buffer,
    /// so it is immune to the lock screen, to focus, to another window covering the game, and to a window
    /// wider than the panel (which clips an external capture).
    /// </para>
    /// </remarks>
    public partial class BS3DGame
    {
        /// <summary>Beside the executable, made on demand — the level set's own path convention.</summary>
        private const string SHOT_DIRECTORY = "Screenshots";

        //Wall-clock seconds after start at which a shot is due, in the order given, and how far down that list
        //the run has got. Null when the "shot=" argument was absent, which is every run but a scripted one.
        private readonly float[] _shotSchedule;
        private int _shotNext;

        //Set by the key and consumed at the end of the NEXT Draw, which is what makes the shot the frame the
        //player was looking at when they pressed rather than the one before it: the key is read in Update, and
        //at that moment the back buffer still holds the previous frame.
        private bool _shotRequested;

        /// <summary>
        /// Asks for a shot of the frame about to be drawn. Wired to <b>F12</b> in both input paths — the menu
        /// chrome and the play loop — for the reason F11 is in both: one of them runs while a page is up and the
        /// other while a level is.
        /// <para>
        /// F12 was the FPS overlay's toggle and the overlay moved to <b>F10</b> to give it up, on the author's
        /// call: F12 is where a hand reaches for a screenshot. It could not take F11 instead — that is
        /// fullscreen here, in the Testbed and in the map editor, and it is the one binding in this project a
        /// player already knows from everything else they run. So the Game's display keys are now a contiguous
        /// F10 / F11 / F12 — overlay, fullscreen, shot — and the <b>Testbed keeps F12 for its own text
        /// overlay</b>, which is a small parity the two executables have lost and the price of the key.
        /// </para>
        /// </summary>
        internal void RequestScreenshot() => _shotRequested = true;

        /// <summary>
        /// Writes the finished frame if one was asked for. Called at the very end of <see cref="Draw"/> — after
        /// the screens, the FPS component and the Myra desktop — so what lands in the file is the frame as
        /// presented, the overlay and whatever page is up included. That is deliberate: a shot of the HUD or of
        /// the result screen is usually the point, and F10 hides the overlay first when a clean plate is wanted.
        /// </summary>
        private void ServiceScreenshots()
        {
            bool due = _shotRequested;

            //The schedule is walked with a while, not an equality test: a frame long enough to skip a whole
            //entry still owes the run its shot, and on the class of machine the quality probe exists for that
            //frame happens. Two entries falling inside one frame collapse to one shot rather than deadlocking
            //on the next.
            while (_shotSchedule != null && _shotNext < _shotSchedule.Length && _wallClock >= _shotSchedule[_shotNext])
            {
                _shotNext++;
                due = true;
            }

            if (!due) return;

            _shotRequested = false;

            SaveBackBuffer();
        }

        /// <summary>
        /// The readback and the write. <b>This is a long frame and cannot be anywhere near a per-frame path</b>:
        /// pulling the back buffer stalls the pipeline behind every command still in flight, and
        /// <see cref="Texture2D.SaveAsPng"/> encodes synchronously on the calling thread — together about a
        /// tenth of a second for a 1600×900 shot. That is nothing for a keypress and nothing for a scheduled
        /// shot; it would be ruinous per frame, which is why the only two callers are an edge and a clock.
        /// <para>
        /// Failures are reported and swallowed. A shot is a diagnostic, and a game that dies because a folder
        /// was read-only, or because the window was minimized to a zero-sized back buffer, would be a worse
        /// bug than the one being photographed.
        /// </para>
        /// </summary>
        private void SaveBackBuffer()
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;

            //A minimized window reports a zero back buffer, the same case EnsureTarget guards
            if (width <= 0 || height <= 0) return;

            try
            {
                Directory.CreateDirectory(ShotDirectory);

                Color[] pixels = new Color[width * height];
                GraphicsDevice.GetBackBufferData(pixels);

                using Texture2D frame = new(GraphicsDevice, width, height);
                frame.SetData(pixels);

                string path = NextShotPath();

                using FileStream file = File.Create(path);
                frame.SaveAsPng(file, width, height);

                //One line, the shape [fps], [level] and [cinematic] already use, so a script can find what it
                //has just taken without guessing the name
                Console.WriteLine($"[shot] {path}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[shot] failed: {exception.Message}");
            }
        }

        private string ShotDirectory => Path.Combine(AppContext.BaseDirectory, SHOT_DIRECTORY);

        /// <summary>
        /// Where the next shot goes: the timestamp first so a folder of them sorts into the order they were
        /// taken, then the scene, because the backdrop is the first thing anybody asks about a shot of this
        /// game. A collision — two shots inside one second, which a schedule can ask for — takes a suffix
        /// rather than overwriting: a diagnostic that quietly replaced the shot before it would be worse than
        /// no diagnostic.
        /// </summary>
        private string NextShotPath()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(ShotDirectory, $"bs3d-{stamp}-{_scene}.png");

            for (int i = 2; File.Exists(path); i++)
                path = Path.Combine(ShotDirectory, $"bs3d-{stamp}-{_scene}-{i}.png");

            return path;
        }
    }
}

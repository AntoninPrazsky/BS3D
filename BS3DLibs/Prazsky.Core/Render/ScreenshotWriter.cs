using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>A program saving its own frame</b>: the swap chain's back buffer written to a PNG, on a key edge or
    /// on a schedule. In one copy for every executable since #371 — #191 built it and it stood in the Game
    /// alone, which left the Testbed, the program every colour and cost judgement in this project is framed
    /// in, as the one that could still only be photographed from outside.
    /// </summary>
    /// <remarks>
    /// It exists because every EXTERNAL route to one of these windows can hand back the wrong picture
    /// <i>silently</i>, all three measured on 2026-08-12 and recorded in
    /// <c>.claude/skills/screenshot/SKILL.md</c>: <c>CopyFromScreen</c> copies a rectangle of the SCREEN, so a
    /// locked desktop returns the lock screen and an unlocked one returns whatever window is in front of the
    /// game (a capture came back with a terminal in it, at the window's exact rect); and the <c>PrintWindow</c>
    /// fallback returned a blank client area from <c>BS3D.exe</c> eleven times running while returning the real
    /// picture from <c>Testbed.exe</c> the same day, so it cannot be trusted either way. A contaminated capture
    /// is not blank — it is a sharp picture of the wrong thing.
    /// <para>
    /// Nothing here touches the screen, the window or the compositor: it is the swap chain's own back buffer,
    /// so it is immune to the lock screen, to focus, to another window covering the game, and to a window
    /// wider than the panel (which clips an external capture).
    /// </para>
    /// <para>
    /// <b>Three triggers, and only the first one serves a person at the machine.</b> <see cref="Request"/> is
    /// the key. The <b>seconds</b> schedule (<c>shot=</c>) serves the case the facility was asked for — a
    /// locked desktop, where neither a synthetic keystroke nor a focus click reaches the application at all —
    /// and it is what makes a shot repeatable: "the result screen at 3.5 s and at 8 s" is a thing a script can
    /// ask for twice and compare. The <b>frame</b> schedule (<c>shotframe=</c>) serves the case seconds cannot:
    /// scheduling in seconds against something that moves measures the sampler and not the effect. Eight times
    /// asked against a 1.6 Hz pulse at ~21 FPS produced three frames, and two attempts half a period apart
    /// moved 4 % and then 2 % — in opposite directions (the journal's #175 entry, which is why that figure is
    /// recorded as unmeasured). A frame index is exact wherever the run is repeatable, and a fixed camera over
    /// a paused simulation is exactly that.
    /// </para>
    /// </remarks>
    public sealed class ScreenshotWriter
    {
        /// <summary>Beside the executable, made on demand — the level set's own path convention.</summary>
        private const string SHOT_DIRECTORY = "Screenshots";

        private readonly GraphicsDevice _device;
        private readonly string _prefix;

        //The two schedules and how far down each one the run has got. Null when the argument was absent, which
        //is every run but a scripted one — null rather than empty, so a run that asked for nothing tests nothing.
        private readonly float[] _seconds;
        private readonly int[] _frames;
        private int _nextSecond;
        private int _nextFrame;

        //Frames SERVICED, not frames drawn: this is called once at the end of a Draw, so the two are the same
        //number for as long as that stays true. It counts from 1, so "shotframe=1" is the first frame presented.
        private int _frame;

        //Set by the key and consumed at the end of the NEXT Draw, which is what makes the shot the frame the
        //player was looking at when they pressed rather than the one before it: the key is read in Update, and
        //at that moment the back buffer still holds the previous frame.
        private bool _requested;

        /// <param name="device">The device whose back buffer is read. Held, not touched, until a shot is due.</param>
        /// <param name="prefix">
        /// Leads the file name, so a folder of shots says which program took them (<c>bs3d</c>, <c>testbed</c>).
        /// The two executables write into two directories anyway — each beside its own exe — but a shot
        /// travels: it gets copied next to another one to be compared, and then the name is all there is.
        /// </param>
        /// <param name="seconds">The <c>shot=</c> schedule, or null for none.</param>
        /// <param name="frames">The <c>shotframe=</c> schedule, or null for none.</param>
        public ScreenshotWriter(GraphicsDevice device, string prefix, float[] seconds = null, int[] frames = null)
        {
            _device = device;
            _prefix = prefix;
            _seconds = seconds;
            _frames = frames;
        }

        /// <summary>Where the shots land: <c>Screenshots\</c> beside the executable.</summary>
        public static string ShotDirectory => Path.Combine(AppContext.BaseDirectory, SHOT_DIRECTORY);

        /// <summary>Asks for a shot of the frame about to be drawn. The host wires this to its own key.</summary>
        public void Request() => _requested = true;

        /// <summary>
        /// Writes the finished frame if one was asked for. Call it at the very END of the host's <c>Draw</c> —
        /// after everything that draws, so what lands in the file is the frame as presented, the overlay and
        /// whatever page is up included, and after any frame-rate reading the host takes, so the readback's own
        /// tenth of a second is never counted as part of the frame it follows.
        /// </summary>
        /// <param name="wallClock">Seconds since the run started, on the host's own clock.</param>
        /// <param name="label">
        /// What the run is looking at, for the file name — the scene, in both executables. The backdrop is the
        /// first thing anybody asks about a shot of this game.
        /// </param>
        public void Service(float wallClock, string label)
        {
            _frame++;

            bool due = _requested;

            //Both schedules are walked with a while, not an equality test: a frame long enough to skip a whole
            //entry still owes the run its shot, and on the class of machine the quality probe exists for, that
            //frame happens. Two entries falling inside one frame collapse to one shot rather than deadlocking
            //on the next.
            while (_seconds != null && _nextSecond < _seconds.Length && wallClock >= _seconds[_nextSecond])
            {
                _nextSecond++;
                due = true;
            }

            while (_frames != null && _nextFrame < _frames.Length && _frame >= _frames[_nextFrame])
            {
                _nextFrame++;
                due = true;
            }

            if (!due) return;

            _requested = false;

            SaveBackBuffer(label);
        }

        /// <summary>
        /// The <c>shot=</c> list: comma-separated seconds, invariant culture like every other numeric argument
        /// in either executable. Kept in ASKED order rather than sorted — a caller who writes them out of order
        /// is telling the run something, and the schedule is walked forward — but an entry that will not parse
        /// is dropped rather than throwing, and an empty result comes back as null so the host sees "no
        /// schedule" instead of an empty one to test every frame.
        /// </summary>
        public static float[] ParseSeconds(string list)
        {
            if (string.IsNullOrEmpty(list)) return null;

            string[] parts = list.Split(',');
            List<float> seconds = new(parts.Length);

            foreach (string part in parts)
                if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && value >= 0f)
                    seconds.Add(value);

            return seconds.Count > 0 ? seconds.ToArray() : null;
        }

        /// <summary>
        /// The <c>shotframe=</c> list, on the same terms as <see cref="ParseSeconds"/>. Frames count from 1,
        /// and 0 is dropped rather than clamped: it can only come from a caller who thinks they are counting
        /// from zero, and handing them the first frame anyway would hide that from them for good.
        /// </summary>
        public static int[] ParseFrames(string list)
        {
            if (string.IsNullOrEmpty(list)) return null;

            string[] parts = list.Split(',');
            List<int> frames = new(parts.Length);

            foreach (string part in parts)
                if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0)
                    frames.Add(value);

            return frames.Count > 0 ? frames.ToArray() : null;
        }

        /// <summary>
        /// The readback and the write. <b>This is a long frame and cannot be anywhere near a per-frame path</b>:
        /// pulling the back buffer stalls the pipeline behind every command still in flight, and
        /// <see cref="Texture2D.SaveAsPng"/> encodes synchronously on the calling thread — together about a
        /// tenth of a second for a 1600×900 shot. That is nothing for a keypress and nothing for a scheduled
        /// shot; it would be ruinous per frame, which is why the only callers are an edge and two clocks.
        /// <para>
        /// Failures are reported and swallowed. A shot is a diagnostic, and a program that dies because a
        /// folder was read-only, or because the window was minimized to a zero-sized back buffer, would be a
        /// worse bug than the one being photographed.
        /// </para>
        /// </summary>
        private void SaveBackBuffer(string label)
        {
            int width = _device.PresentationParameters.BackBufferWidth;
            int height = _device.PresentationParameters.BackBufferHeight;

            //A minimized window reports a zero back buffer, the same case EnsureTarget guards
            if (width <= 0 || height <= 0) return;

            try
            {
                System.IO.Directory.CreateDirectory(ShotDirectory);

                Color[] pixels = new Color[width * height];
                _device.GetBackBufferData(pixels);

                using Texture2D frame = new(_device, width, height);
                frame.SetData(pixels);

                string path = NextShotPath(label);

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

        /// <summary>
        /// Where the next shot goes: the timestamp first so a folder of them sorts into the order they were
        /// taken, then the label. A collision — two shots inside one second, which a schedule can ask for —
        /// takes a suffix rather than overwriting: a diagnostic that quietly replaced the shot before it would
        /// be worse than no diagnostic.
        /// </summary>
        private string NextShotPath(string label)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(ShotDirectory, $"{_prefix}-{stamp}-{label}.png");

            for (int i = 2; File.Exists(path); i++)
                path = Path.Combine(ShotDirectory, $"{_prefix}-{stamp}-{label}-{i}.png");

            return path;
        }
    }
}

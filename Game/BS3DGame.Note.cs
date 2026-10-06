using BS3D.Online;
using BS3D.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace BS3D
{
    /// <summary>
    /// The host's half of <b>Send a Note</b> (#813): the page, the worker that sends notes (<see cref="OnlineNotes"/>),
    /// the context a note carries and the picture of the moment it is about.
    /// </summary>
    public partial class BS3DGame
    {
        /// <summary>
        /// The picture's width at most. Wide enough to read a chip and a ball's colour, small enough that a busy frame
        /// stays well under the service's 400 KB (BS3D-API's <c>NoteMaxPictureBytes</c>).
        /// </summary>
        private const int NOTE_SHOT_WIDTH = 1280;

        /// <summary>A picture bigger than this is taken again at a smaller scale: the service refuses one over 400 KB.</summary>
        private const int NOTE_SHOT_MAX_BYTES = 360_000;

        private OnlineNotes _notes;
        private NotePage _notePage;

        /// <summary>The note worker, or null before <c>LoadContent</c>.</summary>
        internal OnlineNotes Notes => _notes;

        /// <summary>What the player typed and did not send, kept for the next time the page opens in this run.</summary>
        internal string NoteDraft { get; set; }

        /// <summary>Starts the worker, which sends whatever an earlier run left in the outbox.</summary>
        private void StartNotes()
        {
            _notes = OnlineNotes.Start(_settings, () => _online?.Identity, UserData.PathTo(OnlineNotes.FolderName));
            _notePage = new NotePage(this);
        }

        /// <summary>
        /// Opens the note page from <paramref name="where"/>: the context is gathered now, while the screens below
        /// are as the player left them, and the page's first frame is the picture's.
        /// </summary>
        internal void OpenNote(string where)
        {
            _notePage.Begin(where, NoteContext());
            OpenPage(_notePage);
        }

        /// <summary>
        /// What a note says about where it was written: the build, the machine's renderer and size, the scene and
        /// quality, whether the boards are on, and the level the player is in when there is one. Nothing that names
        /// the player or the machine: no paths, no user name, no address.
        /// </summary>
        private JsonObject NoteContext()
        {
            JsonObject context = new()
            {
                ["build"] = BuildVersion.DisplayName,
                ["platform"] = RuntimeInformation.RuntimeIdentifier,
                ["os"] = RuntimeInformation.OSDescription,
                ["gpu"] = GraphicsDevice.Adapter.Description,
                ["resolution"] = string.Create(CultureInfo.InvariantCulture,
                    $"{GraphicsDevice.PresentationParameters.BackBufferWidth}x{GraphicsDevice.PresentationParameters.BackBufferHeight}"),
                ["fullscreen"] = _graphics.IsFullScreen,
                ["quality"] = _quality.ToString(),
                ["scene"] = _scene.ToString(),
                ["online"] = _settings.Online == true,
            };
            if (HasSession) _gameplayScreen.DescribeForNote(context);
            return context;
        }

        /// <summary>
        /// The picture, when the note page asks for this frame (<see cref="NotePage.WantsShot"/>): the back buffer read
        /// back at the end of Draw, when the frame is the game alone, scaled down by a whole factor (each pixel the mean
        /// of a block, so nothing aliases) and encoded as a JPEG, taken again at a smaller scale while it is too big
        /// for the service. Once per note, never per frame, so its allocations are a page's, not the frame's.
        /// </summary>
        private void ServiceNoteShot()
        {
            if (_screens.Active is not NotePage { WantsShot: true } page) return;

            byte[] jpeg = null;
            Texture2D thumbnail = null;
            try
            {
                int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
                int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
                if (width > 0 && height > 0)
                {
                    Color[] frame = new Color[width * height];
                    GraphicsDevice.GetBackBufferData(frame);

                    for (int factor = Math.Max(1, (width + NOTE_SHOT_WIDTH - 1) / NOTE_SHOT_WIDTH); factor <= 8; factor++)
                    {
                        thumbnail?.Dispose();
                        thumbnail = Downscale(frame, width, height, factor);
                        using MemoryStream stream = new();
                        thumbnail.SaveAsJpeg(stream, thumbnail.Width, thumbnail.Height);
                        jpeg = stream.ToArray();
                        if (jpeg.Length <= NOTE_SHOT_MAX_BYTES) break;
                    }

                    Console.WriteLine($"[note] Picture {thumbnail.Width}x{thumbnail.Height}, {jpeg.Length / 1024} KB");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[note] The picture could not be taken: {e.GetType().Name}: {e.Message}");
                thumbnail?.Dispose();
                thumbnail = null;
                jpeg = null;
            }

            page.TakeShot(jpeg, thumbnail);
        }

        /// <summary>The frame at 1/<paramref name="factor"/> of its size, each pixel the mean of its block, opaque.</summary>
        private Texture2D Downscale(Color[] frame, int width, int height, int factor)
        {
            int w = width / factor, h = height / factor;
            Color[] small = new Color[w * h];
            int area = factor * factor;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int dy = 0; dy < factor; dy++)
                    {
                        int row = (y * factor + dy) * width + x * factor;
                        for (int dx = 0; dx < factor; dx++)
                        {
                            Color c = frame[row + dx];
                            r += c.R;
                            g += c.G;
                            b += c.B;
                        }
                    }
                    small[y * w + x] = new Color(r / area, g / area, b / area, 255);
                }

            Texture2D texture = new(GraphicsDevice, w, h);
            texture.SetData(small);
            return texture;
        }

        /// <summary>
        /// Testing only (<c>note=&lt;seconds&gt;:&lt;steps&gt;</c>, #813): pauses a level being played and opens the note
        /// page, as the pause menu's button does. The steps run once the page is writing (<see cref="IsNotePageReady"/>).
        /// </summary>
        internal void OpenNoteForTesting()
        {
            if (!HasSession) return;
            if (_screens.Active is GameplayScreen) PauseGame();
            OpenNote("pause");
        }

        /// <summary>Whether the note page is past its picture and its grace - what <c>note=</c>'s steps wait for.</summary>
        internal bool IsNotePageReady => _screens.Active is NotePage { IsWriting: true };

        /// <summary>Runs <c>note=</c>'s steps on the page - testing only (#813).</summary>
        internal void ActivateNotePageForTesting(string steps) => (_screens.Active as NotePage)?.ActivateForTesting(steps);
    }
}

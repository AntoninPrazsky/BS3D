using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BS3D.Platform
{
    /// <summary>
    /// "Loading" and one to three dots on black while <c>LoadContent</c> builds the game (#637). Measured on the desktop,
    /// the window stood empty for over four seconds between its creation and the first frame — the scene alone takes
    /// two and a half — and several times that on the laptop, long enough for a player to think the machine had
    /// frozen and for Windows to call the window <i>Not Responding</i>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Drawn only when asked</b> (<see cref="Show"/>), between the steps of the load, because the load runs on the
    /// one thread there is and nothing else can draw while it does. So the dots advance at the steps, by the time
    /// that has passed since the first one (<see cref="DOT_SECONDS"/>), and a long step simply holds them — which
    /// is what the owner asked for: the text is not touched, only the dots, now and then.
    /// </para>
    /// <para>
    /// <b>It peeks the message queue, it does not pump it.</b> Windows marks a window hung when its thread has not
    /// looked at its queue for five seconds; <c>PeekMessage</c> with <c>PM_NOREMOVE</c> is that look, and it
    /// dispatches nothing, so no resize or key reaches a game that is still half built.
    /// </para>
    /// <para>
    /// Black, and the menu's Inter in the menu's dim grey, so it hands over to the splash without a seam: the
    /// splash's first frame is the logo on the same black.
    /// </para>
    /// </remarks>
    internal sealed class LoadingIndicator : IDisposable
    {
        /// <summary>How long each of the three dot counts is held before the next.</summary>
        private const float DOT_SECONDS = 0.6f;

        /// <summary>The text's height as a share of the window's: 48 px at 1600 lines, readable at a glance and still quiet.</summary>
        private const float TEXT_HEIGHT = 0.03f;

        /// <summary>Where the text's middle stands, down the window: under the logo the splash is about to draw.</summary>
        private const float TEXT_CENTRE_Y = 0.82f;

        private static readonly string[] Frames = { "Loading.", "Loading..", "Loading..." };

        private readonly GraphicsDevice _device;
        private readonly SpriteBatch _batch;
        private readonly FontSystem _fonts;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private bool _disposed;

        public LoadingIndicator(GraphicsDevice device, FontSystem fonts)
        {
            _device = device;
            _fonts = fonts;
            _batch = new SpriteBatch(device);
        }

        /// <summary>Draws and presents one frame of the indicator, and tells Windows the window is alive.</summary>
        public void Show()
        {
            if (_disposed) return;

            PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);

            Viewport viewport = _device.Viewport;
            if (viewport.Width <= 0 || viewport.Height <= 0) return;

            SpriteFontBase font = _fonts.GetFont(MathF.Max(12f, viewport.Height * TEXT_HEIGHT));
            int frame = (int)(_clock.Elapsed.TotalSeconds / DOT_SECONDS) % Frames.Length;

            //Placed by the widest frame and drawn from its left edge, so the word stands still and only the dots grow
            Vector2 widest = font.MeasureString(Frames[^1]);
            Vector2 position = new(MathF.Round((viewport.Width - widest.X) * 0.5f),
                MathF.Round(viewport.Height * TEXT_CENTRE_Y - widest.Y * 0.5f));

            _device.SetRenderTarget(null);
            _device.Clear(Color.Black);

            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
            font.DrawText(_batch, Frames[frame], position, BS3DGame.MENU_TEXT_DIM);
            _batch.End();

            _device.Present();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _batch.Dispose();
        }

        private const uint PM_NOREMOVE = 0x0000;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr Handle;
            public uint Message;
            public IntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public int X, Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);
    }
}

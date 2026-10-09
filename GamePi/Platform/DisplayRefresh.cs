using System;

namespace BS3D.Platform
{
    /// <summary>
    /// GamePi's <c>Game/Platform/DisplayRefresh.cs</c>: the refresh rate of the monitor the window is on, from SDL
    /// instead of user32. Same name and member, so the adaptive-quality probe (<c>BS3DGame.Quality.cs</c>) calls it
    /// unchanged. On DesktopGL <c>Window.Handle</c> is the <c>SDL_Window*</c>, which is what SDL asks for.
    /// </summary>
    internal static class DisplayRefresh
    {
        /// <summary>
        /// Reads the current refresh rate of the display <paramref name="windowHandle"/> is on, in Hz. False on any
        /// failure. <see cref="IntPtr.Zero"/> asks about the first display, as the Windows version's fallback asks
        /// about the primary one.
        /// </summary>
        internal static bool TryGetForWindow(IntPtr windowHandle, out int refreshHz)
        {
            refreshHz = 0;

            try
            {
                int display = windowHandle != IntPtr.Zero ? Sdl.GetWindowDisplayIndex(windowHandle) : 0;
                if (display < 0) display = 0;

                if (Sdl.GetCurrentDisplayMode(display, out Sdl.DisplayMode mode) != 0) return false;
                //The same "no answer" threshold as the Windows version: below 10 Hz nothing is a real panel rate
                if (mode.RefreshRate < 10) return false;

                refreshHz = mode.RefreshRate;
                return true;
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// The Windows version's display scale and DPI awareness (#825), which this platform does not have: Windows
        /// virtualizes a DPI-unaware window's sizes, and nothing on the Pi's desktop does that to the game. Always no
        /// answer, so the run log's display line and a note's context leave the scale out.
        /// </summary>
        internal static bool TryGetScale(IntPtr windowHandle, out float scale, out bool perMonitorAware)
        {
            scale = 1f;
            perMonitorAware = false;
            return false;
        }
    }
}

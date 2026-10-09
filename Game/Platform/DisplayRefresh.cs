using System;
using System.Runtime.InteropServices;

namespace BS3D.Platform
{
    /// <summary>
    /// The refresh rate of the monitor a given window is on, straight out of user32. It exists because
    /// MonoGame's <c>DisplayMode</c> carries <b>no</b> refresh rate — XNA dropped it and neither the
    /// DesktopGL nor the WindowsDX adapter re-added one — so the adaptive-quality probe's frame-rate floor
    /// would otherwise be a single fixed number for a 60 Hz laptop and a 165 Hz panel alike.
    /// <para>
    /// Split out of <see cref="BS3DGame"/> in #71: it is pure P/Invoke with no game state, and it was three
    /// structs and three <c>DllImport</c>s sitting in the middle of the host's startup path. It stays in the
    /// executable rather than moving to a library because the libraries target plain <c>net10.0</c> and this
    /// is Windows-only by construction.
    /// </para>
    /// </summary>
    internal static class DisplayRefresh
    {
        //The two DEVMODEW fields this reads. The full struct is ~220 bytes and differs across Windows versions
        //only in the trailing private/registry fields, so a buffer sized off the current value of dmSize is
        //always enough; the offsets below are fixed by the public part of the struct and have not moved.
        private const int ENUM_CURRENT_SETTINGS = unchecked((int)0xFFFFFFFF);

        [StructLayout(LayoutKind.Explicit, Size = 220)]
        private struct DEVMODE
        {
            [FieldOffset(68)] public ushort dmSize;
            [FieldOffset(184)] public uint dmDisplayFrequency;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        //Which monitor the window is actually on. MONITOR_DEFAULTTONEAREST rather than the NULL variants because
        //a window is always somewhere: dragged half off the desktop, or onto a monitor that has just been
        //unplugged, "nearest" is the honest answer and never fails.
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        //szDevice is the whole point of the EX variant — the \\.\DISPLAYn name EnumDisplaySettings wants. The
        //rects and flags are read by nobody here; they are declared because the struct is passed by value and
        //cbSize has to match what GetMonitorInfo expects.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX monitorInfo);

        /// <summary>
        /// Reads the current refresh rate of the monitor <paramref name="windowHandle"/> is on, in Hz. False on
        /// any failure. Pass <see cref="IntPtr.Zero"/> to ask about the <b>primary</b> display instead.
        /// <para>
        /// It used to pass <c>null</c> to <c>EnumDisplaySettings</c> unconditionally, which asks for the primary
        /// display device and not the window's — and the comments claimed otherwise, which is how it survived
        /// (#81). On a mixed multi-monitor desktop that read the wrong panel in whichever direction the pair
        /// happened to be arranged: a window on a 144 Hz secondary beside a 60 Hz primary took a floor from 60,
        /// and so tolerated a frame rate its own monitor shows as visible jank — the exact failure the
        /// refresh-derived floor was introduced to stop. Reversed, a 60 Hz window was held to a 144 Hz floor and
        /// stepped quality down for no gain the player could see.
        /// </para>
        /// <para>
        /// Falling back to the primary display when the window cannot be resolved is the old behaviour kept as a
        /// guard, not a path known to be taken: a floor derived from the wrong panel still beats none, since the
        /// caller clamps a zero to its own floor and would otherwise throw the panel's own rate away entirely.
        /// It is deliberately not leaning on MonoGame's construction order — though as it happens the window
        /// <i>is</i> already built when the host's <c>SetGraphics</c> runs from the constructor, which was
        /// measured rather than assumed, so in practice even that first call reads the right monitor.
        /// </para>
        /// </summary>
        internal static bool TryGetForWindow(IntPtr windowHandle, out int refreshHz)
        {
            refreshHz = 0;

            //Null means "the primary display" to EnumDisplaySettings, which is the fallback described above
            string device = null;

            if (windowHandle != IntPtr.Zero)
            {
                IntPtr monitor = MonitorFromWindow(windowHandle, MONITOR_DEFAULTTONEAREST);

                if (monitor != IntPtr.Zero)
                {
                    MONITORINFOEX info = default;
                    info.cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>();

                    if (GetMonitorInfo(monitor, ref info)) device = info.szDevice;
                }
            }

            DEVMODE dm = default;
            dm.dmSize = (ushort)Marshal.SizeOf<DEVMODE>();
            if (!EnumDisplaySettings(device, ENUM_CURRENT_SETTINGS, ref dm)) return false;
            //0 or 1 are what Windows reports for a projector/TV that did not declare a refresh, and 5 is a
            //placeholder for "default" — none is a real panel rate, so treat them as "no answer".
            if (dm.dmDisplayFrequency < 10) return false;
            refreshHz = (int)dm.dmDisplayFrequency;
            return true;
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);

        //DPI_AWARENESS_PER_MONITOR_AWARE, what GetAwarenessFromDpiAwarenessContext answers for PerMonitor and PerMonitorV2 alike
        private const int DPI_AWARENESS_PER_MONITOR_AWARE = 2;

        /// <summary>
        /// The scale Windows sets for the monitor <paramref name="windowHandle"/> is on, its DPI over 96 (1.25 at 125 %),
        /// and whether the window is DPI-aware per monitor (#825). False on any failure, or before the window exists.
        /// <para>
        /// The awareness is what makes the scale mean anything: a DPI-unaware window is told 96 DPI on every display, and
        /// is handed every size divided by the real scale while its frame is stretched back up. <c>Game/app.manifest</c>
        /// is what makes the game aware; a compatibility override in the exe's Properties can still take it away, which
        /// is why the run log reads the window's own context rather than taking the manifest's word for it.
        /// </para>
        /// </summary>
        internal static bool TryGetScale(IntPtr windowHandle, out float scale, out bool perMonitorAware)
        {
            scale = 1f;
            perMonitorAware = false;
            if (windowHandle == IntPtr.Zero) return false;

            try
            {
                uint dpi = GetDpiForWindow(windowHandle);
                if (dpi == 0) return false;

                scale = dpi / 96f;
                perMonitorAware = GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(windowHandle))
                    == DPI_AWARENESS_PER_MONITOR_AWARE;
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                //Windows 10 before 1607 has neither function; that is no reason to fail a startup line
                return false;
            }
        }
    }
}

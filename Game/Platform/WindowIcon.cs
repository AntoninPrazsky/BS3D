using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BS3D.Platform
{
    /// <summary>
    /// Publishes the executable's own icon on the game window at the two sizes Windows actually draws — the
    /// large one for ALT+TAB and the taskbar, the small one for the title bar — each taken from the authored
    /// frame of that size rather than resampled from another.
    /// <para>
    /// Split out of <see cref="BS3DGame"/> in #71, with the cached handles as statics. That is not a
    /// change in lifetime: they were never destroyed as instance fields either (see below), there is one game
    /// per process, and the whole point of caching them is that they outlive every resize. It stays in the
    /// executable because it needs WinForms (<c>Control.FromHandle</c>) and the libraries target plain
    /// <c>net10.0</c>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// MonoGame does set an icon itself, and it is not enough. <c>WinFormsGameWindow.SetIcon</c> calls
    /// shell32's <c>ExtractIcon</c>, which hands back a <b>single</b> handle at the system large-icon size
    /// (measured: 32×32 at 96 DPI), and assigns it through <c>Icon.FromHandle</c> — which keeps no icon-file
    /// bytes. So WinForms' small-icon path has no frames to choose from and can only copy that one image:
    /// measured on the live game, the window published ICON_SMALL as a <b>32×32</b>, i.e. the title bar was
    /// squeezing the 32 px artwork into 16 px while the authored <c>ico6-16.png</c> sat unused in the exe.
    /// <para>
    /// <c>PrivateExtractIcons</c> is the API that picks a named size out of a group, so the frames come
    /// straight out of the running executable's resources — no second copy of the icon embedded as content,
    /// nothing to keep in step. It reads <see cref="Environment.ProcessPath"/> rather than
    /// <c>Assembly.Location</c>, which is what MonoGame uses and what comes back <i>empty</i> under a
    /// single-file publish (where MonoGame's own SetIcon silently does nothing and this becomes the only
    /// thing setting the window icon at all).
    /// </para>
    /// </remarks>
    internal static class WindowIcon
    {
        //The two icon handles the window publishes, extracted from the executable's own icon group once for each DPI
        //the window has been on (#825) - one entry on a single monitor, two for a window dragged between monitors of
        //different scales and back. They are deliberately never destroyed: Windows draws from them for as long as the
        //window lives, so they are freed when the process is, and re-extracting them per resize would be work for nothing.
        private static readonly Dictionary<uint, (IntPtr Big, IntPtr Small)> _frames = new();

        private const int SM_CXICON = 11;
        private const int SM_CYICON = 12;
        private const int SM_CXSMICON = 49;
        private const int SM_CYSMICON = 50;

        //What a window that cannot be asked is taken to be on: 100 %
        private const uint DEFAULT_DPI = 96;

        private const uint WM_SETICON = 0x0080;
        private static readonly IntPtr ICON_SMALL = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int PrivateExtractIcons(string fileName, int iconIndex, int cx, int cy,
            IntPtr[] icons, int[] iconIds, int iconCount, int flags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Publishes the icon on the window behind <paramref name="windowHandle"/>, at the sizes the scale of the
        /// monitor it is on asks for. Safe to call more than once, and called again when that scale changes (#825);
        /// the frames are extracted once per DPI.
        /// </summary>
        /// <remarks>
        /// The order below is mandatory: assigning <c>Form.Icon</c> makes WinForms send <i>both</i> messages,
        /// deriving the small one by stretching the large, so the authored small frame has to be sent after it.
        /// WinForms does the same again on every <c>WM_DPICHANGED</c> (<c>Form.OnDpiChanged</c> calls its
        /// <c>UpdateWindowIcon</c> with the new DPI), which is why a scale change has to bring this back
        /// (<c>DisplayRefresh.WatchScale</c>): left alone, the small slot held the 40 px large frame for the title bar
        /// to shrink, and the large slot kept 40 px at 96 DPI - measured after a change of scale either way and after a
        /// move between monitors of different scales.
        /// </remarks>
        internal static void Apply(IntPtr windowHandle)
        {
            //Nothing here is worth a crash on a machine whose shell answers differently — a stock icon is a
            //cosmetic loss, and every step below can fail independently.
            try
            {
                if (System.Windows.Forms.Control.FromHandle(windowHandle) is not System.Windows.Forms.Form form) return;

                uint dpi = WindowDpi(windowHandle);
                if (!_frames.TryGetValue(dpi, out (IntPtr Big, IntPtr Small) frames))
                {
                    string module = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(module)) return;

                    frames = (ExtractIconFrame(module, Metric(SM_CXICON, dpi), Metric(SM_CYICON, dpi)),
                        ExtractIconFrame(module, Metric(SM_CXSMICON, dpi), Metric(SM_CYSMICON, dpi)));
                    _frames[dpi] = frames;
                }

                if (frames.Big != IntPtr.Zero) form.Icon = System.Drawing.Icon.FromHandle(frames.Big);
                if (frames.Small != IntPtr.Zero) SendMessage(form.Handle, WM_SETICON, ICON_SMALL, frames.Small);
            }
            catch (Exception)
            {
                //Leaves whatever MonoGame published standing.
            }
        }

        /// <summary>The DPI of the monitor the window is on, or 96 where Windows cannot say.</summary>
        private static uint WindowDpi(IntPtr windowHandle)
        {
            try
            {
                uint dpi = GetDpiForWindow(windowHandle);
                return dpi != 0 ? dpi : DEFAULT_DPI;
            }
            catch (EntryPointNotFoundException)
            {
                //Windows 10 before 1607
                return DEFAULT_DPI;
            }
        }

        /// <summary>
        /// An icon size at <paramref name="dpi"/>: 16 and 32 at 96 DPI, 20 and 40 at 120. <c>GetSystemMetrics</c> alone
        /// answers for the process's SYSTEM DPI, the primary monitor's scale when the process started, which a
        /// per-monitor aware window does not have to be on: a window on a second monitor of another scale, or one whose
        /// monitor changed scale under it, got the other scale's frames and the title bar resampled them.
        /// </summary>
        private static int Metric(int index, uint dpi)
        {
            try
            {
                return GetSystemMetricsForDpi(index, dpi);
            }
            catch (EntryPointNotFoundException)
            {
                //Windows 10 before 1607, where the process cannot be per-monitor aware in the first place
                return GetSystemMetrics(index);
            }
        }

        /// <summary>
        /// The one frame of <paramref name="module"/>'s first icon group closest to the requested size, or
        /// <see cref="IntPtr.Zero"/>. Zero rather than a throw for a missing group: an executable built without
        /// an <c>ApplicationIcon</c> has none, and that is not an error worth a stack trace.
        /// </summary>
        private static IntPtr ExtractIconFrame(string module, int width, int height)
        {
            IntPtr[] icons = new IntPtr[1];
            int[] iconIds = new int[1];

            //Returns the count extracted, or -1 on failure — so anything but exactly one is "no icon".
            return PrivateExtractIcons(module, 0, width, height, icons, iconIds, 1, 0) == 1 ? icons[0] : IntPtr.Zero;
        }
    }
}

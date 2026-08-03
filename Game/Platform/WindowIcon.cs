using System;
using System.Runtime.InteropServices;

namespace BS3D.Platform
{
    /// <summary>
    /// Publishes the executable's own icon on the game window at the two sizes Windows actually draws — the
    /// large one for ALT+TAB and the taskbar, the small one for the title bar — each taken from the authored
    /// frame of that size rather than resampled from another.
    /// <para>
    /// Split out of <see cref="BS3DGame"/> in #71, with the two cached handles as statics. That is not a
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
        //The two icon handles the window is publishing, extracted once from the executable's own icon group.
        //They are deliberately never destroyed: Windows draws from them for as long as the window lives, so
        //they are freed when the process is, and re-extracting them per resize would be work for nothing.
        private static IntPtr _big;
        private static IntPtr _small;

        private const int SM_CXICON = 11;
        private const int SM_CYICON = 12;
        private const int SM_CXSMICON = 49;
        private const int SM_CYSMICON = 50;

        private const uint WM_SETICON = 0x0080;
        private static readonly IntPtr ICON_SMALL = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int PrivateExtractIcons(string fileName, int iconIndex, int cx, int cy,
            IntPtr[] icons, int[] iconIds, int iconCount, int flags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Publishes the icon on the window behind <paramref name="windowHandle"/>. Safe to call more than
        /// once; the frames are extracted on the first call only.
        /// </summary>
        /// <remarks>
        /// The order below is mandatory: assigning <c>Form.Icon</c> makes WinForms send <i>both</i> messages,
        /// deriving the small one by stretching the large, so the authored small frame has to be sent after it.
        /// </remarks>
        internal static void Apply(IntPtr windowHandle)
        {
            //Nothing here is worth a crash on a machine whose shell answers differently — a stock icon is a
            //cosmetic loss, and every step below can fail independently.
            try
            {
                if (System.Windows.Forms.Control.FromHandle(windowHandle) is not System.Windows.Forms.Form form) return;

                if (_big == IntPtr.Zero && _small == IntPtr.Zero)
                {
                    string module = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(module)) return;

                    _big = ExtractIconFrame(module, GetSystemMetrics(SM_CXICON), GetSystemMetrics(SM_CYICON));
                    _small = ExtractIconFrame(module, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON));
                }

                if (_big != IntPtr.Zero) form.Icon = System.Drawing.Icon.FromHandle(_big);
                if (_small != IntPtr.Zero) SendMessage(form.Handle, WM_SETICON, ICON_SMALL, _small);
            }
            catch (Exception)
            {
                //Leaves whatever MonoGame published standing.
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

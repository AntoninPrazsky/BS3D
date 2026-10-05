using System;
using System.Runtime.InteropServices;

namespace BS3D.Platform
{
    /// <summary>
    /// The handful of SDL2 calls GamePi's platform classes make themselves, on the SDL that MonoGame DesktopGL already
    /// ships and has already loaded (its package carries <c>runtimes/linux-arm64/native/libSDL2-2.0.so.0</c>, which the
    /// host resolves for this <c>DllImport</c> through the deps file the same way it does for MonoGame). Declared here
    /// rather than reached through MonoGame because MonoGame keeps its own SDL bindings internal.
    /// </summary>
    internal static class Sdl
    {
        private const string LIBRARY = "libSDL2-2.0.so.0";

        //GamePi is the Pi's build, but it also runs on Windows through DesktopGL, which is how the Potato picture is
        //looked at on a machine that can compile its shaders (#804). There MonoGame's SDL is SDL2.dll, so the Linux name
        //above is pointed at it; anywhere else the name resolves as it always did.
        static Sdl()
        {
            if (!OperatingSystem.IsWindows()) return;

            NativeLibrary.SetDllImportResolver(typeof(Sdl).Assembly, (name, assembly, path) =>
                name == LIBRARY && NativeLibrary.TryLoad("SDL2", assembly, path, out IntPtr handle) ? handle : IntPtr.Zero);
        }

        /// <summary><c>SDL_MESSAGEBOX_ERROR</c>.</summary>
        internal const uint MESSAGEBOX_ERROR = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        internal struct DisplayMode
        {
            public uint Format;
            public int Width;
            public int Height;
            public int RefreshRate;
            public IntPtr DriverData;
        }

        /// <summary>The display the window's centre is on, or a negative number on failure.</summary>
        [DllImport(LIBRARY, EntryPoint = "SDL_GetWindowDisplayIndex", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetWindowDisplayIndex(IntPtr window);

        /// <summary>Zero on success. <see cref="DisplayMode.RefreshRate"/> is 0 when the display does not say.</summary>
        [DllImport(LIBRARY, EntryPoint = "SDL_GetCurrentDisplayMode", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetCurrentDisplayMode(int displayIndex, out DisplayMode mode);

        /// <summary>The address of a GL entry point in the current context, or zero when the driver has none by that name.</summary>
        [DllImport(LIBRARY, EntryPoint = "SDL_GL_GetProcAddress", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr GlGetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        /// <summary>A modal message box; it needs no window and no running event loop. Zero on success.</summary>
        [DllImport(LIBRARY, EntryPoint = "SDL_ShowSimpleMessageBox", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ShowSimpleMessageBox(uint flags,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string message,
            IntPtr window);
    }
}

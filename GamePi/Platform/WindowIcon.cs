using System;

namespace BS3D.Platform
{
    /// <summary>
    /// GamePi's <c>Game/Platform/WindowIcon.cs</c>, and for now it does nothing. The Windows version publishes the
    /// exe's icon frames through user32 and WinForms; under SDL, MonoGame sets the window icon itself from an
    /// embedded <c>BS3D.Icon.bmp</c> (<c>SdlGameWindow</c>'s constructor), and until GamePi embeds one the window
    /// carries MonoGame's own icon.
    /// </summary>
    internal static class WindowIcon
    {
        /// <summary>Kept for the call in <c>BS3DGame.Initialize</c>; see the class doc.</summary>
        internal static void Apply(IntPtr windowHandle)
        {
        }
    }
}

using System;

namespace BS3D.Platform
{
    /// <summary>
    /// GamePi's <c>Game/Platform/WindowIcon.cs</c>, and it has nothing to do. The Windows version publishes the exe's
    /// icon frames through user32 and WinForms; under SDL, MonoGame sets the window icon itself from the embedded
    /// <c>BS3D.Icon.bmp</c> (<c>SdlGameWindow</c>'s constructor), which <c>GamePi.csproj</c> embeds (#792).
    /// </summary>
    internal static class WindowIcon
    {
        /// <summary>Kept for the call in <c>BS3DGame.Initialize</c>; see the class doc.</summary>
        internal static void Apply(IntPtr windowHandle)
        {
        }
    }
}

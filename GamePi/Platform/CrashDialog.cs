namespace BS3D.Platform
{
    /// <summary>
    /// GamePi's <c>Game/Platform/CrashDialog.cs</c>: the crash notice through SDL's own message box instead of
    /// WinForms. RunLog already wraps the call in a catch, so a desktop with no way to show one costs nothing more
    /// than the dialog; the [crash] line and the report file are written before it either way.
    /// </summary>
    internal static class CrashDialog
    {
        /// <summary>Shows <paramref name="message"/> in a modal error box and returns when it is closed.</summary>
        internal static void Show(string message) =>
            Sdl.ShowSimpleMessageBox(Sdl.MESSAGEBOX_ERROR, "BS3D", message, System.IntPtr.Zero);
    }
}

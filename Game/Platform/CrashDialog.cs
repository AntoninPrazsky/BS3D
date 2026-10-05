namespace BS3D.Platform
{
    /// <summary>
    /// The one dialog the game ever raises: <see cref="RunLog"/> telling a player the game has to close and where the
    /// report went. Its own file because it is WinForms, the one piece of RunLog that is not plain .NET - GamePi
    /// compiles RunLog as it is and brings an SDL message box under this name instead (GamePi/Platform).
    /// </summary>
    internal static class CrashDialog
    {
        /// <summary>Shows <paramref name="message"/> in a modal error box and returns when it is closed.</summary>
        internal static void Show(string message) =>
            System.Windows.Forms.MessageBox.Show(message, "BS3D",
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
    }
}

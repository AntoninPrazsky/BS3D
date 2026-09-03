using System;
using System.IO;

namespace BS3D
{
    /// <summary>
    /// Where this player's own files live: <c>%LOCALAPPDATA%\BS3D</c> (#353). One directory, so there is one
    /// thing to back up and one thing to migrate.
    /// <para>
    /// It exists because the save did not have a home of its own. Progress was written beside the level set,
    /// which resolves to <c>Game\bin\net10.0-windows\Levels\</c> — inside the build output, and inside
    /// <c>.gitignore</c>, with no second copy anywhere. A <c>dotnet clean</c>, a deleted <c>bin</c> to force a
    /// content rebuild and a fresh clone on the other machine are all routine here and all three took the
    /// campaign with them. <b>A save must outlive the build output.</b>
    /// </para>
    /// <para>
    /// Per <i>user</i> rather than per checkout, which is the second half of the point: this repository is
    /// worked on from two machines, and a path under the build output made each checkout's save pretend to be
    /// the same file. <c>LocalApplicationData</c> rather than roaming, because a save and a measured quality
    /// tier both describe <b>this machine</b> and neither should follow a profile onto another one.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Deliberately in <c>Game/</c> and not in a library. <c>PlayerProgress</c> is handed a path and never
    /// asks where it should be — the library stays about the format, the executable decides the location, and
    /// the Testbed and the MapEditor (which have no player and no settings) inherit nothing.
    /// </remarks>
    internal static class UserData
    {
        /// <summary>
        /// The folder name under <c>%LOCALAPPDATA%</c>. The game's own name, unqualified by a publisher: the
        /// convention two of this machine's other games follow, and there is nothing to disambiguate against.
        /// </summary>
        private const string FOLDER_NAME = "BS3D";

        /// <summary>
        /// The directory itself, resolved once. <b>Not created here</b> — resolving a path is not the same as
        /// deciding to write, and the game asks for this before it knows whether it has anything to save.
        /// <para>
        /// Falls back to the executable's own directory when the profile cannot be found at all, which is a
        /// headless or oddly-configured session rather than anything a player will meet. That is the old
        /// behaviour, so the fallback loses the durability rather than the game.
        /// </para>
        /// </summary>
        internal static string Directory { get; } = Resolve();

        /// <summary>One of this player's files, by name — see <see cref="Directory"/>.</summary>
        internal static string PathTo(string fileName) => Path.Combine(Directory, fileName);

        private static string Resolve()
        {
            string local;

            try
            {
                local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            catch (Exception e) when (e is PlatformNotSupportedException or ArgumentException)
            {
                local = null;
            }

            //GetFolderPath answers with an empty string rather than throwing when the folder is simply not
            //known, so the empty case is the one that actually happens
            return string.IsNullOrEmpty(local)
                ? Path.Combine(AppContext.BaseDirectory, FOLDER_NAME)
                : Path.Combine(local, FOLDER_NAME);
        }
    }
}

using System.IO;

namespace Prazsky.Core.Tools
{
    /// <summary>
    /// Writing a small file so that losing the machine mid-write cannot destroy what was already there
    /// (#353). Two files use it — the player's campaign progress and the game's settings — and it is here
    /// rather than in either of them because the second one would otherwise have been a copy of the first.
    /// </summary>
    /// <remarks>
    /// The failure this exists for is a plain <see cref="File.WriteAllText(string, string)"/>: it opens,
    /// truncates and then writes, so a machine lost in that window leaves a <b>short but syntactically fine</b>
    /// file. A lenient loader — which a save must have, since an absent one is a normal first run — then reads
    /// that as "nothing here", and the next write makes the emptiness permanent. The desktop this game is
    /// built on hard-resets under GPU load (#250), so the window is not hypothetical.
    /// </remarks>
    public static class AtomicFile
    {
        /// <summary>
        /// Where a file is built before it takes the real one's place. The same directory deliberately:
        /// <see cref="File.Replace(string, string, string)"/> cannot cross a volume, and the system temp
        /// directory is on a different one often enough to matter.
        /// </summary>
        private const string TempSuffix = ".tmp";

        /// <summary>
        /// Writes <paramref name="text"/> to <paramref name="path"/> without ever leaving it half-written,
        /// keeping the previous contents at <paramref name="path"/> + <paramref name="backupSuffix"/>.
        /// <para>
        /// The new text goes to a temporary file beside the target and is then swapped in by
        /// <see cref="File.Replace(string, string, string)"/>, which demotes the old file to the backup in the
        /// <b>same</b> operation — so there is no instant at which the target is neither the old contents nor
        /// the new. The very first write is a plain move instead: there is nothing to replace, and nothing yet
        /// that a torn write could destroy.
        /// </para>
        /// <para>
        /// Throws whatever the filesystem throws. Whether a failed write is worth more than a log line is the
        /// caller's decision, not this helper's.
        /// </para>
        /// </summary>
        public static void WriteText(string path, string text, string backupSuffix)
        {
            //The directory can legitimately be absent: both callers write under the player's own profile,
            //which nothing else in the game creates
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = path + TempSuffix;

            File.WriteAllText(temp, text);

            if (File.Exists(path)) File.Replace(temp, path, path + backupSuffix);
            else File.Move(temp, path);
        }
    }
}

using System;
using System.IO;
using System.Text;

namespace Prazsky.Core.Tools
{
    /// <summary>
    /// Writing a small file so that losing the machine mid-write cannot destroy what was already there
    /// (#353). Every player file uses it — the campaign progress, the settings, the online identity and its
    /// outbox — and the saved levels since #571; it is here rather than in any of them because each would
    /// otherwise have been a copy of the first.
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
        /// A null <paramref name="backupSuffix"/> keeps no backup; the swap is atomic all the same.
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

            //Flushed to the DISK before it replaces anything (#571), not just out of this process: File.Replace
            //is a metadata operation, and a machine lost after it but before the cache reached the platter would
            //leave the renamed file empty - with the backup the only good copy, and the next write demoting the
            //empty one over it
            using (FileStream stream = new(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            //No suffix, no backup: a level file saved over another keeps no second copy beside it, where the level
            //loader and the editor's file dialog would both meet it
            if (File.Exists(path)) File.Replace(temp, path, backupSuffix == null ? null : path + backupSuffix);
            else File.Move(temp, path);
        }

        /// <summary>
        /// Keeps a copy of a file its loader could not read, as <c>&lt;path&gt;.unreadable-&lt;utc&gt;</c>, before
        /// anything is written over it (#571). Returns the copy's path, or null when there was no file or the copy
        /// failed.
        /// <para>
        /// <b>Why the loaders need it.</b> A player file that does not read — malformed, cut short, or written by a
        /// NEWER build this one cannot understand — is answered with defaults, which is right: no state of it is
        /// worth not starting the game over. But the first save after that goes through <see cref="WriteText"/>,
        /// which demotes the unreadable file to the backup, and the second save overwrites the backup. Two saves and
        /// the only copy is gone, and a newer build's file is exactly the one somebody wants back — this project
        /// runs older builds on purpose (worktrees, bisects, a release zip beside a checkout). A copy the writes
        /// never touch makes the loss recoverable by hand; nothing ever reads it back automatically.
        /// </para>
        /// </summary>
        public static string KeepUnreadable(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;

                string kept = $"{path}.unreadable-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                if (File.Exists(kept)) return kept;

                File.Copy(path, kept);
                return kept;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
                or NotSupportedException)
            {
                return null;
            }
        }
    }
}

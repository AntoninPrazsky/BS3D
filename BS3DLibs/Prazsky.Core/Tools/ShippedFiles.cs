using System;
using System.IO;

namespace Prazsky.Core.Tools
{
    /// <summary>
    /// The one door to the files a build ships beside its executable (#1049): the levels and the level set, the music,
    /// the sound effects, the compiled shaders. Every reader of them goes through here with the path it always built
    /// from <see cref="AppContext.BaseDirectory"/>, so no call site knows the platform.
    /// <para>
    /// <b>On Windows and the Pi nothing is installed and every member is the <see cref="File"/> or
    /// <see cref="Directory"/> call it replaces</b>, so those builds, the MapEditor, the Testbed and the tools read
    /// exactly as before. <b>On Android</b> the shipped files are assets inside the APK, not files: the Android build
    /// installs a <see cref="IStore"/> over them at startup (<see cref="Install"/>), and a path under the base directory
    /// is then answered by the store first, as the asset at the same relative path, and by the file system after it, so
    /// what the game writes under the base directory at run time (a screenshot) is still found. A path anywhere else (a
    /// save, a level named on the command line) is always the file system's.
    /// </para>
    /// </summary>
    public static class ShippedFiles
    {
        /// <summary>
        /// What a platform answers for the files it ships. Paths are relative to the base directory, separated by '/',
        /// with no leading or trailing separator; the base directory itself is the empty string.
        /// </summary>
        public interface IStore
        {
            /// <summary>Whether a shipped file is at <paramref name="relativePath"/>.</summary>
            bool FileExists(string relativePath);

            /// <summary>Whether a shipped folder is at <paramref name="relativePath"/> (one with anything in it).</summary>
            bool DirectoryExists(string relativePath);

            /// <summary>The names, without a path, of what <paramref name="relativeDirectory"/> holds; empty for none.</summary>
            string[] List(string relativeDirectory);

            /// <summary>Opens a shipped file for reading. The stream can seek, as a file's can.</summary>
            Stream OpenRead(string relativePath);
        }

        private static volatile IStore _store;

        /// <summary>
        /// Installs the platform's store, once, before the first read (the Android build's activity does, ahead of
        /// everything else). Nothing calls it on Windows or the Pi.
        /// </summary>
        public static void Install(IStore store) => _store = store;

        /// <summary>Whether the shipped files are a store's rather than files beside the executable (Android).</summary>
        public static bool IsPackaged => _store != null;

        /// <summary><see cref="File.Exists"/>, or the store's answer for a shipped path.</summary>
        public static bool Exists(string path) =>
            TryShipped(path, out IStore store, out string relative) && store.FileExists(relative) || File.Exists(path);

        /// <summary><see cref="Directory.Exists"/>, or the store's answer for a shipped path.</summary>
        public static bool DirectoryExists(string path) =>
            TryShipped(path, out IStore store, out string relative) && store.DirectoryExists(relative) || Directory.Exists(path);

        /// <summary><see cref="File.OpenRead"/>, or the store's file for a shipped path. Throws as File.OpenRead does when neither has it.</summary>
        public static Stream OpenRead(string path) =>
            TryShipped(path, out IStore store, out string relative) && store.FileExists(relative)
                ? store.OpenRead(relative)
                : File.OpenRead(path);

        /// <summary><see cref="File.ReadAllBytes"/>, or the store's file for a shipped path.</summary>
        public static byte[] ReadAllBytes(string path)
        {
            if (!TryShipped(path, out IStore store, out string relative) || !store.FileExists(relative))
                return File.ReadAllBytes(path);

            using Stream stream = store.OpenRead(relative);
            using MemoryStream copy = new();
            stream.CopyTo(copy);
            return copy.ToArray();
        }

        /// <summary>
        /// <see cref="Directory.GetFiles(string, string)"/>: the full paths of the files in <paramref name="directory"/>
        /// whose names match <paramref name="searchPattern"/> (<c>*</c> and <c>?</c>). For a shipped folder, the store's
        /// files first, in ordinal order, then any written there at run time; a store entry is taken to be a file when it
        /// matches the pattern, which always names an extension here.
        /// </summary>
        public static string[] GetFiles(string directory, string searchPattern)
        {
            if (!TryShipped(directory, out IStore store, out string relative)) return Directory.GetFiles(directory, searchPattern);

            string[] names = store.List(relative);
            string[] written = Directory.Exists(directory) ? Directory.GetFiles(directory, searchPattern) : Array.Empty<string>();

            System.Collections.Generic.List<string> paths = new(names.Length + written.Length);
            Array.Sort(names, StringComparer.Ordinal);
            foreach (string name in names)
                if (Matches(name, searchPattern)) paths.Add(Path.Combine(directory, name));
            foreach (string path in written)
                if (!paths.Contains(path)) paths.Add(path);

            return paths.ToArray();
        }

        //A path under the base directory, as the store names it, when a store is installed
        private static bool TryShipped(string path, out IStore store, out string relative)
        {
            store = _store;
            relative = null;
            return store != null && TryRelative(path, AppContext.BaseDirectory, out relative);
        }

        /// <summary>
        /// <paramref name="path"/> relative to <paramref name="baseDirectory"/> in the store's spelling ('/', no leading or
        /// trailing separator, the base itself empty), or false when it is not under it. A sibling that merely starts with
        /// the base's name (<c>base2</c> beside <c>base</c>) is not under it.
        /// </summary>
        internal static bool TryRelative(string path, string baseDirectory, out string relative)
        {
            relative = null;
            if (string.IsNullOrEmpty(path)) return false;

            string full = Path.GetFullPath(path);
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
            if (!full.StartsWith(root, StringComparison.Ordinal)) return false;
            if (full.Length > root.Length && full[root.Length] != Path.DirectorySeparatorChar) return false;

            relative = full.Substring(root.Length).Trim(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/');
            return true;
        }

        /// <summary>Whether <paramref name="name"/> matches a pattern of literal characters, <c>*</c> and <c>?</c>, ignoring case as Windows does.</summary>
        internal static bool Matches(string name, string pattern) => Matches(name, 0, pattern, 0);

        private static bool Matches(string name, int n, string pattern, int p)
        {
            while (p < pattern.Length)
            {
                char c = pattern[p];
                if (c == '*')
                {
                    for (int skip = n; skip <= name.Length; skip++)
                        if (Matches(name, skip, pattern, p + 1)) return true;
                    return false;
                }

                if (n >= name.Length) return false;
                if (c != '?' && char.ToUpperInvariant(c) != char.ToUpperInvariant(name[n])) return false;
                n++;
                p++;
            }

            return n == name.Length;
        }
    }
}

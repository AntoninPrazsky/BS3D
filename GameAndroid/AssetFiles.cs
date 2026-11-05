using System;
using System.Collections.Concurrent;
using System.IO;
using Android.Content;
using Android.Content.Res;
using Prazsky.Core.Tools;

namespace BS3D
{
    /// <summary>
    /// The APK's assets as the files the Game ships beside its executable (#1049). The levels, the music, the sound
    /// effects and the compiled shaders are packed at the APK's asset root under the paths the Game builds from
    /// <see cref="AppContext.BaseDirectory"/> (<c>Levels/</c>, <c>Music/</c>, <c>Sfx/</c>, <c>Content/</c>), and this is the
    /// <see cref="ShippedFiles.IStore"/> that answers them, so every reader goes through <see cref="ShippedFiles"/> and none
    /// knows it is on a phone. Nothing is copied out: the music's 351 MB stay in the APK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The base directory is the app's own <c>files/base</c></b>, a folder the game can write: what it writes beside
    /// the executable at run time (a screenshot) lands there, and <see cref="ShippedFiles"/> finds it after the assets.
    /// </para>
    /// <para>
    /// <b>The spike's copy is removed once.</b> Until #1049 the assets were copied into that folder on every install
    /// (<c>Levels</c>, <c>Music</c>, <c>Sfx</c> and a <c>.installed</c> stamp); a phone that ran such a build still has them,
    /// so they go when the stamp is found, and only then: the same folders are the store's fallback, where a file put by
    /// hand must survive. <c>Screenshots</c> beside them is the player's. A line says what the folder holds after it.
    /// </para>
    /// <para>
    /// A folder's listing is asked of the <see cref="AssetManager"/> once and kept (a listing is a call into Java, and the
    /// music's folder is asked for at every start). A file is opened into memory, so the stream can seek as a file's can:
    /// a recording is a few megabytes and the decoder reads all of it anyway.
    /// </para>
    /// </remarks>
    internal sealed class AssetFiles : ShippedFiles.IStore
    {
        //What the spike copied out, and the stamp that said which install the copy was of
        private static readonly string[] COPIED = { "Levels", "Music", "Sfx" };
        private const string STAMP = ".installed";

        private readonly AssetManager _assets;
        private readonly ConcurrentDictionary<string, string[]> _listings = new(StringComparer.Ordinal);

        private AssetFiles(AssetManager assets) => _assets = assets;

        /// <summary>
        /// Makes <c>files/base</c> the base directory and installs the store, before anything reads a shipped file (the
        /// first thing the activity does).
        /// </summary>
        internal static void Install(Context context)
        {
            string target = Path.Combine(context.FilesDir!.AbsolutePath, "base");
            Directory.CreateDirectory(target);
            RemoveSpikeCopy(target);

            //AppContext.BaseDirectory answers this key before its own default, every time it is read; the separator at
            //the end is what the default carries too
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", target + Path.DirectorySeparatorChar);
            ShippedFiles.Install(new AssetFiles(context.Assets!));
        }

        private static void RemoveSpikeCopy(string target)
        {
            try
            {
                //Only where the stamp says the spike's copy is there: the same folders are the store's fallback, and a file put
                //there by hand (a level pushed with adb) must survive the next start
                string stamp = Path.Combine(target, STAMP);
                if (File.Exists(stamp))
                {
                    foreach (string folder in COPIED)
                    {
                        string path = Path.Combine(target, folder);
                        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                    }

                    File.Delete(stamp);
                    Console.WriteLine("[files] removed the copy of the APK's files an earlier build made");
                }

                //What is left beside the shipped files, so a log says whether anything but the player's own is still there
                string[] left = Directory.GetFileSystemEntries(target);
                Array.Sort(left, StringComparer.Ordinal);
                Console.WriteLine($"[files] the shipped files are the APK's assets; the base directory '{target}' holds "
                    + (left.Length == 0 ? "nothing" : string.Join(", ", Array.ConvertAll(left, Path.GetFileName))));
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[files] the base directory could not be tidied: {exception.Message}");
            }
        }

        public bool FileExists(string relativePath)
        {
            if (relativePath.Length == 0) return false;

            int slash = relativePath.LastIndexOf('/');
            string folder = slash < 0 ? string.Empty : relativePath[..slash];
            string name = relativePath[(slash + 1)..];

            //In its folder's listing and not a folder itself (the APK holds no empty folders)
            return Array.IndexOf(List(folder), name) >= 0 && List(relativePath).Length == 0;
        }

        public bool DirectoryExists(string relativePath) => List(relativePath).Length > 0;

        public string[] List(string relativeDirectory) =>
            _listings.GetOrAdd(relativeDirectory, folder => _assets.List(folder) ?? Array.Empty<string>());

        public Stream OpenRead(string relativePath)
        {
            using Stream asset = _assets.Open(relativePath);
            MemoryStream copy = new();
            asset.CopyTo(copy);
            copy.Position = 0;
            return copy;
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using Android.Content;
using Android.Content.Res;

namespace BS3D
{
    /// <summary>
    /// The spike's stand-in for #1049 (#1048): the Game reads its levels, music and sound effects as files beside its
    /// executable (<see cref="AppContext.BaseDirectory"/>), and on Android they are assets inside the APK. This copies
    /// the APK's <c>Files/</c> tree out into the app's private storage once per installed build and makes that folder
    /// the base directory, so no Game file knows. It doubles the music's 351 MB on the phone, which is why #1049
    /// replaces it with a layer that reads the assets as streams.
    /// </summary>
    internal static class AssetFiles
    {
        //The APK's folder of files the Game reads from its base directory (the Link in GameAndroid.csproj)
        private const string ROOT = "Files";
        //Which installed build the copy is of: a reinstall changes the package's update time
        private const string STAMP = ".installed";

        internal static void Prepare(Context context)
        {
            string target = Path.Combine(context.FilesDir!.AbsolutePath, "base");
            string stampPath = Path.Combine(target, STAMP);
            string installed = InstalledStamp(context);

            if (!File.Exists(stampPath) || File.ReadAllText(stampPath) != installed)
            {
                Stopwatch watch = Stopwatch.StartNew();
                if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
                long bytes = 0;
                int files = Copy(context.Assets!, ROOT, target, ref bytes);
                File.WriteAllText(stampPath, installed);
                Console.WriteLine($"[files] copied {files} files ({bytes / (1024 * 1024)} MB) out of the APK in {watch.ElapsedMilliseconds} ms");
            }

            //AppContext.BaseDirectory answers this key before its own default, every time it is read; the separator at
            //the end is what the default carries too
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", target + Path.DirectorySeparatorChar);
        }

        private static string InstalledStamp(Context context)
        {
#pragma warning disable CA1422, CS0618 //The flags overload is API 33; the update time is all this reads
            return context.PackageManager!.GetPackageInfo(context.PackageName!, 0)!.LastUpdateTime.ToString();
#pragma warning restore CA1422, CS0618
        }

        /// <returns>How many files were copied.</returns>
        private static int Copy(AssetManager assets, string from, string to, ref long bytes)
        {
            string[] children = assets.List(from) ?? Array.Empty<string>();
            //An asset path with no children is a file (the APK holds no empty folders)
            if (children.Length == 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                using Stream source = assets.Open(from);
                using FileStream target = File.Create(to);
                source.CopyTo(target);
                bytes += target.Length;
                return 1;
            }

            int count = 0;
            foreach (string child in children)
                count += Copy(assets, from + "/" + child, Path.Combine(to, child), ref bytes);
            return count;
        }
    }
}

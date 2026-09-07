using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Prazsky.Core.Tools
{
    /// <summary>
    /// <b>What this run actually is</b> (#372): two <c>[build]</c> lines on stdout at startup, naming the
    /// executable and the compiled shaders sitting beside it — when they were written, and a hash that says
    /// whether they are the same bytes as last time.
    /// </summary>
    /// <remarks>
    /// It exists because a run could not be asked the one question a capture or a measurement depends on:
    /// <i>which shader are you running?</i> Three consecutive rounds of measurement once ran on a Testbed that
    /// did not have the edited shader in it, and produced a conclusion, a change made to satisfy it, and then
    /// a retraction written into five files and committed (<c>e71fdff</c>). All of it was invalid, and one of
    /// the two causes is permanent:
    /// <para>
    /// <b>MGCB skips an <c>.fx</c> whose <c>.xnb</c> is newer — and then copies nothing.</b> The content task
    /// copies only what it built in that invocation, so an intermediate that is already up to date leaves the
    /// output directory untouched. <c>dotnet build</c> prints <c>Skipping …\InstancedModel.fx</c> and reports
    /// success. Deleting <c>bin</c> is not enough; only deleting <c>Content\bin</c> (MGCB's own intermediate)
    /// forces the rebuild and the copy. The other cause is an exe built into another configuration and never
    /// launched, which the first line answers.
    /// </para>
    /// <para>
    /// <b>It reports and never judges.</b> A shader older than the exe is completely normal — an
    /// <c>.xnb</c> only changes when its source does — so there is no verdict here to be wrong: what the lines
    /// carry is the evidence a reader needs in the same log as the capture. The two questions they answer are
    /// "did the rebuild I just ran land?" (the newest entry is the file you edited, seconds ago) and "is this
    /// the same content as the run I am comparing against?" (the set hash).
    /// </para>
    /// <para>
    /// Everything is swallowed on failure. A run must never fail to start because a diagnostic could not read
    /// a directory.
    /// </para>
    /// </remarks>
    public static class BuildStamp
    {
        //Where MonoGame's content build lands beside every one of the three executables
        private const string CONTENT_SHADERS = @"Content\Shaders";

        //Enough of a SHA-256 to say "not the same bytes" at a glance, short enough to sit on a log line next
        //to twenty-eight names. A collision here costs nothing: this is a fingerprint for a human comparing
        //two runs, never an integrity check.
        private const int HASH_CHARS = 8;

        /// <summary>
        /// Writes the two lines. Call it first thing in <c>Main</c>, before the window: it needs no graphics
        /// device, and a run that dies during startup should still have said what it was.
        /// </summary>
        public static void Report()
        {
            try
            {
                ReportExecutable();
                ReportShaders();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[build] failed: {exception.Message}");
            }
        }

        /// <summary>
        /// The entry assembly's file and when it was last written. It names <c>Testbed.dll</c> rather than
        /// <c>Testbed.exe</c> on purpose: the <c>.exe</c> is the apphost launcher and <b>the code is in the
        /// managed assembly beside it</b>, so the assembly's write time is the one that answers "did my C#
        /// change land in what I am running". <b>Not the PE header's timestamp</b>, which
        /// deterministic builds (the SDK default) fill with a content hash rather than a date; and not an
        /// assembly version either, since nothing here bumps one. The file's own write time is what says
        /// whether the exe being launched is the one just built — the <c>-c Release</c> half of the trap, where
        /// every rebuild succeeds into a directory nobody is running from.
        /// </summary>
        private static void ReportExecutable()
        {
            string path = Assembly.GetEntryAssembly()?.Location;

            //Empty for a single-file publish, where the managed assembly has no path of its own on disk
            if (string.IsNullOrEmpty(path)) path = Environment.ProcessPath;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Console.WriteLine("[build] executable unknown");
                return;
            }

            Console.WriteLine($"[build] {Path.GetFileName(path)} {Stamp(File.GetLastWriteTime(path))} {Hash(path)}");
        }

        /// <summary>
        /// The compiled shaders beside the exe: how many, one hash over the whole set, and the two entries a
        /// reader wants named — the newest (which a rebuild should have just made) and the oldest.
        /// </summary>
        private static void ReportShaders()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, CONTENT_SHADERS);

            if (!Directory.Exists(directory))
            {
                Console.WriteLine($"[build] no {CONTENT_SHADERS} beside the executable");
                return;
            }

            //Ordinal, so the set hash below is the same on any machine and any locale: it is a fingerprint of
            //a set of files and must not depend on how a culture happens to sort their names.
            FileInfo[] shaders = new DirectoryInfo(directory)
                .GetFiles("*.xnb")
                .OrderBy(file => file.Name, StringComparer.Ordinal)
                .ToArray();

            if (shaders.Length == 0)
            {
                Console.WriteLine($"[build] {CONTENT_SHADERS} is empty");
                return;
            }

            FileInfo newest = shaders.OrderByDescending(file => file.LastWriteTime).First();
            FileInfo oldest = shaders.OrderBy(file => file.LastWriteTime).First();

            Console.WriteLine($"[build] shaders {shaders.Length} set {HashSet(shaders)}"
                + $", newest {Name(newest)} {Stamp(newest.LastWriteTime)}"
                + $", oldest {Name(oldest)} {Stamp(oldest.LastWriteTime)}");
        }

        /// <summary>
        /// One hash over every shader in the directory — each file's NAME as well as its bytes, so a renamed
        /// or a newly added shader moves the figure and not only an edited one. Reading a couple of megabytes
        /// once at startup is nothing; it is deliberately not per-file output, twenty-eight lines of hashes
        /// being noise in every log to answer a question that is usually about one file.
        /// </summary>
        private static string HashSet(FileInfo[] shaders)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            foreach (FileInfo shader in shaders)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(shader.Name));
                hash.AppendData(File.ReadAllBytes(shader.FullName));
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()[..HASH_CHARS];
        }

        private static string Hash(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream file = File.OpenRead(path);

            return Convert.ToHexString(sha.ComputeHash(file)).ToLowerInvariant()[..HASH_CHARS];
        }

        //Sortable and unambiguous, and the same shape the shot files' names carry
        private static string Stamp(DateTime time) => time.ToString("yyyy-MM-dd HH:mm:ss");

        private static string Name(FileInfo shader) => Path.GetFileNameWithoutExtension(shader.Name);
    }
}

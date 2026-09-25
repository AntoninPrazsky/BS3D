using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BS3D
{
    /// <summary>
    /// What a run said, kept where a player can send it (#570): every line the game writes to the console is also
    /// written to <c>Logs\last-run.log</c> under <see cref="UserData.Directory"/>, and anything that would end the
    /// program leaves a <c>crash-&lt;utc&gt;.txt</c> beside it.
    /// <para>
    /// <b>Why this exists at all.</b> Every diagnostic this game has is a <c>Console.WriteLine</c> — the
    /// <c>[build]</c> lines, <c>[levels]</c>, <c>[progress]</c>, <c>[music]</c>, <c>[online]</c> — and the shipped
    /// build is a GUI program (<c>WinExe</c> in Release) with no console to write them to. A crash on a player's
    /// machine left nothing, although <c>release.yml</c> ships the .pdb files precisely so a player's crash report
    /// can be turned into a stack trace. A Debug build keeps its console window and still writes the file.
    /// </para>
    /// <para>
    /// <b>Installed first, pointed at its file later.</b> <see cref="Install"/> runs before anything prints, so the
    /// <c>[build]</c> lines are kept, but the file cannot be opened until <c>userdata=</c> has been read — resolving
    /// <see cref="UserData.Directory"/> any earlier would refuse it. Until <see cref="Open"/> the lines wait in
    /// memory, and a run that never gets that far (a crash in argument parsing) writes them into its crash file.
    /// </para>
    /// <para>
    /// Every write is flushed at once, because the file is only worth anything after a crash, and a buffered
    /// writer loses exactly the lines that led up to it. That costs a system call per line, which the console
    /// already cost: the rule that nothing is printed on a per-frame path (BestPractices) is what keeps it cheap.
    /// </para>
    /// </summary>
    internal static class RunLog
    {
        private const string FOLDER_NAME = "Logs";
        private const string FILE_NAME = "last-run.log";
        private const string PREVIOUS_FILE_NAME = "previous-run.log";

        private static readonly object Gate = new();
        private static StringBuilder _pending = new();
        private static StreamWriter _file;
        private static string _directory;
        private static int _crashed;

        /// <summary>The folder the log and any crash file are written to, once <see cref="Open"/> has run.</summary>
        internal static string Directory => _directory;

        /// <summary>
        /// Tees the console into this log and hooks the last-chance handlers. Before anything is printed.
        /// </summary>
        internal static void Install()
        {
            Console.SetOut(TextWriter.Synchronized(new Tee(Console.Out)));
            Console.SetError(TextWriter.Synchronized(new Tee(Console.Error)));

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Crash(e.ExceptionObject as Exception, "unhandled exception");

            //A task nobody awaited that failed. Not fatal and not treated as one: logged, marked observed, and the
            //game goes on - the online worker and the music decoder both run on the pool.
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Console.WriteLine($"[error] Unobserved task exception: {e.Exception}");
                e.SetObserved();
            };
        }

        /// <summary>
        /// Opens the file in <see cref="UserData.Directory"/> and writes out what was said before it existed. The
        /// run before this one is kept as <c>previous-run.log</c>, because the launch after a crash is exactly the
        /// one that would otherwise overwrite the evidence.
        /// </summary>
        internal static void Open()
        {
            try
            {
                string directory = Path.Combine(UserData.Directory, FOLDER_NAME);
                System.IO.Directory.CreateDirectory(directory);

                string path = Path.Combine(directory, FILE_NAME);
                if (File.Exists(path)) File.Copy(path, Path.Combine(directory, PREVIOUS_FILE_NAME), overwrite: true);

                StreamWriter file = new(path, append: false, new UTF8Encoding(false)) { AutoFlush = true };

                lock (Gate)
                {
                    file.Write(_pending.ToString());
                    _pending = null;
                    _file = file;
                    _directory = directory;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                //No log is a lost diagnostic, never a reason the game does not start
                lock (Gate) _pending = null;
                Console.WriteLine($"[log] Could not open the run log: {ex.Message}");
            }
        }

        /// <summary>
        /// Writes what ended the run to <c>crash-&lt;utc&gt;.txt</c> beside the log and, for a player, says where it
        /// is. Once per run, whichever of the last-chance paths gets there first.
        /// </summary>
        internal static void Crash(Exception exception, string what)
        {
            if (Interlocked.Exchange(ref _crashed, 1) != 0) return;

            string report = $"BS3D stopped: {what}, {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC{Environment.NewLine}"
                + $"{Environment.OSVersion}, .NET {Environment.Version}{Environment.NewLine}{Environment.NewLine}"
                + $"{exception}{Environment.NewLine}";

            string crashPath = null;
            try
            {
                string directory = _directory ?? Path.Combine(UserData.Directory, FOLDER_NAME);
                System.IO.Directory.CreateDirectory(directory);
                crashPath = Path.Combine(directory, $"crash-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt");

                string earlier;
                lock (Gate) earlier = _pending?.ToString();

                //What was said before the log had a file goes into the crash report instead, or a crash that early
                //would leave only the exception
                File.WriteAllText(crashPath, earlier == null ? report
                    : report + Environment.NewLine + "Before the log was open:" + Environment.NewLine + earlier);
            }
            catch
            {
                crashPath = null;
            }

            Console.WriteLine($"[crash] {what}: {exception?.GetType().Name}: {exception?.Message}"
                + (crashPath != null ? $" - written to {crashPath}" : string.Empty));

            //A player is told where the report is; a scripted run (userdata=) is not stopped by a dialog nobody
            //will close - it exits, and the harness reads the [crash] line
            if (UserData.IsTestingDirectory) return;

            try
            {
                System.Windows.Forms.MessageBox.Show(
                    "BS3D ran into a problem and has to close."
                    + (crashPath != null ? $"{Environment.NewLine}{Environment.NewLine}A report was saved to:{Environment.NewLine}{crashPath}" : string.Empty),
                    "BS3D", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            catch
            {
                //Nothing left to tell anyone with
            }
        }

        /// <summary>
        /// The console's own writer with the log beside it. The console half never fails the write: a Debug build
        /// has a console, a Release build writes to a null stream, and a scripted run to its redirected handle.
        /// </summary>
        private sealed class Tee(TextWriter console) : TextWriter
        {
            public override Encoding Encoding => console.Encoding;

            public override void Write(char value)
            {
                console.Write(value);
                lock (Gate)
                {
                    if (_file != null) _file.Write(value);
                    else _pending?.Append(value);
                }
            }

            public override void Write(string value)
            {
                console.Write(value);
                lock (Gate)
                {
                    if (_file != null) _file.Write(value);
                    else _pending?.Append(value);
                }
            }

            public override void WriteLine(string value)
            {
                console.WriteLine(value);
                lock (Gate)
                {
                    if (_file != null) _file.WriteLine(value);
                    else _pending?.AppendLine(value);
                }
            }

            public override void Flush() => console.Flush();
        }
    }
}

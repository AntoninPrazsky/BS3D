using BS3D.Online;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Tools;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace BS3D
{
    /// <summary>
    /// The game's seam for the play counts (#709): the gameplay screen tells it when an attempt starts, ends on its
    /// result page, is left before it, and when a skip is spent; it counts through <see cref="PlayStatsRecorder"/>
    /// (whose rules are the library's), keeps the totals in <c>Stats.json</c> beside the save, and hands each change to
    /// the online client, which sends it only while the player has online scores on (<see cref="OnlineSession.SubmitStats"/>).
    /// <para>
    /// <b>Never on the frame's clock.</b> An event is a few increments and the totals' text, made where the counts change;
    /// the file is written by a task of its own (<see cref="BackgroundWrite"/>), latest text wins, so a level's start or
    /// end costs the frame no disk flush. Nothing in the game reads the counts back: they observe and never decide.
    /// </para>
    /// </summary>
    internal sealed class PlayStatsSession : IDisposable
    {
        /// <summary>How long the game's exit waits for the last write of the file.</summary>
        internal static readonly TimeSpan DisposeWait = TimeSpan.FromMilliseconds(500);

        private readonly PlayStatsRecorder _recorder;
        private readonly Func<OnlineSession> _online;
        private readonly BackgroundWrite _file;

        /// <param name="path">Where the counts live: <c>Stats.json</c> in the user data folder.</param>
        /// <param name="online">The online seam, read when a change is handed over (it is replaced by nothing, but built after this).</param>
        internal PlayStatsSession(string path, Func<OnlineSession> online)
        {
            PlayStats totals = PlayStats.Load(path);
            _recorder = new PlayStatsRecorder(totals);
            _online = online;
            _file = new BackgroundWrite(path, PlayStats.BackupSuffix);

            switch (totals.Outcome)
            {
                case ProgressLoad.RecoveredFromBackup:
                    Console.WriteLine($"[stats] '{path}' would not read; using its backup" + Kept(totals));
                    break;
                case ProgressLoad.Discarded:
                    Console.WriteLine($"[stats] '{path}' and its backup would not read; counting afresh" + Kept(totals));
                    break;
            }

            Console.WriteLine($"[stats] {totals.Levels.Count} level(s) counted at '{path}'");
        }

        private static string Kept(PlayStats totals) => totals.KeptUnreadable != null ? $" (kept as '{totals.KeptUnreadable}')" : "";

        /// <summary>Whether an attempt is open: what the exit asks before it disposes the online client.</summary>
        internal bool AttemptOpen => _recorder.AttemptOpen;

        /// <summary>An attempt at <paramref name="level"/> starts (null: a level on no board, not counted).</summary>
        internal void Started(LevelIdentity level) => Take(_recorder.Started(level), "started");

        /// <summary>The open attempt reached its result page.</summary>
        internal void Ended(PlayEnding ending, int shots, float seconds, bool firstClear) =>
            Take(_recorder.Ended(ending, shots, seconds, firstClear), ending switch
            {
                PlayEnding.Cleared => firstClear ? "cleared (first clear)" : "cleared",
                PlayEnding.OutOfBalls => "lost: out of balls",
                _ => "lost: the line",
            });

        /// <summary>The open attempt was left before its result page; nothing when none is open.</summary>
        internal void Abandoned(int shots, float seconds) => Take(_recorder.Abandoned(shots, seconds), "abandoned");

        /// <summary>The chapter's skip was spent on <paramref name="level"/>.</summary>
        internal void Skipped(LevelIdentity level) => Take(_recorder.Skipped(level), "skipped");

        private void Take(LevelPlayCounts change, string what)
        {
            if (change == null) return;

            LevelPlayCounts total = _recorder.Totals.For(change);

            //One line an event, which is a few a level: what a scripted run checks the counts against
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[stats] {change.File}#{change.Hash}: {what}{(change.Retries > 0 ? " (a retry)" : "")} — totals: {total.Starts} start(s),"
                + $" {total.Retries} retr{(total.Retries == 1 ? "y" : "ies")}, {total.Clears} clear(s), {total.LostOutOfBalls} out of balls,"
                + $" {total.LostLine} at the line, {total.Abandons} abandoned, {total.Skips} skip(s), {total.Shots} shot(s), {total.Seconds:0.0} s"));

            _file.Write(_recorder.Totals.Serialize());
            _online()?.SubmitStats(change);
        }

        /// <summary>Waits <see cref="DisposeWait"/> at most for the last write of the file.</summary>
        public void Dispose() => _file.Flush(DisposeWait);

        /// <summary>
        /// A file written off the frame: the latest text handed over is written by one task at a time through
        /// <see cref="AtomicFile"/>, and a text handed over while a write runs replaces any still waiting, so the file
        /// always ends as the last text. A failed write is one log line; the counts in memory stand and the next event writes again.
        /// </summary>
        private sealed class BackgroundWrite(string path, string backupSuffix)
        {
            private readonly object _lock = new();
            private string _pending;
            private bool _running;
            private Task _task = Task.CompletedTask;

            internal void Write(string text)
            {
                lock (_lock)
                {
                    _pending = text;
                    if (_running) return;

                    _running = true;
                    _task = Task.Run(Drain);
                }
            }

            internal void Flush(TimeSpan wait)
            {
                Task task;
                lock (_lock) task = _task;

                try
                {
                    task.Wait(wait);
                }
                catch (AggregateException)
                {
                    //Drain catches its own
                }
            }

            private void Drain()
            {
                while (true)
                {
                    string text;
                    lock (_lock)
                    {
                        text = _pending;
                        _pending = null;

                        if (text == null)
                        {
                            _running = false;
                            return;
                        }
                    }

                    try
                    {
                        AtomicFile.WriteText(path, text, backupSuffix);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        Console.WriteLine($"[stats] Could not write '{path}': {e.Message}");
                    }
                }
            }
        }
    }
}

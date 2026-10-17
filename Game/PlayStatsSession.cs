using Prazsky.BS3D.Levels;
using Prazsky.Core.Tools;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace BS3D
{
    /// <summary>
    /// The game's seam for the play counts (#709): the gameplay screen tells it when an attempt starts, ends on its
    /// result page, is left before it, and when a skip is spent; it counts through <see cref="PlayStatsRecorder"/>
    /// (whose rules are the library's), keeps the totals in <c>Stats.json</c> beside the save, and hands each change to
    /// the online client, which sends it only while the player has online scores on (<c>OnlineSession.SubmitStats</c>).
    /// <para>
    /// <b>A run kept off the boards is kept off the online counts.</b> An ending or a leaving can be told it must stay on
    /// this machine (<c>online: false</c>, #876: the run used the dev build's debug row): the totals in <c>Stats.json</c>
    /// still count it, as the save still keeps its record, and nothing of it is handed to the online client. The attempt's
    /// start, handed over when the level began, is not taken back: online it reads as an attempt that never ended.
    /// </para>
    /// <para>
    /// <b>Off the frame but for the increments.</b> An event on the frame is a few additions under a lock and one line of
    /// text handed over; a task of its own (<see cref="BackgroundWrite"/>) prints the line, serializes the totals under the
    /// same lock and writes the file, the latest state winning, so a level's start or end costs the frame no serialization
    /// and no disk flush. Nothing in the game reads the counts back: they observe and never decide.
    /// </para>
    /// </summary>
    internal sealed class PlayStatsSession : IDisposable
    {
        /// <summary>How long the game's exit waits for the last write of the file.</summary>
        internal static readonly TimeSpan DisposeWait = TimeSpan.FromMilliseconds(500);

        private readonly PlayStatsRecorder _recorder;
        private readonly Action<LevelPlayCounts> _online;
        private readonly BackgroundWrite _file;

        /// <summary>Held while the totals change on the frame and while the writer serializes them.</summary>
        private readonly object _lock = new();

        /// <param name="path">Where the counts live: <c>Stats.json</c> in the user data folder.</param>
        /// <param name="online">Where a change goes besides the file - the online client's <c>SubmitStats</c>, which drops it
        /// unless the player has online scores on. A callback rather than the client itself, which is built after this
        /// session and which a test has no use for; null hands nothing on.</param>
        internal PlayStatsSession(string path, Action<LevelPlayCounts> online)
        {
            PlayStats totals = PlayStats.Load(path);
            _recorder = new PlayStatsRecorder(totals);
            _online = online;
            _file = new BackgroundWrite(path, PlayStats.BackupSuffix, () =>
            {
                lock (_lock) return _recorder.Totals.Serialize();
            });

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
        internal bool AttemptOpen
        {
            get
            {
                lock (_lock) return _recorder.AttemptOpen;
            }
        }

        /// <summary>An attempt at <paramref name="level"/> starts (null: a level on no board, not counted).</summary>
        internal void Started(LevelIdentity level)
        {
            lock (_lock) Take(_recorder.Started(level), "started");
        }

        /// <summary>
        /// The open attempt reached its result page. <paramref name="online"/> false counts it here, in the file, and hands
        /// it to nothing online (#876: a run that used the dev build's debug row).
        /// </summary>
        internal void Ended(PlayEnding ending, int shots, float seconds, bool firstClear, bool online = true)
        {
            lock (_lock)
                Take(_recorder.Ended(ending, shots, seconds, firstClear), ending switch
                {
                    PlayEnding.Cleared => firstClear ? "cleared (first clear)" : "cleared",
                    PlayEnding.OutOfBalls => "lost: out of balls",
                    _ => "lost: the line",
                }, online);
        }

        /// <summary>
        /// The open attempt was left before its result page; nothing when none is open. <paramref name="online"/> as in
        /// <see cref="Ended"/>.
        /// </summary>
        internal void Abandoned(int shots, float seconds, bool online = true)
        {
            lock (_lock) Take(_recorder.Abandoned(shots, seconds), "abandoned", online);
        }

        /// <summary>The chapter's skip was spent on <paramref name="level"/>.</summary>
        internal void Skipped(LevelIdentity level)
        {
            lock (_lock) Take(_recorder.Skipped(level), "skipped");
        }

        /// <summary>
        /// One event's change, under the lock: its line and the file to the writer, the change to the online client unless
        /// <paramref name="online"/> says it stays on this machine.
        /// </summary>
        private void Take(LevelPlayCounts change, string what, bool online = true)
        {
            if (change == null) return;

            LevelPlayCounts total = _recorder.Totals.For(change);

            //One line an event, which is a few a level: what a scripted run checks the counts against. Printed by the writer
            _file.Write(string.Create(CultureInfo.InvariantCulture,
                $"[stats] {change.File}#{change.Hash}: {what}{(change.Retries > 0 ? " (a retry)" : "")} — totals: {total.Starts} start(s),"
                + $" {total.Retries} retr{(total.Retries == 1 ? "y" : "ies")}, {total.Clears} clear(s), {total.LostOutOfBalls} out of balls,"
                + $" {total.LostLine} at the line, {total.Abandons} abandoned, {total.Skips} skip(s), {total.Shots} shot(s), {total.Seconds:0.0} s"));

            if (online) _online?.Invoke(change);
        }

        /// <summary>Waits <see cref="DisposeWait"/> at most for the last write of the file.</summary>
        public void Dispose() => _file.Flush(DisposeWait);

        /// <summary>
        /// A file written off the frame: each event hands over its log line and marks the file stale, and one task at a time
        /// prints the lines and writes the latest text <paramref name="serialize"/> makes through <see cref="AtomicFile"/>, so
        /// the file always ends as the last state. A failed write is one log line; the counts in memory stand and the next
        /// event writes again.
        /// </summary>
        private sealed class BackgroundWrite(string path, string backupSuffix, Func<string> serialize)
        {
            private readonly object _lock = new();
            private readonly ConcurrentQueue<string> _lines = new();
            private bool _stale;
            private bool _running;
            private Task _task = Task.CompletedTask;

            internal void Write(string line)
            {
                _lines.Enqueue(line);

                lock (_lock)
                {
                    _stale = true;
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
                    lock (_lock)
                    {
                        if (!_stale)
                        {
                            _running = false;
                            return;
                        }

                        _stale = false;
                    }

                    while (_lines.TryDequeue(out string line)) Console.WriteLine(line);

                    try
                    {
                        AtomicFile.WriteText(path, serialize(), backupSuffix);
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

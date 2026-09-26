using Prazsky.BS3D.Levels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BS3D.Online
{
    /// <summary>
    /// The online score boards' seam in the game (#546, #548): where the client is started and replaced, where a
    /// clear is handed to it, where its answers come back to the frame, and the settings page's verbs — the switch,
    /// the nickname and the removal. The client itself — the outbox, the network, the worker — is
    /// <see cref="OnlineScores"/>; the result page's reading of the answer is #547's.
    /// <para>
    /// An owned object of the host since #583 (it was the <c>BS3DGame.Online.cs</c> partial): the host constructs it
    /// right after the settings are loaded, calls <see cref="Update"/> once a frame and <see cref="Dispose"/> at
    /// unload, and the pages reach it as <c>Game.Online</c>. What it needs of the host is three things only — the
    /// wall clock the board cache ages by, the one writer of the settings file, and a word to the settings page
    /// that what it shows has changed — handed in once as delegates, so nothing here allocates per frame.
    /// </para>
    /// </summary>
    internal sealed class OnlineSession : IDisposable
    {
        private readonly GameSettings _settings;
        private readonly Func<float> _wallClock;
        private readonly Action _saveSettings;
        private readonly Action _changed;

        private OnlineScores _client;

        /// <summary>
        /// Who this install is to the boards, read from <c>Online.json</c> once at start and changed only by the
        /// settings verbs below — null for a player who has never opted in, or who removed their scores.
        /// </summary>
        private OnlineIdentity _identity;

        /// <summary>The submission of the level most recently cleared, which is the only one a page is waiting on.</summary>
        private Guid _submissionId;

        /// <summary>That clear's board key, which the result page's boards are fetched for once it is accepted (#547).</summary>
        private LevelIdentity _submittedLevel;

        //The result page's two boards (#547): requested when the clear is accepted, under these tickets
        private int _boardTicket;
        private int _resultMonthTicket = -1, _resultAllTimeTicket = -1;

        /// <summary>How many of each board the result page shows (#547): the top five, and the player's own row under them.</summary>
        internal const int RESULT_BOARD_ROWS = 5;

        /// <summary>
        /// This month's board of the level just cleared, fetched once the clear was accepted (#547) — the top of it and
        /// the player's own row. Null until it arrives, and from the moment a new clear is submitted.
        /// </summary>
        internal BoardPageBody ResultMonthBoard { get; private set; }

        /// <summary>The all-time board of the level just cleared; see <see cref="ResultMonthBoard"/>.</summary>
        internal BoardPageBody ResultAllTimeBoard { get; private set; }

        /// <summary>
        /// Moves whenever anything the result page shows of the online boards changes — an answer, a board, a new
        /// clear — so the page rewrites its labels only then, rather than every frame (#547).
        /// </summary>
        internal int ResultGeneration { get; private set; }

        //Board pages asked for by a page other than the result's (the picker's board page, #547): their replies by
        //ticket, and a short cache so paging back and forth does not ask the Pi again for what it just answered
        private readonly Dictionary<int, BoardReply> _boardReplies = new();
        private readonly Dictionary<int, (string, string, int, bool, int, int)> _pendingBoardKeys = new();
        private readonly Dictionary<(string, string, int, bool, int, int), (BoardReply Reply, float At)> _boardCache = new();
        private const float BOARD_CACHE_SECONDS = 60f;

        //The result page's line for a player who has not opted in shows once a session and no more (#547): an offer
        //made on every clear is a nag
        private bool _hintOffered;

        //A faulted worker is replaced once a session (#572); a second fault leaves the boards off until the next start
        private bool _restartedAfterFault;

        /// <summary>
        /// Started once, from the host's constructor, right after the settings it reads are loaded — its worker starts
        /// draining an outbox left by an earlier run straight away, off the frame's thread.
        /// </summary>
        /// <param name="settings">The player's settings, shared with the host: the switch and the server are read
        /// from it, and the switch is written to it.</param>
        /// <param name="wallClock">The host's wall clock, which the board cache ages by.</param>
        /// <param name="saveSettings">The host's one writer of the settings file.</param>
        /// <param name="changed">Called when anything the settings page shows of the boards has changed.</param>
        internal OnlineSession(GameSettings settings, Func<float> wallClock, Action saveSettings, Action changed)
        {
            _settings = settings;
            _wallClock = wallClock;
            _saveSettings = saveSettings;
            _changed = changed;

            _identity = OnlineIdentity.Load(IdentityPath);
            _client = OnlineScores.Start(_settings, _identity, OutboxPath);
        }

        /// <summary>
        /// Whether the result page may offer the boards to a player who has not opted in (#547) — true once a session,
        /// on the first ending that asks. Asked when a page is presented, so the decision stands while it is up.
        /// </summary>
        internal bool TakeHint()
        {
            if (Enabled || _hintOffered) return false;

            _hintOffered = true;
            return true;
        }

        /// <summary>This install's player id while online scores are on, for a board page to ask for its own row.</summary>
        internal Guid? PlayerId => Enabled && _identity?.IsUsable == true ? _identity.PlayerId : null;

        /// <summary>
        /// Asks for one page of a level's board (#547) and returns the ticket its reply will carry, or -1 when this run
        /// cannot see the boards. A page asked for in the last minute comes straight from the cache, under a new
        /// ticket, without asking the service again.
        /// </summary>
        internal int RequestLevelBoard(LevelIdentity level, bool allTime, int offset, int limit)
        {
            if (!Enabled || level == null) return -1;

            int ticket = ++_boardTicket;
            int rules = Prazsky.BS3D.Scoring.ScoreKeeper.RulesVersion;
            var key = (level.File, level.Hash, rules, allTime, offset, limit);

            if (_boardCache.TryGetValue(key, out var cached) && _wallClock() - cached.At < BOARD_CACHE_SECONDS)
            {
                _boardReplies[ticket] = cached.Reply with { Ticket = ticket };
                return ticket;
            }

            _pendingBoardKeys[ticket] = key;
            _client.RequestBoard(new BoardRequest(ticket, level.File, level.Hash, rules, allTime, PlayerId, limit, offset));
            return ticket;
        }

        /// <summary>The reply to a <see cref="RequestLevelBoard"/> ticket, once it has come; taken once.</summary>
        internal bool TryTakeLevelBoard(int ticket, out BoardReply reply) => _boardReplies.Remove(ticket, out reply);

        private static string IdentityPath => UserData.PathTo(OnlineIdentity.DefaultFileName);
        private static string OutboxPath => UserData.PathTo(OnlineScores.OutboxFileName);

        /// <summary>
        /// Whether this run submits clears at all — the player turned it on, holds an identity and there is a
        /// server (<see cref="OnlineScores.Start"/>). What a result page asks before it offers to show ranks at
        /// all, or instead a hint that the boards exist (#547).
        /// </summary>
        internal bool Enabled => _client?.Enabled == true;

        /// <summary>The player's own switch (#548) — what the settings row shows, whether or not anything can be sent.</summary>
        internal bool IsOn => _settings.Online;

        /// <summary>The nickname this install sends under, or null when there is no identity.</summary>
        internal string Nickname => _identity?.IsUsable == true ? _identity.Name : null;

        /// <summary>
        /// What is sent and what is kept, in one sentence — the settings page's and the About page's, from the one
        /// place (<see cref="OnlineScores.PrivacySentence"/>), so the two read the same.
        /// </summary>
        internal string PrivacySentence => OnlineScores.PrivacySentence(_settings);

        /// <summary>
        /// What became of the level just cleared, once the worker knows: accepted with its ranks, refused, or
        /// offline. <b>Null while it is on its way</b> — and from the moment a new clear is submitted, so a page
        /// can never show the previous level's ranks against this one. Read it every frame; it is set on the frame
        /// the answer arrives and not before. The page must never wait for it (#546, #547).
        /// </summary>
        internal OnlineAnswer? Result { get; private set; }

        /// <summary>Where the player's "Remove scores" stands (#548), for the settings page to say.</summary>
        internal OnlineRemovalState Removal { get; private set; }

        /// <summary>Why the last removal did not happen, worded by the client; null otherwise.</summary>
        internal string RemovalProblem { get; private set; }

        /// <summary>
        /// The service's refusal of the nickname (#548), when it gave one — shown on the settings page rather than
        /// swallowed, and cleared by the next name the player sets.
        /// </summary>
        internal string NameProblem { get; private set; }

        /// <summary>
        /// The client again, after the switch, the identity or the server changed (#548), or once after its worker
        /// faulted (#572). The old one is stopped and its worker handed to the new one to wait for, so two are never
        /// on the outbox at once; nothing is waited for here. The old worker writes whatever clears it still held
        /// into the outbox as it ends, so none is lost with it.
        /// <para>
        /// What the old client was asked and will now never answer is answered here (#572): a removal cut off
        /// mid-request reports failed rather than leaving the page on "Removing" — which also refused every retry —
        /// and a board page on its way comes back empty, so a page stops waiting for it.
        /// </para>
        /// </summary>
        private void Restart()
        {
            Task previous = _client?.Stop();
            _client = OnlineScores.Start(_settings, _identity, OutboxPath, previous);

            if (Removal == OnlineRemovalState.Removing)
            {
                Removal = OnlineRemovalState.Failed;
                RemovalProblem = "interrupted";
            }

            foreach (int ticket in _pendingBoardKeys.Keys)
                _boardReplies[ticket] = new BoardReply(ticket, null, "interrupted");
            _pendingBoardKeys.Clear();
        }

        /// <summary>
        /// A level was cleared: hand it to the score service (#546). <b>Every clear, not only a new best</b> — the
        /// month's board ranks what was done in that month — and never a loss, because the boards are boards of
        /// clears. A level on no board (the built-in fallback map, whose identity is null) and a run that submits
        /// nowhere both do nothing. Called from the one funnel both endings come through, beside the save's own
        /// record, so the score and the stars sent are the ones the save kept.
        /// </summary>
        internal void SubmitClear(LevelIdentity level, int score, int stars, int shotsUsed, float seconds)
        {
            ScoreSubmission submission = _client?.NewSubmission(level, score, stars, shotsUsed, seconds);
            if (submission == null) return;

            _submissionId = submission.SubmissionId;
            _submittedLevel = level;
            Result = null;
            ResultMonthBoard = null;
            ResultAllTimeBoard = null;
            _resultMonthTicket = _resultAllTimeTicket = -1;
            ResultGeneration++;

            _client.Submit(submission);
        }

        /// <summary>
        /// The settings row's switch (#548). On needs a nickname, which the page asks for first; off keeps the
        /// identity, so turning it on again later is the same player on the same boards. Written back to the
        /// settings, because it is a click.
        /// </summary>
        internal void SetOn(bool on)
        {
            if (_settings.Online == on) return;

            _settings.Online = on;
            _saveSettings();
            Restart();

            _changed();
        }

        /// <summary>
        /// The player's nickname, already normalized by <see cref="BS3D.Online.Nickname.TryNormalize"/>. The first one
        /// creates this install's identity — a fresh id and token, never one reused — and every later one renames it,
        /// here and on the service if it can be reached (the next clear carries the name regardless).
        /// </summary>
        internal void SetNickname(string name)
        {
            NameProblem = null;

            if (_identity?.IsUsable == true)
            {
                if (_identity.Name == name) return;

                _identity.Name = name;
                SaveIdentity();
                _client?.RequestRename(name);
            }
            else
            {
                _identity = OnlineIdentity.Create(name);
                SaveIdentity();
                Restart();
            }

            _changed();
        }

        /// <summary>
        /// "Remove scores" (#548), after the page's own confirmation. With a server in reach the service is asked to
        /// forget the player first, and only its yes removes anything here — so a removal made offline removes
        /// nothing and the player still holds the token that proves the scores are theirs. With no server at all
        /// nothing could have been sent from this build, so this machine's copy goes at once.
        /// </summary>
        internal void RemoveScores()
        {
            if (_identity == null || Removal == OnlineRemovalState.Removing) return;

            RemovalProblem = null;

            if (_client?.CanReachServer == true)
            {
                Removal = OnlineRemovalState.Removing;
                _client.RequestRemoval();
            }
            else
            {
                WipeHere();
                Removal = OnlineRemovalState.RemovedHere;
            }

            _changed();
        }

        /// <summary>The page was opened again: an old removal's outcome is not news any more.</summary>
        internal void ForgetRemovalOutcome()
        {
            if (Removal != OnlineRemovalState.Removing) Removal = OnlineRemovalState.None;
            RemovalProblem = null;
        }

        /// <summary>
        /// This machine's side of a removal: the identity, its backup and the outbox gone, the switch off. A later
        /// opt-in creates a new identity, so the removed id can never come back.
        /// </summary>
        private void WipeHere()
        {
            OnlineScores.DeleteQuietly(IdentityPath);
            OnlineScores.DeleteQuietly(IdentityPath + OnlineIdentity.BackupSuffix);
            OnlineScores.DeleteQuietly(OutboxPath);
            OnlineScores.DeleteQuietly(OutboxPath + OnlineScores.OutboxBackupSuffix);

            _identity = null;
            NameProblem = null;
            _settings.Online = false;
            _saveSettings();
            Restart();

            Console.WriteLine("[online] This machine's identity and outbox are removed; online scores are off");
        }

        private void SaveIdentity()
        {
            try
            {
                _identity.Save(IdentityPath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[online] Could not save '{IdentityPath}': {e.Message}");
            }
        }

        /// <summary>
        /// Once a frame: take whatever the worker has answered. Answers for older submissions — clears drained out
        /// of the outbox at start, the ones queued behind a failure — are taken and dropped; only the latest clear's
        /// answer is anybody's business on screen, and each of those has already said its piece in the log. Notices
        /// about the player's own requests are acted on here, on the frame's thread, because they change the
        /// identity and the settings the frame owns.
        /// </summary>
        internal void Update()
        {
            if (_client == null) return;

            while (_client.TryTakeAnswer(out OnlineAnswer answer))
            {
                if (answer.SubmissionId != _submissionId) continue;

                Result = answer;
                ResultGeneration++;

                //Accepted: now the boards it was ranked on, top five of each and the player's own row (#547)
                if (answer.Outcome == OnlineOutcome.Accepted && _submittedLevel != null)
                {
                    _resultMonthTicket = RequestLevelBoard(_submittedLevel, allTime: false, offset: 0, limit: RESULT_BOARD_ROWS);
                    _resultAllTimeTicket = RequestLevelBoard(_submittedLevel, allTime: true, offset: 0, limit: RESULT_BOARD_ROWS);
                }
            }

            while (_client.TryTakeBoard(out BoardReply board))
            {
                if (_pendingBoardKeys.Remove(board.Ticket, out var key) && board.Page != null) _boardCache[key] = (board, _wallClock());
                _boardReplies[board.Ticket] = board;
            }

            if (TryTakeLevelBoard(_resultMonthTicket, out BoardReply month))
            {
                ResultMonthBoard = month.Page;
                _resultMonthTicket = -1;
                ResultGeneration++;
            }

            if (TryTakeLevelBoard(_resultAllTimeTicket, out BoardReply allTime))
            {
                ResultAllTimeBoard = allTime.Page;
                _resultAllTimeTicket = -1;
                ResultGeneration++;
            }

            bool changed = false;

            while (_client.TryTakeNotice(out OnlineNotice notice))
            {
                changed = true;

                switch (notice.Kind)
                {
                    case OnlineNoticeKind.Removed:
                        //Before the wipe, whose restart would otherwise read the removal as cut off
                        Removal = OnlineRemovalState.Removed;
                        WipeHere();
                        break;

                    case OnlineNoticeKind.RemoveFailed:
                        Removal = OnlineRemovalState.Failed;
                        RemovalProblem = notice.Text;
                        break;

                    //Only over the name that was sent (#572): a rename made since the send is the player's word
                    case OnlineNoticeKind.NameNormalized when _identity != null && _identity.Name == notice.Was:
                        _identity.Name = notice.Text;
                        SaveIdentity();
                        break;

                    case OnlineNoticeKind.NameRefused:
                        NameProblem = notice.Text;
                        break;
                }

                //WipeHere replaced the client, whose queues are empty; the loop reads the new one and stops
            }

            //A worker that ended on something unforeseen reads no queue any more (#572): replaced once, after its
            //last notices were taken above; the clears it held are in the outbox and the new one drains them
            if (_client.Faulted && !_restartedAfterFault)
            {
                _restartedAfterFault = true;
                Console.WriteLine("[online] The submitter faulted; starting it again (once a session)");
                Restart();
                changed = true;
            }

            if (changed) _changed();
        }

        /// <summary>Stops the client; see <see cref="OnlineScores.Dispose"/>.</summary>
        public void Dispose() => _client?.Dispose();
    }

    /// <summary>Where the player's "Remove scores" stands (#548).</summary>
    internal enum OnlineRemovalState : byte
    {
        None,

        /// <summary>Asked of the service; nothing removed yet.</summary>
        Removing,

        /// <summary>The service forgot the player, and this machine's copy is gone.</summary>
        Removed,

        /// <summary>No server could be reached from this build, so only this machine's copy was removed.</summary>
        RemovedHere,

        /// <summary>The service did not answer or refused; nothing was removed anywhere.</summary>
        Failed,
    }
}

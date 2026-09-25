using BS3D.Online;
using Prazsky.BS3D.Levels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BS3D
{
    /// <summary>
    /// The online score boards' seam in the game (#546, #548): where the client is started and replaced, where a
    /// clear is handed to it, where its answers come back to the frame, and the settings page's verbs — the switch,
    /// the nickname and the removal. The client itself — the outbox, the network, the worker — is
    /// <see cref="OnlineScores"/>; the result page's reading of the answer is #547's.
    /// </summary>
    public partial class BS3DGame
    {
        private OnlineScores _online;

        /// <summary>
        /// Who this install is to the boards, read from <c>Online.json</c> once at start and changed only by the
        /// settings verbs below — null for a player who has never opted in, or who removed their scores.
        /// </summary>
        private OnlineIdentity _onlineIdentity;

        /// <summary>The submission of the level most recently cleared, which is the only one a page is waiting on.</summary>
        private Guid _onlineSubmissionId;

        /// <summary>That clear's board key, which the result page's boards are fetched for once it is accepted (#547).</summary>
        private LevelIdentity _onlineSubmittedLevel;

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
        internal int OnlineResultGeneration { get; private set; }

        //Board pages asked for by a page other than the result's (the picker's board page, #547): their replies by
        //ticket, and a short cache so paging back and forth does not ask the Pi again for what it just answered
        private readonly Dictionary<int, BoardReply> _boardReplies = new();
        private readonly Dictionary<int, (string, string, int, bool, int, int)> _pendingBoardKeys = new();
        private readonly Dictionary<(string, string, int, bool, int, int), (BoardReply Reply, float At)> _boardCache = new();
        private const float BOARD_CACHE_SECONDS = 60f;

        //The result page's line for a player who has not opted in shows once a session and no more (#547): an offer
        //made on every clear is a nag
        private bool _onlineHintOffered;

        /// <summary>
        /// Whether the result page may offer the boards to a player who has not opted in (#547) — true once a session,
        /// on the first ending that asks. Asked when a page is presented, so the decision stands while it is up.
        /// </summary>
        internal bool TakeOnlineHint()
        {
            if (OnlineEnabled || _onlineHintOffered) return false;

            _onlineHintOffered = true;
            return true;
        }

        /// <summary>This install's player id while online scores are on, for a board page to ask for its own row.</summary>
        internal Guid? OnlinePlayerId => OnlineEnabled && _onlineIdentity?.IsUsable == true ? _onlineIdentity.PlayerId : null;

        /// <summary>
        /// Asks for one page of a level's board (#547) and returns the ticket its reply will carry, or -1 when this run
        /// cannot see the boards. A page asked for in the last minute comes straight from the cache, under a new
        /// ticket, without asking the service again.
        /// </summary>
        internal int RequestLevelBoard(LevelIdentity level, bool allTime, int offset, int limit)
        {
            if (!OnlineEnabled || level == null) return -1;

            int ticket = ++_boardTicket;
            int rules = Prazsky.BS3D.Scoring.ScoreKeeper.RulesVersion;
            var key = (level.File, level.Hash, rules, allTime, offset, limit);

            if (_boardCache.TryGetValue(key, out var cached) && WallClock - cached.At < BOARD_CACHE_SECONDS)
            {
                _boardReplies[ticket] = cached.Reply with { Ticket = ticket };
                return ticket;
            }

            _pendingBoardKeys[ticket] = key;
            _online.RequestBoard(new BoardRequest(ticket, level.File, level.Hash, rules, allTime, OnlinePlayerId, limit, offset));
            return ticket;
        }

        /// <summary>The reply to a <see cref="RequestLevelBoard"/> ticket, once it has come; taken once.</summary>
        internal bool TryTakeLevelBoard(int ticket, out BoardReply reply) => _boardReplies.Remove(ticket, out reply);

        private static string OnlineIdentityPath => UserData.PathTo(OnlineIdentity.DefaultFileName);
        private static string OnlineOutboxPath => UserData.PathTo(OnlineScores.OutboxFileName);

        /// <summary>
        /// Whether this run submits clears at all — the player turned it on, holds an identity and there is a
        /// server (<see cref="OnlineScores.Start"/>). What a result page asks before it offers to show ranks at
        /// all, or instead a hint that the boards exist (#547).
        /// </summary>
        internal bool OnlineEnabled => _online?.Enabled == true;

        /// <summary>The player's own switch (#548) — what the settings row shows, whether or not anything can be sent.</summary>
        internal bool IsOnlineOn => _settings.Online;

        /// <summary>The nickname this install sends under, or null when there is no identity.</summary>
        internal string OnlineNickname => _onlineIdentity?.IsUsable == true ? _onlineIdentity.Name : null;

        /// <summary>
        /// What is sent and what is kept, in one sentence — the settings page's and the About page's, from the one
        /// place (<see cref="OnlineScores.PrivacySentence"/>), so the two read the same.
        /// </summary>
        internal string OnlinePrivacySentence => OnlineScores.PrivacySentence(_settings);

        /// <summary>
        /// What became of the level just cleared, once the worker knows: accepted with its ranks, refused, or
        /// offline. <b>Null while it is on its way</b> — and from the moment a new clear is submitted, so a page
        /// can never show the previous level's ranks against this one. Read it every frame; it is set on the frame
        /// the answer arrives and not before. The page must never wait for it (#546, #547).
        /// </summary>
        internal OnlineAnswer? OnlineResult { get; private set; }

        /// <summary>Where the player's "Remove scores" stands (#548), for the settings page to say.</summary>
        internal OnlineRemovalState OnlineRemoval { get; private set; }

        /// <summary>Why the last removal did not happen, worded by the client; null otherwise.</summary>
        internal string OnlineRemovalProblem { get; private set; }

        /// <summary>
        /// The service's refusal of the nickname (#548), when it gave one — shown on the settings page rather than
        /// swallowed, and cleared by the next name the player sets.
        /// </summary>
        internal string OnlineNameProblem { get; private set; }

        /// <summary>Started once, from the constructor, right after the settings it reads are loaded.</summary>
        private void StartOnline()
        {
            _onlineIdentity = OnlineIdentity.Load(OnlineIdentityPath);
            _online = OnlineScores.Start(_settings, _onlineIdentity, OnlineOutboxPath);
        }

        /// <summary>
        /// The client again, after the switch, the identity or the server changed (#548). The old one is stopped and
        /// its worker handed to the new one to wait for, so two are never on the outbox at once; nothing is waited
        /// for here.
        /// </summary>
        private void RestartOnline()
        {
            Task previous = _online?.Stop();
            _online = OnlineScores.Start(_settings, _onlineIdentity, OnlineOutboxPath, previous);
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
            ScoreSubmission submission = _online?.NewSubmission(level, score, stars, shotsUsed, seconds);
            if (submission == null) return;

            _onlineSubmissionId = submission.SubmissionId;
            _onlineSubmittedLevel = level;
            OnlineResult = null;
            ResultMonthBoard = null;
            ResultAllTimeBoard = null;
            _resultMonthTicket = _resultAllTimeTicket = -1;
            OnlineResultGeneration++;

            _online.Submit(submission);
        }

        /// <summary>
        /// The settings row's switch (#548). On needs a nickname, which the page asks for first; off keeps the
        /// identity, so turning it on again later is the same player on the same boards. Written back to the
        /// settings, because it is a click.
        /// </summary>
        internal void SetOnline(bool on)
        {
            if (_settings.Online == on) return;

            _settings.Online = on;
            SaveSettings();
            RestartOnline();

            _settingsPage?.Refresh();
        }

        /// <summary>
        /// The player's nickname, already normalized by <see cref="Nickname.TryNormalize"/>. The first one creates
        /// this install's identity — a fresh id and token, never one reused — and every later one renames it, here
        /// and on the service if it can be reached (the next clear carries the name regardless).
        /// </summary>
        internal void SetNickname(string name)
        {
            OnlineNameProblem = null;

            if (_onlineIdentity?.IsUsable == true)
            {
                if (_onlineIdentity.Name == name) return;

                _onlineIdentity.Name = name;
                SaveOnlineIdentity();
                _online?.RequestRename(name);
            }
            else
            {
                _onlineIdentity = OnlineIdentity.Create(name);
                SaveOnlineIdentity();
                RestartOnline();
            }

            _settingsPage?.Refresh();
        }

        /// <summary>
        /// "Remove scores" (#548), after the page's own confirmation. With a server in reach the service is asked to
        /// forget the player first, and only its yes removes anything here — so a removal made offline removes
        /// nothing and the player still holds the token that proves the scores are theirs. With no server at all
        /// nothing could have been sent from this build, so this machine's copy goes at once.
        /// </summary>
        internal void RemoveOnlineScores()
        {
            if (_onlineIdentity == null || OnlineRemoval == OnlineRemovalState.Removing) return;

            OnlineRemovalProblem = null;

            if (_online?.CanReachServer == true)
            {
                OnlineRemoval = OnlineRemovalState.Removing;
                _online.RequestRemoval();
            }
            else
            {
                WipeOnlineHere();
                OnlineRemoval = OnlineRemovalState.RemovedHere;
            }

            _settingsPage?.Refresh();
        }

        /// <summary>The page was opened again: an old removal's outcome is not news any more.</summary>
        internal void ForgetOnlineRemovalOutcome()
        {
            if (OnlineRemoval != OnlineRemovalState.Removing) OnlineRemoval = OnlineRemovalState.None;
            OnlineRemovalProblem = null;
        }

        /// <summary>
        /// This machine's side of a removal: the identity, its backup and the outbox gone, the switch off. A later
        /// opt-in creates a new identity, so the removed id can never come back.
        /// </summary>
        private void WipeOnlineHere()
        {
            OnlineScores.DeleteQuietly(OnlineIdentityPath);
            OnlineScores.DeleteQuietly(OnlineIdentityPath + OnlineIdentity.BackupSuffix);
            OnlineScores.DeleteQuietly(OnlineOutboxPath);
            OnlineScores.DeleteQuietly(OnlineOutboxPath + OnlineScores.OutboxBackupSuffix);

            _onlineIdentity = null;
            OnlineNameProblem = null;
            _settings.Online = false;
            SaveSettings();
            RestartOnline();

            Console.WriteLine("[online] This machine's identity and outbox are removed; online scores are off");
        }

        private void SaveOnlineIdentity()
        {
            try
            {
                _onlineIdentity.Save(OnlineIdentityPath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[online] Could not save '{OnlineIdentityPath}': {e.Message}");
            }
        }

        /// <summary>
        /// Once a frame: take whatever the worker has answered. Answers for older submissions — clears drained out
        /// of the outbox at start, the ones queued behind a failure — are taken and dropped; only the latest clear's
        /// answer is anybody's business on screen, and each of those has already said its piece in the log. Notices
        /// about the player's own requests are acted on here, on the frame's thread, because they change the
        /// identity and the settings the frame owns.
        /// </summary>
        private void UpdateOnline()
        {
            if (_online == null) return;

            while (_online.TryTakeAnswer(out OnlineAnswer answer))
            {
                if (answer.SubmissionId != _onlineSubmissionId) continue;

                OnlineResult = answer;
                OnlineResultGeneration++;

                //Accepted: now the boards it was ranked on, top five of each and the player's own row (#547)
                if (answer.Outcome == OnlineOutcome.Accepted && _onlineSubmittedLevel != null)
                {
                    _resultMonthTicket = RequestLevelBoard(_onlineSubmittedLevel, allTime: false, offset: 0, limit: RESULT_BOARD_ROWS);
                    _resultAllTimeTicket = RequestLevelBoard(_onlineSubmittedLevel, allTime: true, offset: 0, limit: RESULT_BOARD_ROWS);
                }
            }

            while (_online.TryTakeBoard(out BoardReply board))
            {
                if (_pendingBoardKeys.Remove(board.Ticket, out var key) && board.Page != null) _boardCache[key] = (board, WallClock);
                _boardReplies[board.Ticket] = board;
            }

            if (TryTakeLevelBoard(_resultMonthTicket, out BoardReply month))
            {
                ResultMonthBoard = month.Page;
                _resultMonthTicket = -1;
                OnlineResultGeneration++;
            }

            if (TryTakeLevelBoard(_resultAllTimeTicket, out BoardReply allTime))
            {
                ResultAllTimeBoard = allTime.Page;
                _resultAllTimeTicket = -1;
                OnlineResultGeneration++;
            }

            bool changed = false;

            while (_online.TryTakeNotice(out OnlineNotice notice))
            {
                changed = true;

                switch (notice.Kind)
                {
                    case OnlineNoticeKind.Removed:
                        WipeOnlineHere();
                        OnlineRemoval = OnlineRemovalState.Removed;
                        break;

                    case OnlineNoticeKind.RemoveFailed:
                        OnlineRemoval = OnlineRemovalState.Failed;
                        OnlineRemovalProblem = notice.Text;
                        break;

                    case OnlineNoticeKind.NameNormalized when _onlineIdentity != null:
                        _onlineIdentity.Name = notice.Text;
                        SaveOnlineIdentity();
                        break;

                    case OnlineNoticeKind.NameRefused:
                        OnlineNameProblem = notice.Text;
                        break;
                }

                //WipeOnlineHere replaced the client, whose queues are empty; the loop reads the new one and stops
            }

            if (changed) _settingsPage?.Refresh();
        }
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

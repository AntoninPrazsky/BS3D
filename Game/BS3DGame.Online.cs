using BS3D.Online;
using Prazsky.BS3D.Levels;
using System;

namespace BS3D
{
    /// <summary>
    /// The online score boards' seam in the game (#546): where the client is started, where a clear is handed to
    /// it, and where its answers come back to the frame. The client itself — the outbox, the network, the worker —
    /// is <see cref="OnlineScores"/>; the result page's reading of the answer is #547's.
    /// </summary>
    public partial class BS3DGame
    {
        private OnlineScores _online;

        /// <summary>The submission of the level most recently cleared, which is the only one a page is waiting on.</summary>
        private Guid _onlineSubmissionId;

        /// <summary>
        /// Whether this run submits clears at all — the player turned it on, holds an identity and there is a
        /// server (<see cref="OnlineScores.Start"/>). What a result page asks before it offers to show ranks at
        /// all, or instead a hint that the boards exist (#547).
        /// </summary>
        internal bool OnlineEnabled => _online?.Enabled == true;

        /// <summary>
        /// What became of the level just cleared, once the worker knows: accepted with its ranks, refused, or
        /// offline. <b>Null while it is on its way</b> — and from the moment a new clear is submitted, so a page
        /// can never show the previous level's ranks against this one. Read it every frame; it is set on the frame
        /// the answer arrives and not before. The page must never wait for it (#546, #547).
        /// </summary>
        internal OnlineAnswer? OnlineResult { get; private set; }

        /// <summary>Started once, from the constructor, right after the settings it reads are loaded.</summary>
        private void StartOnline() =>
            _online = OnlineScores.Start(_settings,
                UserData.PathTo(OnlineIdentity.DefaultFileName),
                UserData.PathTo(OnlineScores.OutboxFileName));

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
            OnlineResult = null;

            _online.Submit(submission);
        }

        /// <summary>
        /// Once a frame: take whatever the worker has answered. Answers for older submissions — clears drained out
        /// of the outbox at start, the ones queued behind a failure — are taken and dropped; only the latest clear's
        /// answer is anybody's business on screen, and each of those has already said its piece in the log.
        /// </summary>
        private void UpdateOnline()
        {
            if (_online == null) return;

            while (_online.TryTakeAnswer(out OnlineAnswer answer))
                if (answer.SubmissionId == _onlineSubmissionId) OnlineResult = answer;
        }
    }
}

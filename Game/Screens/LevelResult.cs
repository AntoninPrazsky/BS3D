using System;

namespace BS3D.Screens
{
    /// <summary>
    /// Everything the end-of-level screen needs, taken as a snapshot the moment the level ends.
    /// <para>
    /// <b>A snapshot rather than a reference to the session, and the reason is not tidiness.</b> The level does
    /// not stop the instant it is cleared — the collapse is held on screen for a beat, and a player who keeps
    /// firing into the empty field meanwhile moves the balls remaining. A screen that read the score keeper
    /// when it was drawn printed a row that did not add up to the total above it, which is exactly what
    /// happened. Freezing the figures at the end of the level is what makes the arithmetic on the screen true.
    /// </para>
    /// <para>
    /// It also decouples the screen from where the session lives, which is about to move (#65).
    /// </para>
    /// </summary>
    internal readonly struct LevelResult
    {
        /// <summary>The field was emptied — which is the whole of passing a level since #111 retired the score gate.</summary>
        public readonly bool Cleared;

        /// <summary>Which limit ran out, already worded for a player — empty when the level was cleared.</summary>
        public readonly string FailureText;

        /// <summary>
        /// The clear's star rating, 1–<see cref="Prazsky.BS3D.Scoring.StarRating.MAX"/> — the headline the
        /// player reads, where the score is the arithmetic under it. Zero on a failed level, which shows no
        /// stars at all rather than an empty row of them: four hollow stars reads as "rated terrible", and a
        /// loss is not a rating.
        /// </summary>
        public readonly int Stars;

        /// <summary>Whether this clear raised the level's recorded best — its score, its stars, or both.</summary>
        public readonly bool NewBest;

        /// <summary>There is another entry in the set to go to.</summary>
        public readonly bool HasNextLevel;

        /// <summary>
        /// Whether the star total — this clear's own stars already counted — opens the next entry. Decides
        /// whether "Next Level" is offered, and its false is what the unlock note explains.
        /// </summary>
        public readonly bool NextLevelUnlocked;

        /// <summary>The next entry's gate, for the note under the total; 0 when there is no gate or no next level.</summary>
        public readonly int NextLevelMinStars;

        /// <summary>The campaign's star total as of this result, this clear included.</summary>
        public readonly int TotalStars;

        /// <summary>
        /// The last level of a set of more than one, cleared. A single-level set is just a level cleared, and
        /// calling that a campaign's end overstates it.
        /// </summary>
        public readonly bool CampaignComplete;

        /// <summary>
        /// This clear was the one that finished its <b>block</b> (#184) — every level of the chapter cleared, and
        /// it was not already so before this one. The transition and not the state, so replaying the last level
        /// of a block already finished is an ordinary clear, which is what it is.
        /// <para>
        /// False on a set that names no blocks, and false when <see cref="CampaignComplete"/> is true: the last
        /// block completing is also the campaign completing, and two milestones at once is one too many. The
        /// bigger of the two wins and the screen says so.
        /// </para>
        /// </summary>
        public readonly bool BlockComplete;

        /// <summary>What the finished block is called, and where it sits — only meaningful with <see cref="BlockComplete"/>.</summary>
        public readonly string BlockName;
        public readonly int BlockNumber, BlockCount;

        public readonly int Score, MatchedBalls, OrphanedBalls, StreakBonus;

        /// <summary>False on a level that granted unlimited shots, where there is no efficiency to reward.</summary>
        public readonly bool HadBudget;

        /// <summary>What was <b>awarded</b>, not what recomputing it now would give — see the type's summary.</summary>
        public readonly int UnusedShotsAwarded, CompletionBonusAwarded;

        public LevelResult(bool cleared, string failureText, int stars, bool newBest,
            bool hasNextLevel, bool nextLevelUnlocked, int nextLevelMinStars, int totalStars,
            bool campaignComplete,
            bool blockComplete, string blockName, int blockNumber, int blockCount,
            int score, int matchedBalls, int orphanedBalls, int streakBonus,
            bool hadBudget, int unusedShotsAwarded, int completionBonusAwarded)
        {
            Cleared = cleared;
            FailureText = failureText ?? string.Empty;
            Stars = stars;
            NewBest = newBest;
            HasNextLevel = hasNextLevel;
            NextLevelUnlocked = nextLevelUnlocked;
            NextLevelMinStars = nextLevelMinStars;
            TotalStars = totalStars;
            CampaignComplete = campaignComplete;

            //Never both. The last block finishing IS the campaign finishing, and the screen has one heading.
            BlockComplete = blockComplete && !campaignComplete;
            BlockName = blockName;
            BlockNumber = blockNumber;
            BlockCount = blockCount;

            Score = score;
            MatchedBalls = matchedBalls;
            OrphanedBalls = orphanedBalls;
            StreakBonus = streakBonus;
            HadBudget = hadBudget;
            UnusedShotsAwarded = unusedShotsAwarded;
            CompletionBonusAwarded = completionBonusAwarded;
        }

        public bool Failed => !Cleared;

        /// <summary>
        /// Whether the score breakdown is shown at all: whenever the field was cleared. A loss shows none of
        /// it — no completion bonus was awarded, and partial rows would explain a total nobody is being offered.
        /// </summary>
        public bool ShowsBreakdown => Cleared;

        /// <summary>
        /// A loss with nothing to account for. The total still has to be said, or the player is told they lost
        /// and nothing about how they did.
        /// </summary>
        public bool ShowsBareScore => Failed;
    }
}

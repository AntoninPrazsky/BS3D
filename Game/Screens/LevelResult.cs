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
        /// <summary>The field was emptied. Not the same as passing: a clear can still fall short of the gate.</summary>
        public readonly bool Cleared;

        /// <summary>Which limit ran out, already worded for a player — empty when the level was cleared.</summary>
        public readonly string FailureText;

        /// <summary>
        /// The field was cleared but the score did not reach the level's gate. It is a failure, but the
        /// breakdown is still the most useful thing on the screen — it is where the score they missed by
        /// came from.
        /// </summary>
        public readonly bool ShortOfGate;

        /// <summary>There is another entry in the set to go to.</summary>
        public readonly bool HasNextLevel;

        /// <summary>
        /// The last level of a set of more than one, cleared. A single-level set is just a level cleared, and
        /// calling that a campaign's end overstates it.
        /// </summary>
        public readonly bool CampaignComplete;

        public readonly int Score, MatchedBalls, OrphanedBalls, StreakBonus;

        /// <summary>False on a level that granted unlimited shots, where there is no efficiency to reward.</summary>
        public readonly bool HadBudget;

        /// <summary>What was <b>awarded</b>, not what recomputing it now would give — see the type's summary.</summary>
        public readonly int UnusedShotsAwarded, CompletionBonusAwarded;

        /// <summary>The gate the set put on this level, or 0 for none.</summary>
        public readonly int NeededScore;

        public LevelResult(bool cleared, string failureText, bool shortOfGate, bool hasNextLevel, bool campaignComplete,
            int score, int matchedBalls, int orphanedBalls, int streakBonus,
            bool hadBudget, int unusedShotsAwarded, int completionBonusAwarded, int neededScore)
        {
            Cleared = cleared;
            FailureText = failureText ?? string.Empty;
            ShortOfGate = shortOfGate;
            HasNextLevel = hasNextLevel;
            CampaignComplete = campaignComplete;
            Score = score;
            MatchedBalls = matchedBalls;
            OrphanedBalls = orphanedBalls;
            StreakBonus = streakBonus;
            HadBudget = hadBudget;
            UnusedShotsAwarded = unusedShotsAwarded;
            CompletionBonusAwarded = completionBonusAwarded;
            NeededScore = neededScore;
        }

        public bool Failed => !Cleared;

        /// <summary>
        /// Whether the score breakdown is shown at all: whenever the FIELD was cleared, which includes a clear
        /// that fell short of the gate. A hard loss shows none of it — no completion bonus was awarded, and
        /// partial rows would explain a total nobody is being offered.
        /// </summary>
        public bool ShowsBreakdown => Cleared || ShortOfGate;

        /// <summary>
        /// A loss with nothing to account for. The total still has to be said, or the player is told they lost
        /// and nothing about how they did.
        /// </summary>
        public bool ShowsBareScore => Failed && !ShortOfGate;
    }
}

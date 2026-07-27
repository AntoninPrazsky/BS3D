using System;

namespace Prazsky.BS3D.Scoring
{
    /// <summary>
    /// One level's score and its ball budget: the whole of the game's rules for what a shot is worth.
    /// <para>
    /// It knows nothing about MonoGame, Bepu, a map or a frame — it is fed three events (<see cref="Shot"/>,
    /// <see cref="Landed"/>, <see cref="Missed"/>) and answers what the HUD and the result screen need. That is
    /// deliberate: the rules are the part of this game most likely to be argued about and retuned, and they
    /// have no business being another few hundred lines inside a <c>Game</c> subclass where they cannot be
    /// reasoned about — or later tested — on their own.
    /// </para>
    /// <para>
    /// <b>The three events do not all arrive at the same time.</b> A ball is spent the instant it leaves the
    /// barrel, but what it did takes a physics step or more to resolve, and a shot that misses everything
    /// resolves later still. So the budget is driven by <see cref="Shot"/> and the score by
    /// <see cref="Landed"/>/<see cref="Missed"/>, and wiring any two of them to one event would be wrong.
    /// </para>
    /// </summary>
    public sealed class ScoreKeeper
    {
        /// <summary>
        /// What one ball of the group the shot completed is worth. The player aimed at these.
        /// </summary>
        public const int MatchedBallPoints = 10;

        /// <summary>
        /// What one ball that fell because it lost its last anchor is worth — <b>double</b> a matched one.
        /// Cutting a support and collapsing half the cluster is the shot worth learning, and this is the rule
        /// that says so.
        /// </summary>
        public const int OrphanedBallPoints = 20;

        /// <summary>Each ball still unfired when a level is cleared. Efficiency has to be worth something.</summary>
        public const int UnusedShotPoints = 50;

        /// <summary>
        /// How high the streak can climb. Capped so a long level cannot run away with a score that makes every
        /// earlier shot irrelevant.
        /// </summary>
        public const int MaxMultiplier = 5;

        private readonly int? _shotBudget;

        /// <param name="shotBudget">
        /// Balls the level grants, or <c>null</c> for unlimited — which is what an entry that authors no
        /// <c>shots</c> rule means. Unlimited leaves <see cref="ShotsRemaining"/> null and
        /// <see cref="OutOfShots"/> false for ever.
        /// </param>
        public ScoreKeeper(int? shotBudget = null)
        {
            _shotBudget = shotBudget;
        }

        /// <summary>Points awarded so far, streak included.</summary>
        public int Score { get; private set; }

        /// <summary>
        /// What <see cref="Score"/> would have been with the multiplier never leaving ×1. Its difference from
        /// the score is exactly what the streak earned, which is the line a result screen wants to show.
        /// </summary>
        public int BaseScore { get; private set; }

        /// <summary>What the streak has been worth: <see cref="Score"/> less <see cref="BaseScore"/>.</summary>
        public int StreakBonus => Score - BaseScore;

        /// <summary>
        /// What the <b>next</b> scoring shot will be multiplied by. A shot is awarded at the multiplier
        /// standing when it lands and raises it afterwards, so this reads forward — which is the useful thing
        /// for a player to see, and why the applied multiplier is returned from <see cref="Landed"/> rather
        /// than read off here after the fact.
        /// </summary>
        public int Multiplier { get; private set; } = 1;

        public int ShotsFired { get; private set; }

        /// <summary>Balls left in the budget, or <c>null</c> when the level grants unlimited ones.</summary>
        public int? ShotsRemaining => _shotBudget.HasValue ? Math.Max(0, _shotBudget.Value - ShotsFired) : null;

        /// <summary>
        /// Whether the budget is spent. Note this does <b>not</b> stop anything firing — refusing the shot and
        /// ending the level is the lose condition's business, not the scorer's.
        /// </summary>
        public bool OutOfShots => ShotsRemaining is <= 0;

        /// <summary>Balls of the group the player completed, over the whole level.</summary>
        public int MatchedBalls { get; private set; }

        /// <summary>Balls that fell because their support was cut, over the whole level.</summary>
        public int OrphanedBalls { get; private set; }

        /// <summary>A ball has left the barrel. Counts against the budget and nothing else.</summary>
        public void Shot() => ShotsFired++;

        /// <summary>
        /// A shot has landed in the lattice. <paramref name="matched"/> is the group it completed and
        /// <paramref name="orphaned"/> everything that fell with it; both zero means it stuck without
        /// completing anything, which is a spent shot and breaks the streak exactly as a miss does.
        /// </summary>
        /// <returns>What was awarded, and the multiplier it was awarded at — which is what a floating score
        /// popup needs, and is not the same as <see cref="Multiplier"/> once this returns.</returns>
        public ScoreAward Landed(int matched, int orphaned)
        {
            if (matched <= 0)
            {
                Missed();
                return default;
            }

            MatchedBalls += matched;
            OrphanedBalls += orphaned;

            int applied = Multiplier;
            int basePoints = matched * MatchedBallPoints + orphaned * OrphanedBallPoints;

            BaseScore += basePoints;
            Score += basePoints * applied;

            //Awarded first, raised after: the shot that starts a streak scores at ×1 and it is *continuing*
            //that pays. Raising first would make a single lucky shot worth double and there would be no streak
            //to speak of.
            if (Multiplier < MaxMultiplier) Multiplier++;

            return new ScoreAward(basePoints * applied, applied);
        }

        /// <summary>
        /// A shot resolved without dropping anything — it missed the cluster, hit the island or the glass, or
        /// stuck without completing a group. The streak is over.
        /// <para>
        /// Every shot must reach this or <see cref="Landed"/> exactly once, or a streak survives on shots the
        /// player never landed. Whoever owns the balls in flight owns that guarantee.
        /// </para>
        /// </summary>
        public void Missed() => Multiplier = 1;

        /// <summary>
        /// What clearing the level right now would add for the balls left unfired. Zero on an unlimited
        /// budget — there is no efficiency to reward when nothing was scarce.
        /// </summary>
        public int CompletionBonus() => ShotsRemaining.GetValueOrDefault() * UnusedShotPoints;

        /// <summary>
        /// Adds <see cref="CompletionBonus"/> to the score. Called when a level is actually cleared, and by
        /// nothing else — a failed level gets no bonus, which is most of what makes the bonus mean anything.
        /// </summary>
        /// <returns>What was added.</returns>
        public int AwardCompletionBonus()
        {
            int bonus = CompletionBonus();

            BaseScore += bonus;
            Score += bonus;

            return bonus;
        }
    }

    /// <summary>What one landing was worth, and the multiplier it was worth it at.</summary>
    public readonly struct ScoreAward
    {
        public readonly int Points;

        /// <summary>The multiplier actually applied — the one a popup should print beside the points.</summary>
        public readonly int Multiplier;

        public ScoreAward(int points, int multiplier)
        {
            Points = points;
            Multiplier = multiplier;
        }

        public bool Scored => Points > 0;

        public override string ToString() => $"+{Points} ×{Multiplier}";
    }
}

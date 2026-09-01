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

        /// <summary>
        /// What one ball a blast destroyed is worth (#326) — <b>the same as a matched one, and deliberately
        /// not the orphan's double</b>.
        /// <para>
        /// The player aimed at these exactly as they aim at a group, so the matched rate is the honest one:
        /// what a bomb pays is in the <i>count</i>, which is already several times a normal group, and paying
        /// it at the orphan rate as well would pay the player twice for one shot. The orphan rate is not a
        /// "big drop" rate — it exists to reward the one shot in this game that has to be read for rather than
        /// aimed at, cutting a support, and a blast is the opposite of that: it is the shot that needs no
        /// reading at all.
        /// </para>
        /// <para>
        /// <b>⚠ This is the number to suspect first if a bomb level's star rating ever reads wrong</b>, and
        /// #173 is why the warning is here rather than in a commit message: the star rating was once ordered
        /// backwards against skill on every shipped level by a rate that read perfectly reasonably, and no
        /// individual number looked wrong. <c>Tools/ScoreSim</c> is the instrument, and it can only answer
        /// once a level with bombs in it actually ships — until then this rate is reasoned, not measured, and
        /// the shipped set is unaffected because <see cref="Landed"/>'s third argument defaults to zero.
        /// </para>
        /// </summary>
        public const int DestroyedBallPoints = 10;

        /// <summary>
        /// Each ball still unfired when a level is cleared, when the level's own size is not known. Efficiency
        /// has to be worth something — see <see cref="UnusedShotValue"/> for what replaced this flat figure
        /// wherever the size <i>is</i> known, and why it had to.
        /// </summary>
        public const int UnusedShotPoints = 50;

        /// <summary>
        /// How high the streak can climb. Capped so a long level cannot run away with a score that makes every
        /// earlier shot irrelevant.
        /// </summary>
        public const int MaxMultiplier = 5;

        /// <summary>
        /// How much the streak gains per landing. <b>Two, not one, and #173 is why.</b> The multiplier
        /// multiplies every point a shot scores, and a level's total base points barely change with how it is
        /// cleared — every ball drops either way — so the score is really <c>base × the average multiplier</c>,
        /// and the ×1 start is <i>amortised over the shot count</i>. A long grind spends almost all of its
        /// shots at the cap and averages near 5; a four-shot clear averaged 2.5. That made the rating rise
        /// with the number of shots taken on <b>all thirteen shipped levels</b> — ordered backwards against
        /// skill. Climbing by two shortens the ramp to two landings and most of that dilution with it.
        /// </summary>
        public const int MultiplierStep = 2;

        /// <summary>
        /// What one unused shot is worth, as a multiple of <b>the balls an average shot of this level would
        /// have cleared</b>. A flat figure could not do this job: 50 points is a fifth of an average shot on
        /// One and a fiftieth of one on Onion, so it meant something different on every level and far too
        /// little on all of them. Tied to the level's own size it is the same statement everywhere — "not
        /// needing a shot is worth several shots" — which is what makes efficiency able to outweigh a streak
        /// the player could otherwise farm by taking longer.
        /// </summary>
        public const int UnusedShotWorthInShots = 4;

        private readonly int? _shotBudget;
        private readonly int? _ceilingStep;
        private readonly int _levelBalls;

        /// <param name="shotBudget">
        /// Balls the level grants, or <c>null</c> for unlimited — which is what an entry that authors no
        /// <c>shots</c> rule means. Unlimited leaves <see cref="ShotsRemaining"/> null and
        /// <see cref="OutOfShots"/> false for ever.
        /// </param>
        /// <param name="ceilingStep">
        /// Shots between two descents of the glass ceiling, or <c>null</c> for a ceiling that holds still —
        /// which is what an entry that authors no <c>ceilingStep</c> rule means. The descent is driven off
        /// <see cref="ShotsFired"/> by <see cref="StepCeilingThisShot"/>, so the rule is read here exactly as the
        /// budget is.
        /// </param>
        /// <param name="levelBalls">
        /// How many balls the level started with, which is what sizes <see cref="UnusedShotValue"/>. Zero or
        /// unknown falls back to the flat <see cref="UnusedShotPoints"/>, so a caller that cannot say — the
        /// built-in fallback cluster, a test — still scores rather than dividing by nothing.
        /// </param>
        public ScoreKeeper(int? shotBudget = null, int? ceilingStep = null, int levelBalls = 0)
        {
            _shotBudget = shotBudget;
            _ceilingStep = ceilingStep;
            _levelBalls = levelBalls;
        }

        /// <summary>
        /// What one unused shot is worth on <i>this</i> level: <see cref="UnusedShotWorthInShots"/> times the
        /// balls an average shot of it would have cleared. Falls back to the flat <see cref="UnusedShotPoints"/>
        /// when the level's size or budget is unknown.
        /// </summary>
        public int UnusedShotValue =>
            _levelBalls > 0 && _shotBudget is > 0
                ? UnusedShotWorthInShots * MatchedBallPoints * _levelBalls / _shotBudget.Value
                : UnusedShotPoints;

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

        /// <summary>Balls a blast took out by geometry, over the whole level (#326).</summary>
        public int DestroyedBalls { get; private set; }

        /// <summary>A ball has left the barrel. Counts against the budget and nothing else.</summary>
        public void Shot() => ShotsFired++;

        /// <summary>
        /// Whether the shot just fired (after <see cref="Shot"/>) is one that steps the ceiling down. True on
        /// every <c>ceilingStep</c>-th shot; false when the rule is absent (a ceiling that holds still) or the
        /// shot count has not reached the next step. Read here rather than in the game so the rule and the budget
        /// it couples to live in one place — see <see cref="ScoreKeeper"/>'s class doc.
        /// </summary>
        public bool StepCeilingThisShot() => _ceilingStep.HasValue && _ceilingStep.Value > 0 && ShotsFired % _ceilingStep.Value == 0;

        /// <summary>
        /// Shots until the ceiling's next descent, or <c>null</c> when the ceiling holds still. Reads forward,
        /// like <see cref="Multiplier"/>: it is the number a player is holding against, not the count already gone.
        /// </summary>
        public int? ShotsToNextCeiling =>
            _ceilingStep.HasValue && _ceilingStep.Value > 0
                ? _ceilingStep.Value - (ShotsFired % _ceilingStep.Value)
                : null;

        /// <summary>
        /// A shot has landed in the lattice. <paramref name="matched"/> is the group it completed,
        /// <paramref name="orphaned"/> everything that fell with it, and <paramref name="destroyed"/> what a
        /// blast took out by geometry (#326); all three zero means it stuck without doing anything, which is a
        /// spent shot and breaks the streak exactly as a miss does.
        /// <para>
        /// <b>⚠ The spent-shot door reads matched OR destroyed, and getting that wrong would have been silent.</b>
        /// A shot that sets off a bomb and completes no group at all is a shot that did exactly what the player
        /// aimed it at — the door read <c>matched &lt;= 0</c> alone until #326, which would have called every
        /// such shot a miss, broken the streak on it and awarded nothing for a third of the cluster.
        /// </para>
        /// </summary>
        /// <returns>What was awarded, and the multiplier it was awarded at — which is what a floating score
        /// popup needs, and is not the same as <see cref="Multiplier"/> once this returns.</returns>
        public ScoreAward Landed(int matched, int orphaned, int destroyed = 0)
        {
            if (matched <= 0 && destroyed <= 0)
            {
                Missed();
                return default;
            }

            MatchedBalls += matched;
            OrphanedBalls += orphaned;
            DestroyedBalls += destroyed;

            int applied = Multiplier;
            int basePoints = matched * MatchedBallPoints + orphaned * OrphanedBallPoints
                             + destroyed * DestroyedBallPoints;

            BaseScore += basePoints;
            Score += basePoints * applied;

            //Awarded first, raised after: the shot that starts a streak scores at ×1 and it is *continuing*
            //that pays. Raising first would make a single lucky shot worth double and there would be no streak
            //to speak of.
            if (Multiplier < MaxMultiplier) Multiplier = Math.Min(MaxMultiplier, Multiplier + MultiplierStep);

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
        public int CompletionBonus() => ShotsRemaining.GetValueOrDefault() * UnusedShotValue;

        /// <summary>
        /// The bonus that was actually awarded, and the balls it was awarded for. Recorded rather than
        /// recomputed, because <b>the level does not stop the instant it is cleared</b>: the collapse is held
        /// on screen for a beat, and a player who keeps firing into the empty field meanwhile moves
        /// <see cref="ShotsRemaining"/>. A result screen that calls <see cref="CompletionBonus"/> again when it
        /// is drawn then prints a row that does not add up to the total above it — which is exactly what it did.
        /// </summary>
        public int CompletionBonusAwarded { get; private set; }

        /// <inheritdoc cref="CompletionBonusAwarded"/>
        public int UnusedShotsAwarded { get; private set; }

        /// <summary>
        /// Adds <see cref="CompletionBonus"/> to the score. Called when a level is actually cleared, and by
        /// nothing else — a failed level gets no bonus, which is most of what makes the bonus mean anything.
        /// </summary>
        /// <returns>What was added.</returns>
        public int AwardCompletionBonus()
        {
            UnusedShotsAwarded = ShotsRemaining.GetValueOrDefault();
            CompletionBonusAwarded = CompletionBonus();

            BaseScore += CompletionBonusAwarded;
            Score += CompletionBonusAwarded;

            return CompletionBonusAwarded;
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

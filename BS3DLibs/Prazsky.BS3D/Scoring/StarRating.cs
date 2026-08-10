using System;

namespace Prazsky.BS3D.Scoring
{
    /// <summary>
    /// Turns a cleared level's score into a <b>1–4 star rating</b> — the headline a player reads at a glance,
    /// where a bare number says nothing about whether they did well (#111). Clearing at all is worth one star;
    /// the three steps above it measure how much better than the bare minimum the clear was.
    /// <para>
    /// The yardstick is the level's own <b>floor</b>: a cleared field has, by construction, released every one
    /// of its balls, and each released ball scores at least <see cref="ScoreKeeper.MatchedBallPoints"/> at
    /// multiplier ×1 — so no clear of an <paramref name="levelBalls"/>-ball level can score under
    /// <c>MatchedBallPoints × levelBalls</c>. Everything above that floor was <i>earned</i>: orphans pay
    /// double, a held streak multiplies up to ×<see cref="ScoreKeeper.MaxMultiplier"/>, and unused shots pay
    /// the completion bonus. Rating the score as a multiple of the floor therefore self-calibrates across
    /// levels of any size, with no per-level thresholds to author or to drift out of date.
    /// </para>
    /// <para>
    /// It lives beside <see cref="ScoreKeeper"/> because it is the same kind of thing: a scoring rule that
    /// will be argued about and retuned. The three thresholds are a <b>first pass reasoned from the rules</b>
    /// (a sloppy clear lands near ×1.2, mixed orphans on a mid streak near ×3, a sustained ×5 streak with
    /// collapses runs past ×5) rather than measured from play — retune them here, one constant per step.
    /// </para>
    /// </summary>
    public static class StarRating
    {
        /// <summary>The best rating a level can earn — an excellent clear.</summary>
        public const int MAX = 4;

        //Score as a multiple of the floor (MatchedBallPoints × the level's balls, the least any clear can
        //score — so every threshold is > 1, and 1 star is "cleared" by construction rather than by a rule.
        public const float TWO_STAR_FLOOR_MULTIPLE = 1.8f;
        public const float THREE_STAR_FLOOR_MULTIPLE = 3.0f;
        public const float FOUR_STAR_FLOOR_MULTIPLE = 4.5f;

        /// <summary>
        /// The stars a <b>cleared</b> level's <paramref name="score"/> earned, 1 to <see cref="MAX"/>.
        /// A failed level is asked nothing — it has no rating, and the caller says so by never calling this.
        /// </summary>
        /// <param name="levelBalls">
        /// How many balls the level <b>started</b> with, which is what sizes the floor. Balls the player
        /// parked and later cut loose score too, but they also cost shots — letting them nudge the ratio is
        /// the rating working, not an error in it.
        /// </param>
        public static int Rate(int score, int levelBalls)
        {
            //A level authored with no balls has no floor to measure against; clearing it is worth the one
            //star that "cleared" always is.
            if (levelBalls <= 0) return 1;

            float multiple = score / (float)(ScoreKeeper.MatchedBallPoints * levelBalls);

            if (multiple >= FOUR_STAR_FLOOR_MULTIPLE) return 4;
            if (multiple >= THREE_STAR_FLOOR_MULTIPLE) return 3;
            if (multiple >= TWO_STAR_FLOOR_MULTIPLE) return 2;

            return 1;
        }
    }
}

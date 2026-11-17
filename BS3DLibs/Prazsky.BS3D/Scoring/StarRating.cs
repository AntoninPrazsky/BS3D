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
    /// multiplier ×1 — so no clear of a <c>levelBalls</c>-ball level (<see cref="Rate"/>'s parameter) can
    /// score under <c>MatchedBallPoints × levelBalls</c>. Everything above that floor was <i>earned</i>: orphans pay
    /// double, a held streak multiplies up to ×<see cref="ScoreKeeper.MaxMultiplier"/>, unused shots pay
    /// the completion bonus and a fast clear the time bonus (#1045). Rating the score as a multiple of the floor
    /// therefore self-calibrates across levels of any size, with no per-level thresholds to author or to drift out
    /// of date (the time bonus is sized by the floor too, and its par is the level's own).
    /// </para>
    /// <para>
    /// It lives beside <see cref="ScoreKeeper"/> because it is the same kind of thing: a scoring rule that
    /// will be argued about and retuned, one constant per step, against <c>Tools/ScoreSim</c>.
    /// </para>
    /// </summary>
    public static class StarRating
    {
        /// <summary>The best rating a level can earn — an excellent clear.</summary>
        public const int MAX = 4;

        //Score as a multiple of the floor (MatchedBallPoints × the level's balls, the least any clear can
        //score — so every threshold is > 1, and 1 star is "cleared" by construction rather than by a rule.
        //
        //MEASURED, not reasoned, since #173: Tools/ScoreSim drives the real ScoreKeeper over every shipped level at
        //several play styles and refuses thresholds that rate them the wrong way round. Retune against it, never by
        //intuition (the reasoned first values rated backwards).
        //
        //SINCE #1045 (rules v2, the owner's option B of 2026-11-17) TIME IS THE LEVER. Under v1 a clear almost always
        //earned four stars (the owner's save: 38 of 41; the unused balls alone added about three floors). Now the landing
        //part of a clean clear is about 3.5-5 floors, the unused balls under one, and the time bonus up to
        //ScoreKeeper.TimeBonusWorthInFloors (10), draining to nothing at twice par. So:
        //
        //  2.5  a clean clear in the casual probe's shots earns two stars at twice par; the whole budget with a third
        //       of it missing (~1.9) earns one
        //  6.5  three stars within about one and a half par
        //  11   four stars only at about three quarters of par (option B): out of reach at twice par, and within reach
        //       of every level's best clean clear at half par, which ScoreSim's UNREACHABLE gate holds
        //
        //The #1045 model (the owner's 23 clean levels as play styles over all 140 levels) rates an expert 0/10/32/59 %
        //at 1/2/3/4 stars, against 0/0/7/93 under v1.
        public const float TWO_STAR_FLOOR_MULTIPLE = 2.5f;
        public const float THREE_STAR_FLOOR_MULTIPLE = 6.5f;
        public const float FOUR_STAR_FLOOR_MULTIPLE = 11.0f;

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

        /// <summary>
        /// The least score <see cref="Rate"/> gives <paramref name="stars"/> or more on a level of
        /// <paramref name="levelBalls"/> balls, for 2 to <see cref="MAX"/> stars: the star floors the score service checks
        /// a clear's stars against (#1024, AntoninPrazsky/BS3D-API#16), since a clear's stars are its score's and nothing
        /// else's. <b>Found by asking <see cref="Rate"/> itself</b>, never by re-deriving its arithmetic: it compares a
        /// <c>float</c>, and a floor one point off would have the service refuse a real clear on the boundary.
        /// </summary>
        public static int LeastScoreFor(int stars, int levelBalls)
        {
            if (stars < 2 || stars > MAX) throw new ArgumentOutOfRangeException(nameof(stars), stars, "Only 2 to MAX stars have a floor.");
            if (levelBalls <= 0) throw new ArgumentOutOfRangeException(nameof(levelBalls), levelBalls, "A level with no balls rates every clear one star.");

            //Rate never falls as the score rises, so the boundary is found by halving: below it fewer stars, at it this many
            int below = 0, at = 1;
            while (Rate(at, levelBalls) < stars) at = checked(at * 2);

            while (at - below > 1)
            {
                int middle = below + (at - below) / 2;
                if (Rate(middle, levelBalls) >= stars) at = middle;
                else below = middle;
            }

            return at;
        }
    }
}

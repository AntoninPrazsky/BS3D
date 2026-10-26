using Prazsky.BS3D.Scoring;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The scoring rules as <see cref="ScoreKeeper"/> and <see cref="StarRating"/> state them, not a copy of
    /// them: every figure below is derived from the classes' own constants. Tools/ScoreSim judges whether the
    /// rules rate the shipped levels the right way round; this pins the rules' own mechanics.
    /// </summary>
    public class ScoringTests
    {
        /// <summary>
        /// The streak: awarded first, raised after — the first scoring shot is ×1, each consecutive one raises
        /// the multiplier by <see cref="ScoreKeeper.MultiplierStep"/> up to <see cref="ScoreKeeper.MaxMultiplier"/>.
        /// </summary>
        [Fact]
        public void MultiplierRisesAfterEachScoringShotUpToTheCap()
        {
            ScoreKeeper keeper = new();
            int expected = 1;

            for (int shot = 0; shot < 6; shot++)
            {
                ScoreAward award = keeper.Landed(matched: 3, orphaned: 0);

                Assert.Equal(expected, award.Multiplier);
                Assert.Equal(3 * ScoreKeeper.MatchedBallPoints * expected, award.Points);

                expected = System.Math.Min(ScoreKeeper.MaxMultiplier, expected + ScoreKeeper.MultiplierStep);
                Assert.Equal(expected, keeper.Multiplier);
            }
        }

        [Fact]
        public void AMissResetsTheMultiplierAndScoresNothing()
        {
            ScoreKeeper keeper = new();
            keeper.Landed(3, 0);
            keeper.Landed(3, 0);
            Assert.True(keeper.Multiplier > 1);

            int before = keeper.Score;
            ScoreAward miss = keeper.Landed(matched: 0, orphaned: 5); //orphans alone are no match

            Assert.False(miss.Scored);
            Assert.Equal(before, keeper.Score);
            Assert.Equal(1, keeper.Multiplier);
        }

        [Fact]
        public void ScoreIsBasePlusStreakBonus()
        {
            ScoreKeeper keeper = new();
            keeper.Landed(3, 2);
            keeper.Landed(4, 0, destroyed: 1);
            keeper.Missed();
            keeper.Landed(5, 1);

            int base1 = 3 * ScoreKeeper.MatchedBallPoints + 2 * ScoreKeeper.OrphanedBallPoints;
            int base2 = 4 * ScoreKeeper.MatchedBallPoints + ScoreKeeper.DestroyedBallPoints;
            int base3 = 5 * ScoreKeeper.MatchedBallPoints + ScoreKeeper.OrphanedBallPoints;
            int second = 1 + ScoreKeeper.MultiplierStep;

            Assert.Equal(base1 + base2 + base3, keeper.BaseScore);
            Assert.Equal(base1 + base2 * second + base3, keeper.Score);
            Assert.Equal(keeper.Score - keeper.BaseScore, keeper.StreakBonus);
            Assert.Equal((12, 3, 1), (keeper.MatchedBalls, keeper.OrphanedBalls, keeper.DestroyedBalls));
        }

        [Fact]
        public void CompletionBonusPaysForEveryUnusedShot()
        {
            ScoreKeeper keeper = new(shotBudget: 10, ceilingStep: null, levelBalls: 40);
            for (int i = 0; i < 3; i++) keeper.Shot();
            keeper.Landed(3, 0);

            int before = keeper.Score;
            int bonus = keeper.AwardCompletionBonus();

            Assert.Equal(7, keeper.UnusedShotsAwarded);
            Assert.Equal(7 * keeper.UnusedShotValue, bonus);
            Assert.Equal(before + bonus, keeper.Score);

            //Unlimited budget: nothing unused to pay for
            Assert.Equal(0, new ScoreKeeper().CompletionBonus());
        }

        /// <summary>
        /// <see cref="ScoreKeeper.ScoreCeiling"/> bounds every reachable score: a hand-built run better than any
        /// real one — the whole level falling on the first shot, the rest of the budget unused — stays under it.
        /// </summary>
        [Theory]
        [InlineData(30, 10)]
        [InlineData(200, 40)]
        [InlineData(3, 1)]
        public void ScoreCeilingIsAboveAPerfectRun(int levelBalls, int budget)
        {
            ScoreKeeper keeper = new(budget, null, levelBalls);
            keeper.Shot();
            keeper.Landed(matched: 3, orphaned: levelBalls - 3);
            keeper.AwardCompletionBonus();

            Assert.True(keeper.Score <= ScoreKeeper.ScoreCeiling(levelBalls, budget),
                $"{keeper.Score} > ceiling {ScoreKeeper.ScoreCeiling(levelBalls, budget)}");
        }

        /// <summary>
        /// The floor clear — every ball matched at ×1, which is the least any clear can score — is one star,
        /// and the rating never falls as the score rises.
        /// </summary>
        [Fact]
        public void StarsStartAtOneForTheFloorAndNeverFallWithScore()
        {
            const int balls = 60;
            int floor = ScoreKeeper.MatchedBallPoints * balls;

            Assert.Equal(1, StarRating.Rate(floor, balls));
            Assert.Equal(StarRating.MAX, StarRating.Rate(floor * 100, balls));

            int previous = 1;
            for (int score = floor; score <= floor * 10; score += 10)
            {
                int stars = StarRating.Rate(score, balls);
                Assert.True(stars >= previous, $"{score} rates {stars}, below the {previous} a lower score earned");
                previous = stars;
            }
        }

        /// <summary>
        /// The star floors the score service checks a clear's stars by (#1024, BS3D-API#16) sit exactly where the rating
        /// turns, on a small level and a large one alike, and rise.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(61)]
        [InlineData(238)]
        [InlineData(1000)]
        public void EachStarFloorIsWhereTheRatingTurns(int balls)
        {
            int previous = 0;
            for (int stars = 2; stars <= StarRating.MAX; stars++)
            {
                int floor = StarRating.LeastScoreFor(stars, balls);

                Assert.True(StarRating.Rate(floor - 1, balls) < stars, $"{floor - 1} already rates {stars} stars on {balls} balls");
                Assert.True(StarRating.Rate(floor, balls) >= stars, $"{floor} rates under {stars} stars on {balls} balls");
                Assert.True(floor > previous);
                previous = floor;
            }
        }

        /// <summary>
        /// The service's rule, one star for a clear and one more for each floor its score reaches, is the game's rating
        /// for every score: what lets the service refuse a clear whose stars are not its score's.
        /// </summary>
        [Fact]
        public void OneStarPlusTheFloorsReachedIsTheRating()
        {
            const int balls = 61;
            int[] floors = { StarRating.LeastScoreFor(2, balls), StarRating.LeastScoreFor(3, balls), StarRating.LeastScoreFor(4, balls) };

            for (int score = 0; score <= floors[2] + 100; score++)
            {
                int reached = 0;
                foreach (int floor in floors) if (score >= floor) reached++;
                Assert.Equal(StarRating.Rate(score, balls), 1 + reached);
            }
        }

        [Theory]
        [InlineData(1, 61)]
        [InlineData(StarRating.MAX + 1, 61)]
        [InlineData(2, 0)]
        public void OnlyTwoToFourStarsOnALevelWithBallsHaveAFloor(int stars, int balls) =>
            Assert.Throws<System.ArgumentOutOfRangeException>(() => StarRating.LeastScoreFor(stars, balls));
    }
}

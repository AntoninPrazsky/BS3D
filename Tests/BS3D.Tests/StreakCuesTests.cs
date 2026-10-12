using BS3D.Screens;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Scoring;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #845: when the game tells the player about the streak (<see cref="StreakCues"/>, compiled in from the Game). A break
    /// is told above ×1 and never once the level is decided or its last shot spent (the issue: never over the result); it
    /// scales with what was lost; and the Swap chip points at the cure only in the one state the owner named, narrowed by
    /// the conservative condition added to it (the round behind can score).
    /// </summary>
    public class StreakCuesTests
    {
        [Theory]
        [InlineData(3, false, false, true)]
        [InlineData(5, false, false, true)]
        [InlineData(1, false, false, false)]      //no streak, nothing lost
        [InlineData(5, true, false, false)]       //the level is decided: never over the result
        [InlineData(5, false, true, false)]       //the last shot is spent: the loss is the level's ending, not the streak
        [InlineData(3, true, true, false)]
        public void A_break_is_told_only_while_the_level_goes_on(int lost, bool levelDecided, bool outOfShots, bool told)
        {
            Assert.Equal(told, StreakCues.BreakIsTold(lost, levelDecided, outOfShots));
        }

        [Fact]
        public void A_lost_cap_is_more_severe_than_a_lost_three_and_both_are_in_range()
        {
            Assert.Equal(1f, StreakCues.Severity(ScoreKeeper.MaxMultiplier));
            Assert.Equal(0f, StreakCues.Severity(1));
            Assert.True(StreakCues.Severity(5) > StreakCues.Severity(3));
            Assert.InRange(StreakCues.Severity(3), 0.01f, 0.99f);
            Assert.Equal(1f, StreakCues.Severity(99));
        }

        [Fact]
        public void Only_an_ordinary_round_needs_a_match()
        {
            Assert.True(StreakCues.NeedsAMatch(BallKind.Normal));
            Assert.False(StreakCues.NeedsAMatch(BallKind.Wildcard));
            Assert.False(StreakCues.NeedsAMatch(BallKind.Cutter));
        }

        /// <summary>
        /// The whole truth table: one row flashes, every other is dark. The owner's rule of 2026-10-11 is a Swap, a streak
        /// and a dead bore; the live round behind is the conservative condition added to it (<see cref="StreakCues.SwapIsTheCure"/>).
        /// </summary>
        [Theory]
        [InlineData(true, 3, false, true, true)]      //the rule: a Swap to spend, a streak, a dead bore, a live round behind
        [InlineData(true, 5, false, true, true)]
        [InlineData(false, 3, false, true, false)]    //no Swap that would act (spent, mid-takeover, level decided)
        [InlineData(true, 1, false, true, false)]     //no streak running: nothing to keep
        [InlineData(true, 3, true, true, false)]      //the bore can score: fire it
        [InlineData(true, 3, false, false, false)]    //the round behind has nowhere either: the Swap is no cure
        [InlineData(true, 3, true, false, false)]
        public void The_swap_flashes_only_when_it_is_the_cure(bool swapCanAct, int streak, bool boreCanScore, bool behindCanScore,
            bool flashes)
        {
            Assert.Equal(flashes, StreakCues.SwapIsTheCure(swapCanAct, streak, boreCanScore, behindCanScore));
        }
    }
}

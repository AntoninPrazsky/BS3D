using Prazsky.BS3D;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// When a release is worth the drop cinematic (#615, #719). The promises of the rule and not the current value of a dial:
    /// the shot that clears the level always fires, the first drop above the floor fires, a drop has to beat the level's best
    /// by the margin, and a floor that scales with the level lifts it on a big one. The rule #615 replaced is run over the same
    /// run of drops and has to fire on every one of them, which is the complaint that changed it: the check's failing branch,
    /// seen to fire on the case it exists for (<c>BestPractices.md</c> §10).
    /// </summary>
    public class DropTriggerTests
    {
        //#615's own words: "A quarter was beaten by an ordinary ascending run of drops (12, 15, 19, 24, 30 ...)"
        private static readonly int[] AscendingRun = { 24, 30, 38, 48, 60 };

        /// <summary>The cinematics a run of releases would show, none of them clearing the level, the record raised by every one.</summary>
        private static int Fires(int[] run, int initialBalls, float share, float margin)
        {
            int best = 0, fires = 0;

            foreach (int total in run)
            {
                if (DropTrigger.IsWorthWatching(total, false, initialBalls, best, share, margin)) fires++;
                if (total > best) best = total;
            }

            return fires;
        }

        [Fact]
        public void TheShotThatClearsTheLevelAlwaysFires()
        {
            //Three balls on a 600-ball level, with a record of 80 already shown: neither bar is met and the shot still fires
            Assert.True(DropTrigger.IsWorthWatching(3, true, 600, 80));
            Assert.False(DropTrigger.IsWorthWatching(3, false, 600, 80));
        }

        [Fact]
        public void TheFirstDropAboveTheFloorFiresBecauseThereIsNoRecordToBeat()
        {
            int floor = DropTrigger.Floor(300, DropTrigger.MIN_SHARE_OF_LEVEL);

            Assert.True(DropTrigger.IsWorthWatching(floor, false, 300, 0));
            Assert.False(DropTrigger.IsWorthWatching(floor - 1, false, 300, 0));
        }

        [Fact]
        public void ADropHasToBeatTheBestSoFarByTheMargin()
        {
            //A level small enough that the absolute floor governs, so the margin is the only thing in question
            int best = 20;
            int needed = (int)System.MathF.Ceiling(best * DropTrigger.MustBeatBestBy);

            Assert.True(DropTrigger.IsWorthWatching(needed, false, 100, best));
            Assert.False(DropTrigger.IsWorthWatching(needed - 1, false, 100, best));
        }

        [Fact]
        public void TheFloorScalesWithTheLevelAndTheAbsoluteFloorGovernsASmallOne()
        {
            Assert.Equal(DropTrigger.MIN_BALLS, DropTrigger.Floor(60, DropTrigger.MIN_SHARE_OF_LEVEL));
            Assert.True(DropTrigger.Floor(1600, DropTrigger.MIN_SHARE_OF_LEVEL) > DropTrigger.Floor(400, DropTrigger.MIN_SHARE_OF_LEVEL));
            Assert.True(DropTrigger.Floor(400, DropTrigger.MIN_SHARE_OF_LEVEL) > DropTrigger.MIN_BALLS);
        }

        [Fact]
        public void AnOrdinaryAscendingRunOfDropsIsNotAFlourishOnEveryOne()
        {
            int ours = Fires(AscendingRun, 250, DropTrigger.MIN_SHARE_OF_LEVEL, DropTrigger.MustBeatBestBy);

            //The old rule - 12 balls and a quarter over the best - shows every drop of the run, which is what the owner saw
            int old = Fires(AscendingRun, 250, 0f, 1.25f);

            Assert.Equal(AscendingRun.Length, old);
            Assert.True(ours < old, $"the game's dials fired {ours} times on a run the old rule fires {old} times on");
        }
    }
}

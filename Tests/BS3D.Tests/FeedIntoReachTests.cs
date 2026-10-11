using BS3D.Screens;
using Prazsky.BS3D.Levels;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// A tall level's feed (#843): <see cref="CeilingDescent.Feed"/> queues the descents that bring the column's
    /// underside back into the gun's reach and no more — never more than the climb itself owes (the bound it had
    /// before #843: the height the level hung the underside at), never fewer than the reach asks for under that
    /// bound, and none at all while the underside is still in reach. The reach is the session's geometry and
    /// arrives as a count; what is held here is the bookkeeping around it, and that the steps a pour has already
    /// queued are counted in <see cref="CeilingDescent.QueuedTargetY"/> rather than asked for twice.
    /// </summary>
    public class FeedIntoReachTests
    {
        private const float STEP = CeilingDescent.CEILING_DESCENT_PER_STEP;

        private static CeilingDescent Hanging()
        {
            CeilingDescent glass = new();
            glass.Reset(ClusterHang.DEATH_Y + 30f, feedFloorLevel: 0);
            return glass;
        }

        [Fact]
        public void AFeedStopsOnceTheUndersideIsInReach()
        {
            CeilingDescent glass = Hanging();

            //Ten levels cleared (7.07 units) would owe eleven whole steps back to the authored height; two bring it into reach
            Assert.Equal(2, glass.Feed(lowest: 10, intoReach: 2, out float risen, out int heightOwed));
            Assert.Equal(11, heightOwed);
            Assert.Equal(10 / 1.41421356f, risen, 3);
        }

        [Fact]
        public void AnUndersideStillInReachIsNotFed()
        {
            CeilingDescent glass = Hanging();

            Assert.Equal(0, glass.Feed(lowest: 10, intoReach: 0, out _, out int heightOwed));
            Assert.Equal(11, heightOwed);
            Assert.Equal(glass.RestY, glass.QueuedTargetY);
        }

        [Fact]
        public void AFeedNeverTakesTheUndersideBelowWhereTheLevelHungIt()
        {
            CeilingDescent glass = Hanging();

            //However much the reach asks for, the climb's own count is the most: four levels (2.83 units) owe four
            Assert.Equal(4, glass.Feed(lowest: 4, intoReach: 50, out _, out _));

            //And once those are queued, a reach that still asks for more gets nothing further from the same climb
            Assert.Equal(0, glass.Feed(lowest: 4, intoReach: 50, out _, out int heightOwed));
            Assert.Equal(0, heightOwed);
        }

        [Fact]
        public void TheStepsAlreadyQueuedAreWhereTheReachIsMeasuredFrom()
        {
            CeilingDescent glass = Hanging();

            Assert.Equal(3, glass.Feed(lowest: 10, intoReach: 3, out _, out _));

            //Nothing has come down yet, and the hang the next landing measures from already counts all three
            Assert.Equal(glass.RestY, glass.TargetY);
            Assert.Equal(glass.RestY - 3 * STEP, glass.QueuedTargetY, 4);

            //One comes down: the queue and the target trade one step, the queued hang stays where it was
            Assert.True(glass.Update(1f, false, false, out bool feeding, out _) && feeding);
            Assert.Equal(glass.RestY - STEP, glass.TargetY, 4);
            Assert.Equal(glass.RestY - 3 * STEP, glass.QueuedTargetY, 4);
        }

        [Fact]
        public void AClimbThatGoesOnIsFedAgainIntoReach()
        {
            CeilingDescent glass = Hanging();

            Assert.Equal(2, glass.Feed(lowest: 10, intoReach: 2, out _, out _));

            //The next band goes: twenty levels up (14.1 units) owe 23 by the height, 21 after the two already asked,
            //and the reach asks for three more from where the queue leaves the glass
            Assert.Equal(3, glass.Feed(lowest: 20, intoReach: 3, out _, out int heightOwed));
            Assert.Equal(21, heightOwed);
            Assert.Equal(glass.RestY - 5 * STEP, glass.QueuedTargetY, 4);
        }

        [Fact]
        public void TheBoundIsWhatTheClimbStillOwesAndAsksForNothing()
        {
            CeilingDescent glass = Hanging();

            //No climb, no bound — the case the session skips its reach search on
            Assert.Equal(0, glass.FeedHeightOwed(lowest: 0, out float none));
            Assert.Equal(0f, none);

            //Asking queues nothing, and the bound falls by exactly what a feed then takes
            Assert.Equal(11, glass.FeedHeightOwed(lowest: 10, out _));
            Assert.Equal(11, glass.FeedHeightOwed(lowest: 10, out _));
            Assert.Equal(glass.RestY, glass.QueuedTargetY);

            Assert.Equal(4, glass.Feed(lowest: 10, intoReach: 4, out _, out _));
            Assert.Equal(7, glass.FeedHeightOwed(lowest: 10, out _));
        }

        [Fact]
        public void TheQueuedHangNeverPassesTheDeathLine()
        {
            CeilingDescent glass = new();
            glass.Reset(ClusterHang.DEATH_Y + 1f, feedFloorLevel: 0);

            Assert.Equal(10, glass.Feed(lowest: 20, intoReach: 10, out _, out _));
            Assert.Equal(ClusterHang.DEATH_Y, glass.QueuedTargetY);
        }
    }
}

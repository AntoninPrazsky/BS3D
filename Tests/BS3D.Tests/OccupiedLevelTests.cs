using Prazsky.BS3D.GameStructure;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// How far up and down a layout reaches (<see cref="BallsMap.GetLowestOccupiedLevel"/> and
    /// <see cref="BallsMap.GetHighestOccupiedLevel"/>): the two ends a tall level's feed and its aim clamp are
    /// measured against (#842) — the clamp lets go once the highest one hangs inside the frame — and what each
    /// answers for an empty map, which both callers guard against.
    /// </summary>
    public class OccupiedLevelTests
    {
        [Fact]
        public void BothEndsOfALayoutAreFound()
        {
            BallsMap map = new(5, 5, 20);
            map.PutBallAt(2, 2, 4);
            map.PutBallAt(1, 3, 11);
            map.PutBallAt(3, 1, 7);

            Assert.Equal(4, map.GetLowestOccupiedLevel());
            Assert.Equal(11, map.GetHighestOccupiedLevel());
        }

        [Fact]
        public void TheTopLevelIsFoundAsTheHighest()
        {
            BallsMap map = new(3, 3, 20);
            map.PutBallAt(0, 0, 19);

            Assert.Equal(19, map.GetHighestOccupiedLevel());
        }

        [Fact]
        public void AnEmptyMapReachesNowhere()
        {
            BallsMap map = new(3, 3, 20);

            //Each end answers with the other end of the field, so "is the column in the frame" reads true and "how
            //far has the underside climbed" reads the whole field: both callers ask GetBallsCount first
            Assert.Equal(0, map.GetHighestOccupiedLevel());
            Assert.Equal(19, map.GetLowestOccupiedLevel());
        }
    }
}
